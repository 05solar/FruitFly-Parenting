using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Wheelchair
{
    public sealed class WheelchairMotionModel
    {
        readonly GameBalanceConfig config;
        public float Speed { get; private set; }
        public float TurnSpeed { get; private set; }
        public WheelchairMotionModel(GameBalanceConfig config) { this.config = config; }

        public void Step(Vector2 stick, float deltaTime, float turnLimit = -1f)
        {
            float forward = Deadzone(stick.y) * config.wheelchairMaxSpeed;
            float turn = Deadzone(stick.x) * (turnLimit < 0 ? config.wheelchairTurnSpeed : turnLimit);
            // Neutral means stop NOW; smoothing must never produce movement without input.
            Speed = forward == 0 ? 0 : Smooth(Speed, forward, config.wheelchairAcceleration, config.wheelchairDeceleration, deltaTime);
            TurnSpeed = turn == 0 ? 0 : Smooth(TurnSpeed, turn, config.wheelchairTurnAcceleration, config.wheelchairTurnDeceleration, deltaTime);
        }
        float Deadzone(float value) => Mathf.Abs(value) < config.wheelchairStickDeadzone ? 0f : Mathf.Clamp(value, -1f, 1f);
        static float Smooth(float current, float target, float acceleration, float deceleration, float dt)
        {
            float rate = Mathf.Abs(target) < Mathf.Abs(current) || current * target < 0f ? deceleration : acceleration;
            return Mathf.MoveTowards(current, target, Mathf.Max(0f, rate * dt));
        }
    }
}
