---
name: bug-report
description: "증상을 받아 원인을 조사하고 production/qa/bugs/BUG-NNNN.md에 버그 리포트를 남긴다."
argument-hint: "<증상>"
disable-model-invocation: true
---

`$ARGUMENTS` 증상을 조사한다. 수정은 하지 않는다(사용자가 지시하면 `/dev-task`로 넘긴다).

1. 재현 조건을 정리한다. 부족하면 한 번에 묻는다.
2. 콘솔 로그와 관련 코드를 확인해 원인 후보를 확신도와 함께 적는다.
3. 번호를 정한다: `production/qa/bugs/`의 가장 큰 BUG 번호 + 1, 4자리.
4. `/team-qa`와 같은 형식(`.claude/agents/qa-tester.md`의 "버그 리포트 형식")으로 `production/qa/bugs/BUG-NNNN.md`를 쓴다.
   - `**Severity**`: S1-Critical / S2-High / S3-Medium / S4-Low
   - `**Priority**`: P1~P4, `**Status**: Open`, `- **System**:`, `- **Issue**:`
   - 이 줄들은 `/team-qa`가 열린 버그를 셀 때 Grep으로 읽으므로 형식을 바꾸지 않는다.
