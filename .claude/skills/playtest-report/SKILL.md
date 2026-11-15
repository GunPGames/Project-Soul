---
name: playtest-report
description: "사용자의 플레이 소감을 정리해 조정 항목과 다음 작업으로 바꾼다."
argument-hint: "<플레이 소감>"
disable-model-invocation: true
---

`$ARGUMENTS`(사용자가 플레이하며 느낀 점)를 정리한다.

1. 소감을 "조작감 / 전투 / 카메라 / 애니메이션 / 레벨 / 버그"로 분류한다.
2. 각 항목을 구체적인 조정으로 바꾼다: 어떤 컴포넌트의 어떤 필드를 얼마로(예: `PlayerLocomotion.rotationSpeed` 720 → 540).
3. 버그는 `/bug-report` 대상으로, 새 기능은 `/quick-design` 대상으로 표시한다.
4. 결과를 `production/playtests/<날짜>.md`에 쓰고, 우선순위 상위 3개를 보고한다.
