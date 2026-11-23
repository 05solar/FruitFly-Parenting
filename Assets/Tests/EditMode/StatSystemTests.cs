using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class StatSystemTests
    {
        static GameBalanceConfig NewConfig()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            config.statDefaultValue = 50f;
            config.statMin = 0f;
            config.statMax = 100f;
            config.rebellionInitialValue = 0f;
            return config;
        }

        [Test]
        public void ApplyDelta_ClampsAboveMax()
        {
            var stats = new StatSystem(NewConfig());
            stats.ApplyDelta(StatType.Intelligence, 1000f, "test");
            Assert.AreEqual(100f, stats.GetValue(StatType.Intelligence));
        }

        [Test]
        public void ApplyDelta_ClampsBelowMin()
        {
            var stats = new StatSystem(NewConfig());
            stats.ApplyDelta(StatType.Health, -1000f, "test");
            Assert.AreEqual(0f, stats.GetValue(StatType.Health));
        }

        [Test]
        public void ApplyDelta_RaisesOnStatChanged_WithCorrectOldAndNewValues()
        {
            var stats = new StatSystem(NewConfig());
            StatType seenType = default;
            float seenOld = -1f, seenNew = -1f;
            string seenReason = null;

            stats.OnStatChanged += (type, oldValue, newValue, reason) =>
            {
                seenType = type;
                seenOld = oldValue;
                seenNew = newValue;
                seenReason = reason;
            };

            stats.ApplyDelta(StatType.Love, 10f, "test_reason");

            Assert.AreEqual(StatType.Love, seenType);
            Assert.AreEqual(50f, seenOld);
            Assert.AreEqual(60f, seenNew);
            Assert.AreEqual("test_reason", seenReason);
        }

        [Test]
        public void Rebellion_StartsAtZero_NotAtDefaultFifty()
        {
            var stats = new StatSystem(NewConfig());
            Assert.AreEqual(0f, stats.GetValue(StatType.Rebellion));
        }

        [Test]
        public void OtherStats_StartAtDefaultValue()
        {
            var stats = new StatSystem(NewConfig());
            Assert.AreEqual(50f, stats.GetValue(StatType.Intelligence));
            Assert.AreEqual(50f, stats.GetValue(StatType.Stress));
        }
    }
}
