# 구현 프롬프트 사용 가이드

이 폴더의 파일들은 `../docs/spec.md`(기획 명세서)와 `../docs/tech-stack.md`(기술 스택 결정)를 실제 Unity 프로젝트로 옮기기 위한 단계별 작업 지시문이다. 각 파일은 **그 자체로 하나의 Claude Code 작업 세션에 붙여넣을 수 있는 프롬프트**로 작성되어 있다.

## 사용 방법

1. 번호 순서대로 진행한다. 이후 단계는 이전 단계의 산출물(스크립트, 씬, 데이터 에셋)을 전제로 쓰여 있다.
2. 새 Claude Code 세션(또는 같은 세션)에서 다음과 같이 지시한다:
   > `prompts/01-project-setup.md`를 읽고 그 내용을 그대로 수행해. 완료 조건 체크리스트를 전부 통과할 때까지 끝내지 마.
3. 각 프롬프트 파일 끝의 **완료 조건(Acceptance Criteria)** 을 모두 통과해야 다음 단계로 넘어간다. 통과하지 못하면 같은 프롬프트 파일 안에서 원인을 고치고 재검증한다 — 다음 번호로 건너뛰지 않는다.
4. 각 프롬프트는 작업을 마친 뒤 **무엇을 검증했는지, 무엇이 남았는지**를 짧게 보고하도록 지시문에 포함되어 있다. 이 보고를 사용자가 확인하고 다음 프롬프트를 진행시킨다.
5. 수치·태그·엔딩 조건을 코드에 다시 베껴 적지 말고 항상 `docs/spec.md`를 참조하도록 되어 있다. 스펙이 바뀌면 `docs/spec.md`를 먼저 수정한 뒤 해당 프롬프트를 재실행한다.

## 테스트 철학

- **단위 테스트(EditMode):** 순수 C# 로직(스트레스 공식, 반항/순응 판정, 엔딩 분기)은 Unity 씬 없이 빠르게 반복 검증한다. 03번 이후 모든 로직 프롬프트는 대응하는 EditMode 테스트를 같이 만든다.
- **플레이모드 테스트(PlayMode):** NavMesh 이동, 상호작용 영역 트리거, 방-복도 전환처럼 씬 상태가 필요한 것은 PlayMode 테스트로 검증한다.
- **시뮬레이션(16번 프롬프트):** 실제 VR 플레이 없이 수천 회의 "가상 플레이스루"를 자동 실행해 14개 엔딩 전부 도달 가능한지, 수치가 0~100을 벗어나지 않는지, 같은 시드에서 같은 결과가 재현되는지 통계적으로 확인한다. 이건 사람이 VR 헤드셋을 쓰고 1시간씩 반복 플레이하는 것으로는 현실적으로 다 확인할 수 없는 조합을 커버하기 위함이다.
- **수동 플레이테스트(17번 프롬프트):** 시뮬레이션이 잡을 수 없는 "느낌" — 밀기의 타격감, 아이가 바보처럼 보이는 순간, 서사 타이밍 — 은 사람이 직접 플레이하며 조정한다.

## 전체 순서

| # | 파일 | 산출물 |
|---|---|---|
| 01 | project-setup.md | Unity 프로젝트, 패키지, 폴더 구조, Git |
| 02 | core-data-architecture.md | StatSystem, GameState, EventBus, SaveSystem, GameBalanceConfig 뼈대 |
| 03 | room-shell-and-navmesh.md | 표준 방 쉘(24×22m), NavMesh, ItemSlot 프레임워크 |
| 04 | tool-and-stress-model.md | 전기 휠체어(왼손) 이동, 전기 사랑의 매(오른손) 접촉, 휠체어/봉 밀기와 기본 스트레스 공식(4.3) |
| 05 | infant-room-vertical-slice.md | 1차 수직 시연: 영유아 방 완성, 9장 9.1 완료 조건 통과 |
| 06 | corridor-transition-system.md | 복도, 회상 연출, 페이드 전환 |
| 07 | rebellion-coercion-compliance.md | 반항 R, 강제/반복 훈련(4.5), 순응 상태머신(4.6) |
| 08 | preference-learning-system.md | 태그 선호/회피 기억, softmax 선택, 디버그 패널(4.2~4.3) |
| 09 | childhood-room.md | 아동기 방, 종이비행기 사건, 종료 Stress 기록 |
| 10 | adolescence-room.md | 청소년기 방, 히든 의상, 비행·흡연 이벤트 |
| 11 | youngadult-room.md | 청년기 방, 도박파산/고위험계약/연락차단/퇴사 플래그 |
| 12 | middleage-and-endings.md | 중년기 방, 14개 엔딩 분기, 엔딩 카드, 회고 갤러리 |
| 13 | connectome-visualization.md | FlyWire 데이터 출처 명시 시각화(7장, 실험 빌드) |
| 14 | vr-comfort-and-accessibility.md | 이동/편안함/피드백/자막/색+모양 기준(8장) |
| 15 | quest-performance-optimization.md | Quest 스탠드얼론 최적화 패스 |
| 16 | simulation-and-automated-testing.md | 자동 시뮬레이션 러너, 9장 전체 시나리오 검증 |
| 17 | playtest-balance-pass.md | 반복 수동 플레이테스트와 밸런스 조정 루프 |

각 단계는 독립된 git 커밋 단위로 진행하는 것을 권장한다 (커밋 자체는 사용자 명시 요청이 있을 때만 수행).
