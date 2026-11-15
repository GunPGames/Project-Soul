using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// 공격 테스트용 더미다. 맞으면 콘솔에 "Hit: 피해량"을 찍는다. 쓰러지지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [Tooltip("누적 피해량(확인용).")]
        [SerializeField] float totalDamage;

        public float TotalDamage => totalDamage;

        public void TakeDamage(in DamageInfo info)
        {
            totalDamage += info.Amount;
            Debug.Log($"Hit: {info.Amount}", this);
        }
    }
}
