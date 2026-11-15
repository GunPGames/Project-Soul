using System;
using UnityEngine;

namespace Soulslike
{
    public enum WeaponKind { Melee, Ranged }

    /// <summary>
    /// 무기 하나의 데이터다. 모델, 붙일 뼈, 장착 애니메이션, 이동 애니메이션 교체 세트를 담는다.
    /// 코드를 고치지 않고 에셋만 추가해 새 무기를 만들 수 있게 하려는 것이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Soulslike/Weapon Definition", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Serializable]
        public struct Prop
        {
            [Tooltip("무기를 가지고 있는 동안 항상 붙어 있는 부속 모델(예: 칼집).")]
            public GameObject prefab;
            [Tooltip("붙일 뼈 이름(캐릭터 모델 안의 Transform 이름).")]
            public string bone;
            [Tooltip("뼈 기준 회전 보정(도). X = pitch, Y = yaw, Z = roll.")]
            public Vector3 localEulerAngles;

            public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        }

        [Header("Identity")]
        [SerializeField] string displayName = "Weapon";
        [SerializeField] WeaponKind kind = WeaponKind.Melee;

        [Header("Model")]
        [SerializeField] GameObject modelPrefab;
        [Tooltip("장착 중 무기를 붙일 뼈. GhostSamurai 모델은 Weapon_r / Weapon_l이 있다.")]
        [SerializeField] string handBone = "Weapon_r";
        [Tooltip("넣어 둔 상태에서 붙일 뼈(등 등). 비우면 넣어 둔 동안 숨긴다(칼은 칼집 안).")]
        [SerializeField] string holsterBone = "";
        [Tooltip("뼈 기준 위치·회전 보정. GhostSamurai 무기는 0이 맞다.")]
        [SerializeField] Vector3 localPosition = Vector3.zero;
        [SerializeField] Vector3 localEulerAngles = Vector3.zero;
        [Tooltip("넣어 둔 상태(holsterBone)에서만 쓰는 회전 보정(도). 손에 든 자세와 등에 멘 자세가 따로 맞아야 해서 분리한다.")]
        [SerializeField] Vector3 holsterLocalEulerAngles = Vector3.zero;
        [SerializeField] Prop[] props = Array.Empty<Prop>();

        [Header("Animation")]
        [Tooltip("이 무기를 들었을 때의 이동·구르기·점프·장착 클립 교체 세트. 비우면 기본 컨트롤러(칼) 클립을 쓴다.")]
        [SerializeField] AnimatorOverrideController animationOverrides;
        [Tooltip("장착 시 재생할 애니메이터 상태 이름(\"Action\" 태그).")]
        [SerializeField] string equipState = "Equip";
        [Tooltip("장착 애니메이션에서 무기가 손으로 옮겨지는 시점(초).")]
        [SerializeField] float equipAttachTime = 0.35f;

        public string DisplayName => displayName;
        public WeaponKind Kind => kind;
        public GameObject ModelPrefab => modelPrefab;
        public string HandBone => handBone;
        public string HolsterBone => holsterBone;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public Quaternion HolsterLocalRotation => Quaternion.Euler(holsterLocalEulerAngles);
        public Prop[] Props => props;
        public AnimatorOverrideController AnimationOverrides => animationOverrides;
        public string EquipState => equipState;
        public float EquipAttachTime => equipAttachTime;
    }
}
