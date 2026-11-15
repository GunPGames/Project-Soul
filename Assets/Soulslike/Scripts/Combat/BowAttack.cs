using System;
using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// 활 조준·발사를 담당한다(이슈 #2).
    /// - 활을 든 상태에서 조준 버튼(우클릭 / L2)을 누르고 있으면 조준: 카메라 줌인, 화면 중앙 조준점, 걷기 스트레이프.
    /// - 조준 중 약공격 버튼(좌클릭 / R1)으로 화면 중앙을 향해 화살을 쏜다.
    /// - 조준 자세는 상체 레이어(UpperBody, 아바타 마스크)에서 재생하므로 하체는 이동 애니메이션을 그대로 재생한다.
    /// - 카메라 상하 각도에 맞춰 척추를 숙이고 젖힌다(애니메이션 뒤 LateUpdate에서 보정).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public class BowAttack : MonoBehaviour
    {
        [Header("References (비워 두면 자동으로 찾는다)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerLocomotion locomotion;
        [SerializeField] PlayerWeaponController weapons;
        [SerializeField] PlayerStats stats;
        [SerializeField] Animator animator;
        [Tooltip("비워 두면 Camera.main에서 찾는다.")]
        [SerializeField] ThirdPersonCamera cameraRig;

        [Header("Arrow")]
        [Tooltip("화살 모델(시각용). 비우면 보이지 않는 화살이 날아간다.")]
        [SerializeField] GameObject arrowModel;
        [Tooltip("화살 모델의 회전 보정. 모델 앞쪽이 +Z가 아니면 맞춘다.")]
        [SerializeField] Vector3 arrowModelEuler = new Vector3(-90f, 0f, 0f);
        [SerializeField] float arrowSpeed = 45f;
        [Tooltip("화살에 걸리는 중력(m/s²). 0이면 일직선.")]
        [SerializeField] float arrowGravity = 3f;
        [SerializeField] float arrowDamage = 15f;
        [SerializeField] float arrowLifetime = 8f;
        [SerializeField] float aimMaxDistance = 100f;
        [SerializeField] LayerMask hitMask = ~0;

        [Header("Timing")]
        [Tooltip("시위를 끝까지 당기는 데 걸리는 시간(초). 끝까지 당겨야만 쏠 수 있다.")]
        [SerializeField] float drawDuration = 0.8f;
        [Tooltip("쏜 뒤 다시 당기기 시작할 때까지의 시간(초).")]
        [SerializeField] float fireCooldown = 0.6f;
        [Tooltip("발사 입력 후 화살이 실제로 나가는 시간(초). 시위를 놓는 순간에 맞춘다.")]
        [SerializeField] float releaseDelay = 0.05f;

        [Header("Stamina (이슈 #3)")]
        [SerializeField] float staminaCostPerShot = 8f;

        [Header("Animator (상체 레이어)")]
        [SerializeField] string upperBodyLayer = "UpperBody";
        [SerializeField] string aimStartState = "Bow_AimStart";
        [SerializeField] string fireState = "Bow_Fire";
        [SerializeField] string aimEndState = "Bow_AimEnd";
        [Tooltip("상체 레이어 가중치가 바뀌는 속도. 클수록 조준 자세로 빨리 바뀐다.")]
        [SerializeField] float layerBlendSharpness = 12f;

        [Header("Spine (조준 상하)")]
        [Tooltip("카메라 상하 각도를 척추·가슴·윗가슴에 나눠 주는 비율. 합이 1이면 카메라 각도만큼 상체가 기운다.")]
        [SerializeField] Vector3 spineWeights = new Vector3(0.3f, 0.35f, 0.35f);
        [Tooltip("카메라 각도가 이 값일 때 상체를 그대로 둔다(조준 클립이 원래 보는 높이).")]
        [SerializeField] float neutralPitch = 0f;
        [SerializeField] float maxSpinePitch = 60f;

        [Header("Hip Stabilize (걸을 때 상체 흔들림 상쇄)")]
        [Tooltip("골반이 기준 자세에서 벗어난 회전을 척추에서 되돌리는 비율. 1이면 상체가 골반 흔들림을 전혀 따라가지 않는다.")]
        [Range(0f, 1f)] [SerializeField] float hipStabilizeWeight = 1f;
        [Tooltip("수평 속도가 이 값(m/s)보다 느리면 '서서 조준' 중으로 보고, 그때의 골반 자세를 기준으로 삼는다.")]
        [SerializeField] float stationarySpeed = 0.15f;
        [Tooltip("서 있는 동안 기준 골반 자세를 따라가는 빠르기. 숨쉬기 같은 작은 움직임은 걸러진다.")]
        [SerializeField] float hipReferenceSharpness = 4f;

        readonly RaycastHit[] _aimHits = new RaycastHit[16];

        int _layerIndex = -1;
        float _layerWeight;
        float _cooldown;
        float _releaseTimer = -1f;
        float _drawTimer;
        Camera _viewCamera;
        Transform _spine, _chest, _upperChest;
        Transform _hips;
        CharacterController _controller;
        // 서서 조준할 때 골반의 캐릭터 기준 회전. 걸을 때는 이 자세에서 벗어난 만큼을 상체에서 되돌린다.
        Quaternion _hipsReference = Quaternion.identity;
        bool _hasHipsReference;

        public bool IsAiming { get; private set; }
        /// <summary>상체 조준 레이어의 현재 가중치(0~1).</summary>
        public float UpperBodyWeight => _layerWeight;
        /// <summary>시위를 당긴 정도(0~1). 1이 되어야 쏠 수 있다. HUD 조준점 연출이 읽는다.</summary>
        public float DrawProgress => drawDuration > 0f ? Mathf.Clamp01(_drawTimer / drawDuration) : 1f;
        public bool IsFullyDrawn => IsAiming && DrawProgress >= 1f;
        /// <summary>이번 세션에 쏜 화살 수.</summary>
        public int ArrowsFired { get; private set; }
        /// <summary>마지막으로 쏜 화살. 테스트·디버그용.</summary>
        public Arrow LastArrow { get; private set; }
        /// <summary>마지막으로 척추에 적용한 상하 각도(도).</summary>
        public float AppliedSpinePitch { get; private set; }
        /// <summary>마지막으로 척추에서 되돌린 골반 흔들림 각도(도).</summary>
        public float AppliedHipCompensation { get; private set; }

        public event Action<Arrow> ArrowFired;

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
            if (weapons == null) weapons = GetComponent<PlayerWeaponController>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _controller = GetComponent<CharacterController>();

            if (animator != null)
            {
                _layerIndex = animator.GetLayerIndex(upperBodyLayer);
                if (_layerIndex < 0) Debug.LogWarning($"[BowAttack] 애니메이터에 '{upperBodyLayer}' 레이어가 없다. Tools > Soulslike > 1을 다시 실행한다.", this);
                if (animator.isHuman)
                {
                    _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                    _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                    _upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                }
            }
        }

        void Start()
        {
            if (Camera.main != null)
            {
                _viewCamera = Camera.main;
                if (cameraRig == null) cameraRig = _viewCamera.GetComponent<ThirdPersonCamera>();
            }
            // 줌·척추 보정이 조용히 빠지지 않게 알린다(QA B2-16).
            if (cameraRig == null) Debug.LogWarning("[BowAttack] ThirdPersonCamera를 찾지 못했다. 조준 줌과 척추 보정이 동작하지 않는다.", this);
        }

        void OnDisable()
        {
            if (IsAiming) SetAiming(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_cooldown > 0f) _cooldown -= dt;

            bool bowReady = weapons != null && weapons.IsCurrentKind(WeaponKind.Ranged);
            // 구르기·점프·낙하·장착 중에는 조준할 수 없다(조준 중이었다면 풀린다).
            bool canAim = bowReady && (locomotion == null || locomotion.IsGroundedState);
            bool wantAim = canAim && input != null && input.AimHeld;
            if (wantAim != IsAiming) SetAiming(wantAim);

            // 쏜 직후(시위를 놓는 중·쿨다운 중)에는 다시 당기지 않는다.
            if (IsAiming && _releaseTimer < 0f && _cooldown <= 0f) _drawTimer += dt;

            // 끝까지 당기기 전의 입력은 버린다(미리 눌러 둔 입력을 쌓아 두지 않는다).
            if (IsFullyDrawn && input.LightAttackPressedThisFrame && _releaseTimer < 0f)
                TryFire();

            if (_releaseTimer >= 0f)
            {
                _releaseTimer -= dt;
                if (_releaseTimer < 0f) SpawnArrow();
            }

            UpdateLayerWeight(dt);
        }

        void LateUpdate()
        {
            // 흔들림을 먼저 되돌린 뒤 상하 숙임을 얹는다. 순서가 바뀌면 숙임 축이 골반을 따라 흔들린다.
            StabilizeUpperBody(Time.deltaTime);
            ApplySpinePitch();
        }

        void StabilizeUpperBody(float dt)
        {
            AppliedHipCompensation = 0f;
            if (_hips == null || _spine == null || _layerWeight <= 0.001f) return;

            Quaternion hipsRelative = Quaternion.Inverse(transform.rotation) * _hips.rotation;

            // 기준 자세는 서서 조준할 때만 갱신한다. 걷는 중의 골반은 상쇄 대상이므로 기준에 섞지 않는다.
            if (IsAiming)
            {
                Vector3 velocity = _controller != null ? _controller.velocity : Vector3.zero;
                velocity.y = 0f;
                bool stationary = velocity.sqrMagnitude < stationarySpeed * stationarySpeed;
                if (!_hasHipsReference)
                {
                    _hipsReference = hipsRelative;
                    _hasHipsReference = true;
                }
                else if (stationary && _layerWeight > 0.95f)
                {
                    _hipsReference = Quaternion.Slerp(_hipsReference, hipsRelative, 1f - Mathf.Exp(-hipReferenceSharpness * dt));
                }
            }

            // 골반이 기준 자세에 있었다면 상체가 놓였을 회전으로 척추를 되돌린다.
            Quaternion correction = transform.rotation * _hipsReference * Quaternion.Inverse(_hips.rotation);
            correction = Quaternion.Slerp(Quaternion.identity, correction, hipStabilizeWeight * _layerWeight);
            _spine.rotation = correction * _spine.rotation;
            AppliedHipCompensation = Quaternion.Angle(Quaternion.identity, correction);
        }

        void SetAiming(bool aiming)
        {
            IsAiming = aiming;
            if (locomotion != null) locomotion.SetAimOverride(aiming);
            if (cameraRig != null) cameraRig.SetAimMode(aiming);

            if (_layerIndex >= 0)
                animator.CrossFadeInFixedTime(aiming ? aimStartState : aimEndState, 0.1f, _layerIndex);

            // 조준을 풀면 아직 시위를 놓지 않은 화살은 쏘지 않는다.
            if (!aiming) _releaseTimer = -1f;
            // 조준을 시작하거나 풀면 시위는 처음부터 다시 당긴다.
            _drawTimer = 0f;
        }

        void TryFire()
        {
            // 기력은 화살이 실제로 나갈 때 쓴다. 시위를 놓기 전에 구르기 등으로 취소되면 쓰지 않는다(QA B2-07).
            if (stats != null && !stats.HasStamina) return;
            _cooldown = fireCooldown;
            _releaseTimer = releaseDelay;
            _drawTimer = 0f;
            if (_layerIndex >= 0) animator.CrossFadeInFixedTime(fireState, 0.05f, _layerIndex);
        }

        void SpawnArrow()
        {
            if (stats != null) stats.TryConsumeStamina(staminaCostPerShot);

            Vector3 aimPoint = GetAimPoint();
            Vector3 origin = GetSafeArrowOrigin(aimPoint);
            Vector3 direction = aimPoint - origin;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();

            var arrowObject = new GameObject("Arrow");
            arrowObject.transform.position = origin;
            if (arrowModel != null)
            {
                GameObject model = Instantiate(arrowModel, arrowObject.transform, false);
                model.transform.localRotation = Quaternion.Euler(arrowModelEuler);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            }

            Arrow arrow = arrowObject.AddComponent<Arrow>();
            arrow.Launch(direction * arrowSpeed, arrowDamage, gameObject, arrowGravity, arrowLifetime, hitMask);

            ArrowsFired++;
            LastArrow = arrow;
            ArrowFired?.Invoke(arrow);
        }

        /// <summary>화면 중앙(조준점)이 가리키는 월드 지점. 플레이어 자신은 무시한다.</summary>
        public Vector3 GetAimPoint()
        {
            if (_viewCamera == null) return transform.position + transform.forward * aimMaxDistance;

            Ray ray = _viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(ray, _aimHits, aimMaxDistance, hitMask, QueryTriggerInteraction.Ignore);
            float best = aimMaxDistance;
            for (int i = 0; i < count; i++)
            {
                if (_aimHits[i].transform.IsChildOf(transform)) continue;
                if (_aimHits[i].distance < best) best = _aimHits[i].distance;
            }
            return ray.GetPoint(best);
        }

        /// <summary>
        /// 화살 시작점을 정한다. 기본은 활(왼손)이다. 다음 두 경우에는 위치를 옮긴다.
        /// - 손이 벽 안이나 벽 너머에 있을 때(가슴 → 손 사이가 막힘): 벽을 뚫거나 뒤로 날아가지 않게 가슴 앞에서 쏜다(QA B2-01).
        /// - 손 → 조준점 사이가 조준점 앞에서 막힐 때(모서리·기둥): 화면 중앙에 보이는 표적을 맞히도록
        ///   카메라 광선 위, 캐릭터 옆 지점에서 쏜다(QA B2-04).
        /// </summary>
        Vector3 GetSafeArrowOrigin(Vector3 aimPoint)
        {
            Vector3 chest = transform.position + Vector3.up * 1.4f;
            Vector3 hand = GetArrowOrigin();

            if (TryRaycastIgnoringSelf(chest, hand, out RaycastHit wall))
                return wall.point - (hand - chest).normalized * 0.05f;

            if (_viewCamera != null && TryRaycastIgnoringSelf(hand, aimPoint, out RaycastHit block) &&
                Vector3.Distance(block.point, aimPoint) > 0.15f)
            {
                Ray ray = _viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                float toPlayer = Vector3.Dot(chest - ray.origin, ray.direction);
                Vector3 onRay = ray.GetPoint(Mathf.Max(0f, toPlayer));
                // 카메라 광선 위 지점도 벽 안이면 손에서 그대로 쏜다(막힌 곳에 박힌다).
                if (!TryRaycastIgnoringSelf(chest, onRay, out _)) return onRay;
            }
            return hand;
        }

        bool TryRaycastIgnoringSelf(Vector3 from, Vector3 to, out RaycastHit nearest)
        {
            nearest = default;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.001f) return false;

            int count = Physics.RaycastNonAlloc(from, delta / distance, _aimHits, distance, hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_aimHits[i].transform.IsChildOf(transform)) continue;
                if (_aimHits[i].distance < best)
                {
                    best = _aimHits[i].distance;
                    nearest = _aimHits[i];
                }
            }
            return best < float.MaxValue;
        }

        Vector3 GetArrowOrigin()
        {
            // 활(왼손)에서 쏜다. 활 모델이 없으면 가슴 높이에서 쏜다.
            Transform bow = weapons != null ? weapons.CurrentModel : null;
            if (bow != null && bow.gameObject.activeInHierarchy) return bow.position;
            return transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
        }

        void UpdateLayerWeight(float dt)
        {
            if (_layerIndex < 0) return;
            float target = IsAiming ? 1f : 0f;
            _layerWeight = Mathf.Lerp(_layerWeight, target, 1f - Mathf.Exp(-layerBlendSharpness * dt));
            if (target == 0f && _layerWeight < 0.01f) _layerWeight = 0f;
            animator.SetLayerWeight(_layerIndex, _layerWeight);
        }

        void ApplySpinePitch()
        {
            AppliedSpinePitch = 0f;
            if (_layerWeight <= 0.001f || cameraRig == null) return;

            // 카메라가 내려다보면(+) 상체를 숙이고, 올려다보면(-) 젖힌다.
            float pitch = Mathf.Clamp(cameraRig.Pitch - neutralPitch, -maxSpinePitch, maxSpinePitch) * _layerWeight;
            Vector3 axis = transform.right;
            Bend(_spine, pitch * spineWeights.x, axis);
            Bend(_chest, pitch * spineWeights.y, axis);
            Bend(_upperChest, pitch * spineWeights.z, axis);
            AppliedSpinePitch = pitch;
        }

        static void Bend(Transform bone, float degrees, Vector3 axis)
        {
            if (bone == null || Mathf.Abs(degrees) < 0.01f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
