using System;
using System.Collections;
using System.Collections.Generic;
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
    /// 전투 테스트 씬(CombatTest.unity)을 띄우고 가짜 패드·키보드 입력으로 조작을 시뮬레이션한다.
    /// 공격은 패드 R1으로 넣는다. 테스트 중에는 마우스 커서 잠금이 보장되지 않아서, 마우스 공격은 무시될 수 있기 때문이다.
    /// 실행: Window > General > Test Runner > PlayMode > Run All
    /// </summary>
    public class CombatSimulationTests : InputTestFixture
    {
        const string ScenePath = "Assets/Scenes/CombatTest.unity";
        const float DummyDistance = 2.5f;

        Gamepad _pad;
        Keyboard _keyboard;

        PlayerLocomotion _player;
        LockOnController _lockOn;
        MeleeComboAttack _combo;
        Animator _animator;
        TrainingDummy _dummy;
        Transform _camera;

        public override void Setup()
        {
            base.Setup();
            _pad = InputSystem.AddDevice<Gamepad>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        // ───────────────────────── 록온 ─────────────────────────

        [UnityTest]
        public IEnumerator LockOn_TargetInFront_LocksOn()
        {
            yield return LoadScene();

            yield return Tap(_pad.rightStickButton);

            Assert.IsTrue(_lockOn.IsLockedOn, "정면 2.5m의 더미를 록온하지 못했다.");
            Assert.AreSame(_dummy.GetComponent<LockOnTarget>(), _lockOn.Current);
        }

        [UnityTest]
        public IEnumerator LockOn_TargetBehindPlayer_DoesNotLock_AndRecentersCamera()
        {
            yield return LoadScene();
            PlaceDummyBehindPlayer();
            yield return null;

            int recenterCount = 0;
            Action onRecenter = () => recenterCount++;
            _lockOn.RecenterRequested += onRecenter;
            try
            {
                yield return Tap(_pad.rightStickButton);
                Assert.IsFalse(_lockOn.IsLockedOn, "카메라 뒤(플레이어 뒤)의 더미가 록온되면 안 된다(정면 60도 제한).");
                Assert.AreEqual(1, recenterCount, "대상이 없으면 카메라 재정렬이 요청되어야 한다.");

                yield return WaitSeconds(0.5f);
                float yawDiff = Mathf.Abs(Mathf.DeltaAngle(_camera.eulerAngles.y, _player.transform.eulerAngles.y));
                TestContext.WriteLine($"재정렬 후 카메라와 캐릭터 방향 차이: {yawDiff:F1}도 (더미는 여전히 뒤에 있다)");
                Assert.Less(yawDiff, 15f, "재정렬하면 카메라가 캐릭터 등 뒤로 돌아와야 한다.");

                // 다시 눌러도 더미는 여전히 카메라 뒤에 있으므로 록온되지 않는다.
                yield return Tap(_pad.rightStickButton);
                Assert.IsFalse(_lockOn.IsLockedOn, "재정렬 후 다시 눌러도 뒤의 더미는 록온되지 않아야 한다.");
            }
            finally
            {
                _lockOn.RecenterRequested -= onRecenter;
            }
        }

        [UnityTest]
        public IEnumerator LockOn_TargetBehind_TurnCameraThenLock_Succeeds()
        {
            yield return LoadScene();
            PlaceDummyBehindPlayer();
            yield return null;

            // 오른쪽 스틱으로 카메라를 돌려 더미를 화면에 넣는다.
            // 록온 판정은 카메라 위치가 아니라 캐릭터 눈 높이에서 잰 각도다. 더미가 카메라와 캐릭터 사이에 있으면
            // 카메라 기준으로는 정면이지만 캐릭터 기준으로는 뒤라서 록온되지 않는다(2026-11-15 테스트 수정 사유).
            Set(_pad.rightStick, new Vector2(1f, 0f), queueEventOnly: true);
            float timeout = 4f;
            while (LockOnAngleTo(_dummy.GetComponent<LockOnTarget>().AimPoint) > 20f && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Set(_pad.rightStick, Vector2.zero, queueEventOnly: true);
            yield return null;
            Assert.Greater(timeout, 0f, "4초 안에 카메라를 더미 쪽으로 돌리지 못했다.");

            yield return Tap(_pad.rightStickButton);
            Assert.IsTrue(_lockOn.IsLockedOn, "카메라를 돌려 더미를 보고 있으면 록온되어야 한다.");

            yield return WaitSeconds(0.6f);
            float facing = Vector3.Angle(Flat(_player.transform.forward), Flat(_dummy.transform.position - _player.transform.position));
            TestContext.WriteLine($"록온 0.6초 뒤 캐릭터가 더미를 향한 각도 오차: {facing:F1}도");
            Assert.Less(facing, 15f, "록온 중 캐릭터는 대상을 바라봐야 한다.");
        }

        [UnityTest]
        public IEnumerator LockOn_TargetMovesBehindWhileLocked_KeepsLockAndTracks()
        {
            yield return LoadScene();
            yield return Tap(_pad.rightStickButton);
            Assert.IsTrue(_lockOn.IsLockedOn);

            PlaceDummyBehindPlayer();
            yield return WaitSeconds(1.0f);

            Assert.IsTrue(_lockOn.IsLockedOn, "록온 중 대상이 뒤로 가도 거리 안이면 록온이 유지되어야 한다.");
            float facing = Vector3.Angle(Flat(_player.transform.forward), Flat(_dummy.transform.position - _player.transform.position));
            float cameraAngle = CameraAngleTo(_dummy.transform.position);
            TestContext.WriteLine($"1초 뒤 캐릭터 방향 오차 {facing:F1}도, 카메라 방향 오차 {cameraAngle:F1}도");
            Assert.Less(facing, 20f, "캐릭터가 뒤로 간 대상을 다시 바라봐야 한다.");
            Assert.Less(cameraAngle, 45f, "카메라가 뒤로 간 대상을 따라 돌아야 한다.");
        }

        [UnityTest]
        public IEnumerator LockOn_TargetBeyondBreakDistance_Unlocks()
        {
            yield return LoadScene();
            yield return Tap(_pad.rightStickButton);
            Assert.IsTrue(_lockOn.IsLockedOn);

            _dummy.transform.position = _player.transform.position + _player.transform.forward * 30f + Vector3.up;
            yield return null;
            yield return null;

            Assert.IsFalse(_lockOn.IsLockedOn, "25m보다 멀어지면 록온이 풀려야 한다.");
        }

        // ───────────────────────── 공격 ─────────────────────────

        [UnityTest]
        public IEnumerator Combo_WhileLockedOn_Deals200Damage()
        {
            yield return LoadScene();
            yield return Tap(_pad.rightStickButton);
            Assert.IsTrue(_lockOn.IsLockedOn, "QA 목표의 전제: 록온이 되어야 한다.");

            var stepsSeen = new HashSet<int>();
            var damagePerStep = new float[_combo.StepCount];
            float lastDamage = _dummy.TotalDamage;
            float elapsed = 0f;
            const float limit = 40f;

            // 0.1초마다 R1을 연타한다.
            while (_dummy.TotalDamage < 200f && elapsed < limit)
            {
                Press(_pad.rightShoulder, queueEventOnly: true);
                yield return null;
                Release(_pad.rightShoulder, queueEventOnly: true);

                float wait = 0.1f;
                while (wait > 0f)
                {
                    if (_combo.CurrentStep >= 0) stepsSeen.Add(_combo.CurrentStep);
                    if (_dummy.TotalDamage > lastDamage && _combo.CurrentStep >= 0)
                        damagePerStep[_combo.CurrentStep] += _dummy.TotalDamage - lastDamage;
                    lastDamage = _dummy.TotalDamage;
                    wait -= Time.deltaTime;
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            TestContext.WriteLine($"누적 피해 {_dummy.TotalDamage}, 걸린 시간 {elapsed:F1}초, 나온 타: {string.Join(",", stepsSeen)}");
            TestContext.WriteLine($"타별 피해 합계: 1타 {damagePerStep[0]}, 2타 {damagePerStep[1]}, 3타 {damagePerStep[2]}");
            float distance = Vector3.Distance(Flat(_player.transform.position), Flat(_dummy.transform.position));
            TestContext.WriteLine($"끝났을 때 플레이어-더미 수평 거리: {distance:F2}m");

            Assert.IsTrue(stepsSeen.SetEquals(new[] { 0, 1, 2 }), "연타하면 1·2·3타가 모두 나와야 한다.");
            Assert.GreaterOrEqual(_dummy.TotalDamage, 200f, $"{limit}초 안에 200 피해를 주지 못했다.");
            Assert.IsTrue(_lockOn.IsLockedOn, "콤보 중 록온이 풀리면 안 된다.");
        }

        [UnityTest]
        public IEnumerator Attack_IgnoresMovementInput()
        {
            yield return LoadScene();

            yield return Tap(_pad.rightShoulder);
            yield return WaitUntil(() => _player.IsInAction, 0.5f, "공격이 시작되지 않았다.");

            // 공격 중 스틱을 옆으로 밀어도 행동이 유지되고, 옆으로 밀려나지 않아야 한다.
            Vector3 start = _player.transform.position;
            Set(_pad.leftStick, new Vector2(1f, 0f), queueEventOnly: true);
            float t = 0f;
            while (t < 0.25f)
            {
                Assert.IsTrue(_player.IsInAction, $"공격 시작 {t:F2}초 만에 이동 입력으로 행동이 끊겼다.");
                t += Time.deltaTime;
                yield return null;
            }
            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);

            Vector3 delta = _player.transform.InverseTransformDirection(_player.transform.position - start);
            TestContext.WriteLine($"공격 중 0.25초 이동량(캐릭터 기준): 옆 {delta.x:F2}m, 앞 {delta.z:F2}m");
            Assert.Less(Mathf.Abs(delta.x), 0.15f, "공격 중 옆 입력으로 옆으로 이동하면 안 된다.");
        }

        [UnityTest]
        public IEnumerator RollInput_BeforeHitWindow_DoesNotCancel()
        {
            yield return LoadScene();

            yield return Tap(_pad.rightShoulder);
            yield return WaitUntil(() => AttackNormalized(0) >= 0f, 0.5f, "1타가 재생되지 않았다.");

            float hitStart = _combo.GetStep(0).hitStart;
            Assume.That(AttackNormalized(0), Is.LessThan(hitStart), "이미 판정 구간이라 이 테스트를 할 수 없다.");

            yield return Tap(_keyboard.leftCtrlKey);
            Assert.IsFalse(_player.IsRolling, "타격 판정 전에는 구르기로 캔슬되면 안 된다.");
            Assert.IsTrue(_player.IsInAction);
        }

        [UnityTest]
        public IEnumerator RollInput_AfterHitWindow_CancelsIntoRoll()
        {
            yield return LoadScene();

            yield return Tap(_pad.rightShoulder);
            float hitEnd = _combo.GetStep(0).hitEnd;
            yield return WaitUntil(() => AttackNormalized(0) > hitEnd + 0.02f, 3f, "1타 판정 구간이 끝나지 않았다.");

            yield return Tap(_keyboard.leftCtrlKey);
            yield return WaitUntil(() => _player.IsRolling, 0.3f, "판정 구간이 지난 뒤 Ctrl로 구르기 캔슬이 되지 않았다.");
            Assert.AreEqual(-1, _combo.CurrentStep, "구르기로 끊으면 콤보가 처음으로 돌아가야 한다.");
        }

        [UnityTest]
        public IEnumerator LastStep_MoveCancelOnlyAfter70Percent()
        {
            yield return LoadScene();

            // 3타까지 연타한다.
            float timeout = 6f;
            while (_combo.CurrentStep < 2 && timeout > 0f)
            {
                yield return Tap(_pad.rightShoulder);
                yield return WaitSeconds(0.08f);
                timeout -= 0.08f + Time.deltaTime * 2f;
            }
            Assert.AreEqual(2, _combo.CurrentStep, "연타로 3타까지 가지 못했다.");

            float cancelPoint = _combo.LastStepMoveCancelPoint;
            yield return WaitUntil(() => AttackNormalized(2) >= 0.5f, 3f, "3타가 50%에 도달하지 않았다.");

            // 50%부터 스틱을 앞으로 민다. 70% 전에는 행동이 유지되어야 한다.
            Set(_pad.leftStick, new Vector2(0f, 1f), queueEventOnly: true);
            // 행동이 끝난 프레임에는 이미 이동 상태로 넘어가 있으므로, 직전 프레임의 재생 지점을 기록한다.
            float cancelledAt = -1f;
            float lastNormalized = AttackNormalized(2);
            float wait = 3f;
            while (wait > 0f)
            {
                if (!_player.IsInAction)
                {
                    cancelledAt = lastNormalized;
                    break;
                }
                lastNormalized = AttackNormalized(2);
                wait -= Time.deltaTime;
                yield return null;
            }
            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);

            TestContext.WriteLine($"이동으로 끊긴 지점: {(cancelledAt >= 0f ? cancelledAt.ToString("P0") : "알 수 없음(상태 이탈 후)")}");
            Assert.IsFalse(_player.IsInAction, "3타 70% 이후 이동 입력으로 행동이 끊겨야 한다.");
            if (cancelledAt >= 0f) Assert.GreaterOrEqual(cancelledAt, cancelPoint - 0.02f);
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
            _dummy = UnityEngine.Object.FindFirstObjectByType<TrainingDummy>();
            Assert.IsNotNull(_player, "CombatTest 씬에 Player가 없다. Tools > Soulslike > 3을 다시 실행한다.");
            Assert.IsNotNull(_dummy, "CombatTest 씬에 TrainingDummy가 없다. Tools > Soulslike > 3을 다시 실행한다.");

            _lockOn = _player.GetComponent<LockOnController>();
            _combo = _player.GetComponent<MeleeComboAttack>();
            _animator = _player.GetComponentInChildren<Animator>();
            _camera = Camera.main != null ? Camera.main.transform : null;
            Assert.IsNotNull(_combo, "Player에 MeleeComboAttack이 없다.");
            Assert.IsNotNull(_camera, "Main Camera가 없다.");

            // 매 테스트가 같은 조건에서 시작하도록 더미를 정면 2.5m에 둔다.
            _dummy.transform.position = _player.transform.position + _player.transform.forward * DummyDistance + Vector3.up;

            // 착지·카메라 안정화를 기다린다.
            yield return WaitSeconds(0.5f);
        }

        void PlaceDummyBehindPlayer()
        {
            _dummy.transform.position = _player.transform.position - _player.transform.forward * DummyDistance + Vector3.up;
        }

        IEnumerator Tap(ButtonControl button)
        {
            // queueEventOnly: 이벤트를 다음 프레임의 일반 입력 업데이트에서 처리하게 한다.
            // 즉시 처리하면 컴포넌트 Update에서 WasPressedThisFrame이 거짓이 된다.
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

        /// <summary>해당 공격 상태의 정규화 시간. 재생 중이 아니면 -1.</summary>
        float AttackNormalized(int step)
        {
            string state = _combo.GetStep(step).stateName;
            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(state)) return next.normalizedTime;
            }
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
            return current.IsName(state) ? current.normalizedTime : -1f;
        }

        float CameraAngleTo(Vector3 point) => Vector3.Angle(Flat(_camera.forward), Flat(point - _camera.position));

        /// <summary>LockOnController와 같은 기준(카메라 정면 vs 캐릭터 눈 → 대상)으로 잰 각도.</summary>
        float LockOnAngleTo(Vector3 aimPoint)
        {
            Vector3 eye = _player.transform.position + Vector3.up * 1.5f;
            return Vector3.Angle(_camera.forward, aimPoint - eye);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
