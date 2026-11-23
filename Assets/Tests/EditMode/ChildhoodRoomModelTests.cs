using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class ChildhoodRoomModelTests
    {
        GameBalanceConfig config;
        GameState state;
        [SetUp] public void SetUp() { config=ScriptableObject.CreateInstance<GameBalanceConfig>(); state=new GameState(config,9); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(config);
        [TestCase("contest",10,12,-2,2,0,0,0)]
        [TestCase("craft",1,7,1,2,3,0,0)]
        [TestCase("soccer",1,0,10,5,2,0,0)]
        [TestCase("library",-3,6,0,0,1,0,0)]
        [TestCase("birthday",-2,1,0,9,5,0,0)]
        [TestCase("arcade",-5,1,-2,3,1,0,0)]
        [TestCase("walk",-6,1,7,0,2,0,0)]
        [TestCase("family",-5,0,2,0,8,0,0)]
        [TestCase("gifted",20,23,-9,7,-7,-4,0)]
        [TestCase("star",17,2,-13,22,-8,-4,0)]
        [TestCase("hideout",-16,-11,4,8,-7,-5,15)]
        [TestCase("nightgame",-18,-9,-17,11,-5,-4,12)]
        public void SavedItems_ApplyExactSpec53_OnceIncludingTrustAndRebellion(string id,float s,float i,float h,float p,float l,float t,float r)
        {
            var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/Childhood/childhood_"+id+".asset");
            Assert.IsNotNull(item); Assert.AreEqual(t!=0,item.isExtreme); item.ValidateTags();
            var effects=new ItemEffectApplier(config,state,"03_Childhood");
            Assert.IsTrue(effects.Visit(item,true,true,0).EffectsApplied);
            Assert.IsFalse(effects.Visit(item,false,true,1).EffectsApplied);
            Assert.IsFalse(effects.Visit(item,true,true,2).EffectsApplied);
            float[] expected={s,i,h,p,l};
            for(int n=0;n<5;n++) Assert.AreEqual(50+expected[n],state.Stats.GetValue((StatType)n));
            Assert.AreEqual(50+t,state.Relationship.Trust); Assert.AreEqual(r,state.Stats.GetValue(StatType.Rebellion));
            Assert.IsFalse(state.IsFlightSmokeEventTriggered); Assert.IsFalse(state.AdolescenceExitCaptured);
        }
        [TestCase("craft",39,PaperAirplaneOutcome.ApologizeAndRepair)]
        [TestCase("birthday",39,PaperAirplaneOutcome.ApologizeAndRepair)]
        [TestCase("hideout",39.99f,PaperAirplaneOutcome.PostponeApology)]
        [TestCase("hideout",40,PaperAirplaneOutcome.TalkTomorrow)]
        [TestCase("none",20,PaperAirplaneOutcome.TalkTomorrow)]
        public void PaperEvent_SnapshotsAt210Seconds_AndNeverRejudges(string id,float trust,PaperAirplaneOutcome expected)
        {
            var paper=new PaperAirplaneEvent(config);
            var visited=id=="craft" || id=="birthday" ? new[]{"childhood_"+id,"childhood_hideout"} : new[]{"childhood_"+id};
            Assert.IsFalse(paper.TryTrigger(209.99f,visited,trust)); Assert.IsTrue(paper.TryTrigger(210,visited,trust));
            Assert.AreEqual(expected,paper.Outcome); Assert.IsFalse(paper.TryTrigger(400,new[]{"childhood_craft"},100)); Assert.AreEqual(expected,paper.Outcome);
            var before=state.ToSaveData(); state.RecordPaperAirplane(expected,210);
            state.RecordPaperAirplane(PaperAirplaneOutcome.ApologizeAndRepair,211);
            CollectionAssert.AreEqual(before.statValues,state.ToSaveData().statValues); Assert.AreEqual(before.trust,state.Relationship.Trust);
            Assert.AreEqual(expected,state.ChildhoodAirplaneOutcome); Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="paper_airplane"));
            Assert.AreNotEqual(PaperAirplaneEvent.CorridorLine(expected,false),PaperAirplaneEvent.CorridorLine(expected,true));
        }
        [TestCase(70,70,true)] [TestCase(69,81,true)] [TestCase(69,80,false)] [TestCase(100,49,false)]
        public void ExitStress_UsesBothSnapshotsAndAverage_PersistsAndCannotBeOverwritten(float infant,float childhood,bool eligible)
        {
            state.Stats.ApplyDelta(StatType.Stress,infant-50,"test"); state.CaptureRoomExitStress(LifeStage.Infant,10);
            Assert.IsFalse(state.IsPressureOutfitEligible);
            state.Stats.ApplyDelta(StatType.Stress,childhood-infant,"test"); state.CaptureRoomExitStress(LifeStage.Childhood,20);
            state.Stats.ApplyDelta(StatType.Stress,-100,"test"); state.CaptureRoomExitStress(LifeStage.Childhood,30);
            state.RecordPaperAirplane(PaperAirplaneOutcome.PostponeApology,210);
            var loaded=GameState.FromSaveData(config,JsonUtility.FromJson<GameStateSaveData>(JsonUtility.ToJson(state.ToSaveData())));
            Assert.AreEqual(infant,loaded.InfantExitStress); Assert.AreEqual(childhood,loaded.ChildhoodExitStress);
            Assert.AreEqual(eligible,loaded.IsPressureOutfitEligible); Assert.AreEqual(PaperAirplaneOutcome.PostponeApology,loaded.ChildhoodAirplaneOutcome);
            Assert.AreEqual(2,loaded.EventLog.Count(e=>e.eventType=="room_exit_stress"));
        }
        [Test] public void ExtremeHesitation_PrecedesDwell_AndLeavingResetsBoth()
        {
            var dwell=new ItemDwellTracker(2,2); dwell.OnEnter();
            Assert.IsFalse(dwell.Tick(1.99f)); Assert.IsTrue(dwell.IsHesitating);
            Assert.IsFalse(dwell.Tick(.02f)); Assert.IsFalse(dwell.IsHesitating);
            Assert.IsFalse(dwell.Tick(1.98f)); Assert.IsTrue(dwell.Tick(.02f)); Assert.IsFalse(dwell.Tick(10));
            dwell.OnExit(); dwell.OnEnter(); Assert.IsFalse(dwell.Tick(3.99f)); Assert.IsTrue(dwell.Tick(.02f));
            var normal=new ItemDwellTracker(2); normal.OnEnter(); Assert.IsTrue(normal.Tick(2));
        }
        [Test] public void TwelveFootprints_DoNotOverlap_AndBoundaryItemsStayAtX12()
        {
            var entries=ChildhoodCatalog.Entries; Assert.AreEqual(12,entries.Length); Assert.AreEqual(4,entries.Count(e=>e.Extreme));
            foreach(var e in entries) { Assert.That(e.Position.x,Is.InRange(1.25f,22.75f)); Assert.That(e.Position.y,Is.InRange(1.25f,20.75f)); }
            for(int a=0;a<entries.Length;a++) for(int b=a+1;b<entries.Length;b++)
            { var delta=entries[a].Position-entries[b].Position; Assert.IsTrue(Mathf.Abs(delta.x)>=2.5f || Mathf.Abs(delta.y)>=2.5f,entries[a].Id+" / "+entries[b].Id); }
            foreach(string id in new[]{"craft","birthday"}) Assert.AreEqual(12,entries.Single(e=>e.Id=="childhood_"+id).Position.x);
        }
    }
}
