using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike
{
    /// <summary>
    /// 캐릭터 주위를 도는 3인칭 궤도 카메라다.
    /// 록온 중에는 대상 쪽으로 자동 회전하고, 벽에 막히면 카메라를 앞으로 당긴다.
    /// Main Camera에 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform target;
        [Tooltip("비워 두면 target에서 찾는다.")]
        [SerializeField] PlayerInputReader input;
        [Tooltip("비워 두면 target에서 찾는다.")]
        [SerializeField] LockOnController lockOn;

        [Header("Orbit")]
        [SerializeField] float pivotHeight = 1.6f;
        [SerializeField] float distance = 4.5f;
        [SerializeField] float minPitch = -35f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float stickSpeed = 180f;
        [SerializeField] bool invertY;
        [SerializeField] float followSmoothTime = 0.05f;

        [Header("Lock-On")]
        [SerializeField] float lockOnTurnSharpness = 10f;
        [Tooltip("록온 시 대상을 약간 내려다보도록 더하는 피치(도).")]
        [SerializeField] float lockOnPitchOffset = 12f;
        [SerializeField] float recenterDuration = 0.25f;

        [Header("Collision")]
        [SerializeField] float collisionRadius = 0.25f;
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] float minDistance = 0.6f;

        [Header("Cursor")]
        [SerializeField] bool lockCursor = true;

        readonly RaycastHit[] _hits = new RaycastHit[16];

        float _yaw;
        float _pitch = 15f;
        Vector3 _pivot;
        Vector3 _pivotVelocity;
        float _currentDistance;
        float _recenterTimer;
        bool _subscribed;

        void Start()
        {
            if (target == null)
            {
                Debug.LogWarning("[ThirdPersonCamera] target이 비어 있다. 플레이어 Transform을 지정해야 한다.", this);
                enabled = false;
                return;
            }

            if (input == null) input = target.GetComponent<PlayerInputReader>();
            if (lockOn == null) lockOn = target.GetComponent<LockOnController>();
            if (lockOn != null)
            {
                lockOn.RecenterRequested += OnRecenterRequested;
                _subscribed = true;
            }

            _yaw = target.eulerAngles.y;
            _pivot = target.position + Vector3.up * pivotHeight;
            _currentDistance = distance;

            if (lockCursor) SetCursorLocked(true);
        }

        void OnDestroy()
        {
            if (_subscribed && lockOn != null) lockOn.RecenterRequested -= OnRecenterRequested;
        }

        void OnRecenterRequested() => _recenterTimer = recenterDuration;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            HandleCursor();

            Vector3 desiredPivot = target.position + Vector3.up * pivotHeight;
            _pivot = Vector3.SmoothDamp(_pivot, desiredPivot, ref _pivotVelocity, followSmoothTime);

            if (lockOn != null && lockOn.IsLockedOn)
            {
                UpdateLockOnAngles(dt);
            }
            else
            {
                UpdateFreeAngles(dt);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float allowed = GetUnobstructedDistance(_pivot, back, distance);
            // 막히면 즉시 당기고, 풀리면 천천히 원래 거리로 돌아간다(카메라 튐 방지).
            _currentDistance = allowed < _currentDistance
                ? allowed
                : Mathf.Lerp(_currentDistance, allowed, 1f - Mathf.Exp(-6f * dt));

            transform.SetPositionAndRotation(_pivot + back * _currentDistance, rotation);
        }

        void UpdateFreeAngles(float dt)
        {
            if (_recenterTimer > 0f)
            {
                _recenterTimer -= dt;
                float t = 1f - Mathf.Exp(-15f * dt);
                _yaw = Mathf.LerpAngle(_yaw, target.eulerAngles.y, t);
                _pitch = Mathf.LerpAngle(_pitch, 15f, t);
            }

            // 커서가 풀려 있을 때(Esc 누른 뒤)는 마우스로 시점을 돌리지 않는다.
            if (input == null || (lockCursor && Cursor.lockState != CursorLockMode.Locked)) return;

            Vector2 look = input.MouseLookDelta * mouseSensitivity + input.StickLook * (stickSpeed * dt);
            if (look.sqrMagnitude > 1e-6f) _recenterTimer = 0f;

            _yaw += look.x;
            _pitch += invertY ? look.y : -look.y;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        void UpdateLockOnAngles(float dt)
        {
            Vector3 toTarget = lockOn.Current.AimPoint - _pivot;
            if (toTarget.sqrMagnitude < 0.01f) return;

            float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float horizontal = new Vector2(toTarget.x, toTarget.z).magnitude;
            float desiredPitch = -Mathf.Atan2(toTarget.y, horizontal) * Mathf.Rad2Deg + lockOnPitchOffset;
            desiredPitch = Mathf.Clamp(desiredPitch, minPitch, maxPitch);

            float t = 1f - Mathf.Exp(-lockOnTurnSharpness * dt);
            _yaw = Mathf.LerpAngle(_yaw, desiredYaw, t);
            _pitch = Mathf.LerpAngle(_pitch, desiredPitch, t);
        }

        float GetUnobstructedDistance(Vector3 origin, Vector3 direction, float maxDistance)
        {
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, _hits, maxDistance,
                collisionMask, QueryTriggerInteraction.Ignore);

            float nearest = maxDistance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.transform.IsChildOf(target)) continue;
                // distance 0은 시작 지점이 이미 겹친 경우다. 무시하지 않으면 카메라가 캐릭터 머리에 붙는다.
                if (hit.distance <= 0f) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }

            return Mathf.Max(nearest, minDistance);
        }

        void HandleCursor()
        {
            if (!lockCursor) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
