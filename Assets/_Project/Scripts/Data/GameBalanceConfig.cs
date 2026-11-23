using UnityEngine;

namespace FruitFlyParenting.Data
{
    // 스펙에 등장하는 밸런스 상수를 한 곳에 모아 둔다.
    // 매직 넘버를 코드에 박지 않고 여기서만 조정할 수 있게 하기 위함(tech-stack.md 4장).
    [CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "FruitFlyParenting/Game Balance Config")]
    public class GameBalanceConfig : ScriptableObject
    {
        [Header("스탯 공통 (스펙 4.1)")]
        public float statDefaultValue = 50f;
        public float statMin = 0f;
        public float statMax = 100f;

        [Header("반항 R 전용 — 다른 스탯과 달리 초기값 0 (스펙 4.5)")]
        public float rebellionInitialValue = 0f;

        [Header("신뢰 T (스펙 4.1, 4.3)")]
        public float trustDefaultValue = 50f;
        public float trustMin = 0f;
        public float trustMax = 100f;

        [Header("초파리 자율 이동 (스펙 4.2)")]
        [Tooltip("방 진입 후 이 시간(초) 동안은 선호 없이 100% 무작위로 목적지를 고른다(기본 120=2분)")]
        public float freeRoamDurationSeconds = 120f;
        [Tooltip("자유 로밍 구간이 끝난 뒤에도 유지되는 무작위 탐색 확률의 하한(기본 0.35). 실제 선호 가중 혼합은 08번에서 구현한다.")]
        public float randomExplorationFloor = 0.35f;
        [Tooltip("한 물품에 이 시간(초) 이상 머물면 강제로 다른 지점으로 이동한다")]
        public float maxItemDwellSeconds = 30f;

        [Header("개입 기본 스트레스 공식 (스펙 4.3)")]
        [Tooltip("1회 개입의 기본 스트레스 증가량")]
        public float baseInterventionStressDelta = 0.4f;
        [Tooltip("직전 60초 개입 횟수(n)에 곱하는 가중치 계수")]
        public float interventionWindowWeightCoefficient = 0.25f;
        [Tooltip("개입 1회당 스트레스 증가량 상한")]
        public float interventionStressDeltaCap = 3.0f;
        [Tooltip("개입 횟수를 세는 슬라이딩 윈도 길이(초)")]
        public float interventionWindowSeconds = 60f;

        [Header("개입 쿨다운 / 신뢰 변화 (스펙 4.3)")]
        [Tooltip("이보다 짧은 간격으로 반복 개입하면 신뢰 T가 감소한다")]
        public float rapidRepeatThresholdSeconds = 5f;
        public float rapidRepeatTrustPenalty = -1f;
        [Tooltip("개입 없이 이 시간(초)이 지날 때마다 신뢰 T를 보상한다")]
        public float neglectRewardIntervalSeconds = 60f;
        public float neglectRewardTrustGain = 1f;
        [Tooltip("방치 보상으로 얻는 신뢰 T의 방당 누적 최대치")]
        public float neglectRewardTrustCapPerRoom = 4f;

        // ----- 아래는 07번 프롬프트(4.5 강제/반복 훈련, 4.6 순응)에서 값을 채우는 자리다. -----
        // 지금은 필드 이름과 주석만 정의해 둔다. 기본값 0은 "아직 미설정"을 의미한다.

        [Header("4.5 강제 유도 판정 — 07번 프롬프트에서 값 채움")]
        [Tooltip("TODO(07): 휠체어 약한 밀기로 유도한 강제 훈련의 R 증가")]
        public float coercionWheelchairRebellionDelta;
        [Tooltip("TODO(07): 휠체어 약한 밀기로 유도한 강제 훈련의 S 증가")]
        public float coercionWheelchairStressDelta;
        [Tooltip("TODO(07): 전기 봉(사랑의 매)으로 유도한 강제 훈련의 R 증가")]
        public float coercionElectricStickRebellionDelta;
        [Tooltip("TODO(07): 전기 봉으로 유도한 강제 훈련의 S 증가")]
        public float coercionElectricStickStressDelta;
        [Tooltip("TODO(07): 물품 방문 전 강제 유도로 판정하는 윈도(초)")]
        public float coercionDetectionWindowSeconds;
        [Tooltip("TODO(07): '같은 방향으로 밀었다' 판정 각도 허용치(도)")]
        public float coercionDirectionToleranceDegrees;

        [Header("4.5 반복 훈련 — 07번 프롬프트에서 값 채움")]
        [Tooltip("TODO(07): 반복 훈련 추가 R(세 번째 방문부터)")]
        public float repeatedTrainingRebellionDelta;
        [Tooltip("TODO(07): 반복 훈련 추가 S(세 번째 방문부터)")]
        public float repeatedTrainingStressDelta;
        [Tooltip("TODO(07): 반복 훈련 추가 효과가 시작되는 방문 순번(기본 3번째)")]
        public int repeatedTrainingStartVisitCount;
        [Tooltip("TODO(07): 한 방에서 반복 훈련 추가 효과의 최대 적용 횟수(기본 3회)")]
        public int repeatedTrainingMaxApplications;

        [Header("4.5 회복 행동 — 07번 프롬프트에서 값 채움")]
        [Tooltip("TODO(07): 60초 무개입 지켜볼 때 R 감소량")]
        public float watchingOnlyRebellionDelta;
        [Tooltip("TODO(07): 방치 관찰로 인한 R 감소의 방당 누적 최대치")]
        public float watchingOnlyRebellionCapPerRoom;

        [Header("4.6 지속적인 밀기와 순응 — 07번 프롬프트에서 값 채움")]
        [Tooltip("TODO(07): 순응 판정 누적 윈도(초, 기본 30)")]
        public float complianceAccumulationWindowSeconds;
        [Tooltip("TODO(07): 휠체어 밀기 1회당 누적 점수(기본 1)")]
        public float complianceWheelchairPushScore;
        [Tooltip("TODO(07): 전기 봉 1회당 누적 점수(기본 2)")]
        public float complianceElectricStickPushScore;
        [Tooltip("TODO(07): R < complianceRebellionBoundary일 때 순응 임계점(기본 6)")]
        public float complianceThresholdLowRebellion;
        [Tooltip("TODO(07): R >= complianceRebellionBoundary일 때 순응 임계점(기본 8)")]
        public float complianceThresholdHighRebellion;
        [Tooltip("TODO(07): 임계점 분기에 쓰는 R 경계값(기본 70)")]
        public float complianceRebellionBoundary;
        [Tooltip("TODO(07): 추가 접촉 없이 누적이 리셋되는 유휴 시간(초, 기본 10)")]
        public float complianceResetIdleSeconds;
        [Tooltip("TODO(07): 순응 상태 지속 시간(초, 기본 8)")]
        public float complianceDurationSeconds;
        [Tooltip("TODO(07): 순응 임계점 도달 즉시 적용되는 S 증가")]
        public float complianceThresholdStressDelta;
        [Tooltip("TODO(07): 순응 임계점 도달 즉시 적용되는 R 증가")]
        public float complianceThresholdRebellionDelta;
        [Tooltip("TODO(07): 순응 상태 중 추가 유효 접촉마다 적용되는 S 증가")]
        public float compliancePerContactStressDelta;
        [Tooltip("TODO(07): 순응 상태 중 추가 유효 접촉마다 적용되는 R 증가")]
        public float compliancePerContactRebellionDelta;
    }
}
