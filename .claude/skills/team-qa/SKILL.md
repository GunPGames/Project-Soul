---
name: team-qa
description: "GitHub 이슈 하나에 대해 QA 전 과정(qa-lead 전략·계획 → qa-tester 케이스·정적 분석 → 실행 → 승인 보고서)을 진행하고 production/qa/에 문서로 남긴다."
argument-hint: "<이슈 번호, 예: 1> [추가 QA 목표]"
disable-model-invocation: true
model: sonnet
---

# Team QA (Project-Soul 판)

원본 Claude Code Game Studios의 `/team-qa`를 이 프로젝트에 맞게 옮긴 것이다.
- 범위 단위: 스프린트·스토리 → **GitHub 이슈의 완료 조건**
- 스모크 체크: `/smoke-check` → **컴파일 확인 + PlayMode 테스트 결과**
- 모든 출력은 한국어(~한다 체)

인자: `$ARGUMENTS` (첫 토큰 = 이슈 번호, 나머지 = 이번 QA의 추가 목표)

## 팀 구성과 위임 원칙
- `qa-lead`(sonnet): 전략·분류·판정·승인 보고서
- `qa-tester`(sonnet): 정적 분석, 테스트 케이스, 버그 리포트, 필요 시 PlayMode 테스트

**에이전트에게 맥락을 통째로 넘기지 않는다.** 메인 세션이 입력을 한 번 읽고, 필요한 줄만 요약해 프롬프트에 넣는다. 이미 읽은 문서의 경로를 넘기면 에이전트가 다시 통째로 읽는다.

**모든 에이전트 프롬프트 끝에 반환 계약을 붙인다:**
"결과 전체를 `[경로]`에 써라. 이 경로가 쓰기 승인이다. 반환은 (1) 쓴 경로, (2) 결정 요약 5줄 이내, (3) BLOCKED/우려 사항 한 줄씩만 한다. 읽은 문서를 다시 옮겨 적지 마라."
에이전트가 돌아오면 **그 파일이 실제로 있는지 확인한다.** 파일이 없으면 그 단계는 실패다.

날짜는 `YYYY-MM-DD`, 이슈 슬러그는 `issue[N]`(예: `issue1`)로 쓴다.

## 1단계: 범위 불러오기 (메인 세션)
1. 이슈를 읽는다: `Invoke-RestMethod https://api.github.com/repos/GunPGames/Project-Soul/issues/[N]` (User-Agent 헤더 필요). `gh`가 있으면 `gh issue view [N]`. 둘 다 안 되면 사용자에게 본문을 붙여넣어 달라고 하고 멈춘다.
2. 완료 조건 체크박스와 범위 밖 항목을 뽑는다.
3. 열린 버그를 확인한다: `production/qa/bugs/*.md`의 `**Severity**`, `**Status**`, `- **Issue**` 줄을 Grep한다. Status가 `Closed`·`Verified Fixed`가 아니면 열린 버그다.
4. 이전 QA 기록을 확인한다: `production/qa/*issue[N]*.md`.
5. 관련 코드 파일 목록을 Grep으로 잡는다(전체를 읽지 않는다).
6. 사용자에게 한 줄로 알린다: "이슈 #N QA 시작: 완료 조건 X개, 열린 버그 Y개(S1 a, S2 b), 이전 QA 기록 Z개."

## 2단계: QA 전략 (qa-lead)
qa-lead에게 넘긴다: 완료 조건 목록, 추가 QA 목표, 열린 버그, 관련 파일 목록, 자동 검증 현황(아래).
- **자동 검증 현황**: 최근 컴파일 결과(메인 세션이 Roslyn 또는 UnityMCP로 확인), 사용자가 붙여넣은 PlayMode 테스트 결과. 없으면 `UNKNOWN`이다.

qa-lead가 할 일:
- 완료 조건마다 유형(Logic/Integration/Visual/Feel/UI/Config/Data)을 정하고, 자동·수동 중 어느 증거가 필요한지 표로 만든다.
- 측정할 수 없는 조건을 표시하고 측정 가능한 대안을 제안한다.
- 자동 검증 판정: **PASS / PASS WITH WARNINGS / FAIL / UNKNOWN**

출력 경로: `production/qa/qa-plan-issue[N]-[날짜].md`. 다음 항목을 포함한다.
- 범위
- 분류표(조건 | 유형 | 자동 필요 | 수동 필요 | 차단 여부)
- 자동 테스트 요구(파일·테스트 이름)
- 수동 QA 범위
- 범위 밖과 이유
- 진입 조건(컴파일 통과, 크래시 없음)
- 종료 조건

전략을 사용자에게 보여 주고 AskUserQuestion으로 확인한다: "계획대로 진행 / 유형 조정 / 차단 항목 빼고 진행 / 취소"
- 자동 검증이 **FAIL**이면 3단계로 가지 않는다. 실패 내용을 보여 주고 멈춘다.
- **UNKNOWN**이면 경고하고 진행한다. 다만 승인 보고서는 NOT ASSESSED가 된다.

## 3단계: 정적 분석과 테스트 케이스 (qa-tester)
qa-tester에게 넘긴다: 분류표, 수동 QA 범위, 관련 파일 목록, 추가 QA 목표.
- 출력 1: `production/qa/static-review-issue[N]-[날짜].md`
  - 상황별 예상 동작 표(상황 → 현재 코드 동작 → 사양 일치)
  - 의심 버그 표(번호 | 파일:줄 | 재현 조건 | 증상 | 확신도 | 수정 제안 | 상태=`open`)
- 출력 2: `production/qa/test-cases-issue[N]-[날짜].md`
  - 완료 조건과 추가 목표를 모두 덮는 테스트 케이스(qa-tester 형식, 실제 결과·판정 칸은 비움)
- 자동 테스트로 덮을 수 있는데 아직 없는 시나리오는 목록만 적는다. 테스트 코드 작성은 사용자가 지시한 경우에만 한다.

케이스를 사용자에게 보여 주고 AskUserQuestion으로 확인한다: "승인, 실행 시작 / 케이스 수정 / 일부 건너뛰기"

## 4단계: 실행 (메인 세션 + 사용자)
- **자동**: PlayMode 테스트는 사용자가 Test Runner에서 실행한다(UnityMCP가 연결되어 있으면 메인 세션이 실행할 수 있다). 실행이 끝나면 `TestRunReporter`가 결과를 `production/qa/test-runs/last-test-run.md`에 저장한다. **메인 세션이 이 파일을 직접 읽는다.** 사용자에게 실패 메시지를 복사해 달라고 하지 않는다. 파일의 실행 시각이 사용자가 돌린 시점보다 오래됐으면 그때만 다시 실행을 부탁한다.
- **수동**: 케이스를 3~4개씩 묶어 AskUserQuestion으로 묻는다: "PASS / PASS WITH NOTES / FAIL / BLOCKED"
  - **사용자가 답한 것만 기록한다.** 답이 없는 케이스는 미실행으로 남긴다. 대신 채워 넣지 않는다.
- FAIL이 나오면 설명을 받은 뒤 qa-tester에게 `production/qa/bugs/BUG-NNNN.md`를 쓰게 한다(번호 = 최대 + 1).
- 결과를 test-cases 문서의 "실제 결과·판정" 칸에 반영한다.

## 5단계: 승인 보고서 (qa-lead)
qa-lead에게 **증거와 판정 규칙만** 넘긴다. 증거는 분류표, 자동 검증 판정과 출처, 케이스별 결과, 이번에 쓴 버그, 열린 버그다. 판정을 미리 알려 주지 않는다. 돌아온 판정을 메인 세션이 같은 규칙으로 다시 확인하고, 다르면 둘 다 보여 주고 묻는다.

판정 규칙 (원본 그대로):
1. 범위 안에 열린 S1/S2 버그가 있거나, 우회 방법 없이 실패한 조건이 있으면 **NOT APPROVED**(가장 먼저 확인).
2. 실행된 증거가 없는 조건이 하나라도 있거나, 자동 검증이 UNKNOWN/FAIL이면 **NOT ASSESSED**. 실행한 게 없으면 "실패 0건"이어도 승인이 아니다.
3. 모두 PASS이고 S1/S2가 없으면 **APPROVED**.
4. S3/S4만 열려 있거나 PASS WITH NOTES가 있으면 **APPROVED WITH CONDITIONS**.

보고서 형식 (`production/qa/qa-signoff-issue[N]-[날짜].md`, 사용자 승인 후 메인 세션이 쓴다):
```markdown
## QA 승인 보고서: 이슈 #N [제목]
**날짜**: / **대상 커밋**:

### 커버리지 요약
| 완료 조건 | 유형 | 자동 테스트 | 수동 QA | 결과 |

### 버그
| ID | 심각도 | 상태 | 요약 |

### 판정: NOT ASSESSED / APPROVED / APPROVED WITH CONDITIONS / NOT APPROVED
**조건**(있으면):

### 다음 단계
```
미실행·BLOCKED 조건도 빠짐없이 한 줄씩 넣는다.

## 오류 처리
- 에이전트가 BLOCKED이거나 오류를 내면 즉시 알리고, 그 단계에 의존하는 다음 단계로 가지 않고, 부분 보고서를 남긴다.
- 반환 계약의 파일이 없으면 그 에이전트에게 계약을 다시 알려 이어서 하게 한다.

## 최종 출력
이슈, 자동 검증 판정, 수동 QA 결과, 새로 쓴 버그(ID·심각도), 최종 판정, 만든 문서 경로 목록을 요약한다.
