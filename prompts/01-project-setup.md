# 01. 프로젝트 셋업

## 목표

`C:\Users\이솔하\Desktop\fruit-fly-parenting` 아래에 Unity VR 프로젝트의 뼈대를 만든다. 이 단계에서는 게임플레이 로직을 작성하지 않는다 — 다음 단계들이 올라갈 수 있는 깨끗한 토대만 만든다.

## 선행 조건

없음 (첫 단계). 단, 시작 전에 `docs/spec.md`와 `docs/tech-stack.md`를 반드시 읽고 내용을 이해한다.

## 참고 스펙 섹션

`docs/tech-stack.md` 전체.

## 구현 지시사항

1. Unity Hub로 LTS 최신 버전(Unity 6 LTS 계열, 설치되어 있지 않으면 사용 가능한 가장 최신 LTS)을 사용해 `fruit-fly-parenting` 폴더 안에 Unity 프로젝트를 생성한다. 템플릿은 **3D (URP)** 로 시작한다.
   - 이미 `docs/`, `prompts/` 폴더가 있으니 Unity 프로젝트 생성 시 이를 덮어쓰거나 삭제하지 않는다. Unity 프로젝트의 `Assets/`, `Packages/`, `ProjectSettings/` 등은 같은 루트에 함께 둔다.
2. Package Manager에서 다음을 설치한다: XR Interaction Toolkit, OpenXR Plugin, XR Plug-in Management, AI Navigation, Input System, TextMeshPro(Essential Resources 포함), Unity Test Framework.
3. Project Settings → XR Plug-in Management에서 PC(Windows)와 Android 양쪽 탭에 OpenXR을 활성화한다. PC 탭에는 일단 Meta Quest Link/SteamVR을 통한 연결을 가정해 OpenXR 기능 그룹을 켠다. Android 탭은 지금 당장 기능을 다 켜지 않아도 되며, 15번 프롬프트(Quest 최적화)에서 다시 다룬다는 주석을 `docs/tech-stack.md`에 추가해도 된다.
4. `docs/tech-stack.md` 3장에 정의된 폴더 구조를 `Assets/_Project/` 아래 실제로 생성한다 (빈 폴더라도 `.gitkeep` 등으로 커밋되게 한다).
5. Git 저장소를 초기화한다(이미 초기화돼 있지 않다면). Unity용 표준 `.gitignore`(Library/, Temp/, Obj/, Build/, Logs/, UserSettings/, .vs/, *.csproj, *.sln 등 제외)를 추가한다. 대용량 바이너리(오디오/텍스처/모델)가 생기면 Git LFS를 쓸지 결정하고 `docs/tech-stack.md`에 한 줄로 기록한다.
6. 빈 씬 `Assets/_Project/Scenes/00_Bootstrap.unity`를 만들고 Build Settings의 첫 씬으로 등록한다. 이 씬에는 아직 아무 오브젝트도 없어도 된다.
7. XR Origin(VR) 프리팹을 하나 배치해서 헤드셋을 쓰면 실제로 화면이 나오는지 최소한으로 확인한다 (손 상호작용/휠체어 로코모션은 아직 필요 없음). XR Interaction Toolkit의 Locomotion System 샘플(Continuous Move/Turn Provider)을 Package Manager에서 임포트해 둔다 — 03번 이후 이 XR Origin이 휠체어 리그의 자식으로 재배치되고, 이 Provider가 왼손 스틱 입력을 받게 된다(스펙 4.3, 8장). 지금 단계에서는 임포트만 해 두고 실제 연결은 하지 않는다.

## 완료 조건 (Acceptance Criteria)

- [ ] Unity 프로젝트가 에디터에서 에러/경고 없이 열린다.
- [ ] Package Manager에 지시사항 2의 패키지가 모두 설치되어 있다.
- [ ] `Assets/_Project/` 하위 폴더 구조가 `docs/tech-stack.md` 3장과 일치한다.
- [ ] `git status`가 Library/Temp 등 생성 산물을 추적하지 않는다.
- [ ] `00_Bootstrap` 씬을 Play 모드로 실행했을 때 콘솔에 에러가 없다.
- [ ] (VR 헤드셋이 연결되어 있다면) 헤드셋을 쓰면 빈 씬이 양쪽 눈에 렌더링된다. 헤드셋이 당장 없다면 이 항목은 생략하고 보고서에 "헤드셋 미연결로 확인 못함"이라고 명시한다.
- [ ] Locomotion System(Continuous Move/Turn Provider) 샘플이 임포트되어 있다(아직 사용하지 않아도 됨).

## 보고

완료 후 다음을 보고한다: 설치된 Unity 버전, 설치된 패키지 버전 목록, 생성된 폴더 트리, Git 초기 커밋 여부(실제 커밋은 사용자가 명시적으로 요청할 때만 수행 — 이 단계에서는 커밋하지 말고 "커밋 준비 완료"라고만 보고), 헤드셋 확인 여부.
