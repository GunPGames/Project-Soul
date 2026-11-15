# 테스트 케이스: 이슈 #1 [칼] 약공격 3연타 콤보

- **날짜**: 2026-11-15
- **씬**: `Assets/Scenes/CombatTest.unity` (Tools > Soulslike > 3). 플레이어 (0, 0.05, 0), 더미는 정면 2.5m
- **QA 목표**: 록온 후 더미에게 누적 200 피해
- **관련 정적 분석**: `static-review-issue1-2026-11-15.md`

## A. 자동 시뮬레이션 (PlayMode, `CombatSimulationTests`)

실행: Window > General > Test Runner > PlayMode > Run All. 입력은 가짜 패드·키보드로 넣는다.

| ID | 테스트 | 유형 | 통과 기준 | 결과 | 측정값 |
|---|---|---|---|---|---|
| AT-01 | `LockOn_TargetInFront_LocksOn` | Integration | R3 → 정면 더미가 Current | 미실행 | |
| AT-02 | `LockOn_TargetBehindPlayer_DoesNotLock_AndRecentersCamera` | Integration | 뒤의 더미는 록온 안 됨, 재정렬 1회, 카메라-캐릭터 yaw 차 < 15도, 재입력도 실패 | 미실행 | |
| AT-03 | `LockOn_TargetBehind_TurnCameraThenLock_Succeeds` | Integration | 오른쪽 스틱으로 4초 안에 더미 쪽(20도 이내)을 보고, 록온 후 캐릭터 방향 오차 < 15도 | 미실행 | |
| AT-04 | `LockOn_TargetMovesBehindWhileLocked_KeepsLockAndTracks` | Integration | 록온 유지, 1초 뒤 캐릭터 오차 < 20도, 카메라 오차 < 45도 | 미실행 | |
| AT-05 | `LockOn_TargetBeyondBreakDistance_Unlocks` | Logic | 30m로 옮기면 해제 | 미실행 | |
| AT-06 | `Combo_WhileLockedOn_Deals200Damage` | Integration | **40초 안에 200 피해**, 1·2·3타 모두 등장, 록온 유지 | 미실행 | |
| AT-07 | `Attack_IgnoresMovementInput` | Logic | 공격 중 0.25초 동안 행동 유지, 옆 이동 < 0.15m | 미실행 | |
| AT-08 | `RollInput_BeforeHitWindow_DoesNotCancel` | Logic | 판정 전 Ctrl → 구르지 않음 | 미실행 | |
| AT-09 | `RollInput_AfterHitWindow_CancelsIntoRoll` | Logic | 판정 후 Ctrl → 0.3초 안에 구르기, 콤보 리셋 | 미실행 | |
| AT-10 | `LastStep_MoveCancelOnlyAfter70Percent` | Logic | 3타는 70% 이후에만 이동으로 끊김 | 미실행 | |

## B. 수동 테스트 (Play 모드)

콘솔의 `Hit: n` 로그와 더미 인스펙터의 `totalDamage`를 본다. Scene 뷰에서 Player를 선택하면 판정 구체가 보인다.

| ID | 유형 | 절차 | 통과 기준 | 관련 버그 | 실제 결과 | 판정 |
|---|---|---|---|---|---|---|
| MT-01 | Integration | 스폰 직후 제자리 좌클릭 1회 | `Hit: 10` 1회 | #1 | | |
| MT-02 | Logic | Esc → 좌클릭 → 좌클릭 | 첫 클릭은 커서만 잠김, 두 번째에 공격 | | | |
| MT-03 | UI | 더미가 정면일 때 Q | 더미 가슴 높이에 점, 록온 | #9 | | |
| MT-04 | Logic | 록온 중 Q | 해제 | | | |
| MT-05 | Integration | 더미 뒤로 돌아가 카메라 기본 상태에서 Q 여러 번 | 매번 록온 실패, 카메라만 짧게 재정렬 | #11 | | |
| MT-06 | Integration | 마우스로 카메라를 돌려 더미를 화면 중앙에 두고 Q | 록온, 캐릭터가 더미 쪽으로 돈다 | | | |
| MT-07 | Visual/Feel | 록온 후 WASD로 더미 주위 한 바퀴 | 캐릭터는 계속 더미를 향함, 카메라 튐 없음 | #13 | | |
| MT-08 | Integration | 록온 후 Shift 질주로 더미를 지나침 | 이동 방향으로 질주, 놓으면 더미로 돌아섬 | | | |
| MT-09 | Logic | 록온 중 더미와 26m 이상 떨어짐 | 25m를 넘으면 점이 사라지고 해제 | | | |
| MT-10 | Integration | **록온 후 좌클릭 연타** | `Hit:` 10 → 12 → 18 순서, 5콤보(15타)에 200 도달 | #1, #2 | | |
| MT-11 | Visual/Feel | 연타를 아주 빠르게 / 30% 전에 한 번만 | 빠르게: 피해 누락 없음 / 한 번만: 정상 연결 | #2 | | |
| MT-12 | Logic | 3타 진행 중 클릭 | (현재) 무시되고 끝난 뒤 다시 눌러야 1타 | #3 | | |
| MT-13 | Logic | 1타 판정 후(약 45% 이후) Ctrl | 구르기로 캔슬, 콤보 리셋 | | | |
| MT-14 | Logic | 1타 시작 직후 Ctrl | 캔슬 안 됨(0.2초 버퍼 안이면 판정 후 구름) | #6 | | |
| MT-15 | Logic | 1·2타 중 WASD / 3타 70% 이후 WASD | 1·2타는 무시, 3타 70% 이후 이동 | | | |
| MT-16 | Logic | 구르기 중 좌클릭 | (현재) 반응 없음 | #4 | | |
| MT-17 | Logic | 점프(F) 중 좌클릭, 착지 직후 좌클릭 | 공중은 반응 없음, 착지 직후는 공격 | #4 | | |
| MT-18 | Logic | 공격 중 더미 비활성화 / 삭제 | 오류 없이 자유 이동·자유 카메라 | | | |
| MT-19 | Logic | 공격 중 Q로 해제 | 이후 타는 입력 방향(없으면 현재 몸 방향) | #19 | | |
| MT-20 | Integration | 더미에 붙은 채 연타 | 통과·겹침 없이 멈춤, 모든 타 `Hit:` | | | |
| MT-21 | Integration | 패드: R1 / R3 / B 짧게·길게 | 공격 / 록온 / 구르기·질주, 커서 상태와 무관 | | | |

## 실행 기록
| 날짜 | 실행자 | 범위 | 요약 |
|---|---|---|---|
| 2026-11-15 | 사용자 | A. 자동 전체 | Test Runner 목록 4번째 테스트 실패. 실패 메시지 미확인. 이름순 정렬상 AT-03(`LockOn_TargetBehind_TurnCameraThenLock_Succeeds`) 또는 AT-02(`..._TargetBehindPlayer_...`)다. 유력 원인은 정적 분석 #20(커서가 잠기지 않으면 스틱 시점 입력도 무시됨, Cam:126). 나머지 테스트 결과 미보고 |
| 2026-11-15 | Claude | 수정 | #20 수정: `ThirdPersonCamera.UpdateFreeAngles`에서 커서 조건을 마우스 이동량에만 적용. 컴파일 통과. Test Runner 재실행 필요 |
| 2026-11-15 | 사용자 | A. 자동 전체(재실행) | 실패 1건 이상 보고. 리포터 추가 전이라 상세는 미확인. `Editor.log` 기준 콤보 피해 10→12→18 × 5회 연속 확인(200 도달) |
| 2026-11-15 | Claude | 도구 | `TestRunReporter` 추가. 이후 실행 결과는 `test-runs/last-test-run.md`에 자동 저장된다 |
| 2026-11-15 07:04 | 사용자 | AT-03 단독 | ❌ `LockOn_TargetBehind_TurnCameraThenLock_Succeeds` — "카메라를 돌려 더미를 보고 있으면 록온되어야 한다. Expected True But was False"(리포터 기록) |
| 2026-11-15 | Claude | 분석·수정 | **테스트 결함**이었다. 더미가 카메라와 캐릭터 사이에 있어서 "카메라 기준 20도 이내" 조건이 카메라를 돌리기 전에 이미 충족됐다. 록온은 캐릭터 눈 기준 각도로 판정하므로 실패했다. 테스트의 각도 계산을 LockOnController와 같은 기준으로 바꿨다. 게임 코드는 그대로다. 재실행 필요 |
