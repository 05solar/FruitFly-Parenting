using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class InfantRoomModelTests
    {
        GameBalanceConfig config;
        GameState state;
        ItemEffectApplier effects;
        [SetUp] public void SetUp()
        { config = ScriptableObject.CreateInstance<GameBalanceConfig>(); state = new GameState(config, 999); effects = new ItemEffectApplier(config, state, "infant"); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(config);

        [TestCase("book",3,8,0,0,2)]
        [TestCase("blocks",0,5,2,0,3)]
        [TestCase("learning",9,11,-2,0,-2)]
        [TestCase("hug",-7,0,3,0,8)]
        [TestCase("ball",-2,1,5,5,2)]
        [TestCase("snack",-3,0,-3,1,1)]
        [TestCase("crawl",2,2,8,0,2)]
        [TestCase("bed",-6,0,5,0,3)]
        [TestCase("tablet",8,-6,-7,-4,-5)]
        public void SavedDefinition_MatchesSpec52_AndRevisitNeverReapplies(string id, float s, float i, float h, float p, float l)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/Infant/infant_" + id + ".asset");
            Assert.IsNotNull(item);
            Assert.IsFalse(item.isExtreme);
            Assert.IsNotEmpty(item.tags);
            Assert.GreaterOrEqual(item.narrativeLines.Distinct().Count(), 2);
            var expected = new[] { s,i,h,p,l };
            var first = effects.Visit(item, true, true, 3f);
            Assert.IsTrue(first.EffectsApplied);
            for (int n=0;n<5;n++) Assert.AreEqual(50f+expected[n], state.Stats.GetValue((StatType)n));
            var revisit = effects.Visit(item, false, true, 10f);
            Assert.IsFalse(revisit.EffectsApplied);
            Assert.AreNotEqual(first.Narrative, revisit.Narrative);
            // 잘못 중복 발행된 FirstVisit도 효과를 다시 적용하지 않는다.
            Assert.IsFalse(effects.Visit(item, true, true, 15f).EffectsApplied);
            for (int n=0;n<5;n++) Assert.AreEqual(50f+expected[n], state.Stats.GetValue((StatType)n));
            Assert.AreEqual(1, state.Relationship.AutonomousVisitCount);
            Assert.AreEqual(2, state.Relationship.GetItemRevisitCount(item.itemId));
            Assert.AreEqual(1, state.EventLog.Count(e => e.eventType == "item_first"));
        }

        [Test] public void PerVisitClamp_AggregatesDuplicateStats_AndUsesStatClamp()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                item.itemId = "clamp";
                item.statDeltas = new[] { new ItemStatDelta(StatType.Stress, 20), new ItemStatDelta(StatType.Stress, 20),
                    new ItemStatDelta(StatType.Health, -40), new ItemStatDelta(StatType.Love, 50) };
                state.Stats.ApplyDelta(StatType.Love, 49, "setup");
                effects.Visit(item,true,true,0);
                Assert.AreEqual(62,state.Stats.GetValue(StatType.Stress));
                Assert.AreEqual(38,state.Stats.GetValue(StatType.Health));
                Assert.AreEqual(100,state.Stats.GetValue(StatType.Love));
            }
            finally { Object.DestroyImmediate(item); }
        }
        [Test] public void LateFirstVisit_StillNarrates_ButCannotGainEffectsLater()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/Infant/infant_book.asset");
            var result = effects.Visit(item,true,false,601);
            Assert.IsTrue(result.FirstVisit); Assert.IsFalse(result.EffectsApplied); Assert.IsNotEmpty(result.Narrative);
            Assert.IsFalse(effects.Visit(item,true,true,602).EffectsApplied);
            Assert.AreEqual(50,state.Stats.GetValue(StatType.Intelligence));
        }
        [Test] public void NewRoom_GetsItsOwnOnceOnlyScope()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/Infant/infant_book.asset");
            effects.Visit(item,true,true,0);
            var next = new ItemEffectApplier(config,state,"next");
            Assert.IsTrue(next.Visit(item,true,true,10).EffectsApplied);
            Assert.AreEqual(66,state.Stats.GetValue(StatType.Intelligence));
        }
        [Test] public void RecentIntervention_IsNotCountedAsAutonomousVisit()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/Infant/infant_book.asset");
            state.Relationship.RecordIntervention(1);
            effects.Visit(item,true,true,2);
            Assert.AreEqual(0,state.Relationship.AutonomousVisitCount);
        }
        [Test] public void Timeline_EnforcesNinetySeconds_EightMinuteDoor_TenMinuteEffectStop()
        {
            var clock = new RoomTimeline(config);
            clock.Advance(89.99f); Assert.IsFalse(clock.ToolsEnabled);
            clock.Advance(.02f); Assert.IsTrue(clock.ToolsEnabled);
            clock.Advance(329.98f); Assert.IsFalse(clock.DoorOpen); // 419.99
            clock.Advance(60f); Assert.IsFalse(clock.DoorOpen); // 479.99
            clock.Advance(.02f); Assert.IsTrue(clock.DoorOpen); Assert.IsTrue(clock.EffectsEnabled);
            clock.Advance(119.98f); Assert.IsTrue(clock.EffectsEnabled);
            clock.Advance(.02f); Assert.IsFalse(clock.EffectsEnabled); Assert.IsTrue(clock.ExitOnly);
        }
        [Test] public void Door_CannotOpenBeforeSevenMinutes_EvenIfConfiguredEarlier()
        {
            config.roomDoorOpenSeconds = 10;
            var clock = new RoomTimeline(config);
            clock.Advance(419.99f); Assert.IsFalse(clock.DoorOpen);
            clock.Advance(.02f); Assert.IsTrue(clock.DoorOpen);
        }
    }
}
