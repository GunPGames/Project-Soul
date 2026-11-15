using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// GhostSamurai 에셋으로 이동·구르기·점프 애니메이터를 만들고, 플레이어·카메라·록온 더미를 씬에 배치하는 메뉴다.
    /// Tools > Soulslike 메뉴에서 실행한다.
    /// </summary>
    public static class SoulslikeSetupMenu
    {
        const string AnimRoot = "Assets/ThirdParty/GhostSamurai_Animset/Animation/katana/APose/";
        const string MovementRootDir = AnimRoot + "Movement/Root/";
        const string MovementInplaceDir = AnimRoot + "Movement/Inplace/";
        const string DodgeRootDir = AnimRoot + "Dodge/Root/";
        const string IdlePath = AnimRoot + "GhostSamurai_APose_Idle.FBX";
        const string ModelPath = "Assets/ThirdParty/GhostSamurai_Animset/Model/Model_Unity_Ver1.FBX";
        const string OutputDir = "Assets/Soulslike/Animations";
        const string ControllerPath = OutputDir + "/PlayerLocomotion.controller";

        // ── 공격 (이슈 #1: 칼 약공격 3연타) ──
        // 2·3타는 _ALL이 아닌 버전을 쓴다. 앞 타에서 바로 이어지도록 시작 자세가 잘려 있다.
        const string AttackRootDir = AnimRoot + "Attack/Root/";
        static readonly string[] AttackFiles =
        {
            "GhostSamurai_APose_Attack01_1_ALL_Root",
            "GhostSamurai_APose_Attack01_2_Root",
            "GhostSamurai_APose_Attack01_3_Root",
        };
        // MeleeComboAttack의 기본 상태 이름과 같아야 한다.
        static readonly string[] AttackStates = { "Attack_1", "Attack_2", "Attack_3" };
        const string WeaponModelDir = "Assets/ThirdParty/GhostSamurai_Animset/Model/Weapon/";
        const string KatanaModelPath = WeaponModelDir + "SM_Katana01.FBX";
        const string KatanaBone = "Weapon_r";
        const string CombatTestScenePath = "Assets/Scenes/CombatTest.unity";

        // ── 무기 교체 (이슈 #2) ──
        const string KatanaEquipPath = "Assets/ThirdParty/GhostSamurai_Animset/Animation/katana/Common/Unarm&Equip/Root/GhostSamurai_APose_Equip01_Root.FBX";
        const string BowAnimRoot = "Assets/ThirdParty/GhostSamurai_Animset/Animation/Bow/";
        const string WeaponsDir = "Assets/Soulslike/Weapons";
        const string KatanaAssetPath = WeaponsDir + "/Katana.asset";
        const string BowAssetPath = WeaponsDir + "/Bow.asset";
        const string BowOverridePath = OutputDir + "/PlayerLocomotion_Bow.overrideController";
        const string UpperBodyMaskPath = OutputDir + "/UpperBody.mask";
        public const string EquipState = "Equip";
        public const string UpperBodyLayer = "UpperBody";
        public const string UpperBodyEquipLayer = "UpperBodyEquip";

        // 활 이동 교체 클립. Motions와 같은 순서(걷기 8방향 → 달리기 8방향 → 질주)여야 한다.
        static readonly string[] BowMotionFiles =
        {
            "Movement/Root/GhostSamurai_Bow_Walk_F_Root", "Movement/Root/GhostSamurai_Bow_Walk_FR_Root",
            "Movement/Root/GhostSamurai_Bow_Walk_R_Root", "Movement/Root/GhostSamurai_Bow_Walk_BR_Root",
            "Movement/Root/GhostSamurai_Bow_Walk_B_Root", "Movement/Root/GhostSamurai_Bow_Walk_BL_Root",
            "Movement/Root/GhostSamurai_Bow_Walk_L_Root", "Movement/Root/GhostSamurai_Bow_Walk_FL_Root",
            "Movement/Root/GhostSamurai_Bow_Run_F_Root", "Movement/Root/GhostSamurai_Bow_Run_FR_Root",
            "Movement/Root/GhostSamurai_Bow_Run_R_Root", "Movement/Root/GhostSamurai_Bow_Run_BR_Root",
            "Movement/Root/GhostSamurai_Bow_Run_B_Root", "Movement/Root/GhostSamurai_Bow_Run_BL_Root",
            "Movement/Root/GhostSamurai_Bow_Run_L_Root", "Movement/Root/GhostSamurai_Bow_Run_FL_Root",
            "Movement/Root/GhostSamurai_Bow_Sprint_Root",
        };
        // RollFiles 순서(F, R, B, L)와 같아야 한다.
        static readonly string[] BowRollFiles =
        {
            "Dodge/Root/GhostSamurai_Bow_Dodge_F_Root", "Dodge/Root/GhostSamurai_Bow_Dodge_R_Root",
            "Dodge/Root/GhostSamurai_Bow_Dodge_B_Root", "Dodge/Root/GhostSamurai_Bow_Dodge_L_Root",
        };
        const string BowIdleFile = "Attack/Root/GhostSamurai_Bow_Idle_Root";
        const string BowEquipFile = "Common/Root/GhostSamurai_Bow_Common_Equip_Root";
        static readonly string[] BowJumpFiles =
        {
            "Movement/Inplace/GhostSamurai_Bow_Jump_Start_Inplace",
            "Movement/Inplace/GhostSamurai_Bow_Jump_Loop_Inplace",
            "Movement/Inplace/GhostSamurai_Bow_Jump_End_Inplace",
        };

        // 활 상체 레이어(조준). 상체 마스크라서 제자리(Inplace) 클립을 쓴다.
        const string BowAimStartFile = "Attack/Inplace/GhostSamurai_Bow_Shoot_Start_Inplace";
        const string BowAimIdleFile = "Attack/Inplace/GhostSamurai_Bow_Shoot_Aim_Idle_Inplace";
        const string BowFireFile = "Attack/Inplace/GhostSamurai_Bow_Shoot_Loop_Inplace";
        const string BowAimEndFile = "Attack/Inplace/GhostSamurai_Bow_Shoot_Cancel_Inplace";
        const string ArrowModelPath = WeaponModelDir + "SM_Arrow_01.FBX";

        const float WalkRadius = 0.5f;
        const float RunRadius = 1f;
        const float SprintRadius = 2f;

        // 구르기 → 이동으로 넘어가는 시점(정규화 시간). 구르기 뒤 일어서는 동작이 길면 낮추면 된다.
        const float RollExitTime = 0.75f;

        // 2D Freeform Directional 블렌드 트리 배치. 루트 모션(Root) 클립을 써서 발 미끄러짐을 없앤다.
        // 안쪽 원(반지름 0.5) = 걷기 8방향, 바깥 원(반지름 1) = 달리기 8방향, 전방 2 = 질주.
        static readonly (string file, float angleDeg, float radius)[] Motions =
        {
            ("GhostSamurai_APose_Strafe_Walk_F_Loop_Root", 0f, WalkRadius),
            ("GhostSamurai_APose_Strafe_Walk_FR_Root", 45f, WalkRadius),
            ("GhostSamurai_APose_Strafe_Walk_R_Root", 90f, WalkRadius),
            ("GhostSamurai_APose_Strafe_Walk_BR", 135f, WalkRadius), // 원본 파일명에 _Root가 빠져 있다.
            ("GhostSamurai_APose_Strafe_Walk_B_Root", 180f, WalkRadius),
            ("GhostSamurai_APose_Strafe_Walk_BL_Root", 225f, WalkRadius),
            ("GhostSamurai_APose_Strafe_Walk_L_Root", 270f, WalkRadius),
            ("GhostSamurai_APose_Strafe_Walk_FL_Root", 315f, WalkRadius),

            ("GhostSamurai_APose_Strafe_Run_F_Loop_Root", 0f, RunRadius),
            ("GhostSamurai_APose_Strafe_Run_FR_Root", 45f, RunRadius),
            ("GhostSamurai_APose_Strafe_Run_R_Root", 90f, RunRadius),
            ("GhostSamurai_APose_Strafe_Run_BR_Root", 135f, RunRadius),
            ("GhostSamurai_APose_Strafe_Run_B_Root", 180f, RunRadius),
            ("GhostSamurai_APose_Strafe_Run_BL_Root", 225f, RunRadius),
            ("GhostSamurai_APose_Strafe_Run_L_Root", 270f, RunRadius),
            ("GhostSamurai_APose_Strafe_Run_FL_Root", 315f, RunRadius),

            ("GhostSamurai_APose_Strafe_Run_F_Sprint_Root", 0f, SprintRadius),
        };

        // PlayerLocomotion.RollStates 순서(F, R, B, L)와 같아야 한다.
        static readonly string[] RollFiles =
        {
            "GhostSamurai_APose_Dodge_F_Root",
            "GhostSamurai_APose_Dodge_R_Root",
            "GhostSamurai_APose_Dodge_B_Root",
            "GhostSamurai_APose_Dodge_L_Root",
        };

        [MenuItem("Tools/Soulslike/1. Build Player Animator", priority = 1)]
        public static AnimatorController BuildAnimator()
        {
            EnsureFolder(OutputDir);

            AnimationClip idle = LoadClip(IdlePath, loop: true);
            if (idle == null) return null;

            var moveClips = new AnimationClip[Motions.Length];
            for (int i = 0; i < Motions.Length; i++)
            {
                moveClips[i] = LoadClip(MovementRootDir + Motions[i].file + ".FBX", loop: true);
                if (moveClips[i] == null) return null;
            }

            var rollClips = new AnimationClip[RollFiles.Length];
            for (int i = 0; i < RollFiles.Length; i++)
            {
                rollClips[i] = LoadClip(DodgeRootDir + RollFiles[i] + ".FBX", loop: false);
                if (rollClips[i] == null) return null;
            }

            AnimationClip jumpStart = LoadClip(MovementInplaceDir + "GhostSamurai_APose_Jump_Start_Inplace.FBX", loop: false);
            AnimationClip jumpLoop = LoadClip(MovementInplaceDir + "GhostSamurai_APose_Jump_Loop_Inplace.FBX", loop: true);
            AnimationClip jumpEnd = LoadClip(MovementInplaceDir + "GhostSamurai_APose_Jump_End_Inplace.FBX", loop: false);
            if (jumpStart == null || jumpLoop == null || jumpEnd == null) return null;

            var attackClips = new AnimationClip[AttackFiles.Length];
            for (int i = 0; i < AttackFiles.Length; i++)
            {
                attackClips[i] = LoadClip(AttackRootDir + AttackFiles[i] + ".FBX", loop: false);
                if (attackClips[i] == null) return null;
            }

            AnimationClip katanaEquip = LoadClip(KatanaEquipPath, loop: false);
            AnimationClip bowAimStart = LoadClip(BowAnimRoot + BowAimStartFile + ".FBX", loop: false);
            AnimationClip bowAimIdle = LoadClip(BowAnimRoot + BowAimIdleFile + ".FBX", loop: true);
            AnimationClip bowFire = LoadClip(BowAnimRoot + BowFireFile + ".FBX", loop: false);
            AnimationClip bowAimEnd = LoadClip(BowAnimRoot + BowAimEndFile + ".FBX", loop: false);
            if (katanaEquip == null || bowAimStart == null || bowAimIdle == null || bowFire == null || bowAimEnd == null) return null;

            // 이 메뉴가 만든 컨트롤러이므로 다시 실행하면 새로 만든다.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "Grounded",
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true,
            });

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // ── 이동 블렌드 트리 ──
            var tree = new BlendTree
            {
                name = "Locomotion8Dir",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.AddChild(idle, Vector2.zero);
            for (int i = 0; i < Motions.Length; i++)
            {
                float rad = Motions[i].angleDeg * Mathf.Deg2Rad;
                // 각도 0 = 전방(+Y), 90 = 오른쪽(+X)
                Vector2 position = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * Motions[i].radius;
                tree.AddChild(moveClips[i], position);
            }

            AnimatorState locomotion = sm.AddState(PlayerLocomotion.LocomotionState, new Vector3(300, 0, 0));
            locomotion.motion = tree;
            sm.defaultState = locomotion;

            // ── 구르기: 코드가 CrossFade로 진입하고, 끝나면 이동으로 돌아온다 ──
            for (int i = 0; i < rollClips.Length; i++)
            {
                AnimatorState roll = sm.AddState(PlayerLocomotion.RollStates[i], new Vector3(600, -150 + i * 70, 0));
                roll.motion = rollClips[i];
                roll.tag = PlayerLocomotion.RollTag;
                AddExitTimeTransition(roll, locomotion, RollExitTime, 0.15f);
            }

            // ── 점프: 시작 → 공중 반복 → 착지 → 이동 ──
            AnimatorState start = sm.AddState(PlayerLocomotion.JumpStartState, new Vector3(0, 150, 0));
            start.motion = jumpStart;
            AnimatorState loop = sm.AddState(PlayerLocomotion.JumpLoopState, new Vector3(250, 220, 0));
            loop.motion = jumpLoop;
            AnimatorState end = sm.AddState("JumpEnd", new Vector3(500, 150, 0));
            end.motion = jumpEnd;

            AddExitTimeTransition(start, loop, 0.95f, 0.1f);
            AddConditionTransition(start, end, "Grounded", 0.1f);
            AddConditionTransition(loop, end, "Grounded", 0.1f);
            AddExitTimeTransition(end, locomotion, 0.7f, 0.2f);

            // ── 약공격 3연타: "Action" 태그 동안 이동이 잠긴다 ──
            // 애니메이션 끝(1.0)에서 이동으로 돌아간다. 그래야 마지막 0.3초 콤보 입력 구간이 온전히 남는다.
            for (int i = 0; i < attackClips.Length; i++)
            {
                AnimatorState attack = sm.AddState(AttackStates[i], new Vector3(-250, -150 + i * 70, 0));
                attack.motion = attackClips[i];
                attack.tag = PlayerLocomotion.ActionTag;
                AddExitTimeTransition(attack, locomotion, 1f, 0.15f);
            }

            // ── 무기 장착 (이슈 #2): "Action" 태그 동안 이동이 잠긴다. 활은 교체 세트가 클립을 바꾼다 ──
            AnimatorState equip = sm.AddState(EquipState, new Vector3(0, -150, 0));
            equip.motion = katanaEquip;
            equip.tag = PlayerLocomotion.ActionTag;
            AddExitTimeTransition(equip, locomotion, 0.85f, 0.15f);

            AvatarMask upperMask = BuildUpperBodyMask();

            // ── 상체 장착 레이어: 무기 교체 동작을 상체에서만 재생해 달리면서도 바꿀 수 있게 한다 ──
            // 조준 레이어보다 앞(아래)에 둔다. 장착 동작이 끝나기 전에 조준을 시작하면 조준 자세가 이긴다.
            controller.AddLayer(UpperBodyEquipLayer);
            AnimatorControllerLayer[] equipLayers = controller.layers;
            int equipIndex = equipLayers.Length - 1;
            equipLayers[equipIndex].avatarMask = upperMask;
            equipLayers[equipIndex].blendingMode = AnimatorLayerBlendingMode.Override;
            equipLayers[equipIndex].defaultWeight = 0f; // 가중치는 PlayerWeaponController가 교체할 때만 올린다.
            controller.layers = equipLayers;

            AnimatorStateMachine equipMachine = controller.layers[equipIndex].stateMachine;
            AnimatorState equipEmpty = equipMachine.AddState("Empty", new Vector3(300, 0, 0));
            equipMachine.defaultState = equipEmpty;
            AnimatorState upperEquip = equipMachine.AddState(EquipState, new Vector3(300, 120, 0));
            // 기본 레이어의 장착 상태와 같은 클립이라 활의 교체 세트(오버라이드)도 그대로 적용된다.
            upperEquip.motion = katanaEquip;
            AddExitTimeTransition(upperEquip, equipEmpty, 0.85f, 0.15f);

            // ── 상체 레이어 (이슈 #2): 활 조준 자세. 하체는 기본 레이어의 이동을 그대로 재생한다 ──
            controller.AddLayer(UpperBodyLayer);
            AnimatorControllerLayer[] layers = controller.layers;
            int upperIndex = layers.Length - 1;
            layers[upperIndex].avatarMask = upperMask;
            layers[upperIndex].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[upperIndex].defaultWeight = 0f; // 가중치는 BowAttack이 조준할 때만 올린다.
            controller.layers = layers;

            AnimatorStateMachine upper = controller.layers[upperIndex].stateMachine;
            AnimatorState empty = upper.AddState("Empty", new Vector3(300, 0, 0));
            upper.defaultState = empty;
            AnimatorState aimStart = upper.AddState("Bow_AimStart", new Vector3(0, 120, 0));
            aimStart.motion = bowAimStart;
            AnimatorState aimIdle = upper.AddState("Bow_AimIdle", new Vector3(300, 200, 0));
            aimIdle.motion = bowAimIdle;
            AnimatorState fire = upper.AddState("Bow_Fire", new Vector3(600, 120, 0));
            fire.motion = bowFire;
            AnimatorState aimEnd = upper.AddState("Bow_AimEnd", new Vector3(600, 0, 0));
            aimEnd.motion = bowAimEnd;
            AddExitTimeTransition(aimStart, aimIdle, 0.95f, 0.1f);
            AddExitTimeTransition(fire, aimIdle, 0.9f, 0.1f);
            AddExitTimeTransition(aimEnd, empty, 0.95f, 0.1f);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            if (!BuildWeaponAssets(controller, idle, katanaEquip, moveClips, rollClips, new[] { jumpStart, jumpLoop, jumpEnd }))
                return null;

            // 컨트롤러를 새로 만들면 GUID가 바뀌므로, 씬에 이미 있는 플레이어의 참조를 다시 연결한다.
            foreach (PlayerLocomotion player in Object.FindObjectsByType<PlayerLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                if (animator == null) continue;
                Undo.RecordObject(animator, "Assign Player Animator");
                animator.runtimeAnimatorController = controller;
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            }

            Debug.Log($"[Soulslike] 애니메이터를 만들었다: {ControllerPath}", controller);
            return controller;
        }

        [MenuItem("Tools/Soulslike/2. Create Or Update Player In Scene", priority = 2)]
        public static void CreatePlayerInScene()
        {
            // 던전 Room A 안쪽(바닥 위)에 놓는다. 다른 씬이면 Inspector에서 옮기면 된다.
            CreateOrUpdatePlayer(new Vector3(4f, 0.05f, 4f), new Vector3(4f, 0f, 4f));
        }

        [MenuItem("Tools/Soulslike/3. Create Combat Test Scene", priority = 3)]
        public static void CreateCombatTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 기본 카메라·조명이 있는 새 씬에 바닥(Plane)·플레이어·더미를 둔다.
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f); // Plane 기본 10m → 40m × 40m

            // 더미는 플레이어 정면 2.5m에 둔다. 바로 공격해 볼 수 있다.
            CreateOrUpdatePlayer(new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0f, 2.5f));

            EditorSceneManager.SaveScene(scene, CombatTestScenePath);
            Debug.Log($"[Soulslike] 전투 테스트 씬을 만들었다: {CombatTestScenePath}");
        }

        static void CreateOrUpdatePlayer(Vector3 spawn, Vector3 dummyOffset)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            // 이전 버전(구르기·점프·공격·장착·활 레이어 없음) 컨트롤러면 다시 만든다.
            bool outdated = controller == null ||
                            controller.parameters.All(p => p.name != "Grounded") ||
                            controller.layers[0].stateMachine.states.All(s => s.state.name != AttackStates[0]) ||
                            controller.layers[0].stateMachine.states.All(s => s.state.name != EquipState) ||
                            controller.layers.All(l => l.name != UpperBodyLayer) ||
                            controller.layers.All(l => l.name != UpperBodyEquipLayer) ||
                            AssetDatabase.LoadAssetAtPath<WeaponDefinition>(BowAssetPath) == null;
            if (outdated) controller = BuildAnimator();
            if (controller == null) return;

            // 이미 플레이어가 있으면 새로 만들지 않고 빠진 컴포넌트만 채운다.
            PlayerLocomotion existing = Object.FindFirstObjectByType<PlayerLocomotion>(FindObjectsInactive.Include);
            if (existing != null)
            {
                ConfigurePlayer(existing.gameObject, existing.GetComponentInChildren<Animator>(true), controller);
                SetupCamera(existing.transform, moveCamera: false);
                EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Soulslike] 기존 Player를 갱신했다(무기 교체·활·체력/기력·HUD 포함).", existing);
                return;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[Soulslike] 모델을 찾을 수 없다: {ModelPath}");
                return;
            }

            var player = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(player, "Create Soulslike Player");
            player.transform.position = spawn;

            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, 0.9f, 0f);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, player.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            if (animator.avatar == null)
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();

            ConfigurePlayer(player, animator, controller);
            SetupCamera(player.transform, moveCamera: true);
            CreateLockOnDummy(spawn + dummyOffset);

            EditorSceneManager.MarkSceneDirty(player.scene);
            Selection.activeGameObject = player;
            Debug.Log("[Soulslike] Player, 카메라, 록온 더미를 배치했다. Play를 눌러 확인한다.", player);
        }

        static void ConfigurePlayer(GameObject player, Animator animator, AnimatorController controller)
        {
            if (animator != null)
            {
                Undo.RecordObject(animator, "Configure Player Animator");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = true;
                // 화면 밖에서도 루트 모션이 계산되어야 이동이 멈추지 않는다.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                EnsureComponent<AnimatorRootMotionRelay>(animator.gameObject);
                RemoveStaticKatana(animator.transform);
            }

            EnsureComponent<PlayerInputReader>(player);
            EnsureComponent<LockOnController>(player);
            EnsureComponent<PlayerLocomotion>(player);
            EnsureComponent<LockOnIndicator>(player);
            EnsureComponent<MeleeComboAttack>(player);

            // 이슈 #3: 체력·기력과 HUD.
            EnsureComponent<PlayerStats>(player);
            EnsureComponent<PlayerHud>(player);

            // 이슈 #2: 무기 슬롯(1 = 칼, 2 = 활)과 활 공격.
            PlayerWeaponController weapons = EnsureComponent<PlayerWeaponController>(player);
            var weaponsSo = new SerializedObject(weapons);
            SerializedProperty slots = weaponsSo.FindProperty("slots");
            slots.arraySize = 2;
            slots.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(KatanaAssetPath);
            slots.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(BowAssetPath);
            weaponsSo.ApplyModifiedProperties();

            BowAttack bow = EnsureComponent<BowAttack>(player);
            var bowSo = new SerializedObject(bow);
            bowSo.FindProperty("arrowModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowModelPath);
            // SM_Arrow_01은 앞쪽이 +Z가 아니라서 X축으로 -90° 돌려야 날아가는 방향을 향한다(플레이 테스트로 확인).
            bowSo.FindProperty("arrowModelEuler").vector3Value = new Vector3(-90f, 0f, 0f);
            bowSo.ApplyModifiedProperties();
        }

        /// <summary>
        /// 이슈 #1 때 오른손에 고정으로 붙여 둔 칼 모델을 지운다. 이제 PlayerWeaponController가 실행 중에 붙인다.
        /// 남겨 두면 칼이 두 자루가 되고, 활을 들어도 오른손에 칼이 남는다.
        /// </summary>
        static void RemoveStaticKatana(Transform modelRoot)
        {
            var katana = AssetDatabase.LoadAssetAtPath<GameObject>(KatanaModelPath);
            if (katana == null) return;
            foreach (Transform bone in modelRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == KatanaBone).ToArray())
            {
                Transform stale = bone.Find(katana.name);
                if (stale != null) Undo.DestroyObjectImmediate(stale.gameObject);
            }
        }

        /// <summary>
        /// 상체 마스크: 몸통(척추)·머리·팔·손가락·손 IK만 켠다. 다리·발·루트는 기본 레이어(이동)를 따른다.
        /// </summary>
        static AvatarMask BuildUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "UpperBody" };
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                             part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers ||
                             part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>
        /// 활 교체 세트(AnimatorOverrideController)와 칼·활 무기 에셋을 만든다.
        /// 에셋이 이미 있으면 새로 만들지 않고 값만 갱신한다. 그래야 씬의 참조(GUID)가 유지된다.
        /// </summary>
        static bool BuildWeaponAssets(AnimatorController controller, AnimationClip idle, AnimationClip equip,
            AnimationClip[] moveClips, AnimationClip[] rollClips, AnimationClip[] jumpClips)
        {
            EnsureFolder(WeaponsDir);

            var pairs = new System.Collections.Generic.List<(AnimationClip original, string bowFile, bool loop)>
            {
                (idle, BowIdleFile, true),
                (equip, BowEquipFile, false),
            };
            for (int i = 0; i < moveClips.Length; i++) pairs.Add((moveClips[i], BowMotionFiles[i], true));
            for (int i = 0; i < rollClips.Length; i++) pairs.Add((rollClips[i], BowRollFiles[i], false));
            for (int i = 0; i < jumpClips.Length; i++) pairs.Add((jumpClips[i], BowJumpFiles[i], i == 1));

            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(BowOverridePath);
            if (overrides == null)
            {
                overrides = new AnimatorOverrideController(controller);
                AssetDatabase.CreateAsset(overrides, BowOverridePath);
            }
            else
            {
                // 컨트롤러를 다시 만들었으므로 기준 컨트롤러를 새것으로 바꾼다.
                overrides.runtimeAnimatorController = controller;
            }

            foreach ((AnimationClip original, string bowFile, bool loop) in pairs)
            {
                AnimationClip bowClip = LoadClip(BowAnimRoot + bowFile + ".FBX", loop);
                if (bowClip == null) return false;
                overrides[original] = bowClip;
            }
            EditorUtility.SetDirty(overrides);

            GameObject katanaModel = AssetDatabase.LoadAssetAtPath<GameObject>(KatanaModelPath);
            GameObject scabbardModel = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponModelDir + "SM_Scabbard01.FBX");
            GameObject bowModel = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponModelDir + "SM_Bow_01.FBX");
            if (katanaModel == null || bowModel == null)
            {
                Debug.LogError($"[Soulslike] 무기 모델을 찾을 수 없다: {WeaponModelDir}");
                return false;
            }

            // 칼: 오른손(Weapon_r). 넣어 두면 칼집 안에 있는 것으로 보고 숨긴다. 칼집은 허리(Scabbard_Target01)에 항상 찬다.
            WriteWeapon(KatanaAssetPath, "Katana", WeaponKind.Melee, katanaModel, KatanaBone, "",
                scabbardModel, "Scabbard_Target01", null, propEuler: new Vector3(0f, 90f, 0f));
            // 활: 왼손(Weapon_l). 넣어 두면 등(Bow_BackTarget)에 멘다.
            // 회전 보정은 플레이 테스트로 맞춘 값이다(칼집 yaw 90°, 손에 든 활 yaw 90°).
            WriteWeapon(BowAssetPath, "Bow", WeaponKind.Ranged, bowModel, "Weapon_l", "Bow_BackTarget",
                null, null, overrides, handEuler: new Vector3(0f, 90f, 0f));

            AssetDatabase.SaveAssets();
            return true;
        }

        static void WriteWeapon(string path, string displayName, WeaponKind kind, GameObject model, string handBone,
            string holsterBone, GameObject propPrefab, string propBone, AnimatorOverrideController overrides,
            Vector3 propEuler = default, Vector3 holsterEuler = default, Vector3 handEuler = default)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, path);
            }

            var so = new SerializedObject(weapon);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("modelPrefab").objectReferenceValue = model;
            so.FindProperty("handBone").stringValue = handBone;
            so.FindProperty("holsterBone").stringValue = holsterBone;
            so.FindProperty("animationOverrides").objectReferenceValue = overrides;
            so.FindProperty("equipState").stringValue = EquipState;
            so.FindProperty("localEulerAngles").vector3Value = handEuler;
            so.FindProperty("holsterLocalEulerAngles").vector3Value = holsterEuler;

            SerializedProperty props = so.FindProperty("props");
            props.arraySize = propPrefab != null ? 1 : 0;
            if (propPrefab != null)
            {
                SerializedProperty prop = props.GetArrayElementAtIndex(0);
                prop.FindPropertyRelative("prefab").objectReferenceValue = propPrefab;
                prop.FindPropertyRelative("bone").stringValue = propBone;
                prop.FindPropertyRelative("localEulerAngles").vector3Value = propEuler;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        static void SetupCamera(Transform player, bool moveCamera)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                moveCamera = true;
            }

            ThirdPersonCamera follow = EnsureComponent<ThirdPersonCamera>(camera.gameObject);
            var serialized = new SerializedObject(follow);
            serialized.FindProperty("target").objectReferenceValue = player;
            serialized.ApplyModifiedProperties();

            if (!moveCamera) return;
            camera.nearClipPlane = 0.1f;
            camera.transform.SetPositionAndRotation(
                player.position + new Vector3(0f, 2.5f, -4.5f),
                Quaternion.Euler(15f, 0f, 0f));
        }

        static void CreateLockOnDummy(Vector3 position)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(dummy, "Create Lock-On Dummy");
            dummy.name = "LockOn Dummy";
            dummy.transform.position = position + Vector3.up;
            dummy.AddComponent<LockOnTarget>();
            dummy.AddComponent<TrainingDummy>();
        }

        static void AddExitTimeTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = duration;
        }

        static void AddConditionTransition(AnimatorState from, AnimatorState to, string boolParameter, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
            t.AddCondition(AnimatorConditionMode.If, 0f, boolParameter);
        }

        /// <summary>
        /// 클립을 불러오고 임포트 설정을 맞춘다.
        /// - loop: 반복 재생(Loop Time)을 켠다. GhostSamurai 클립은 기본값이 꺼져 있다.
        /// - 루트 회전과 높이(Y)는 포즈에 굽고(Bake Into Pose), 수평 이동(XZ)만 루트 모션으로 남긴다.
        ///   그래야 코드가 정한 방향을 애니메이션이 흐트러뜨리지 않는다.
        /// </summary>
        static AnimationClip LoadClip(string path, bool loop)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[Soulslike] 애니메이션 FBX를 찾을 수 없다: {path}");
                return null;
            }

            ModelImporterClipAnimation[] clipSettings = importer.clipAnimations;
            if (clipSettings == null || clipSettings.Length == 0) clipSettings = importer.defaultClipAnimations;

            bool changed = false;
            foreach (ModelImporterClipAnimation setting in clipSettings)
            {
                if (loop && !setting.loopTime) { setting.loopTime = true; changed = true; }
                if (!setting.lockRootRotation) { setting.lockRootRotation = true; changed = true; }
                if (!setting.lockRootHeightY) { setting.lockRootHeightY = true; changed = true; }
                if (setting.lockRootPositionXZ) { setting.lockRootPositionXZ = false; changed = true; }
                if (!setting.keepOriginalOrientation) { setting.keepOriginalOrientation = true; changed = true; }
                if (!setting.keepOriginalPositionY) { setting.keepOriginalPositionY = true; changed = true; }
            }

            if (changed)
            {
                importer.clipAnimations = clipSettings;
                importer.SaveAndReimport();
            }

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            if (clip == null) Debug.LogError($"[Soulslike] FBX 안에 애니메이션 클립이 없다: {path}");
            return clip;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
