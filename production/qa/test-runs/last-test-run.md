# 테스트 실행 결과

- **실행 시각**: 2026-11-15 21:20:17
- **대상**: `My project (2)`
- **결과**: 전체 12 / 통과 12 / 실패 0 / 건너뜀 0 / 판정 불가 0
- **소요 시간**: 29.3초

## 요약

| 결과 | 테스트 | 시간(초) |
|---|---|---|
| ✅ 통과 | `Aim_FireAtDummy_DealsDamageAndLogsHit` | 2.3 |
| ✅ 통과 | `Aim_HoldL2_ZoomsInAndShowsCrosshair` | 2.3 |
| ✅ 통과 | `Aim_LookUpAndDown_BendsSpine` | 3.2 |
| ✅ 통과 | `Aim_WhileMoving_LowerBodyKeepsLocomotion` | 2.8 |
| ✅ 통과 | `Fire_BeforeFullDraw_DoesNothing_ThenFiresAndRedraws` | 2.5 |
| ✅ 통과 | `Fire_WithoutAim_DoesNothing` | 1.4 |
| ✅ 통과 | `Health_Damage_RedBarDropsNow_WhiteBarFollowsSlowly` | 3.0 |
| ✅ 통과 | `Stamina_Empty_BlocksRollAndAttack` | 0.8 |
| ✅ 통과 | `Stamina_LightAttackAndRoll_ConsumeStamina_HudFollows` | 2.9 |
| ✅ 통과 | `Stamina_RegeneratesAfterDelay` | 3.7 |
| ✅ 통과 | `WeaponSwap_Key2EquipsBow_Key1ReturnsKatana_HolstersCorrectly` | 1.5 |
| ✅ 통과 | `WeaponSwap_WhileRunning_KeepsMoving_PlaysUpperBodyOnly` | 2.7 |

## 상세

### ✅ 통과 `Aim_FireAtDummy_DealsDamageAndLogsHit`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Aim_FireAtDummy_DealsDamageAndLogsHit`
- 출력(측정값):
```
쏜 화살 1발, 더미 피해 0 → 15
```

### ✅ 통과 `Aim_HoldL2_ZoomsInAndShowsCrosshair`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Aim_HoldL2_ZoomsInAndShowsCrosshair`
- 출력(측정값):
```
카메라 거리 4.50m → 2.08m, 시야각 60 → 45
```

### ✅ 통과 `Aim_LookUpAndDown_BendsSpine`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Aim_LookUpAndDown_BendsSpine`
- 출력(측정값):
```
척추 보정: 위 -35.0도, 아래 60.0도 / 가슴 forward.y: 위 -0.32, 아래 0.60
```

### ✅ 통과 `Aim_WhileMoving_LowerBodyKeepsLocomotion`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Aim_WhileMoving_LowerBodyKeepsLocomotion`
- 출력(측정값):
```
조준 중 1.2초 옆걸음: 오른쪽 1.82m, 전체 1.82m, 카메라 대비 방향 오차 0.0도, 상체 레이어 가중치 1.00
```

### ✅ 통과 `Fire_BeforeFullDraw_DoesNothing_ThenFiresAndRedraws`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Fire_BeforeFullDraw_DoesNothing_ThenFiresAndRedraws`
- 출력(측정값):
```
조준점 조각 거리: 시작 45.7px → 끝 9.0px
```

### ✅ 통과 `Fire_WithoutAim_DoesNothing`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Fire_WithoutAim_DoesNothing`

### ✅ 통과 `Health_Damage_RedBarDropsNow_WhiteBarFollowsSlowly`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Health_Damage_RedBarDropsNow_WhiteBarFollowsSlowly`
- 출력(측정값):
```
[Player] 피해 30, 남은 체력 70/100
체력 바: 빨강 0.70, 흰색 직후 1.00 → 0.8초 0.85 → 2.3초 0.70
```

### ✅ 통과 `Stamina_Empty_BlocksRollAndAttack`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Stamina_Empty_BlocksRollAndAttack`

### ✅ 통과 `Stamina_LightAttackAndRoll_ConsumeStamina_HudFollows`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Stamina_LightAttackAndRoll_ConsumeStamina_HudFollows`
- 출력(측정값):
```
기력: 최대 100 → 공격 후 88.0 → 구르기 전 100.0 → 구른 후 82.0
```

### ✅ 통과 `Stamina_RegeneratesAfterDelay`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.Stamina_RegeneratesAfterDelay`
- 출력(측정값):
```
기력 50.0 → 3초 후 100.0
```

### ✅ 통과 `WeaponSwap_Key2EquipsBow_Key1ReturnsKatana_HolstersCorrectly`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.WeaponSwap_Key2EquipsBow_Key1ReturnsKatana_HolstersCorrectly`
- 출력(측정값):
```
활 장착 중 이동 잠금 관측: False
```

### ✅ 통과 `WeaponSwap_WhileRunning_KeepsMoving_PlaysUpperBodyOnly`
- 전체 이름: `Soulslike.Tests.BowAndStatsTests.WeaponSwap_WhileRunning_KeepsMoving_PlaysUpperBodyOnly`
- 출력(측정값):
```
Account API did not become accessible within 30 seconds. This may be due to network issues or editor focus.
교체 중 0.8초 이동 거리 3.53m, 상체 장착 레이어 최대 가중치 1.00, 이동 잠금 False
```
