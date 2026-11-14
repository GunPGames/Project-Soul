using System;
using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// 록온 대상을 고르고 유지·해제한다.
    /// 대상이 없을 때 록온 버튼을 누르면 카메라 재정렬을 요청한다(소울라이크 관례).
    /// </summary>
    [DisallowMultipleComponent]
    public class LockOnController : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [Tooltip("후보 판정 기준 방향으로 쓸 카메라. 비워 두면 Camera.main을 쓴다.")]
        [SerializeField] Transform cameraTransform;

        [Header("Search")]
        [SerializeField] float maxLockDistance = 20f;
        [Tooltip("카메라 정면에서 이 각도 안에 있는 대상만 후보가 된다.")]
        [SerializeField, Range(10f, 90f)] float maxLockAngle = 60f;
        [Tooltip("록온 중 대상이 이 거리보다 멀어지면 자동 해제한다.")]
        [SerializeField] float breakDistance = 25f;
        [SerializeField] LayerMask lineOfSightMask = ~0;
        [SerializeField] float eyeHeight = 1.5f;

        readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>현재 록온 대상. 없으면 null이다.</summary>
        public LockOnTarget Current { get; private set; }
        public bool IsLockedOn => Current != null;

        /// <summary>대상 없이 록온 버튼을 눌렀을 때 발생한다. 카메라가 캐릭터 뒤로 재정렬한다.</summary>
        public event Action RecenterRequested;

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            // 대상이 파괴·비활성화되었거나 너무 멀어지면 해제한다. (Unity의 == null은 파괴된 오브젝트도 잡아낸다)
            if (Current != null && (!Current.isActiveAndEnabled ||
                (Current.transform.position - transform.position).sqrMagnitude > breakDistance * breakDistance))
            {
                Current = null;
            }

            if (input == null || !input.LockOnPressedThisFrame) return;

            if (Current != null)
            {
                Current = null;
                return;
            }

            Current = FindBestTarget();
            if (Current == null) RecenterRequested?.Invoke();
        }

        LockOnTarget FindBestTarget()
        {
            Vector3 viewForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;

            LockOnTarget best = null;
            float bestScore = float.MaxValue;

            foreach (LockOnTarget candidate in LockOnTarget.Active)
            {
                if (candidate.transform.IsChildOf(transform)) continue;

                Vector3 toTarget = candidate.AimPoint - eye;
                float distance = toTarget.magnitude;
                if (distance > maxLockDistance || distance < 0.01f) continue;

                float angle = Vector3.Angle(viewForward, toTarget);
                if (angle > maxLockAngle) continue;

                if (!HasLineOfSight(eye, candidate, toTarget / distance, distance)) continue;

                // 화면 중앙에 가까운 대상을 우선하고, 거리는 보조 기준으로 쓴다.
                float score = angle + distance * 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        bool HasLineOfSight(Vector3 origin, LockOnTarget candidate, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
            float nearestBlocker = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Transform hit = _hits[i].transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(candidate.transform)) continue;
                nearestBlocker = Mathf.Min(nearestBlocker, _hits[i].distance);
            }
            return nearestBlocker >= distance;
        }
    }
}
