using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class RelationshipStateTests
    {
        static GameBalanceConfig NewConfig()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            config.trustDefaultValue = 50f;
            config.trustMin = 0f;
            config.trustMax = 100f;
            return config;
        }

        [Test]
        public void AdjustTrust_ClampsWithinRange()
        {
            var relationship = new RelationshipState(NewConfig());
            relationship.AdjustTrust(1000f, "test");
            Assert.AreEqual(100f, relationship.Trust);

            relationship.AdjustTrust(-1000f, "test");
            Assert.AreEqual(0f, relationship.Trust);
        }

        [Test]
        public void CountRecentInterventions_ExcludesTimestampsOutside60SecondWindow()
        {
            var relationship = new RelationshipState(NewConfig());
            relationship.RecordIntervention(currentTime: 0f);
            relationship.RecordIntervention(currentTime: 10f);

            // 20초 시점: 0초, 10초 개입 모두 60초 창 안이어야 함
            Assert.AreEqual(2, relationship.CountRecentInterventions(currentTime: 20f, windowSeconds: 60f));

            // 65초 시점: 0초 개입(65초 전)은 창 밖, 10초 개입(55초 전)은 창 안
            Assert.AreEqual(1, relationship.CountRecentInterventions(currentTime: 65f, windowSeconds: 60f));

            // 200초 시점: 둘 다 창 밖
            Assert.AreEqual(0, relationship.CountRecentInterventions(currentTime: 200f, windowSeconds: 60f));
        }

        [Test]
        public void RecordAndRetrieve_ItemRevisitCount()
        {
            var relationship = new RelationshipState(NewConfig());
            relationship.RecordItemRevisit("item_a");
            relationship.RecordItemRevisit("item_a");
            relationship.RecordItemRevisit("item_b");

            Assert.AreEqual(2, relationship.GetItemRevisitCount("item_a"));
            Assert.AreEqual(1, relationship.GetItemRevisitCount("item_b"));
            Assert.AreEqual(0, relationship.GetItemRevisitCount("item_never_visited"));
        }

        [Test]
        public void RecordAutonomousVisit_IncrementsCount()
        {
            var relationship = new RelationshipState(NewConfig());
            relationship.RecordAutonomousVisit();
            relationship.RecordAutonomousVisit();
            Assert.AreEqual(2, relationship.AutonomousVisitCount);
        }
    }
}
