# 02. 핵심 데이터 아키텍처

## 목표

게임 전체가 공유할 상태/스탯/저장/이벤트 시스템의 뼈대를 만든다. 이후 모든 방·도구·엔딩 로직은 이 시스템을 통해서만 수치를 읽고 쓴다. 이 단계는 **아직 방이나 초파리 없이** 순수 로직 + EditMode 테스트로만 검증한다.

## 선행 조건

01-project-setup.md 완료.

## 참고 스펙 섹션

`docs/spec.md` 4.1(상태값과 행동 로그), 8장(저장 규칙), `docs/tech-stack.md` 4장(코딩 원칙).

## 구현 지시사항

1. **`GameBalanceConfig` (ScriptableObject, `Assets/_Project/Scripts/Data/`):** 스펙에 등장하는 모든 밸런스 상수를 필드로 둔다. 지금 채울 것: 스탯 기본값(50), 스탯 범위(0~100), 신뢰 T 기본값(50), 기본 스트레스 증가량(0.4), 60초 윈도 개입 가중치 계수(0.25), 개입 쿨다운(5초 미만 반복 시 T-1), 방치 보상(60초당 T+1, 방당 최대 +4). 아직 구현하지 않는 4.5/4.6의 상수(강제훈련 R/S, 순응 임계점 등)도 필드만 미리 만들어 두고 07번 프롬프트에서 값을 채운다 — 필드 이름과 주석만 지금 정의해도 된다.
2. **`StatType` enum:** `Stress, Intelligence, Health, Popularity, Love, Rebellion` (스펙 S/I/H/P/L/R). `Trust`(T)는 별도 비공개 값이므로 이 enum에 넣지 않고 `RelationshipState`에서 따로 관리한다.
3. **`StatSystem` (순수 C# 클래스, MonoBehaviour 아님):**
   - 내부에 `Dictionary<StatType, float>` 보관, 모든 값 생성 시 `GameBalanceConfig`의 기본값(50)으로 초기화.
   - `ApplyDelta(StatType type, float amount, string reasonTag)` 메서드 하나로만 값이 바뀌게 한다. 내부에서 0~100 clamp를 강제한다.
   - 변경 발생 시 `OnStatChanged(StatType, oldValue, newValue, reasonTag)` C# 이벤트를 발생시킨다 (UI/로그가 구독).
   - 방 단위 최대 변화량 제한(일반 물품 ±12, ★ 물품 ±25)은 이 레벨에서 강제하지 않는다 — 이건 "한 물품의 효과 적용"이라는 상위 개념이므로 05번 이후 `ItemEffect` 쪽 책임으로 둔다. `StatSystem`은 순수하게 clamp와 이벤트만 담당한다는 책임 분리를 주석으로 명시한다.
4. **`RelationshipState` (순수 C# 클래스):** 신뢰 T(0~100, 초기 50), 마지막 개입 시각, 최근 60초 개입 타임스탬프 리스트, 개입 누적 횟수(전체), 자율 방문 수, 물품별 재방문 수 딕셔너리. `AdjustTrust(float amount, string reasonTag)` 메서드로만 변경.
5. **`GameState` (싱글턴 또는 DI로 접근 가능한 순수 C# 클래스, MonoBehaviour 어댑터 하나만 둠):** `StatSystem`, `RelationshipState`, 현재 방 식별자, `RunSeed`(정수, `System.Random` 시드), 방별/전역 이벤트 로그 리스트를 들고 있다. `EventLogEntry { timestamp, roomId, eventType, payload }` 구조체 정의.
6. **`EventBus` (정적 클래스 또는 단일 인스턴스):** 느슨한 결합을 위한 Pub/Sub. 최소 이벤트: `ItemVisited(itemId, isFirstVisit)`, `ToolUsed(strength, direction, targetItemId?)`, `RoomEntered(roomId)`, `RoomExited(roomId)`. 04번 이후 프롬프트들이 이 이벤트들을 발행/구독한다.
7. **`SaveSystem` 뼈대:** 8장의 저장 시점(방 진입/물품 최초 방문/복도 시작/엔딩 확정)에 대응하는 `SaveCheckpoint(string checkpointName)` 메서드만 정의하고, 지금은 JSON으로 `Application.persistentDataPath`에 `GameState`를 직렬화/역직렬화하는 최소 구현을 한다. 세이브 슬롯 UI는 만들지 않는다.
8. `Assets/Tests/EditMode/`에 다음 테스트를 작성한다 (Unity Test Framework, NUnit):
   - `StatSystem`: 0 미만/100 초과로 가려는 변경이 clamp되는지.
   - `StatSystem`: `ApplyDelta` 호출 시 `OnStatChanged` 이벤트가 정확한 old/new 값으로 발생하는지.
   - `RelationshipState`: 60초 창을 벗어난 개입 타임스탬프가 "최근 60초 개입 수" 계산에서 제외되는지(가짜 시간 소스를 주입할 수 있게 `RelationshipState`가 실제 시간이 아니라 전달받은 `currentTime` 매개변수를 쓰도록 설계 — Unity의 `Time.time`에 직접 의존하지 않는다. 이건 이후 시뮬레이션 테스트에서 가짜 시간을 빠르게 흘려보내기 위해 필수적인 설계 결정이다).
   - `GameState`: `RunSeed`로 생성한 `System.Random`이 같은 시드에서 같은 시퀀스를 내는지.
   - `SaveSystem`: 저장 후 불러온 `GameState`가 저장 전과 값이 같은지(round-trip).

## 완료 조건 (Acceptance Criteria)

- [ ] 위 8개 항목의 모든 테스트가 Unity Test Runner(EditMode)에서 통과한다.
- [ ] `StatSystem`, `RelationshipState`, `GameState`, `EventBus`, `SaveSystem` 중 어느 것도 `MonoBehaviour`를 상속하지 않는다(어댑터 제외).
- [ ] `GameBalanceConfig` ScriptableObject 에셋이 `Assets/_Project/ScriptableObjects/`에 하나 생성되어 있고 Inspector에서 값을 바꿀 수 있다.
- [ ] 모든 수치 변경 경로가 `StatSystem.ApplyDelta`/`RelationshipState.AdjustTrust`를 통과한다 — 직접 딕셔너리를 수정하는 코드가 없다(코드 검색으로 확인).

## 보고

작성한 클래스 목록과 책임 요약, 테스트 실행 결과(통과/실패 수), 아직 비워둔 `GameBalanceConfig` 필드(4.5/4.6용) 목록을 보고한다.
