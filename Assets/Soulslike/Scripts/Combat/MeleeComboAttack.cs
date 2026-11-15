using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// 약공격 콤보(기본 3연타)를 처리한다.
    /// - 지상에서 약공격 버튼 → 1타. 현재 타 애니메이션의 comboTransitionPoint(기본 30%) 지점부터 다음 타로 넘어간다.
    /// - 공격 중 이동은 PlayerLocomotion의 행동(Action) 상태가 잠근다.
    /// - 타격 판정 구간(정규화 시간)에서 앞쪽 구체 범위의 IDamageable에 한 번씩 피해를 준다.
    /// - 타격 판정 구간이 지나면 구르기로 캔슬할 수 있다.
    /// 이동보다 먼저 실행해 같은 프레임에 행동을 시작할 수 있게 한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class MeleeComboAttack : MonoBehaviour
    {
        [Serializable]
        public struct ComboStep
        {
            [Tooltip("재생할 애니메이터 상태 이름(\"Action\" 태그 필요).")]
            public string stateName;
            public float damage;
            [Tooltip("타격 판정 시작(애니메이션 정규화 시간 0~1).")]
            [Range(0f, 1f)] public float hitStart;
            [Tooltip("타격 판정 끝. 이후 구르기 캔슬이 가능하다.")]
            [Range(0f, 1f)] public float hitEnd;
        }

        [Header("References (비워 두면 자동으로 찾는다)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerLocomotion locomotion;
        [SerializeField] Animator animator;
        [Tooltip("있으면 근접 무기(칼)를 들고 있을 때만 공격한다.")]
        [SerializeField] PlayerWeaponController weapons;
        [Tooltip("있으면 타마다 기력을 쓰고, 기력이 0이면 공격하지 못한다.")]
        [SerializeField] PlayerStats stats;

        [Header("Stamina (이슈 #3)")]
        [Tooltip("약공격 1타의 기력 소모량.")]
        [SerializeField] float staminaCostPerHit = 12f;

        [Header("Combo")]
        [SerializeField] ComboStep[] steps =
        {
            new ComboStep { stateName = "Attack_1", damage = 10f, hitStart = 0.25f, hitEnd = 0.45f },
            new ComboStep { stateName = "Attack_2", damage = 12f, hitStart = 0.20f, hitEnd = 0.40f },
            new ComboStep { stateName = "Attack_3", damage = 18f, hitStart = 0.25f, hitEnd = 0.50f },
        };
        [Tooltip("현재 타 애니메이션의 이 지점(정규화 시간 0~1)부터 다음 타로 넘어간다.")]
        [SerializeField, Range(0f, 1f)] float comboTransitionPoint = 0.3f;
        [Tooltip("켜면 전환 지점 전에 누른 입력을 기억했다가 전환 지점에서 바로 다음 타로 넘어간다.")]
        [SerializeField] bool bufferEarlyInput = true;
        [Tooltip("마지막 타 애니메이션의 이 지점(정규화 시간 0~1)부터 이동 입력으로 후딜레이를 끊을 수 있다.")]
        [SerializeField, Range(0f, 1f)] float lastStepMoveCancelPoint = 0.7f;
        [SerializeField] float crossFade = 0.1f;

        [Header("Hitbox (캐릭터 기준)")]
        [SerializeField] Vector3 hitOffset = new Vector3(0f, 1.1f, 1.0f);
        [SerializeField] float hitRadius = 0.9f;
        [SerializeField] LayerMask hitMask = ~0;

        readonly Collider[] _overlaps = new Collider[16];
        readonly HashSet<IDamageable> _alreadyHit = new HashSet<IDamageable>();

        int _step = -1;
        bool _cancelOpened;
        bool _moveCancelOpened;
        bool _queued;

        /// <summary>현재 몇 번째 타인지(0부터). 공격 중이 아니면 -1.</summary>
        public int CurrentStep => _step;
        public int StepCount => steps.Length;
        public ComboStep GetStep(int index) => steps[index];
        public float ComboTransitionPoint => comboTransitionPoint;
        public float LastStepMoveCancelPoint => lastStepMoveCancelPoint;

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (weapons == null) weapons = GetComponent<PlayerWeaponController>();
            if (stats == null) stats = GetComponent<PlayerStats>();
        }

        public float StaminaCostPerHit => staminaCostPerHit;

        void Update()
        {
            if (input == null || locomotion == null || animator == null || steps.Length == 0) return;
            // 활을 들고 있거나 교체 중이면 근접 공격 입력을 받지 않는다(진행 중인 콤보는 마저 처리한다).
            bool meleeEquipped = weapons == null || weapons.IsCurrentKind(WeaponKind.Melee);
            bool pressed = input.LightAttackPressedThisFrame && meleeEquipped;

            // 애니메이션이 끝났거나 구르기로 끊겼으면 콤보를 처음으로 돌린다.
            if (_step >= 0 && !locomotion.IsInAction) _step = -1;

            if (_step < 0)
            {
                if (pressed) StartStep(0, chain: false);
                return;
            }

            if (!TryGetStepState(steps[_step].stateName, out AnimatorStateInfo info)) return;

            ComboStep step = steps[_step];
            float normalized = info.normalizedTime;

            if (normalized >= step.hitStart && normalized <= step.hitEnd) ApplyHits(step);

            if (!_cancelOpened && normalized > step.hitEnd)
            {
                locomotion.SetActionCancelable(true);
                _cancelOpened = true;
            }

            if (_step >= steps.Length - 1)
            {
                // 마지막 타는 이어질 다음 타가 없으므로, 후반부터 이동으로 끊을 수 있게 한다.
                if (!_moveCancelOpened && normalized >= lastStepMoveCancelPoint)
                {
                    locomotion.SetActionMoveCancelable(true);
                    _moveCancelOpened = true;
                }
                return;
            }

            // 전환 지점 전에 누른 입력은 기억해 두고, 전환 지점이 되면 다음 타로 넘어간다.
            if (pressed && bufferEarlyInput) _queued = true;
            if (normalized >= comboTransitionPoint && (pressed || _queued))
                StartStep(_step + 1, chain: true);
        }

        void StartStep(int index, bool chain)
        {
            // 기력이 0이면 휘두르지 않는다. 먼저 확인하고, 행동이 실제로 시작됐을 때만 소모한다.
            if (stats != null && !stats.HasStamina) return;
            if (!locomotion.TryStartAction(steps[index].stateName, crossFade, chain)) return;
            if (stats != null) stats.TryConsumeStamina(staminaCostPerHit);
            _step = index;
            _cancelOpened = false;
            _moveCancelOpened = false;
            _queued = false;
            _alreadyHit.Clear();
        }

        /// <summary>전환 중이면 들어가는 쪽(다음 상태)을, 아니면 현재 상태를 본다.</summary>
        bool TryGetStepState(string stateName, out AnimatorStateInfo info)
        {
            if (animator.IsInTransition(0))
            {
                info = animator.GetNextAnimatorStateInfo(0);
                if (info.IsName(stateName)) return true;
            }
            info = animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(stateName);
        }

        void ApplyHits(ComboStep step)
        {
            Vector3 center = transform.TransformPoint(hitOffset);
            int count = Physics.OverlapSphereNonAlloc(center, hitRadius, _overlaps, hitMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider other = _overlaps[i];
                if (other.transform.IsChildOf(transform)) continue;

                IDamageable target = other.GetComponentInParent<IDamageable>();
                // 한 번 휘두를 때 같은 대상은 한 번만 맞는다(콜라이더가 여러 개여도).
                if (target == null || !_alreadyHit.Add(target)) continue;

                Vector3 point = other.ClosestPoint(center);
                target.TakeDamage(new DamageInfo(step.damage, gameObject, point, transform.forward));
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.TransformPoint(hitOffset), hitRadius);
        }
    }
}
