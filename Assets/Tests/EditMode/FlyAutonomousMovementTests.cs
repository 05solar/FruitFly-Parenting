using System;
using System.Collections.Generic;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class FlyAutonomousMovementTests
    {
        static GameBalanceConfig NewConfig()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            config.freeRoamDurationSeconds = 120f;
            config.randomExplorationFloor = 0.35f;
            config.maxItemDwellSeconds = 30f;
            return config;
        }

        [Test]
        public void IsFreeRoamPhase_TrueBeforeThreshold_FalseAtAndAfter()
        {
            var movement = new FlyAutonomousMovement(NewConfig(), new SystemRandomSource(new System.Random(1)));
            Assert.IsTrue(movement.IsFreeRoamPhase(0f));
            Assert.IsTrue(movement.IsFreeRoamPhase(119.9f));
            Assert.IsFalse(movement.IsFreeRoamPhase(120f));
            Assert.IsFalse(movement.IsFreeRoamPhase(121f));
        }

        [Test]
        public void ShouldForceRepick_TrueAtAndAfterMaxDwell_FalseBefore()
        {
            var movement = new FlyAutonomousMovement(NewConfig(), new SystemRandomSource(new System.Random(1)));
            Assert.IsFalse(movement.ShouldForceRepick(29.9f));
            Assert.IsTrue(movement.ShouldForceRepick(30f));
            Assert.IsTrue(movement.ShouldForceRepick(31f));
        }

        [Test]
        public void PickNextDestination_NeverReturnsCurrentId_WhenOtherCandidatesExist()
        {
            var movement = new FlyAutonomousMovement(NewConfig(), new SystemRandomSource(new System.Random(42)));
            var candidates = new List<string> { "a", "b", "c", "d" };

            for (int i = 0; i < 300; i++)
            {
                string result = movement.PickNextDestination(candidates, "a", timeSinceRoomEnter: 50f);
                Assert.AreNotEqual("a", result, "직전 목적지는 연속으로 다시 선택되면 안 된다");
            }
        }

        [Test]
        public void PickNextDestination_AllowsRepeat_WhenOnlyOneCandidateExists()
        {
            var movement = new FlyAutonomousMovement(NewConfig(), new SystemRandomSource(new System.Random(1)));
            var candidates = new List<string> { "only" };

            string result = movement.PickNextDestination(candidates, "only", timeSinceRoomEnter: 10f);
            Assert.AreEqual("only", result);
        }

        [Test]
        public void PickNextDestination_Throws_WhenCandidateListEmpty()
        {
            var movement = new FlyAutonomousMovement(NewConfig(), new SystemRandomSource(new System.Random(1)));
            Assert.Throws<ArgumentException>(() =>
                movement.PickNextDestination(new List<string>(), null, 0f));
        }

        [Test]
        public void PickNextDestination_DuringFreeRoamPhase_DistributesRoughlyUniformly()
        {
            var movement = new FlyAutonomousMovement(NewConfig(), new SystemRandomSource(new System.Random(7)));
            var candidates = new List<string> { "a", "b", "c", "d" };
            var counts = new Dictionary<string, int> { { "a", 0 }, { "b", 0 }, { "c", 0 }, { "d", 0 } };

            const int trials = 4000;
            for (int i = 0; i < trials; i++)
            {
                // 매 trial마다 currentId를 null로 둬서(연속 금지 제약 없이) 순수 분포를 확인한다.
                string result = movement.PickNextDestination(candidates, null, timeSinceRoomEnter: 10f);
                counts[result]++;
            }

            // 4개 후보 균등이면 기대값 1000회. 통계적 변동을 고려해 넉넉한 허용 범위를 둔다.
            foreach (var kvp in counts)
            {
                Assert.That(kvp.Value, Is.InRange(700, 1300),
                    $"'{kvp.Key}' 후보가 {kvp.Value}회 선택됨 — 균등 분포에서 벗어남");
            }
        }
    }
}
