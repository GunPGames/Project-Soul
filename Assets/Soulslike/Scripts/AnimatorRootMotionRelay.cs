using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// Animator가 있는 모델 오브젝트에 붙어 루트 모션 이동량을 부모의 PlayerLocomotion에 넘긴다.
    /// OnAnimatorMove는 Animator와 같은 GameObject의 스크립트에서만 호출되므로 이 중계 컴포넌트가 필요하다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class AnimatorRootMotionRelay : MonoBehaviour
    {
        Animator _animator;
        PlayerLocomotion _locomotion;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _locomotion = GetComponentInParent<PlayerLocomotion>();
        }

        // 이 메서드가 있으면 Animator는 루트 모션을 Transform에 직접 적용하지 않고 여기로 넘긴다.
        void OnAnimatorMove()
        {
            if (_locomotion != null) _locomotion.ApplyRootMotion(_animator.deltaPosition);
        }
    }
}
