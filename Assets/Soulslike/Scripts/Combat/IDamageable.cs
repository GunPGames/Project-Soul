using UnityEngine;

namespace Soulslike
{
    /// <summary>피해 한 번의 정보. 피해량 외에 넉백·피격 방향 처리를 나중에 붙일 수 있게 출처와 위치를 함께 넘긴다.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;

        public DamageInfo(float amount, GameObject source, Vector3 point, Vector3 direction)
        {
            Amount = amount;
            Source = source;
            Point = point;
            Direction = direction;
        }
    }

    /// <summary>피해를 받을 수 있는 대상. 더미·적·부서지는 오브젝트가 구현한다.</summary>
    public interface IDamageable
    {
        void TakeDamage(in DamageInfo info);
    }
}
