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

        [Header("04 휠체어 이동 — 임시 편안함 설정")]
        public float wheelchairMaxSpeed = 0.8f;
        public float wheelchairAcceleration = 0.6f;
        public float wheelchairDeceleration = 1.2f;
        public float wheelchairTurnSpeed = 30f;
        public float wheelchairTurnAcceleration = 60f;
        public float wheelchairTurnDeceleration = 90f;
        public float wheelchairStickDeadzone = 0.15f;
        public float wheelchairMinSpeedForPush = 0.05f;
        public float wheelchairPushCooldownSeconds = 1f;

        [Header("04 밀기 — NavMesh 위 이동 거리(m), 힘의 게임 내 표현")]
        public float wheelchairPushForce = 1.5f;
        public float strongPushForce = 3f;
        public float pushDurationSeconds = 0.3f;
        public float electricStickImmediateStressDelta = 2f;
        public float shockVisualDurationSeconds = 0.25f;
        public int interventionUnweightedCount = 2;

        [Header("04 잠깐 혼자 해볼래")]
        public int avoidanceContactThreshold = 4;
        public float avoidanceDurationSeconds = 12f;
        [Tooltip("연속 접촉의 임시 정의: 직전 접촉과 5초 미만. 5초 이상 쉬면 새 연속 구간.")]
        public float consecutiveContactGapSeconds = 5f;

        [Header("05 영유아 방 시간과 1회 효과")]
        public float toolCooldownOnRoomEnterSeconds = 90f;
        public float roomEarliestExitSeconds = 420f;
        public float roomDoorOpenSeconds = 480f;
        public float roomEffectsStopSeconds = 600f;
        public int roomExitChoiceWeight = 3;
        public float roomFadeSeconds = 0.75f;
        public float normalItemStatDeltaLimit = 12f;
        public float autonomousVisitContactWindowSeconds = 6f;
        [Tooltip("개발용 게임 시간 배속. 페이드는 실제 시간을 사용한다. 출하 기본값 1.")]
        public float infantDebugTimeScale = 1f;

        [Header("06 복도 — 길이 4 + 6 + 4m, 폭 4m")]
        public float corridorWidth = 4f;
        public float corridorLength = 14f;
        public float corridorRecapLength = 4f;
        public float corridorSilhouetteLength = 4f;
        public int corridorRecapCount = 3;
        public int corridorUneasyContactThreshold = 4;
        public float corridorFrameTiltDegrees = 4f;
        public float corridorMusicVolume = 0.12f;
        public float corridorAudioBlendSeconds = 1f;

        [Header("07 Rebellion and coercion")]
        public float extremeItemStatDeltaLimit = 25f;
        public float trainingMinimumBenefit = 5f;
        public float coercionWheelchairRebellionDelta = 2f;
        public float coercionWheelchairStressDelta = 1f;
        public float coercionElectricStickRebellionDelta = 5f;
        public float coercionElectricStickStressDelta = 3f;
        public float coercionDetectionWindowSeconds = 10f;
        [Tooltip("Item direction tolerance; tune through playtesting.")]
        public float coercionDirectionToleranceDegrees = 45f;
        public float repeatedTrainingRebellionDelta = 4f;
        public float repeatedTrainingStressDelta = 3f;
        public int repeatedTrainingStartVisitCount = 3;
        public int repeatedTrainingMaxApplications = 3;
        public float watchingOnlyRebellionDelta = -2f;
        public float watchingOnlyRebellionCapPerRoom = 8f;
        public float highRebellionDialogueThreshold = 35f;
        public float flightSmokeRebellionThreshold = 60f;

        [Header("07 Directional compliance")]
        public float complianceAccumulationWindowSeconds = 30f;
        public float complianceWheelchairPushScore = 1f;
        public float complianceElectricStickPushScore = 2f;
        public float complianceThresholdLowRebellion = 6f;
        public float complianceThresholdHighRebellion = 8f;
        public float complianceRebellionBoundary = 70f;
        public float complianceResetIdleSeconds = 10f;
        public float complianceDurationSeconds = 8f;
        public float complianceThresholdStressDelta = 4f;
        public float complianceThresholdRebellionDelta = 6f;
        public float compliancePerContactStressDelta = 1f;
        public float compliancePerContactRebellionDelta = 2f;
        public float complianceMinimumContactIntervalSeconds = 1f;
        [Tooltip("90 degrees leaves the forward hemisphere and resets accumulation.")]
        public float complianceOppositeAngleDegrees = 90f;
        public float complianceSameDirectionToleranceDegrees = 45f;
        public float complianceNavigationDistance = 6f;
        public float complianceNavigationSampleRadius = 0.5f;
        public int complianceNavigationDistanceSteps = 6;
        public float complianceNavigationFallbackAngleDegrees = 60f;
        public int complianceNavigationAngleSteps = 6;
    }
}
