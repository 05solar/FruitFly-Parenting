# 13단계 커넥톰 데이터 명세

고정일: 2026-11-24. 이 문서를 먼저 작성한 뒤 시각화를 구현한다.

## 데이터와 라이선스

성체 암컷 FlyWire **FAFB v783 (2023년 10월 스냅샷)**의 Codex 공개 정적 파일을 사용한다. 현재 Codex 포털은 로그인을 요구하므로, Codex 자체 [공식 로더](https://github.com/murthylab/codex/blob/main/codex/data/local_data_loader.py)에 명시된 공개 GCS 정적 저장소에서 내려받았다. 원본은 `Logs/ConnectomeRaw/`에만 보관하며 Git과 Unity Assets에서 제외한다. 파일은 추가 주석 갱신 가능성이 있어 버전 이름뿐 아니라 SHA-256까지 고정한다. 다른 해시를 자동 수용하지 않는다.

| 실제 사용 자료 | 원본 URL | SHA-256 | 적용 라이선스 |
| --- | --- | --- | --- |
| 뉴런 분류·좌우 주석, 934,402 bytes | https://storage.googleapis.com/flywire-data/codex/data/fafb/783/classification.csv.gz | `e946b552f4056dfc977707be0674609832c3f64332a22d69dc0d9615e7aae663` | CC BY-NC 4.0 |
| 영역별 방향성 연결·시냅스 수, 50,289,304 bytes | https://storage.googleapis.com/flywire-data/codex/data/fafb/783/connections.csv.gz | `d49dd692e59e153aa3c83f5257bfc0eff51247b86d7bb183386c6d1622c70fc9` | CC BY-NC 4.0 |
| 기존 한국어 표시 글꼴 `Assets/_Project/Art/Fonts/NotoSansCJKkr-Regular.otf` | https://github.com/notofonts/noto-cjk | 기존 `Art/Fonts/OFL.txt` 참조 | SIL Open Font License 1.1 |

데이터·주석 라이선스 근거: https://join.flywire.ai/guidelines 및 https://creativecommons.org/licenses/by-nc/4.0/ . 파생 JSON 두 개에도 CC BY-NC 4.0을 명시한다. 이 데이터 포함 실험 배포는 비상업 용도다. 자체 작성 코드 전체의 라이선스를 데이터 라이선스로 바꾸는 것은 아니다. 학술 도판, EM 이미지, 뇌/뉴런 3D 메시, 외부 좌표 및 신경전달물질 예측은 사용하지 않는다. 구·선·화살표와 공간 배치는 자체 생성한다.

화면에 표시할 출처 문구:

> FlyWire Consortium, Nature 2024 · FAFB v783 · CC BY-NC 4.0
>
> 실제 데이터: 선택 뉴런의 연결·시냅스 수 / 창작: 배치·색·빛의 움직임
>
> 이 시각화는 창작된 게임 연출이며 실제 초파리의 행동을 재현하지 않습니다

동봉 상세 출처 문구:

> Derived from FlyWire FAFB v783 connectivity and classification data. Dorkenwald, S. et al., Neuronal wiring diagram of an adult brain, Nature 634, 124–138 (2024), https://doi.org/10.1038/s41586-024-07558-y. Schlegel, P. et al., Whole-brain annotation and multi-connectome cell typing of Drosophila, Nature 634, 139–152 (2024), https://doi.org/10.1038/s41586-024-07686-5. Licensed under CC BY-NC 4.0, https://creativecommons.org/licenses/by-nc/4.0/. Changes: deterministic pathway selection, regional summation, class aggregation, logarithmic visual scaling and authored layout. No endorsement by the authors is implied.

## 선택 규칙 (고정)

5개 열을 만든다. 0: `super_class=sensory AND class=olfactory`; 1: `class=ALPN`; 2: `class=Kenyon_Cell`; 3: `class=MBON`; 4: `super_class=descending`. 마지막 열은 하행 신호의 출구이며 근육 운동뉴런이 아니다. 실제 운동이나 학습 동역학을 실행하지 않는다.

인접 열 사이의 방향성 연결만 남긴다. 한 뉴런 쌍의 모든 neuropil 행을 합산한 정수 시냅스 수 `w >= 5`를 사용한다. 원본이 포함하지 않은 연결을 생성하지 않는다. 각 하행 뉴런에 도달하는 4-edge 경로 중 최소 edge 가중치가 가장 큰 경로(최대 병목 경로)를 동적 계획법으로 선택한다. 동점이면 문자열 root ID 경로의 사전순 앞쪽을 선택한다. 끝점마다 구한 경로를 병목값 내림차순, ID 경로 오름차순으로 정렬해 **상위 6개 경로의 합집합**만 출력한다. 따라서 최대 30 nodes / 24 edges이며, 분리된 class 집계만으로 존재하지 않는 개별 뉴런 경로를 암시하지 않는다. 선택한 모든 ID·경로·각 연결의 영역별 원래 수를 JSON에 보존한다.

이 기준으로 후보는 ORN 2,281 / ALPN 685 / KC 5,177 / MBON 96 / descending 1,305개다. 선택 영역은 AL, MB_CA, MB_ML, MB_VL, CRE, SMP, LAL, VES, FLA의 좌·우 중 실제 선택 edge가 가진 영역이다. 해부학적 영역 경계를 렌더링하지 않는다. 선별 경로는 전체 회로의 대표 표본 또는 유일한 감각운동 경로라는 주장이 아니다.

## 변환과 입출력

Python 3 표준 라이브러리만 사용한다. `tools/connectome/preprocess.py`는 gzip CSV를 스트리밍하고 위 기준에 필요한 후보 연결만 메모리에 둔다. `classification.csv.gz`: root_id, super_class, class, side 필수. `connections.csv.gz`: pre_root_id, post_root_id, neuropil, syn_count 필수. ID는 64-bit 정밀도 손실을 피하기 위해 문자열로 유지한다. 헤더 누락, 중복 뉴런 ID, 음수/비정수 가중치, 해시 불일치, 완전 경로 부족은 실패로 처리한다.

출력 경로: `Assets/_Project/Experimental/Connectome/Data/`. `connectome-v783.json`은 개별 뉴런 그래프, `memory-motif-v783.json`은 **선택된 그래프만** 열별 5개 노드/4개 edge로 합산한 독립 기억 모티브다. 모티브를 전체 뇌의 집계로 표현하지 않는다. 각 파일의 schemaVersion=1, dataset, license, sources(원본 URL/hash), nodes(id, label, stage, side, members), edges(source, target, synapses, visualWeight, regions), paths를 기록한다. 각 edge의 regions는 neuropil별 시냅스 수를 보존한다.

고정된 최종 산출물은 개별 그래프 **17 nodes / 16 edges / 10,290 bytes**, 모티브 **5 nodes / 4 edges / 3,829 bytes**다. 합계 14,119 bytes이며, 두 파일을 다시 생성해 byte-identical임을 검사한다. 동점 처리는 마지막 edge에서 병목이 낮아져 이전 후보들이 동점이 되는 경우까지 포함한다. 최대 병목값 계산 후 그 임계값을 통과하는 경로 중 사전순 첫 경로를 재구성한다.

`visualWeight = round(log(1+w) / log(1+max_w), 6)` (각 출력 그래프 안에서 정규화). 선 굵기는 `0.018 + 0.065 * visualWeight` Unity 단위. 시냅스 수의 양수 크기만 표현하며 흥분/억제, 발화 확률, 인과 효과를 뜻하지 않는다. 좌표는 단계별 열과 ID 정렬에 의한 자체 배치다. 색은 단계별 구분이며 전기활동 데이터가 아니다. 움직이는 빛은 항상 일정 주기의 창작 연출이다.

재현 명령 (저장소 루트):

```powershell
python tools/connectome/preprocess.py --download --raw-dir Logs/ConnectomeRaw
python -m unittest discover -s tools/connectome -p "test_*.py"
```

다운로드 없이 같은 명령에서 `--download`를 빼면 로컬 원본과 고정 해시를 검사하고 재생성한다. 누락 원본을 합성 데이터로 대체하지 않는다. 출력은 시간값을 포함하지 않아 동일 입력에서 byte-identical이다. 원본과 파생본의 해시는 `docs/reports/step13-data.json`에 기록한다.

## 런타임·빌드 경계

`Experimental_ConnectomeViewer.unity` 하나만 사용하는 별도 Windows 실험 빌드를 제공한다. 데이터는 씬의 TextAsset 직접 참조로만 포함하며 Resources/StreamingAssets/Addressables/preloaded assets를 사용하지 않는다. 독립 asmdef는 Game/Fly 어셈블리 참조가 없고 `UNITY_EDITOR || CONNECTOME_EXPERIMENT`일 때만 컴파일한다. 본편 빌드 씬 목록에는 추가하지 않는다. 본편의 모든 policy, 상태, 저장, 복도 코드는 수정하지 않는다. 기억 모티브도 이 씬의 전환 기능으로만 시연한다.

뷰어는 데스크톱 실험용이다. VR 본편 복도 통합, HMD 성능 검증, 실제 회로 기반 정책 및 IFlyDecisionPolicy 추상화는 이번 범위 밖이다. 항상 보이는 UI는 출처·제약과 실제 관계/창작 규칙을 구분한다.

`scripts/Build-Connectome.ps1 -Test`는 `Builds/ConnectomeProject`에 실험 폴더·기존 폰트·URP 설정만 복사하고, `tools/connectome/standalone-manifest.json`의 최소 패키지로 독립 프로젝트를 생성한다. 원본 프로젝트의 Packages/ProjectSettings와 본편 씬은 수정하지 않는다. 사용자 작업 중 추가된 Meta XR SDK의 전역 네이티브 초기화가 데스크톱 실험에도 실행되는 문제를 피하기 위해, XR/vendor 패키지를 이 생성 프로젝트에 포함하지 않는다. 출력 EXE 옆에 `CONNECTOME-NOTICE.txt`, `FONT-OFL.txt`를 동봉한다. 생성 프로젝트도 Git 제외 대상이며 원본 데이터는 복사하지 않는다.

독립 빌드 명령:

```powershell
.\scripts\Build-Connectome.ps1 -Test
# 씬만 생성해 독립 Unity 에디터로 확인할 때:
.\scripts\Build-Connectome.ps1 -PrepareOnly
```

EXE: `Builds/ConnectomeProject/Builds/Step13/ConnectomeExperiment/ConnectomeExperiment.exe`.
원본 에디터 메뉴 `Fruit Fly Parenting/Experimental/13 Build Windows Viewer (isolated)`도 같은 스크립트를 실행한다. M/버튼으로 모드 전환, Space로 빛 일시정지, R로 시점 초기화, 우클릭 드래그/좌우 화살표로 회전, 휠로 확대한다.

## 스펙의 참고 문헌

- [1] https://www.nature.com/articles/s41586-024-07558-y
- [2] https://codex.flywire.ai/faq?dataset=fafb
- [3] https://www.nature.com/articles/s41586-024-07763-9
- 분류 주석: https://www.nature.com/articles/s41586-024-07686-5

논문은 근거/출처로만 인용하며 도판이나 모델 코드를 복제하지 않는다. [3]의 계산 모델을 구현하거나 실제 행동을 재현했다고 주장하지 않는다.
