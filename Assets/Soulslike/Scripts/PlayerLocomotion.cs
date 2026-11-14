using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// 소울라이크 3인칭 이동을 담당한다.
    /// - 자유 이동: 카메라 기준 8방향으로 움직이고, 캐릭터는 이동 방향으로 몸을 돌린다(전진 애니메이션).
    /// - 록온 이동: 캐릭터는 대상을 계속 바라보고, 입력 방향에 맞는 8방향 스트레이프 애니메이션을 재생한다.
    /// - 질주: 록온 중이어도 이동 방향으로 몸을 돌려 달린다.
    /// - 구르기: 입력 방향으로 구른다. 록온 중이면 대상을 바라본 채 전후좌우 구르기 중 가장 가까운 것을 쓴다.
    /// - 점프: 이륙 순간의 수평 속도를 유지하며 포물선으로 난다.
    ///
    /// 지상 이동과 구르기는 루트 모션(애니메이션 안의 이동량)으로 움직여 발이 미끄러지지 않는다.
    /// 회전·중력·점프는 코드가 결정한다. 루트 모션은 모델의 AnimatorRootMotionRelay가 넘겨준다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerLocomotion : MonoBehaviour
    {
        enum Gait { Idle, Walk, Run, Sprint }
        enum MotorState { Grounded, Rolling, Airborne }

        // 애니메이터 상태 이름. SoulslikeSetupMenu가 같은 이름으로 만든다.
        public const string LocomotionState = "Locomotion";
        public const string JumpStartState = "JumpStart";
        public const string JumpLoopState = "JumpLoop";
        public const string RollTag = "Roll";
        public static readonly string[] RollStates = { "Roll_F", "Roll_R", "Roll_B", "Roll_L" };

        static readonly Vector3[] RollLocalDirections = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        [Header("References (비워 두면 자동으로 찾는다)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] LockOnController lockOn;
        [SerializeField] Animator animator;
        [SerializeField] Transform cameraTransform;

        [Header("Root Motion")]
        [Tooltip("켜면 애니메이션의 이동량으로 움직인다(발 미끄러짐 없음). 끄면 아래 속도 값으로 코드가 움직인다.")]
        [SerializeField] bool useRootMotion = true;

        [Header("Speed (m/s) — 루트 모션을 끈 경우에만 쓴다")]
        [SerializeField] float walkSpeed = 1.8f;
        [SerializeField] float runSpeed = 4.2f;
        [SerializeField] float sprintSpeed = 6.5f;
        [SerializeField] float acceleration = 25f;

        [Header("Gait")]
        [Tooltip("스틱 기울기가 이 값보다 작으면 걷는다.")]
        [SerializeField, Range(0.1f, 0.9f)] float walkInputThreshold = 0.5f;

        [Header("Rotation")]
        [Tooltip("초당 회전 각도. 소울라이크는 회전이 빠르지만 즉시 돌지는 않는다.")]
        [SerializeField] float rotationSpeed = 720f;

        [Header("Roll")]
        [Tooltip("구르기 입력을 미리 받아 두는 시간(초). 구르는 도중 누르면 끝나자마자 다시 구른다.")]
        [SerializeField] float rollBufferTime = 0.2f;
        [SerializeField] float rollCrossFade = 0.08f;
        [Tooltip("애니메이터 상태를 확인할 수 없을 때 구르기를 강제로 끝내는 시간(초).")]
        [SerializeField] float maxRollDuration = 2.5f;
        [Tooltip("루트 모션을 끈 경우의 구르기 속도와 시간.")]
        [SerializeField] float fallbackRollSpeed = 5f;
        [SerializeField] float fallbackRollDuration = 0.7f;

        [Header("Jump / Air")]
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float jumpBufferTime = 0.15f;
        [Tooltip("공중에서 입력 방향으로 속도를 바꾸는 가속도(m/s²). 0이면 이륙 속도를 그대로 유지한다.")]
        [SerializeField] float airControl = 4f;
        [Tooltip("바닥에서 이 시간(초) 이상 떨어져 있으면 낙하 상태가 된다. 계단에서 깜빡이지 않게 한다.")]
        [SerializeField] float coyoteTime = 0.15f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float groundedStickVelocity = -2f;

        [Header("Animator")]
        [Tooltip("블렌드 파라미터 감쇠 시간(초). 방향 전환 시 애니메이션이 튀지 않게 한다.")]
        [SerializeField] float animatorDampTime = 0.1f;

        static readonly int MoveXHash = Animator.StringToHash("MoveX");
        static readonly int MoveYHash = Animator.StringToHash("MoveY");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");

        CharacterController _controller;
        MotorState _state = MotorState.Grounded;
        float _stateTime;
        float _ungroundedTime;
        float _rollBuffer;
        float _jumpBuffer;

        Vector3 _desiredDirection;
        Gait _gait;
        Vector3 _planarVelocity;
        Vector3 _airVelocity;
        float _verticalVelocity;

        Vector3 _rollDirection;
        Quaternion _rollMotionRotation = Quaternion.identity;

        public bool IsStrafing { get; private set; }
        public bool IsRolling => _state == MotorState.Rolling;
        public bool IsAirborne => _state == MotorState.Airborne;
        public Vector3 PlanarVelocity => _planarVelocity;

        bool RootMotionActive => useRootMotion && animator != null && animator.isActiveAndEnabled;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (lockOn == null) lockOn = GetComponent<LockOnController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            if (animator != null)
            {
                animator.applyRootMotion = useRootMotion;
                // 루트 모션을 받으려면 Animator와 같은 오브젝트에 OnAnimatorMove가 있어야 한다.
                if (animator.GetComponent<AnimatorRootMotionRelay>() == null)
                    animator.gameObject.AddComponent<AnimatorRootMotionRelay>();
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;
            _ungroundedTime = _controller.isGrounded ? 0f : _ungroundedTime + dt;

            Vector2 moveInput = input != null ? input.Move : Vector2.zero;
            _desiredDirection = GetCameraRelativeDirection(moveInput);
            _gait = SelectGait(moveInput.magnitude);

            _rollBuffer = input != null && input.RollPressedThisFrame ? rollBufferTime : _rollBuffer - dt;
            _jumpBuffer = input != null && input.JumpPressedThisFrame ? jumpBufferTime : _jumpBuffer - dt;

            switch (_state)
            {
                case MotorState.Grounded: UpdateGrounded(dt); break;
                case MotorState.Rolling: UpdateRolling(); break;
                case MotorState.Airborne: UpdateAirborne(dt); break;
            }

            UpdateVerticalVelocity(dt);
            UpdateAnimator(dt);

            // 루트 모션을 쓰지 않으면 여기서 직접 움직인다. 쓰면 OnAnimatorMove → ApplyRootMotion에서 움직인다.
            if (!RootMotionActive) MoveCharacter(GetCodeDrivenVelocity(dt) * dt, dt);
        }

        // ───────────────────────── 상태별 처리 ─────────────────────────

        void UpdateGrounded(float dt)
        {
            if (_ungroundedTime > coyoteTime)
            {
                EnterAirborne(jumped: false);
                return;
            }
            if (_jumpBuffer > 0f)
            {
                StartJump();
                return;
            }
            if (_rollBuffer > 0f)
            {
                StartRoll();
                return;
            }

            // 록온 중(질주 제외)이면 대상을, 아니면 이동 방향을 바라본다.
            IsStrafing = lockOn != null && lockOn.IsLockedOn && _gait != Gait.Sprint;
            Vector3 face = IsStrafing
                ? Flatten(lockOn.Current.AimPoint - transform.position)
                : (_gait == Gait.Idle ? Vector3.zero : _desiredDirection);
            RotateTowards(face, rotationSpeed, dt);
        }

        void UpdateRolling()
        {
            bool finished = RootMotionActive
                ? _stateTime > 0.1f && !IsAnimatorInRoll()
                : _stateTime >= fallbackRollDuration;

            if (finished || _stateTime > maxRollDuration) _state = MotorState.Grounded;
        }

        void UpdateAirborne(float dt)
        {
            // 공중 제어: 이륙 속도를 기본으로 유지하고, 입력 방향으로 조금만 바꿀 수 있다.
            float carrySpeed = Mathf.Max(_airVelocity.magnitude, _gait == Gait.Idle ? 0f : walkSpeed);
            Vector3 targetAir = _desiredDirection * carrySpeed;
            if (_gait != Gait.Idle) _airVelocity = Vector3.MoveTowards(_airVelocity, targetAir, airControl * dt);

            IsStrafing = lockOn != null && lockOn.IsLockedOn;
            Vector3 face = IsStrafing ? Flatten(lockOn.Current.AimPoint - transform.position) : _desiredDirection;
            RotateTowards(face, rotationSpeed * 0.5f, dt);

            if (_stateTime > 0.05f && _controller.isGrounded && _verticalVelocity <= 0f) Land();
        }

        void StartRoll()
        {
            _rollBuffer = 0f;
            _state = MotorState.Rolling;
            _stateTime = 0f;

            bool hasInput = _desiredDirection.sqrMagnitude > 0.01f;
            int index;

            if (lockOn != null && lockOn.IsLockedOn)
            {
                // 록온: 대상을 바라본 채 입력 방향에 가장 가까운 전후좌우 구르기를 고른다. 입력이 없으면 뒤로 구른다.
                Vector3 face = Flatten(lockOn.Current.AimPoint - transform.position);
                if (face.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(face);

                _rollDirection = hasInput ? _desiredDirection : -transform.forward;
                Vector3 local = transform.InverseTransformDirection(_rollDirection);
                float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                index = ((Mathf.RoundToInt(angle / 90f) % 4) + 4) % 4;
            }
            else if (hasInput)
            {
                // 자유 이동: 입력 방향으로 즉시 몸을 돌려 앞구르기한다.
                transform.rotation = Quaternion.LookRotation(_desiredDirection);
                _rollDirection = _desiredDirection;
                index = 0;
            }
            else
            {
                // 입력이 없으면 제자리에서 뒤로 피한다(백스텝).
                _rollDirection = -transform.forward;
                index = 2;
            }

            // 대각선 입력이면 클립의 이동 방향(전후좌우)을 실제 입력 방향으로 최대 45도 비틀어 준다.
            Vector3 clipDirection = transform.TransformDirection(RollLocalDirections[index]);
            _rollMotionRotation = Quaternion.AngleAxis(Vector3.SignedAngle(clipDirection, _rollDirection, Vector3.up), Vector3.up);

            if (animator != null) animator.CrossFadeInFixedTime(RollStates[index], rollCrossFade, 0);
        }

        void StartJump()
        {
            _jumpBuffer = 0f;
            _verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
            EnterAirborne(jumped: true);
        }

        void EnterAirborne(bool jumped)
        {
            _state = MotorState.Airborne;
            _stateTime = 0f;
            _airVelocity = _planarVelocity;
            if (animator != null) animator.CrossFadeInFixedTime(jumped ? JumpStartState : JumpLoopState, jumped ? 0.1f : 0.2f, 0);
        }

        void Land()
        {
            _state = MotorState.Grounded;
            _stateTime = 0f;
            _ungroundedTime = 0f;
            // 움직이는 중이면 착지 동작을 건너뛰고 바로 이동으로 이어서 멈칫하지 않게 한다.
            if (animator != null && _gait != Gait.Idle) animator.CrossFadeInFixedTime(LocomotionState, 0.15f, 0);
        }

        void UpdateVerticalVelocity(float dt)
        {
            if (_state != MotorState.Airborne && _controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStickVelocity;
            else
                _verticalVelocity += gravity * dt;

            // 천장에 머리를 부딪히면 상승을 멈춘다.
            if (_verticalVelocity > 0f && (_controller.collisionFlags & CollisionFlags.Above) != 0)
                _verticalVelocity = 0f;
        }

        // ───────────────────────── 이동 적용 ─────────────────────────

        /// <summary>AnimatorRootMotionRelay가 매 애니메이션 프레임 호출한다.</summary>
        public void ApplyRootMotion(Vector3 animationDelta)
        {
            if (!RootMotionActive || !_controller.enabled) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 horizontal;
            switch (_state)
            {
                case MotorState.Rolling:
                    horizontal = _rollMotionRotation * Flatten(animationDelta, keepLength: true);
                    break;
                case MotorState.Airborne:
                    // 공중에서는 애니메이션 이동량을 쓰지 않고 이륙 속도로 난다.
                    horizontal = _airVelocity * dt;
                    break;
                default:
                    horizontal = Flatten(animationDelta, keepLength: true);
                    break;
            }

            MoveCharacter(horizontal, dt);
        }

        Vector3 GetCodeDrivenVelocity(float dt)
        {
            switch (_state)
            {
                case MotorState.Rolling:
                    return _rollDirection * fallbackRollSpeed;
                case MotorState.Airborne:
                    return _airVelocity;
                default:
                    Vector3 target = _gait == Gait.Idle ? Vector3.zero : _desiredDirection * GetSpeed(_gait);
                    return Vector3.MoveTowards(_planarVelocity, target, acceleration * dt);
            }
        }

        void MoveCharacter(Vector3 horizontal, float dt)
        {
            // 점프 이륙 속도로 쓰기 위해 실제 수평 속도를 기록한다(루트 모션 값이 프레임마다 튀지 않게 약간 부드럽게).
            Vector3 measured = horizontal / dt;
            _planarVelocity = RootMotionActive && _state != MotorState.Airborne
                ? Vector3.Lerp(_planarVelocity, measured, 1f - Mathf.Exp(-20f * dt))
                : measured;

            _controller.Move(horizontal + Vector3.up * (_verticalVelocity * dt));
        }

        // ───────────────────────── 애니메이터 ─────────────────────────

        void UpdateAnimator(float dt)
        {
            if (animator == null) return;

            animator.SetBool(GroundedHash, _state != MotorState.Airborne);

            // 블렌드 트리 반지름: 걷기 0.5, 달리기 1, 질주 2. 루트 모션이므로 이 값이 곧 이동 속도를 정한다.
            Vector2 blend = Vector2.zero;
            if (_gait != Gait.Idle)
            {
                float magnitude = GaitToBlendMagnitude(_gait);
                if (IsStrafing)
                {
                    // 캐릭터 기준 입력 방향 → 8방향 스트레이프 클립이 선택된다.
                    Vector3 local = transform.InverseTransformDirection(_desiredDirection);
                    blend = new Vector2(local.x, local.z).normalized * magnitude;
                }
                else
                {
                    // 자유 이동은 몸을 이동 방향으로 돌리므로 항상 전진 클립을 쓴다.
                    blend = Vector2.up * magnitude;
                }
            }

            animator.SetFloat(MoveXHash, blend.x, animatorDampTime, dt);
            animator.SetFloat(MoveYHash, blend.y, animatorDampTime, dt);
        }

        bool IsAnimatorInRoll()
        {
            // 구르기 → 이동 전환이 시작되면 구르기가 끝난 것으로 보고 조작을 돌려준다.
            return animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0).IsTag(RollTag)
                : animator.GetCurrentAnimatorStateInfo(0).IsTag(RollTag);
        }

        // ───────────────────────── 보조 함수 ─────────────────────────

        Vector3 GetCameraRelativeDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude < 1e-4f) return Vector3.zero;

            Vector3 forward = cameraTransform != null ? Flatten(cameraTransform.forward) : Vector3.forward;
            if (forward.sqrMagnitude < 1e-4f) forward = Flatten(transform.forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // 크기는 걷기/달리기 판정에서 이미 썼으므로 방향만 남긴다.
            return (forward * moveInput.y + right * moveInput.x).normalized;
        }

        Gait SelectGait(float inputMagnitude)
        {
            if (inputMagnitude < 0.1f || input == null) return Gait.Idle;
            if (input.SprintHeld) return Gait.Sprint;
            if (input.WalkHeld || inputMagnitude < walkInputThreshold) return Gait.Walk;
            return Gait.Run;
        }

        float GetSpeed(Gait gait)
        {
            switch (gait)
            {
                case Gait.Walk: return walkSpeed;
                case Gait.Run: return runSpeed;
                case Gait.Sprint: return sprintSpeed;
                default: return 0f;
            }
        }

        static float GaitToBlendMagnitude(Gait gait)
        {
            switch (gait)
            {
                case Gait.Walk: return 0.5f;
                case Gait.Run: return 1f;
                case Gait.Sprint: return 2f;
                default: return 0f;
            }
        }

        void RotateTowards(Vector3 direction, float degreesPerSecond, float dt)
        {
            if (direction.sqrMagnitude < 1e-4f) return;
            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * dt);
        }

        static Vector3 Flatten(Vector3 v, bool keepLength = false)
        {
            v.y = 0f;
            if (keepLength) return v;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }
    }
}
