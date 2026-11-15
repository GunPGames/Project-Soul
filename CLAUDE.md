# Project-Soul — 3인칭 소울라이크 (Unity)

말투·약어 설명·코드 수정 허락·설계 근거 규칙은 전역 CLAUDE.md를 따른다. 여기서는 반복하지 않는다.

## 기술 스택

- Unity 6000.3 (URP 17.3), C#, 새 Input System만 사용(구 Input Manager 꺼짐)
- Cinemachine 없음. 카메라는 `ThirdPersonCamera`가 직접 처리한다.
- 에디터 조작은 UnityMCP(MCP for Unity)로 한다. 연결이 끊기면 사용자에게 알리고, 컴파일 확인은 Unity 내장 Roslyn으로 대신한다.

## 폴더

| 경로 | 내용 | 규칙 |
|---|---|---|
| `Assets/Soulslike/Scripts` | 런타임 코드 (namespace `Soulslike`) | |
| `Assets/Soulslike/Editor` | 에디터 메뉴 (`Tools/Soulslike/*`) | |
| `Assets/Soulslike/Animations` | 메뉴가 생성하는 컨트롤러 | 손으로 고치지 말고 메뉴로 다시 만든다 |
| `Assets/ThirdParty` | 유료 에셋. 비공개 레포 서브모듈 | 공개 레포에 내용이 들어가면 안 된다 |
| `Assets/Soulslike/Tests/PlayMode` | PlayMode 시뮬레이션 테스트 (어셈블리 `Soulslike.Tests.PlayMode`) | 실행은 사용자가 Test Runner에서. 결과는 `production/qa/test-runs/last-test-run.md`에 자동 저장되므로 직접 읽는다 |
| `design/` | 짧은 기획 노트 (`/quick-design`이 작성) | |
| `production/qa/` | QA 계획·정적 분석·테스트 케이스·승인 보고서 (`/team-qa`가 작성) | 버그는 `production/qa/bugs/BUG-NNNN.md` |

서브모듈 커밋 순서: `Assets/ThirdParty`에서 커밋·푸시 → 상위 레포에서 서브모듈 포인터 커밋·푸시.

## 작업 방식 (요금제 절약)

- 기본은 **메인 세션에서 직접** 처리한다. 서브에이전트는 사용자가 요청했거나, 넓은 탐색이 필요할 때만 쓴다. 한 번에 하나만 띄운다.
  - 예외: `/team-qa`는 qa-lead·qa-tester(둘 다 sonnet)를 단계별로 차례로 띄운다.
- 문서·코드는 필요한 부분만 읽는다(Grep → offset/limit). 큰 파일 통째 읽기를 피한다.
- 질문 → 선택지 → 결정 → 구현 순서를 지키되, 기본값이 분명한 사소한 선택은 묻지 않고 진행 후 알린다.
- 커밋·푸시는 사용자가 지시할 때만 한다.

## 검증

- 코드 변경 후 컴파일을 확인한다(UnityMCP `read_console` 또는 Roslyn).
- 플레이 테스트를 못 했으면 보고에 "미검증"이라고 분명히 적는다.
