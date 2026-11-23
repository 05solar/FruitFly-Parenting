# 기술 스택 및 프로젝트 규칙

이 문서는 `spec.md`를 실제로 구현하기 위한 공통 전제다. 모든 `prompts/*.md`는 이 문서의 규칙을 따른다고 가정하고 작성되었다. 엔진이나 폴더 구조를 바꾸면 이 문서를 먼저 고치고 프롬프트들을 다시 확인해야 한다.

## 1. 엔진/플랫폼 결정

- **엔진:** Unity (LTS 최신 버전, 예: Unity 6 LTS 계열). URP(Universal Render Pipeline) 사용 — Quest 스탠드얼론 최적화를 염두에 둔 선택.
- **VR SDK:** OpenXR Plugin + XR Interaction Toolkit (XRI). SteamVR 전용 플러그인은 쓰지 않는다 (OpenXR 하나로 PCVR과 Quest를 모두 커버).
- **1단계 타깃:** PCVR (OpenXR, SteamVR/Quest Link로 헤드셋 연결). 그래픽 품질과 반복 테스트 속도를 우선한다.
- **2단계 타깃:** Meta Quest 스탠드얼론 (Android 빌드, OpenXR Meta 기능 그룹). 폴리곤/드로우콜/쉐이더 예산을 줄이는 최적화 패스를 별도 단계(14번 프롬프트)에서 수행한다.
- **입력:** Unity Input System + XRI 기본 액션 맵. 왼손 스틱 = 전기 휠체어 이동(전진·후진·회전) 전용, 오른손 = 항상 전기가 흐르는 사랑의 매 고정 장착(손 바꾸기 없음, 역할 고정). 휠체어 이동은 XRI의 Continuous Move/Turn Locomotion Provider를 쓰고, 텔레포트/순간이동 Provider는 사용하지 않는다 (8장 VR 기준).

## 2. 필수 패키지

| 패키지 | 용도 |
|---|---|
| XR Interaction Toolkit | 손 상호작용, 포인터, 소켓, 그랩 |
| OpenXR Plugin | PCVR/Quest 공통 런타임 |
| XR Plug-in Management | 플랫폼별 XR 로더 관리 |
| AI Navigation (NavMesh) | 초파리 자율 이동 (4.2) |
| Input System | 컨트롤러 입력 추상화 |
| TextMeshPro | 자막, UI 수치, 디버그 패널 |
| Unity Test Framework | PlayMode/EditMode 자동 테스트 (9장 검증) |
| Cinemachine (선택) | 복도 전환 페이드, 컷신 카메라 보조 |

Asset Store/Mixamo 등 외부 애셋은 캐릭터·가구 비주얼에만 쓰고, 이 문서에 정의된 로직(수치, NavMesh, 상태머신)은 항상 직접 작성한 스크립트로 구현한다.

## 3. 폴더 구조

```
Assets/
  _Project/
    Scripts/
      Core/            // GameState, StatSystem, EventBus, SaveSystem
      Room/            // RoomController, ItemSlot, RoomTimerController
      Fly/              // FlyAgent(NavMesh), FlyMemory(선호/회피), FlyMoodState
      Wheelchair/       // WheelchairLocomotion(왼손 스틱), WheelchairBumperPushResolver, 끼임 방지
      Tool/             // LoveStickTool(상시 전기, 오른손 고정), ElectricPushResolver, ComplianceStateMachine
      Corridor/         // CorridorController, RecapPanel
      Ending/           // EndingEvaluator, EndingData, EndingCardUI
      Debug/            // DebugHud, SimulationRunner
      Data/             // ScriptableObject 정의 (ItemDefinition, RoomDefinition, EndingDefinition)
    Prefabs/
      Rooms/
      Items/
      Fly/
      Tool/
      UI/
    ScriptableObjects/
      Items/            // 방별 물품 데이터 에셋
      Rooms/            // 방별 설정 에셋
      Endings/
    Scenes/
      00_Prologue.unity
      01_Infant.unity
      02_Corridor.unity
      03_Childhood.unity
      ... (스펙 2장 동선 순서를 그대로 따름)
    Audio/
    Art/
    Settings/        // URP 파이프라인/렌더러 에셋, 품질 레벨별 에셋
  Tests/
    EditMode/
    PlayMode/
```

- 모든 수치/태그/엔딩 조건은 하드코딩하지 않고 ScriptableObject 데이터 에셋으로 뺀다. 이렇게 해야 밸런스 조정과 시뮬레이션(9장, 16번 프롬프트)이 코드 재컴파일 없이 가능하다.
- 씬 사이를 이동하지 않고 하나의 영속 씬 + 방 단위 활성화/비활성화로 구현할지, 씬별 로드로 구현할지는 02번 프롬프트(방 쉘)에서 결정한다. 기본 권장은 **단일 영속 씬 + 방 컨테이너 활성화** (복도 전환 중 로딩 끊김을 피하기 위함).

## 4. 코딩/데이터 원칙

- 휠체어 가속/감속/최고속도/회전속도, 범퍼 접촉 판정 쿨다운(1초) 등 로코모션 상수도 `GameBalanceConfig`에 둔다.
- 스펙의 모든 수치 상수(예: 기본 스트레스 +0.4, 순응 임계점 6/8점 등)는 코드 안에 매직 넘버로 박지 말고 `GameBalanceConfig` 라는 단일 ScriptableObject에 모아 둔다. 이후 밸런스 패스(17번 프롬프트)에서 여기만 조정한다.
- 모든 스탯 변경은 `StatSystem` 한 곳을 통과시키고, 변경 시마다 0~100 clamp와 이벤트 로그 기록을 강제한다. 개별 스크립트에서 직접 수치를 더하지 않는다.
- 결정적 재현성을 위해 전역 `System.Random` 시드 하나를 `RunSeed`로 관리한다 (8장 저장 규칙, 16번 시뮬레이션 프롬프트에서 필수).
- 자동 테스트와 시뮬레이션이 가능해야 하므로, 모든 게임플레이 로직은 **Unity 생명주기(MonoBehaviour)에서 분리된 순수 C# 클래스**로 먼저 작성하고, MonoBehaviour는 그 클래스를 감싸는 얇은 어댑터로만 둔다 (예: `StressModel`은 순수 클래스, `FlyAgent`는 MonoBehaviour에서 `StressModel`을 들고 있음). 이렇게 해야 EditMode 테스트에서 Unity 씬 없이 로직만 빠르게 검증할 수 있다.

## 5. 버전 관리

- Git 저장소 초기화, `.gitignore`는 Unity 공식 템플릿(Library/, Temp/, Obj/, Build/, .vs/ 등 제외) 사용.
- Git LFS를 대용량 아트/오디오에 사용할지는 01번 프롬프트(프로젝트 셋업)에서 결정하고 문서화한다.
