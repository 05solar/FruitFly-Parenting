using FruitFlyParenting.Data;

namespace FruitFlyParenting.Fly
{
    // 순응 시스템(07)이 ForceEndAvoidance로 즉시 해제할 수 있는 순수 시간 기반 상태.
    public sealed class FlyMoodState
    {
        readonly GameBalanceConfig config;
        float avoidUntil = float.NegativeInfinity;
        float lastContact = float.NegativeInfinity;
        int consecutive;

        public FlyMoodState(GameBalanceConfig config) { this.config = config; }
        public bool IsAvoiding(float now) => now < avoidUntil;

        public void RecordContact(float now)
        {
            if (IsAvoiding(now)) return; // 회피 중 접촉으로 회피 시간을 무한 연장하지 않는다.
            if (now - lastContact >= config.consecutiveContactGapSeconds) consecutive = 0;
            lastContact = now;
            if (++consecutive >= config.avoidanceContactThreshold)
            {
                avoidUntil = now + config.avoidanceDurationSeconds;
                consecutive = 0;
                lastContact = float.NegativeInfinity;
            }
        }

        public void ForceEndAvoidance()
        {
            avoidUntil = float.NegativeInfinity;
            lastContact = float.NegativeInfinity;
            consecutive = 0;
        }
    }
}
