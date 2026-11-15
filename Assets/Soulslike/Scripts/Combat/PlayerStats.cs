using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike
{
    /// <summary>
    /// 플레이어의 체력·기력이다. 피해를 받는 대상(IDamageable)이기도 하다.
    /// - 기력은 행동(공격·구르기·사격)마다 소모되고, 마지막 소모 후 regenDelay초가 지나면 회복된다.
    /// - 소울라이크 관례대로 기력이 0보다 크면 비용보다 적어도 행동할 수 있고, 남은 기력은 0으로 내려간다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] float maxHealth = 100f;

        [Header("Stamina")]
        [SerializeField] float maxStamina = 100f;
        [Tooltip("초당 기력 회복량.")]
        [SerializeField] float staminaRegenRate = 30f;
        [Tooltip("마지막으로 기력을 쓴 뒤 회복이 시작되기까지의 시간(초).")]
        [SerializeField] float staminaRegenDelay = 0.8f;

        [Header("Debug (에디터·개발 빌드에서만)")]
        [Tooltip("이 키를 누르면 자기 자신에게 피해를 준다. 체력바 확인용. None이면 끈다.")]
        [SerializeField] Key debugDamageKey = Key.K;
        [SerializeField] float debugDamageAmount = 15f;

        float _health;
        float _stamina;
        float _regenTimer;

        public float MaxHealth => maxHealth;
        public float MaxStamina => maxStamina;
        public float Health => _health;
        public float Stamina => _stamina;
        public float HealthRatio => maxHealth > 0f ? _health / maxHealth : 0f;
        public float StaminaRatio => maxStamina > 0f ? _stamina / maxStamina : 0f;
        public bool IsDead => _health <= 0f;
        /// <summary>기력이 조금이라도 남아 있으면 행동할 수 있다.</summary>
        public bool HasStamina => _stamina > 0f;

        /// <summary>피해를 받았을 때(피해 정보, 남은 체력).</summary>
        public event Action<DamageInfo, float> Damaged;
        public event Action Died;

        void Awake()
        {
            _health = maxHealth;
            _stamina = maxStamina;
        }

        void Update()
        {
            if (_regenTimer > 0f) _regenTimer -= Time.deltaTime;
            else if (_stamina < maxStamina) _stamina = Mathf.Min(maxStamina, _stamina + staminaRegenRate * Time.deltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (debugDamageKey != Key.None && keyboard != null && keyboard[debugDamageKey].wasPressedThisFrame)
                TakeDamage(new DamageInfo(debugDamageAmount, gameObject, transform.position, Vector3.zero));
#endif
        }

        /// <summary>기력을 쓴다. 남은 기력이 0이면 실패한다. 성공하면 회복 대기 시간이 다시 시작된다.</summary>
        public bool TryConsumeStamina(float cost)
        {
            if (!HasStamina) return false;
            _stamina = Mathf.Max(0f, _stamina - cost);
            _regenTimer = staminaRegenDelay;
            return true;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f) return;
            _health = Mathf.Max(0f, _health - info.Amount);
            Debug.Log($"[Player] 피해 {info.Amount}, 남은 체력 {_health}/{maxHealth}", this);
            Damaged?.Invoke(info, _health);
            if (IsDead) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            _health = Mathf.Min(maxHealth, _health + amount);
        }
    }
}
