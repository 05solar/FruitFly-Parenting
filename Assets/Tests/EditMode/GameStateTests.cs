using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class GameStateTests
    {
        static GameBalanceConfig NewConfig()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            config.statDefaultValue = 50f;
            config.statMin = 0f;
            config.statMax = 100f;
            config.trustDefaultValue = 50f;
            config.trustMin = 0f;
            config.trustMax = 100f;
            return config;
        }

        [Test]
        public void SameSeed_ProducesSameRandomSequence()
        {
            var stateA = new GameState(NewConfig(), 12345);
            var stateB = new GameState(NewConfig(), 12345);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(stateA.Random.Next(), stateB.Random.Next());
            }
        }

        [Test]
        public void DifferentSeed_ProducesDifferentSequenceAtSomePoint()
        {
            var stateA = new GameState(NewConfig(), 1);
            var stateB = new GameState(NewConfig(), 2);

            bool anyDifferent = false;
            for (int i = 0; i < 20; i++)
            {
                if (stateA.Random.Next() != stateB.Random.Next())
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifferent, "다른 시드는 사실상 항상 다른 시퀀스를 내야 한다");
        }
    }
}
