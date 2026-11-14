using System.Collections.Generic;
using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// 록온 가능한 대상임을 표시한다. 활성화된 대상은 정적 목록에 등록되어
    /// LockOnController가 씬 전체를 검색하지 않고 후보를 찾을 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LockOnTarget : MonoBehaviour
    {
        static readonly List<LockOnTarget> s_active = new List<LockOnTarget>();

        /// <summary>현재 활성화된 록온 대상 목록(읽기 전용).</summary>
        public static IReadOnlyList<LockOnTarget> Active => s_active;

        [Tooltip("카메라와 캐릭터가 바라볼 지점. 대상 위치 기준 월드 오프셋이다(보통 가슴 높이).")]
        [SerializeField] Vector3 aimOffset = new Vector3(0f, 1.2f, 0f);

        public Vector3 AimPoint => transform.position + aimOffset;

        void OnEnable() => s_active.Add(this);
        void OnDisable() => s_active.Remove(this);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(AimPoint, 0.15f);
        }
    }
}
