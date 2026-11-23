using FruitFlyParenting.Data;

namespace FruitFlyParenting.Wheelchair
{
    public sealed class WheelchairBumperPushResolver
    {
        readonly GameBalanceConfig config;
        float lastPush = float.NegativeInfinity;
        public WheelchairBumperPushResolver(GameBalanceConfig config) { this.config = config; }
        public bool TryContact(bool bumperOverlapsFly, float actualSpeed, float currentTime)
        {
            if (!bumperOverlapsFly || actualSpeed < config.wheelchairMinSpeedForPush ||
                currentTime - lastPush < config.wheelchairPushCooldownSeconds) return false;
            lastPush = currentTime;
            return true;
        }
    }
}
