using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// 무기 슬롯·장착·교체를 담당한다(이슈 #2).
    /// - 무기 모델과 부속(칼집)은 이 컴포넌트가 만들고(Instantiate) 파괴할 때 함께 지운다.
    /// - 교체 시 애니메이터 클립을 무기별 세트로 바꾸고, 장착 동작을 상체 레이어에서만 재생한다(이동은 멈추지 않는다).
    /// - 공격은 하지 않는다. MeleeComboAttack·BowAttack이 Current.Kind를 보고 스스로 동작한다.
    /// 입력보다 늦게, 이동보다 먼저 실행해 같은 프레임에 행동을 시작할 수 있게 한다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public class PlayerWeaponController : MonoBehaviour
    {
        [Header("References (비워 두면 자동으로 찾는다)")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerLocomotion locomotion;
        [SerializeField] Animator animator;

        [Header("Slots")]
        [Tooltip("슬롯 순서 = 숫자 키 순서(1 = 칼, 2 = 활).")]
        [SerializeField] WeaponDefinition[] slots = Array.Empty<WeaponDefinition>();
        [SerializeField] int startSlot;
        [Tooltip("장착 동작으로 들어가는 크로스페이드 시간(초).")]
        [SerializeField] float equipCrossFade = 0.1f;
        [Tooltip("장착 동작을 재생할 상체 레이어. 있으면 이동을 멈추지 않고 상체만 장착 동작을 한다. 없으면 예전처럼 전신 동작(이동 잠금)으로 재생한다.")]
        [SerializeField] string upperBodyEquipLayer = "UpperBodyEquip";
        [Tooltip("상체 장착 레이어 가중치가 바뀌는 빠르기.")]
        [SerializeField] float equipLayerBlendSharpness = 15f;

        class WeaponInstance
        {
            public WeaponDefinition definition;
            public Transform model;
            public Transform hand;
            public Transform holster;
            public readonly List<GameObject> props = new List<GameObject>();
        }

        readonly List<WeaponInstance> _instances = new List<WeaponInstance>();
        readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        readonly List<KeyValuePair<AnimationClip, AnimationClip>> _overrideBuffer = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        AnimatorOverrideController _runtimeOverrides;
        int _currentSlot = -1;
        int _pendingAttachSlot = -1;
        float _equipTimer;
        int _equipLayerIndex = -1;
        float _equipLayerWeight;
        float _equipLayerTimer;
        // 상체 레이어에서 장착 동작을 재생 중인지. 전신 동작으로 재생했으면 false다.
        bool _upperEquipPlaying;
        bool _pendingOnUpperLayer;

        public WeaponDefinition Current => _currentSlot >= 0 ? _instances[_currentSlot].definition : null;
        public int CurrentSlot => _currentSlot;
        public int SlotCount => _instances.Count;
        /// <summary>장착 동작 중(무기가 아직 손에 오지 않음)인지.</summary>
        public bool IsEquipping => _pendingAttachSlot >= 0;
        /// <summary>상체 장착 레이어의 현재 가중치(0~1). 레이어가 없으면 0.</summary>
        public float UpperEquipWeight => _equipLayerWeight;
        public bool IsCurrentKind(WeaponKind kind) => Current != null && Current.Kind == kind && !IsEquipping;

        /// <summary>현재 무기 모델의 Transform. 없으면 null.</summary>
        public Transform CurrentModel => _currentSlot >= 0 ? _instances[_currentSlot].model : null;

        /// <summary>무기가 바뀌었을 때(이전, 새 무기). 시작 시 첫 장착도 포함한다.</summary>
        public event Action<WeaponDefinition, WeaponDefinition> WeaponChanged;

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator != null)
            {
                foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
                    if (!_bones.ContainsKey(t.name)) _bones.Add(t.name, t);
                SetupRuntimeOverrides();
                _equipLayerIndex = animator.GetLayerIndex(upperBodyEquipLayer);
            }

            foreach (WeaponDefinition definition in slots)
                if (definition != null) _instances.Add(CreateInstance(definition));
        }

        void Start()
        {
            if (_instances.Count == 0) return;
            int slot = Mathf.Clamp(startSlot, 0, _instances.Count - 1);
            // 시작할 때는 장착 동작 없이 바로 손에 든다.
            ApplyAnimationSet(_instances[slot].definition);
            for (int i = 0; i < _instances.Count; i++) Place(_instances[i], inHand: i == slot);
            SetCurrent(slot);
        }

        void OnDestroy()
        {
            foreach (WeaponInstance instance in _instances)
            {
                if (instance.model != null) Destroy(instance.model.gameObject);
                foreach (GameObject prop in instance.props) if (prop != null) Destroy(prop);
            }
            if (_runtimeOverrides != null) Destroy(_runtimeOverrides);
        }

        void Update()
        {
            UpdatePendingAttach();
            UpdateEquipLayer(Time.deltaTime);
            if (input == null || _instances.Count == 0) return;

            int requested = input.WeaponSlotPressedThisFrame;
            if (requested < 0 && input.WeaponCycleThisFrame != 0 && _instances.Count > 1)
            {
                int from = Mathf.Max(0, _currentSlot);
                requested = (from + input.WeaponCycleThisFrame + _instances.Count) % _instances.Count;
            }
            if (requested >= 0) TryEquip(requested);
        }

        /// <summary>슬롯의 무기로 바꾼다. 지상에서 다른 행동을 하지 않을 때만 성공한다.</summary>
        public bool TryEquip(int slot)
        {
            if (slot < 0 || slot >= _instances.Count || slot == _currentSlot || IsEquipping) return false;
            if (locomotion != null && !locomotion.CanStartAction) return false;

            WeaponInstance next = _instances[slot];
            WeaponInstance previous = _currentSlot >= 0 ? _instances[_currentSlot] : null;

            // 클립 세트를 먼저 바꿔야 장착 상태가 새 무기의 장착 클립으로 재생된다.
            ApplyAnimationSet(next.definition);

            bool started;
            bool onUpperLayer = _equipLayerIndex >= 0 && animator != null;
            if (onUpperLayer)
            {
                // 상체만 장착 동작을 한다. 하체는 이동을 계속하므로 달리면서도 바꿀 수 있다.
                animator.CrossFadeInFixedTime(next.definition.EquipState, equipCrossFade, _equipLayerIndex, 0f);
                _upperEquipPlaying = true;
                _equipLayerTimer = 0f;
                started = true;
            }
            else
            {
                // 상체 장착 레이어가 없는 예전 컨트롤러: 전신 동작으로 재생한다(이동 잠금).
                started = locomotion != null && locomotion.TryStartAction(next.definition.EquipState, equipCrossFade);
                // 소울라이크 관례: 무기 교체는 구르기로 끊을 수 있다.
                if (started) locomotion.SetActionCancelable(true);
            }
            _pendingOnUpperLayer = started && onUpperLayer;

            // 이전 무기는 바로 넣는다(넣는 동작은 아직 없다).
            if (previous != null) Place(previous, inHand: false);

            if (started)
            {
                _pendingAttachSlot = slot;
                _equipTimer = 0f;
            }
            else
            {
                // 장착 상태가 없으면 애니메이션 없이 바로 든다.
                Place(next, inHand: true);
            }

            SetCurrent(slot);
            return true;
        }

        void UpdatePendingAttach()
        {
            if (_pendingAttachSlot < 0) return;
            _equipTimer += Time.deltaTime;

            WeaponInstance pending = _instances[_pendingAttachSlot];
            // 장착 동작이 구르기 등으로 끊겨도 무기는 손에 들어야 한다.
            // 상체 장착은 지상 이동 중이 정상이므로 구르기·점프·공격 등으로 지상 상태를 벗어나면 끊긴 것으로 본다.
            bool interrupted = locomotion == null ||
                               (_pendingOnUpperLayer ? !locomotion.IsGroundedState : !locomotion.IsInAction);
            if (_equipTimer >= pending.definition.EquipAttachTime || interrupted)
            {
                Place(pending, inHand: true);
                _pendingAttachSlot = -1;
            }
        }

        void UpdateEquipLayer(float dt)
        {
            if (_equipLayerIndex < 0 || animator == null) return;

            if (_upperEquipPlaying)
            {
                _equipLayerTimer += dt;
                // 구르기·점프·공격이 시작되면 그 동작이 상체까지 보이도록 장착 동작을 바로 내린다.
                if (locomotion != null && !locomotion.IsGroundedState) _upperEquipPlaying = false;
                // CrossFade 직후 한두 프레임은 아직 이전 상태이므로 잠시 기다린 뒤 끝났는지 본다.
                else if (_equipLayerTimer > 0.2f && !IsEquipStatePlaying()) _upperEquipPlaying = false;
            }

            float target = _upperEquipPlaying ? 1f : 0f;
            _equipLayerWeight = Mathf.Lerp(_equipLayerWeight, target, 1f - Mathf.Exp(-equipLayerBlendSharpness * dt));
            if (target == 0f && _equipLayerWeight < 0.01f) _equipLayerWeight = 0f;
            animator.SetLayerWeight(_equipLayerIndex, _equipLayerWeight);
        }

        /// <summary>상체 장착 레이어가 장착 상태에 머물러 있는지. 빠져나가는 전환이 시작되면 false다.</summary>
        bool IsEquipStatePlaying()
        {
            string equipState = Current != null ? Current.EquipState : "Equip";
            if (animator.IsInTransition(_equipLayerIndex))
                return animator.GetNextAnimatorStateInfo(_equipLayerIndex).IsName(equipState);
            return animator.GetCurrentAnimatorStateInfo(_equipLayerIndex).IsName(equipState);
        }

        void SetCurrent(int slot)
        {
            WeaponDefinition previous = Current;
            _currentSlot = slot;
            WeaponChanged?.Invoke(previous, Current);
        }

        // ───────────────────────── 애니메이션 세트 ─────────────────────────

        void SetupRuntimeOverrides()
        {
            RuntimeAnimatorController baseController = animator.runtimeAnimatorController;
            if (baseController == null) return;
            if (baseController is AnimatorOverrideController existing) baseController = existing.runtimeAnimatorController;

            // 에셋을 직접 바꾸지 않도록 실행 중에만 쓰는 사본을 만든다.
            _runtimeOverrides = new AnimatorOverrideController(baseController) { name = "PlayerWeaponOverrides (Runtime)" };
            animator.runtimeAnimatorController = _runtimeOverrides;
        }

        void ApplyAnimationSet(WeaponDefinition definition)
        {
            if (_runtimeOverrides == null) return;

            _overrideBuffer.Clear();
            AnimatorOverrideController source = definition != null ? definition.AnimationOverrides : null;
            if (source != null)
            {
                source.GetOverrides(_overrideBuffer);
            }
            else
            {
                // 교체 세트가 없는 무기(칼)는 기본 클립으로 되돌린다.
                _runtimeOverrides.GetOverrides(_overrideBuffer);
                for (int i = 0; i < _overrideBuffer.Count; i++)
                    _overrideBuffer[i] = new KeyValuePair<AnimationClip, AnimationClip>(_overrideBuffer[i].Key, null);
            }
            _runtimeOverrides.ApplyOverrides(_overrideBuffer);
        }

        // ───────────────────────── 모델 배치 ─────────────────────────

        WeaponInstance CreateInstance(WeaponDefinition definition)
        {
            var instance = new WeaponInstance
            {
                definition = definition,
                hand = FindBone(definition.HandBone),
                holster = FindBone(definition.HolsterBone),
            };

            if (definition.ModelPrefab != null)
            {
                Transform parent = instance.holster != null ? instance.holster : transform;
                instance.model = Instantiate(definition.ModelPrefab, parent, false).transform;
                instance.model.name = definition.ModelPrefab.name;
                // 무기 모델의 콜라이더가 자기 CharacterController나 판정과 부딪히지 않게 끈다.
                foreach (Collider c in instance.model.GetComponentsInChildren<Collider>()) c.enabled = false;
                ApplyOffset(instance, inHand: instance.holster == null);
            }

            foreach (WeaponDefinition.Prop prop in definition.Props)
            {
                Transform bone = FindBone(prop.bone);
                if (prop.prefab == null || bone == null) continue;
                GameObject p = Instantiate(prop.prefab, bone, false);
                // "(Clone)" 없이 원래 이름으로 둔다. 찾기·테스트가 이름으로 확인한다(QA B2-12).
                p.name = prop.prefab.name;
                p.transform.localRotation = prop.LocalRotation;
                foreach (Collider c in p.GetComponentsInChildren<Collider>()) c.enabled = false;
                instance.props.Add(p);
            }

            return instance;
        }

        void Place(WeaponInstance instance, bool inHand)
        {
            if (instance.model == null) return;
            Transform parent = inHand ? instance.hand : instance.holster;

            // 넣어 둘 뼈가 없는 무기(칼: 칼집 안에 있는 것으로 본다)는 숨긴다.
            bool visible = parent != null;
            instance.model.gameObject.SetActive(visible);
            if (!visible) return;

            instance.model.SetParent(parent, false);
            ApplyOffset(instance, inHand);
        }

        static void ApplyOffset(WeaponInstance instance, bool inHand)
        {
            instance.model.localPosition = instance.definition.LocalPosition;
            instance.model.localRotation = inHand ? instance.definition.LocalRotation : instance.definition.HolsterLocalRotation;
        }

        Transform FindBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;
            if (_bones.TryGetValue(boneName, out Transform bone)) return bone;
            Debug.LogWarning($"[Weapon] 뼈를 찾을 수 없다: {boneName}", this);
            return null;
        }
    }
}
