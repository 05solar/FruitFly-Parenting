# 15. Quest 스탠드얼론 최적화 패스

## 목표

`docs/tech-stack.md`에서 정한 2단계 전략(PCVR 선개발 → Quest 최적화)을 실행한다. 지금까지(01~14번) PCVR 기준으로 만든 전체 게임을 Meta Quest 스탠드얼론에서 목표 프레임으로 돌리도록 최적화한다.

## 선행 조건

14-vr-comfort-and-accessibility.md 완료 (게임플레이·접근성 기준으로 완결된 상태).

## 참고 스펙 섹션

`docs/spec.md` 9.3("VR 타깃 기기에서 프레임 유지... 목표 프레임과 폴리곤 예산은 대상 HMD 확정 후 수치화한다"), `docs/tech-stack.md` 1장.

## 구현 지시사항

1. **목표 수치 확정:** 실제 타깃 Quest 기종(Quest 2 / Quest 3 등, 사용자에게 확인되지 않았다면 현재 보유 기종을 기준으로 정하고 보고서에 명시)에 맞춰 목표 프레임(예: 72/90Hz)과 씬당 폴리곤/드로우콜/텍스처 메모리 예산을 정해 `docs/tech-stack.md`에 구체적 수치로 기록한다.
2. **Android/OpenXR 빌드 설정:** 01번에서 미뤄둔 XR Plug-in Management의 Android 탭을 완성한다(OpenXR + Meta 기능 그룹 활성화, Color Space는 Linear 유지, Graphics API는 Vulkan 권장). Player Settings의 Android 최소/타깃 API 레벨, IL2CPP + ARM64 빌드를 설정한다.
3. **렌더링 최적화:**
   - URP 품질 설정을 Quest용 별도 Quality Level로 분리(그림자 거리/해상도 축소, 안티앨리어싱을 MSAA 4x 이하로, 포스트프로세싱 최소화).
   - 03번 `RoomShell`의 폴리곤 수를 측정하고, 2.2 규격(24×22m, 가구 8~10개)을 유지하면서 필요한 경우 가구 메시를 LOD 또는 단순화된 버전으로 교체한다.
   - 동적 라이트 수를 줄이고 가능한 부분은 베이크된 라이트맵으로 교체.
4. **CPU/로직 최적화:**
   - 07번 `ComplianceStateMachine`, 08번 `ItemChoiceModel`의 매 프레임 계산량을 점검해 필요 이상으로 자주 계산되지 않는지 확인(예: 매 프레임이 아니라 일정 간격으로 재계산).
   - `DebugHud`(08번)가 릴리즈/Quest 빌드에서 완전히 비활성화되는지 재확인(14번에서 이미 다뤘지만 Quest 빌드 심볼 기준으로 다시 검증).
5. **드로우콜/배칭:** 가구/방 구조물에 정적 배칭(Static Batching) 또는 GPU Instancing을 적용. 복도(06번)의 반복 구조물도 동일하게 점검.
6. **메모리:** 텍스처 압축(ASTC) 설정, 오디오 압축 포맷(Vorbis 등) 확인. 13번 커넥톰 시각화 데이터가 본편 Quest 빌드에 포함되지 않는지 다시 확인(13번에서 이미 분리했지만 Android 빌드 타깃에서 최종 확인).
7. **실기기 프로파일링:** Unity Profiler + Android용 프로파일링(예: OVR Metrics Tool 또는 Unity의 Android Device Profiler)으로 각 방(05, 09, 10, 11, 12)을 실기기에서 재생하며 프레임타임, GPU/CPU 바운드 여부를 측정한다. 헤드셋이 당장 없다면 이 항목은 보고서에 "실기기 미보유로 측정 못함 — PC에서 Android 빌드만 생성해 설치 가능 여부 확인"으로 명시한다.

## 완료 조건 (Acceptance Criteria)

- [x] Android(Quest) 릴리즈·개발용 APK가 에러 없이 생성된다. ARM64/IL2CPP/서명/메타데이터도 검사했다.
- [x] 목표 프레임/폴리곤/드로우콜 예산이 `docs/tech-stack.md`에 구체적 수치로 기록되어 있다. 보유 기종 미확인으로 Quest 2·72Hz 임시 기준이다.
- [ ] 각 방 프리팹의 폴리곤 수/드로우콜 수가 예산 안에 든다. **삼각형 예산과 정적 머티리얼 슬롯 수는 전수 측정 완료. 실제 GPU 드로우콜은 HMD 연결 후 측정 필요**하며 슬롯 수와 혼동하지 않는다.
- [x] 디버그 패널/에디터 전용 Gizmo가 Quest 릴리즈·개발 빌드에 포함되지 않는다. APK 메타데이터에서 해당 프로젝트 타입 제외를 확인했다.
- [ ] Development Build를 끈 릴리즈 플레이어에서 F8을 눌러도 `DebugHud`가 생성·표시되지 않는지 확인한다(08 단계의 Windows 개발/릴리즈 검증을 Quest에서도 재검증).
- [ ] (실기기가 있다면) 72Hz 또는 90Hz 기준 프레임 드랍이 없는 구간과 있는 구간을 식별하고, 있는 구간에 대한 추가 최적화 계획을 남긴다.

## 보고

목표 수치, 방별 최적화 전/후 폴리곤·드로우콜 비교표, 실기기 측정 결과(또는 미보유 사유), 남은 최적화 과제를 보고한다.

구현·빌드·측정 범위와 남은 실기기 항목은 [`docs/quest-performance.md`](../docs/quest-performance.md)에 기록했다. 현재 ADB 연결 기기는 0대다. APK 구조 검사 성공을 기기 설치·실행·목표 프레임 달성으로 표현하지 않는다.
