using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Soulslike.Tests
{
    /// <summary>
    /// 이슈 #2(활)·#3(체력·기력 HUD) 시뮬레이션 테스트.
    /// 조준은 패드 L2, 발사는 패드 R1로 넣는다(테스트 중 마우스 커서 잠금이 보장되지 않기 때문이다).
    /// 무기 교체는 키보드 1·2로 넣는다.
    /// </summary>
    public class BowAndStatsTests : InputTestFixture
    {
        const string ScenePath = "Assets/Scenes/CombatTest.unity";

        Gamepad _pad;
        Keyboard _keyboard;

        PlayerLocomotion _player;
        PlayerWeaponController _weapons;
        BowAttack _bow;
        PlayerStats _stats;
        PlayerHud _hud;
        MeleeComboAttack _combo;
        Animator _animator;
        TrainingDummy _dummy;
        ThirdPersonCamera _cameraRig;
        Camera _camera;

        public override void Setup()
        {
            base.Setup();
            _pad = InputSystem.AddDevice<Gamepad>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        // ───────────────────────── #2 무기 교체 ─────────────────────────

        [UnityTest]
        public IEnumerator WeaponSwap_Key2EquipsBow_Key1ReturnsKatana_HolstersCorrectly()
        {
            yield return LoadScene();

            Assert.AreEqual(WeaponKind.Melee, _weapons.Current.Kind, "시작 무기는 칼이어야 한다.");
            Assert.AreEqual("Weapon_r", _weapons.CurrentModel.parent.name, "칼은 오른손 무기 뼈에 있어야 한다.");
            Transform scabbard = FindUnderPlayer("SM_Scabbard01");
            Assert.IsNotNull(scabbard, "칼집 모델이 없다.");
            Assert.AreEqual("Scabbard_Target01", scabbard.parent.name, "칼집은 허리 뼈(Scabbard_Target01)에 있어야 한다.");
            Transform bowModel = FindUnderPlayer("SM_Bow_01");
            Assert.IsNotNull(bowModel, "활 모델이 없다.");
            Assert.AreEqual("Bow_BackTarget", bowModel.parent.name, "칼을 든 동안 활은 등에 있어야 한다.");

            yield return Tap(_keyboard.digit2Key);
            bool lockedDuringEquip = false;
            float t = 3f;
            while ((_weapons.IsEquipping || _weapons.Current.Kind != WeaponKind.Ranged) && t > 0f)
            {
                lockedDuringEquip |= _player.IsInAction;
                t -= Time.deltaTime;
                yield return null;
            }
            TestContext.WriteLine($"활 장착 중 이동 잠금 관측: {lockedDuringEquip}");
            Assert.IsFalse(lockedDuringEquip, "무기 교체는 상체에서만 재생하고 이동을 잠그면 안 된다.");
            Assert.AreEqual(WeaponKind.Ranged, _weapons.Current.Kind, "2를 누르면 활로 바뀌어야 한다.");
            Assert.AreEqual("Weapon_l", _weapons.CurrentModel.parent.name, "활은 왼손 무기 뼈에 있어야 한다.");
            Transform katana = FindUnderPlayer("SM_Katana01");
            Assert.IsFalse(katana.gameObject.activeInHierarchy, "활을 들면 칼은 칼집 안(숨김)이어야 한다.");
            Assert.IsTrue(scabbard.gameObject.activeInHierarchy, "칼집은 계속 허리에 있어야 한다.");

            yield return WaitUntil(() => _player.IsGroundedState, 4f, "활 장착 동작이 끝나지 않았다.");
            yield return Tap(_keyboard.digit1Key);
            yield return WaitUntil(() => !_weapons.IsEquipping && _weapons.Current.Kind == WeaponKind.Melee, 3f, "1을 누르면 칼로 돌아와야 한다.");
            Assert.AreEqual("Bow_BackTarget", bowModel.parent.name, "칼로 바꾸면 활은 등으로 가야 한다.");
            Assert.IsTrue(bowModel.gameObject.activeInHierarchy);
            Assert.AreEqual("Weapon_r", katana.parent.name);
            Assert.IsTrue(katana.gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator WeaponSwap_WhileRunning_KeepsMoving_PlaysUpperBodyOnly()
        {
            yield return LoadScene();
            // 더미에 막히지 않게 치운다.
            _dummy.transform.position = _player.transform.position + Vector3.right * 30f;
            Physics.SyncTransforms();

            Set(_pad.leftStick, new Vector2(0f, 1f), queueEventOnly: true);
            yield return WaitSeconds(0.8f);

            Vector3 start = _player.transform.position;
            yield return Tap(_keyboard.digit2Key);

            bool locked = false;
            float maxEquipWeight = 0f;
            float t = 0.8f;
            while (t > 0f)
            {
                locked |= _player.IsInAction;
                maxEquipWeight = Mathf.Max(maxEquipWeight, _weapons.UpperEquipWeight);
                t -= Time.deltaTime;
                yield return null;
            }
            float moved = Flat(_player.transform.position - start).magnitude;
            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);

            TestContext.WriteLine($"교체 중 0.8초 이동 거리 {moved:F2}m, 상체 장착 레이어 최대 가중치 {maxEquipWeight:F2}, 이동 잠금 {locked}");
            Assert.AreEqual(WeaponKind.Ranged, _weapons.Current.Kind, "달리면서 2를 눌러도 활로 바뀌어야 한다.");
            Assert.IsFalse(locked, "달리면서 무기를 바꿔도 이동이 잠기면 안 된다.");
            Assert.Greater(moved, 1.5f, "무기를 바꾸는 동안에도 계속 달려야 한다.");
            Assert.Greater(maxEquipWeight, 0.8f, "장착 동작은 상체 장착 레이어에서 재생돼야 한다. 메뉴 3을 다시 실행했는지 확인한다.");
            yield return WaitUntil(() => _weapons.UpperEquipWeight == 0f, 2f, "장착 동작이 끝난 뒤 상체 장착 레이어가 꺼지지 않았다.");
        }

        // ───────────────────────── #2 조준·발사 ─────────────────────────

        [UnityTest]
        public IEnumerator Aim_HoldL2_ZoomsInAndShowsCrosshair()
        {
            yield return LoadScene();
            yield return EquipBow();

            float normalDistance = CameraDistance();
            float normalFov = _camera.fieldOfView;
            Assert.IsFalse(_hud.CrosshairVisible, "조준 전에는 조준점이 없어야 한다.");

            Press(_pad.leftTrigger, queueEventOnly: true);
            yield return WaitSeconds(0.6f);

            float aimDistance = CameraDistance();
            TestContext.WriteLine($"카메라 거리 {normalDistance:F2}m → {aimDistance:F2}m, 시야각 {normalFov:F0} → {_camera.fieldOfView:F0}");
            Assert.IsTrue(_bow.IsAiming, "L2를 누르고 있으면 조준 상태여야 한다.");
            Assert.IsTrue(_hud.CrosshairVisible, "조준 중에는 화면 가운데 조준점이 보여야 한다.");
            Assert.Less(aimDistance, normalDistance - 1.0f, "조준하면 카메라가 줌인(가까이)되어야 한다.");
            Assert.Less(_camera.fieldOfView, normalFov - 5f, "조준하면 시야각이 좁아져야 한다.");

            Release(_pad.leftTrigger, queueEventOnly: true);
            yield return WaitSeconds(0.6f);
            Assert.IsFalse(_bow.IsAiming);
            Assert.IsFalse(_hud.CrosshairVisible, "조준을 풀면 조준점이 사라져야 한다.");
            Assert.Greater(CameraDistance(), aimDistance + 0.8f, "조준을 풀면 카메라가 원래 거리로 돌아가야 한다.");
        }

        [UnityTest]
        public IEnumerator Aim_FireAtDummy_DealsDamageAndLogsHit()
        {
            yield return LoadScene();
            yield return EquipBow();

            Press(_pad.leftTrigger, queueEventOnly: true);
            yield return WaitSeconds(0.7f);
            Assert.IsTrue(_bow.IsAiming);
            // 끝까지 당겨야 쏠 수 있다.
            yield return WaitUntil(() => _bow.IsFullyDrawn, 2f, "조준 후 2초 안에 시위를 끝까지 당기지 못했다.");

            // 조준점(화면 중앙)이 가리키는 5m 지점에 더미를 둔다.
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            _dummy.transform.position = ray.GetPoint(5f);
            Physics.SyncTransforms();
            yield return null;

            float before = _dummy.TotalDamage;
            LogAssert.Expect(LogType.Log, "Hit: 15");
            yield return Tap(_pad.rightShoulder);

            yield return WaitUntil(() => _bow.LastArrow != null && _bow.LastArrow.HasHit, 1.5f, "화살이 1.5초 안에 아무것에도 맞지 않았다.");
            Release(_pad.leftTrigger, queueEventOnly: true);

            TestContext.WriteLine($"쏜 화살 {_bow.ArrowsFired}발, 더미 피해 {before} → {_dummy.TotalDamage}");
            Assert.AreEqual(1, _bow.ArrowsFired, "한 번 누르면 한 발만 나가야 한다.");
            Assert.AreSame(_dummy, _bow.LastArrow.HitTarget as TrainingDummy, "화살은 조준점의 더미에 맞아야 한다.");
            Assert.AreEqual(before + 15f, _dummy.TotalDamage, 0.01f, "화살에 맞은 더미는 피해를 입어야 한다.");
        }

        [UnityTest]
        public IEnumerator Fire_BeforeFullDraw_DoesNothing_ThenFiresAndRedraws()
        {
            yield return LoadScene();
            yield return EquipBow();

            Press(_pad.leftTrigger, queueEventOnly: true);
            yield return WaitUntil(() => _bow.IsAiming, 1f, "L2를 눌러도 조준하지 않았다.");
            yield return null;
            yield return null;

            // 막 당기기 시작했을 때: 아직 쏠 수 없고, 조준점 조각은 멀리 있다.
            Assert.Less(_bow.DrawProgress, 1f, "조준 직후에는 시위가 다 당겨져 있으면 안 된다.");
            float startRadius = _hud.DrawIndicatorRadius;
            yield return Tap(_pad.rightShoulder);
            yield return WaitSeconds(0.1f);
            Assert.AreEqual(0, _bow.ArrowsFired, "끝까지 당기기 전에는 화살이 나가면 안 된다.");

            // 끝까지 당기면 조각이 중심으로 모이고, 그때는 쏠 수 있다.
            yield return WaitUntil(() => _bow.IsFullyDrawn, 2f, "조준 후 2초 안에 시위를 끝까지 당기지 못했다.");
            yield return null;
            float fullRadius = _hud.DrawIndicatorRadius;
            TestContext.WriteLine($"조준점 조각 거리: 시작 {startRadius:F1}px → 끝 {fullRadius:F1}px");
            Assert.Less(fullRadius, startRadius - 10f, "당길수록 조준점 조각이 중심으로 모여야 한다.");

            yield return Tap(_pad.rightShoulder);
            yield return WaitUntil(() => _bow.ArrowsFired == 1, 0.5f, "끝까지 당긴 뒤 눌렀는데 화살이 나가지 않았다.");
            Assert.Less(_bow.DrawProgress, 1f, "쏜 뒤에는 시위를 다시 당겨야 한다.");

            Release(_pad.leftTrigger, queueEventOnly: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire_WithoutAim_DoesNothing()
        {
            yield return LoadScene();
            yield return EquipBow();

            yield return Tap(_pad.rightShoulder);
            yield return WaitSeconds(0.3f);

            Assert.AreEqual(0, _bow.ArrowsFired, "조준하지 않고 누르면 화살이 나가면 안 된다.");
            Assert.AreEqual(-1, _combo.CurrentStep, "활을 든 상태에서는 칼 공격이 나가면 안 된다.");
            Assert.IsFalse(_player.IsInAction);
        }

        [UnityTest]
        public IEnumerator Aim_WhileMoving_LowerBodyKeepsLocomotion()
        {
            yield return LoadScene();
            yield return EquipBow();

            Press(_pad.leftTrigger, queueEventOnly: true);
            yield return WaitSeconds(0.5f);

            // 오른쪽으로 옆걸음한다.
            Vector3 start = _player.transform.position;
            Vector3 startRight = _player.transform.right;
            Set(_pad.leftStick, new Vector2(1f, 0f), queueEventOnly: true);
            bool baseLayerLocomotion = true;
            float t = 1.2f;
            while (t > 0f)
            {
                baseLayerLocomotion &= _animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerLocomotion.LocomotionState) ||
                                       _animator.IsInTransition(0);
                t -= Time.deltaTime;
                yield return null;
            }
            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);

            Vector3 moved = _player.transform.position - start;
            float sideways = Vector3.Dot(moved, startRight);
            float facingError = Vector3.Angle(Flat(_player.transform.forward), Flat(_camera.transform.forward));
            int upper = _animator.GetLayerIndex("UpperBody");
            TestContext.WriteLine($"조준 중 1.2초 옆걸음: 오른쪽 {sideways:F2}m, 전체 {moved.magnitude:F2}m, 카메라 대비 방향 오차 {facingError:F1}도, 상체 레이어 가중치 {_animator.GetLayerWeight(upper):F2}");

            Assert.IsTrue(_bow.IsAiming, "이동해도 조준이 유지되어야 한다.");
            Assert.IsTrue(baseLayerLocomotion, "조준 중에도 하체(기본 레이어)는 이동 상태여야 한다.");
            Assert.Greater(sideways, 0.4f, "조준 중 옆 입력으로 옆으로 걸어가야 한다(하체 애니메이션 루트 모션).");
            Assert.Less(facingError, 15f, "조준 중에는 카메라 방향을 바라본 채 옆걸음해야 한다.");
            Assert.Greater(_animator.GetLayerWeight(upper), 0.9f, "조준 중 상체 레이어가 켜져 있어야 한다.");

            Release(_pad.leftTrigger, queueEventOnly: true);
        }

        [UnityTest]
        public IEnumerator Aim_LookUpAndDown_BendsSpine()
        {
            yield return LoadScene();
            yield return EquipBow();

            Press(_pad.leftTrigger, queueEventOnly: true);
            yield return WaitSeconds(0.5f);
            Transform chest = _animator.GetBoneTransform(HumanBodyBones.Chest) ?? _animator.GetBoneTransform(HumanBodyBones.Spine);

            // 위를 본다(스틱 위 = 카메라가 올려다봄).
            Set(_pad.rightStick, new Vector2(0f, 1f), queueEventOnly: true);
            yield return WaitSeconds(0.5f);
            Set(_pad.rightStick, Vector2.zero, queueEventOnly: true);
            yield return WaitSeconds(0.1f);
            float upPitch = _bow.AppliedSpinePitch;
            float upChestY = chest.forward.y;

            // 아래를 본다.
            Set(_pad.rightStick, new Vector2(0f, -1f), queueEventOnly: true);
            yield return WaitSeconds(0.9f);
            Set(_pad.rightStick, Vector2.zero, queueEventOnly: true);
            yield return WaitSeconds(0.1f);
            float downPitch = _bow.AppliedSpinePitch;
            float downChestY = chest.forward.y;

            Release(_pad.leftTrigger, queueEventOnly: true);
            TestContext.WriteLine($"척추 보정: 위 {upPitch:F1}도, 아래 {downPitch:F1}도 / 가슴 forward.y: 위 {upChestY:F2}, 아래 {downChestY:F2}");
            Assert.Less(upPitch, -10f, "위를 보면 상체를 젖혀야 한다(음수 보정).");
            Assert.Greater(downPitch, 10f, "아래를 보면 상체를 숙여야 한다(양수 보정).");
            Assert.Greater(Mathf.Abs(upChestY - downChestY), 0.2f, "위·아래에 따라 가슴 방향이 눈에 띄게 달라져야 한다.");
        }

        // ───────────────────────── #3 체력·기력 ─────────────────────────

        [UnityTest]
        public IEnumerator Stamina_LightAttackAndRoll_ConsumeStamina_HudFollows()
        {
            yield return LoadScene();
            Assert.AreEqual(_stats.MaxStamina, _stats.Stamina, 0.01f);

            yield return Tap(_pad.rightShoulder);
            yield return null;
            float afterAttack = _stats.Stamina;
            Assert.AreEqual(_stats.MaxStamina - _combo.StaminaCostPerHit, afterAttack, 0.5f, "약공격 1타만큼 기력이 줄어야 한다.");
            Assert.AreEqual(_stats.StaminaRatio, _hud.DisplayedStaminaRatio, 0.01f, "HUD 기력 바가 기력과 같아야 한다.");

            yield return WaitUntil(() => _combo.CurrentStep < 0 || _player.IsGroundedState, 4f, "공격이 끝나지 않았다.");
            yield return WaitUntil(() => _player.IsGroundedState, 4f, "공격 뒤 지상 상태로 돌아오지 않았다.");
            float beforeRoll = _stats.Stamina;
            yield return Tap(_keyboard.leftCtrlKey);
            yield return WaitUntil(() => _player.IsRolling, 0.5f, "Ctrl로 구르지 않았다.");
            TestContext.WriteLine($"기력: 최대 {_stats.MaxStamina} → 공격 후 {afterAttack:F1} → 구르기 전 {beforeRoll:F1} → 구른 후 {_stats.Stamina:F1}");
            Assert.Less(_stats.Stamina, beforeRoll - 5f, "구르기는 기력을 소모해야 한다.");
        }

        [UnityTest]
        public IEnumerator Stamina_RegeneratesAfterDelay()
        {
            yield return LoadScene();
            _stats.TryConsumeStamina(50f);
            float spent = _stats.Stamina;

            yield return WaitSeconds(0.4f);
            Assert.AreEqual(spent, _stats.Stamina, 0.5f, "회복 대기 시간(0.8초) 전에는 회복되지 않아야 한다.");

            yield return WaitSeconds(2.6f);
            TestContext.WriteLine($"기력 {spent:F1} → 3초 후 {_stats.Stamina:F1}");
            Assert.AreEqual(_stats.MaxStamina, _stats.Stamina, 0.5f, "대기 후 회복되어 3초 안에 가득 차야 한다.");
        }

        [UnityTest]
        public IEnumerator Stamina_Empty_BlocksRollAndAttack()
        {
            yield return LoadScene();
            _stats.TryConsumeStamina(10000f);
            Assert.AreEqual(0f, _stats.Stamina, 0.01f);

            yield return Tap(_keyboard.leftCtrlKey);
            yield return null;
            Assert.IsFalse(_player.IsRolling, "기력이 0이면 구르지 못해야 한다.");

            yield return Tap(_pad.rightShoulder);
            yield return null;
            Assert.AreEqual(-1, _combo.CurrentStep, "기력이 0이면 공격하지 못해야 한다.");
        }

        [UnityTest]
        public IEnumerator Health_Damage_RedBarDropsNow_WhiteBarFollowsSlowly()
        {
            yield return LoadScene();
            Assert.AreEqual(1f, _hud.DisplayedHealthRatio, 0.001f);

            _stats.TakeDamage(new DamageInfo(30f, null, _player.transform.position, Vector3.zero));
            yield return null;
            yield return null;
            float red = _hud.DisplayedHealthRatio;
            float white = _hud.LagHealthRatio;
            Assert.AreEqual(0.7f, red, 0.01f, "빨간 체력 바는 피해 즉시 내려가야 한다.");
            Assert.AreEqual(1f, white, 0.01f, "흰 바는 피해 직후 그대로 남아 잃은 양을 보여 줘야 한다.");

            yield return WaitSeconds(0.8f);
            float whiteMid = _hud.LagHealthRatio;
            Assert.Less(whiteMid, 1f, "잠시 뒤 흰 바가 내려가기 시작해야 한다.");
            Assert.Greater(whiteMid, 0.7f, "흰 바는 천천히 내려가야 한다(아직 빨간 바보다 위).");

            yield return WaitSeconds(1.5f);
            TestContext.WriteLine($"체력 바: 빨강 {red:F2}, 흰색 직후 {white:F2} → 0.8초 {whiteMid:F2} → 2.3초 {_hud.LagHealthRatio:F2}");
            Assert.AreEqual(0.7f, _hud.LagHealthRatio, 0.01f, "흰 바는 결국 빨간 바까지 내려와야 한다.");
        }

        // ───────────────────────── 보조 ─────────────────────────

        IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("에디터에서만 실행한다.");
            yield break;
#endif
            yield return null;

            _player = UnityEngine.Object.FindFirstObjectByType<PlayerLocomotion>();
            Assert.IsNotNull(_player, "CombatTest 씬에 Player가 없다. Tools > Soulslike > 3을 다시 실행한다.");
            _weapons = _player.GetComponent<PlayerWeaponController>();
            _bow = _player.GetComponent<BowAttack>();
            _stats = _player.GetComponent<PlayerStats>();
            _hud = _player.GetComponent<PlayerHud>();
            _combo = _player.GetComponent<MeleeComboAttack>();
            _animator = _player.GetComponentInChildren<Animator>();
            _dummy = UnityEngine.Object.FindFirstObjectByType<TrainingDummy>();
            _camera = Camera.main;
            _cameraRig = _camera != null ? _camera.GetComponent<ThirdPersonCamera>() : null;

            string missing = string.Join(", ", new (string, object)[]
            {
                ("PlayerWeaponController", _weapons), ("BowAttack", _bow), ("PlayerStats", _stats),
                ("PlayerHud", _hud), ("MeleeComboAttack", _combo), ("TrainingDummy", _dummy), ("ThirdPersonCamera", _cameraRig),
            }.Where(p => p.Item2 == null || (p.Item2 is UnityEngine.Object o && o == null)).Select(p => p.Item1));
            Assert.IsEmpty(missing, $"테스트 씬에 없는 구성 요소: {missing}. Tools > Soulslike > 3. Create Combat Test Scene을 다시 실행한다.");
            Assert.IsTrue(_weapons.SlotCount >= 2, "무기 슬롯이 2개(칼·활)가 아니다. 메뉴 3을 다시 실행한다.");

            _dummy.transform.position = _player.transform.position + _player.transform.forward * 2.5f + Vector3.up;
            yield return WaitSeconds(0.5f);
        }

        IEnumerator EquipBow()
        {
            yield return Tap(_keyboard.digit2Key);
            yield return WaitUntil(() => !_weapons.IsEquipping && _weapons.Current.Kind == WeaponKind.Ranged, 3f, "활로 바뀌지 않았다.");
            yield return WaitUntil(() => _player.IsGroundedState, 4f, "활 장착 동작이 끝나지 않았다.");
        }

        Transform FindUnderPlayer(string name) =>
            _player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        float CameraDistance() => Vector3.Distance(_camera.transform.position, _player.transform.position + Vector3.up * 1.6f);

        IEnumerator Tap(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            yield return null;
            Release(button, queueEventOnly: true);
            yield return null;
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            while (seconds > 0f)
            {
                seconds -= Time.deltaTime;
                yield return null;
            }
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout, string failMessage)
        {
            while (!condition())
            {
                timeout -= Time.deltaTime;
                if (timeout <= 0f) Assert.Fail(failMessage);
                yield return null;
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
