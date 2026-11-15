# 테스트 실행 결과

- **실행 시각**: 2026-11-15 07:04:27
- **대상**: `My project (2)`
- **결과**: 전체 1 / 통과 0 / 실패 1 / 건너뜀 0 / 판정 불가 0
- **소요 시간**: 1.0초

## 요약

| 결과 | 테스트 | 시간(초) |
|---|---|---|
| ❌ 실패 | `LockOn_TargetBehind_TurnCameraThenLock_Succeeds` | 1.0 |

## 상세

### ❌ 실패 `LockOn_TargetBehind_TurnCameraThenLock_Succeeds`
- 전체 이름: `Soulslike.Tests.CombatSimulationTests.LockOn_TargetBehind_TurnCameraThenLock_Succeeds`
- 메시지:
```
카메라를 돌려 더미를 보고 있으면 록온되어야 한다.
  Expected: True
  But was:  False
```
- 스택(프로젝트 코드만):
```
at Soulslike.Tests.CombatSimulationTests+<LockOn_TargetBehind_TurnCameraThenLock_Succeeds>d__13.MoveNext () [0x00186] in C:\Users\JUNGLE\My project (2)\Assets\Soulslike\Tests\PlayMode\CombatSimulationTests.cs:108
```
