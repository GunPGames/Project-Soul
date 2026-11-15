# 테스트 케이스: 이슈 #2 [활] 활 동작 추가 · #3 [UI] 캐릭터 스탯 표시

- **날짜**: 2026-11-15
- **씬**: `Assets/Scenes/CombatTest.unity` — **Tools > Soulslike > 3. Create Combat Test Scene을 다시 실행해야 한다**(무기·활·HUD 구성 요소가 추가됨)
- **관련 정적 분석**: `static-review-issue2-3-2026-11-15.md`

## A. 자동 시뮬레이션 (PlayMode, `BowAndStatsTests`)

실행: Window > General > Test Runner > PlayMode > Run All. 결과는 `production/qa/test-runs/last-test-run.md`에 자동 저장된다.

| ID | 테스트 | 이슈·조건 | 통과 기준 | 결과 |
|---|---|---|---|---|
| BT-01 | `WeaponSwap_Key2EquipsBow_Key1ReturnsKatana_HolstersCorrectly` | #2-3 | 시작은 칼(오른손), 칼집은 `Scabbard_Target01`, 활은 `Bow_BackTarget` / 2 → 활이 왼손, 칼 숨김, 칼집 유지 / 1 → 활이 등, 칼이 오른손 | 미실행 |
| BT-02 | `Aim_HoldL2_ZoomsInAndShowsCrosshair` | #2-1 | 조준 중: 카메라 거리 1m 이상 감소, 시야각 5도 이상 감소, 조준점 표시 / 해제 시 원복 | 미실행 |
| BT-03 | `Aim_FireAtDummy_DealsDamageAndLogsHit` | #2-2, #2-6, #2-7 | 조준점의 더미에 1발 명중, 피해 +15, 콘솔 `Hit: 15` | 미실행 |
| BT-04 | `Fire_WithoutAim_DoesNothing` | #2-2 | 조준 없이 R1 → 화살 0발, 칼 공격도 안 나감 | 미실행 |
| BT-05 | `Aim_WhileMoving_LowerBodyKeepsLocomotion` | #2-4 | 조준 중 1.2초 옆걸음 0.4m 이상, 기본 레이어 = Locomotion, 카메라 방향 유지(15도 이내), 상체 레이어 가중치 > 0.9 | 미실행 |
| BT-06 | `Aim_LookUpAndDown_BendsSpine` | #2-5 | 위 보기 → 척추 보정 < -10도, 아래 보기 → > +10도, 가슴 방향 차 > 0.2 | 미실행 |
| ST-01 | `Stamina_LightAttackAndRoll_ConsumeStamina_HudFollows` | #3-3 | 약공격 1타 = 기력 -12, HUD 기력 바 일치, 구르기 기력 감소 | 미실행 |
| ST-02 | `Stamina_RegeneratesAfterDelay` | #3-3 | 0.4초까지 회복 없음, 3초 안에 가득 | 미실행 |
| ST-03 | `Stamina_Empty_BlocksRollAndAttack` | #3-3 | 기력 0이면 구르기·공격 불가 | 미실행 |
| ST-04 | `Health_Damage_RedBarDropsNow_WhiteBarFollowsSlowly` | #3-1, #3-2 | 30 피해 → 빨간 바 즉시 0.7, 흰 바 1.0 유지 → 0.8초 뒤 0.7~1.0 사이 → 2.3초 뒤 0.7 | 미실행 |

이슈 #1 테스트(`CombatSimulationTests`)도 회귀 확인용으로 함께 돈다. AT-03(`LockOn_TargetBehind_TurnCameraThenLock_Succeeds`)은 테스트 결함을 고쳤다(아래 실행 기록).

## B. 수동 테스트 (Play 모드, 마우스·키보드)

| ID | 이슈 | 절차 | 기대 결과 | 실제 결과 | 판정 |
|---|---|---|---|---|---|
| MB-01 | #2-3 | Play 직후 캐릭터를 살펴본다 | 오른손 칼, **왼쪽 허리 칼집**, **등에 활** | | |
| MB-02 | #2-3 | 2 | 활 장착 동작 → 왼손에 활, 칼은 칼집 안(숨김), 이동 애니메이션이 활 버전으로 바뀜 | | |
| MB-03 | #2-3 | 활 장착 동작 중 WASD / Ctrl | 이동 잠김 / Ctrl은 구르기로 끊고 활은 손에 든다 | | |
| MB-04 | #2-1 | 우클릭을 누르고 있기 | 줌인(어깨 너머), 화면 중앙에 흰 점, 상체 조준 자세 | | |
| MB-05 | #2-2 | 조준 중 좌클릭 | 화살이 조준점 방향으로 날아감. 연타 시 0.6초 간격 | | |
| MB-06 | #2-6·7 | 더미에 조준해 좌클릭 | 화살이 더미에 박힘, 콘솔 `Hit: 15` | | |
| MB-07 | #2-4 | 조준한 채 WASD | 하체는 걷기(옆·뒷걸음 포함), 상체는 조준 유지, 몸은 카메라 방향 | | |
| MB-08 | #2-5 | 조준한 채 마우스를 위·아래로 | 상체가 따라서 젖혀지고 숙여짐(활이 조준점을 향함) | | |
| MB-09 | #2 | 조준 없이 좌클릭(활 장착) | 아무 일도 없음 | | |
| MB-10 | #2 | 조준 중 Ctrl / F | 조준이 풀리며 구르기·점프 | | |
| MB-11 | #2 | 가까운 벽(1m)에 조준해 발사 | 화살이 벽에 박힘(플레이어 몸에 맞지 않음) | | |
| MB-12 | #2-3 | 1 | 칼로 돌아옴, 활은 등으로 | | |
| MB-13 | #3-1·2 | K(디버그: 자기 자신에게 15 피해) 여러 번 | 왼쪽 위 빨간 바는 즉시 줄고, 흰 바가 0.5초 뒤 천천히 따라 내려옴 | | |
| MB-14 | #3-3 | 칼 약공격 연타 | 노란 기력 바가 타마다 줄고, 0이 되면 공격이 안 나감. 멈추면 0.8초 뒤 회복 | | |
| MB-15 | #3-3 | Ctrl 연속 구르기 | 구를 때마다 기력 감소, 0이면 구르지 않음 | | |
| MB-16 | #3-3 | 활 발사 | 발사마다 기력 소량 감소 | | |

## 실행 기록
| 날짜 | 실행자 | 범위 | 요약 |
|---|---|---|---|
| 2026-11-15 | Claude | 컴파일 | Soulslike.Runtime·Editor·Tests.PlayMode Roslyn 컴파일 통과(오류·경고 0). Unity 실행·Test Runner는 미실행(UnityMCP 끊김, Unity가 열려 있어 배치 모드 불가) |
