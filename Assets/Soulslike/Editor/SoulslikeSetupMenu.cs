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

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

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
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || controller.parameters.All(p => p.name != "Grounded"))
                controller = BuildAnimator(); // 이전 버전(구르기·점프 없음) 컨트롤러면 다시 만든다.
            if (controller == null) return;

            // 이미 플레이어가 있으면 새로 만들지 않고 빠진 컴포넌트만 채운다.
            PlayerLocomotion existing = Object.FindFirstObjectByType<PlayerLocomotion>(FindObjectsInactive.Include);
            if (existing != null)
            {
                ConfigurePlayer(existing.gameObject, existing.GetComponentInChildren<Animator>(true), controller);
                SetupCamera(existing.transform, moveCamera: false);
                EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Soulslike] 기존 Player를 갱신했다(루트 모션·구르기·점프·록온 표시).", existing);
                return;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[Soulslike] 모델을 찾을 수 없다: {ModelPath}");
                return;
            }

            // 던전 Room A 안쪽(바닥 위)에 놓는다. 다른 씬이면 Inspector에서 옮기면 된다.
            Vector3 spawn = new Vector3(4f, 0.05f, 4f);

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
            CreateLockOnDummy(spawn + new Vector3(4f, 0f, 4f));

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
            }

            EnsureComponent<PlayerInputReader>(player);
            EnsureComponent<LockOnController>(player);
            EnsureComponent<PlayerLocomotion>(player);
            EnsureComponent<LockOnIndicator>(player);
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
