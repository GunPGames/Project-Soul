---
paths:
  - "Assets/Soulslike/**/*.cs"
---

# Unity C# 규칙

- 네임스페이스: 런타임 `Soulslike`, 에디터 `Soulslike.EditorTools`. 에디터 코드는 `Editor/` 폴더에만 둔다.
- 필드: `[SerializeField] private`(접근자 생략 가능) + `[Tooltip]`. 비공개 필드는 `_camelCase`, 공개 멤버는 `PascalCase`.
- 컴포넌트 참조는 Awake에서 캐시하고, 비어 있을 때만 자동으로 찾는다.
- 이벤트 구독은 해제 지점(OnDisable/OnDestroy)을 함께 작성한다.
- 핫패스에서 LINQ·new 배열·문자열 연결을 쓰지 않는다. 레이캐스트는 `NonAlloc`.
- 실행 순서에 의존하면 `[DefaultExecutionOrder]`로 명시한다.
- 주석은 "왜"를 한국어로 짧게 적는다.
