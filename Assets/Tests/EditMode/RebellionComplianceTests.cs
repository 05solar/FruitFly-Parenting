using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class RebellionComplianceTests
    {
        GameBalanceConfig config;
        GameState state;
        readonly List<ItemDefinition> items = new List<ItemDefinition>();
        [SetUp] public void SetUp() { config = ScriptableObject.CreateInstance<GameBalanceConfig>(); state = new GameState(config, 7); }
        [TearDown] public void TearDown()
        { foreach (var item in items) Object.DestroyImmediate(item); items.Clear(); Object.DestroyImmediate(config); }
        ItemDefinition Item(string id, float benefit = 5f)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>(); items.Add(item);
            item.itemId = id; item.statDeltas = new[] { new ItemStatDelta(StatType.Intelligence, benefit) };
            return item;
        }
        [TestCase(CoercionKind.Wheelchair, 2f, 1f)]
        [TestCase(CoercionKind.ElectricStick, 5f, 3f)]
        public void ForcedTraining_OncePerUniqueFirstVisit(CoercionKind kind, float r, float s)
        {
            var effects = new ItemEffectApplier(config, state, "room"); var item = Item("training");
            var context = new ItemVisitContext(false, kind);
            effects.Visit(item, true, true, 1f, context);
            effects.Visit(item, false, true, 2f, context);
            effects.Visit(item, true, true, 3f, context);
            Assert.AreEqual(r, state.Stats.GetValue(StatType.Rebellion));
            Assert.AreEqual(50f+s, state.Stats.GetValue(StatType.Stress));
            Assert.AreEqual(1, state.EventLog.Count(e => e.eventType == "coerced_training"));
            Assert.AreEqual(0, state.Relationship.AutonomousVisitCount);
        }
        [Test] public void Coercion_UsesContactOriginResolvedDirectionAndInclusiveWindow_ElectricWins()
        {
            var detector = new CoercionDetector(config);
            detector.Record(0, PushSource.Wheelchair, new Vector3(4,0,0), Vector3.right);
            Assert.AreEqual(CoercionKind.Wheelchair, detector.Evaluate(10, new Vector3(7,0,0)));
            Assert.AreEqual(CoercionKind.None, detector.Evaluate(10, Vector3.zero));
            detector.Record(9, PushSource.ElectricStick, Vector3.zero, Vector3.left);
            Assert.AreEqual(CoercionKind.Wheelchair, detector.Evaluate(10, new Vector3(7,0,0)));
            detector.Record(9, PushSource.ElectricStick, Vector3.zero, Vector3.right);
            Assert.AreEqual(CoercionKind.ElectricStick, detector.Evaluate(10, new Vector3(7,0,0)));
            Assert.AreEqual(CoercionKind.None, detector.Evaluate(19.01f, new Vector3(7,0,0)));
            detector.Reset();
            detector.Record(20, PushSource.ElectricStick, Vector3.zero, Vector3.right);
            Assert.AreEqual(CoercionKind.None, detector.Evaluate(19, Vector3.right));
        }
        [TestCase(44f, true)] [TestCase(46f, false)]
        public void CoercionAngleBoundary(float angle, bool expected)
        {
            var detector = new CoercionDetector(config);
            detector.Record(0, PushSource.Wheelchair, Vector3.zero, Vector3.forward);
            Assert.AreEqual(expected, detector.Evaluate(1, Quaternion.Euler(0,angle,0)*Vector3.forward) != CoercionKind.None);
        }
        [Test] public void ThirdFourthFifthTrainingVisitsStackWithCoercion_ThenCap_AndResetPerRoom()
        {
            var effects = new ItemEffectApplier(config, state, "a");
            var expectedR = new[] { 0f,0f,9f,13f,17f,17f };
            var expectedS = new[] { 50f,50f,56f,59f,62f,62f };
            for (int i=0;i<6;i++)
            {
                var item = Item("item"+i);
                effects.Visit(item,true,true,i,new ItemVisitContext(false,i==2 ? CoercionKind.ElectricStick : CoercionKind.None));
                effects.Visit(item,false,true,i);
                Assert.AreEqual(expectedR[i],state.Stats.GetValue(StatType.Rebellion));
                Assert.AreEqual(expectedS[i],state.Stats.GetValue(StatType.Stress));
            }
            Assert.AreEqual(3,state.EventLog.Count(e=>e.eventType=="repeated_training"));
            new ItemEffectApplier(config,state,"b").Visit(Item("new-room"),true,true,10);
            Assert.AreEqual(17,state.Stats.GetValue(StatType.Rebellion));
        }
        [Test] public void NonTrainingAndExpiredEffects_DoNotConsumeTrainingAllowance()
        {
            var effects = new ItemEffectApplier(config,state,"a");
            for(int i=0;i<4;i++) effects.Visit(Item("small"+i,4.99f),true,true,i,new ItemVisitContext(false,CoercionKind.ElectricStick));
            for(int i=0;i<4;i++) effects.Visit(Item("expired"+i),true,false,i,new ItemVisitContext(false,CoercionKind.ElectricStick));
            effects.Visit(Item("first"),true,true,10);
            Assert.AreEqual(0,state.Stats.GetValue(StatType.Rebellion)); Assert.AreEqual(50,state.Stats.GetValue(StatType.Stress));
        }
        [TestCase(false,12f)] [TestCase(true,25f)]
        public void ExtremeAndNormalLimitsIncludeRebellion_AndRevisitsDoNothing(bool extreme,float limit)
        {
            var item=Item("extreme"); item.isExtreme=extreme;
            item.statDeltas=new[]{new ItemStatDelta(StatType.Rebellion,20),new ItemStatDelta(StatType.Rebellion,20),new ItemStatDelta(StatType.Health,-40)};
            var effects=new ItemEffectApplier(config,state,"a");
            effects.Visit(item,true,true,0); effects.Visit(item,false,true,1);
            Assert.AreEqual(limit,state.Stats.GetValue(StatType.Rebellion)); Assert.AreEqual(50-limit,state.Stats.GetValue(StatType.Health));
        }
        [Test] public void WatchingOnly_SharesTimerButNotTrustCap_PreservesRBetweenRooms()
        {
            state.Stats.ApplyDelta(StatType.Rebellion,50,"test"); config.neglectRewardTrustCapPerRoom=0;
            var usage=new ToolUsageTracker(config,state,0); usage.EnterRoom("a",0);
            usage.Tick(59.99f); Assert.AreEqual(50,state.Stats.GetValue(StatType.Rebellion));
            usage.Tick(60); Assert.AreEqual(48,state.Stats.GetValue(StatType.Rebellion));
            usage.RecordContact(PushSource.Wheelchair,119); usage.Tick(178); Assert.AreEqual(48,state.Stats.GetValue(StatType.Rebellion));
            usage.Tick(179); Assert.AreEqual(46,state.Stats.GetValue(StatType.Rebellion));
            usage.Tick(1000); Assert.AreEqual(42,state.Stats.GetValue(StatType.Rebellion));
            usage.EnterRoom("b",1000); Assert.AreEqual(42,state.Stats.GetValue(StatType.Rebellion));
            usage.Tick(1060); Assert.AreEqual(40,state.Stats.GetValue(StatType.Rebellion));
        }
        [TestCase(0f,PushSource.Wheelchair,6)] [TestCase(69.99f,PushSource.ElectricStick,3)]
        [TestCase(70f,PushSource.Wheelchair,8)] [TestCase(100f,PushSource.Wheelchair,8)] [TestCase(100f,PushSource.ElectricStick,4)]
        public void ThresholdsIncludingR100_OverrideAvoidance(float r,PushSource source,int count)
        {
            var mood=new FlyMoodState(config); var machine=new ComplianceStateMachine(config,mood);
            for(int i=0;i<count;i++)
            {
                mood.RecordContact(i);
                var result=machine.RecordContact(source,Vector3.right,r,i);
                Assert.AreEqual(i==count-1?ComplianceContactResult.Entered:ComplianceContactResult.Accumulated,result);
            }
            Assert.AreEqual(ComplianceState.Complying,machine.State); Assert.IsFalse(mood.IsAvoiding(count-1));
        }
        [Test] public void RapidContactsStillShock_ButDoNotCountTowardCompliance()
        {
            var usage=new ToolUsageTracker(config,state,0); var machine=new ComplianceStateMachine(config,usage.Mood);
            for(int i=0;i<4;i++) { usage.RecordContact(PushSource.ElectricStick,i*.1f); machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,i*.1f); }
            Assert.AreEqual(2,machine.Score); Assert.AreEqual(4,state.Relationship.TotalInterventionCount);
            Assert.AreEqual(60.35f,state.Stats.GetValue(StatType.Stress),.001f);
            Assert.AreEqual(ComplianceState.AvoidingTool,machine.State);
            Assert.AreEqual(ComplianceContactResult.Accumulated,machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,1));
            Assert.AreEqual(ComplianceContactResult.Entered,machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,2));
        }
        [Test] public void ReverseAndIdleReset_AndRollingWindowExpiresOldScore()
        {
            var machine=new ComplianceStateMachine(config,new FlyMoodState(config));
            machine.RecordContact(PushSource.ElectricStick,Vector3.right,100,0);
            machine.RecordContact(PushSource.Wheelchair,Vector3.left,100,1); Assert.AreEqual(1,machine.Score);
            machine.Tick(10.99f); Assert.AreEqual(1,machine.Score);
            machine.Tick(11); Assert.AreEqual(0,machine.Score);
            config.complianceThresholdHighRebellion=100;
            foreach(float now in new[]{20f,29f,38f,47f,51f}) machine.RecordContact(PushSource.Wheelchair,Vector3.right,100,now);
            Assert.AreEqual(4,machine.Score);
        }
        [Test] public void ComplianceRefreshExpiresAtEightSeconds_VisitAndReverseRelease()
        {
            var machine=new ComplianceStateMachine(config,new FlyMoodState(config));
            for(int i=0;i<3;i++) machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,i);
            Assert.AreEqual(10,machine.ComplyUntil);
            Assert.AreEqual(ComplianceContactResult.Continued,machine.RecordContact(PushSource.Wheelchair,Vector3.right,0,9));
            Assert.AreEqual(17,machine.ComplyUntil); machine.Tick(16.99f); Assert.AreEqual(ComplianceState.Complying,machine.State);
            machine.Tick(17); Assert.AreEqual(ComplianceState.Free,machine.State); Assert.AreEqual(0,machine.Score);
            for(int i=20;i<23;i++) machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,i);
            machine.RecordContact(PushSource.Wheelchair,Vector3.left,0,23); Assert.AreEqual(ComplianceState.Free,machine.State); Assert.AreEqual(1,machine.Score);
            machine.CompleteVisit(); Assert.AreEqual(0,machine.Score);
        }
        [TestCase(34.99f,false)] [TestCase(35f,true)]
        public void DialogueFlagIsLive(float r,bool expected)
        {
            state.Stats.ApplyDelta(StatType.Rebellion,r,"test"); Assert.AreEqual(expected,state.IsHighRebellionDialogue);
            state.Stats.ApplyDelta(StatType.Rebellion,-100,"test"); Assert.IsFalse(state.IsHighRebellionDialogue);
        }
        [Test] public void CompletingVisitCannotBypassOneSecondGate_NewRoomResetsIt()
        {
            var machine=new ComplianceStateMachine(config,new FlyMoodState(config));
            for(int i=0;i<3;i++) machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,i);
            machine.CompleteVisit();
            Assert.AreEqual(ComplianceContactResult.Ignored,machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,2.1f));
            Assert.AreEqual(0,machine.Score);
            Assert.AreEqual(ComplianceContactResult.Accumulated,machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,3f));
            machine.EnterRoom();
            Assert.AreEqual(ComplianceContactResult.Accumulated,machine.RecordContact(PushSource.Wheelchair,Vector3.left,0,3.1f));
            Assert.AreEqual(1,machine.Score);
        }
        [Test] public void ReversingDuringAvoidanceResetsScore_ButOnlyThresholdEndsAvoidance()
        {
            var mood=new FlyMoodState(config); var machine=new ComplianceStateMachine(config,mood);
            for(int i=0;i<4;i++)
            {
                mood.RecordContact(i);
                machine.RecordContact(PushSource.Wheelchair,Vector3.right,0,i);
            }
            Assert.AreEqual(ComplianceState.AvoidingTool,machine.State);
            machine.RecordContact(PushSource.Wheelchair,Vector3.left,0,4);
            Assert.AreEqual(1,machine.Score);
            Assert.AreEqual(ComplianceState.AvoidingTool,machine.State); Assert.IsTrue(mood.IsAvoiding(4));
            for(int i=5;i<10;i++) machine.RecordContact(PushSource.Wheelchair,Vector3.left,0,i);
            Assert.AreEqual(ComplianceState.Complying,machine.State); Assert.IsFalse(mood.IsAvoiding(9));
        }
        [TestCase(59f,false)] [TestCase(60f,true)]
        public void FlightSmokeDecisionIsCapturedOnceAtDoorOpenAndSaved(float r,bool expected)
        {
            state.Stats.ApplyDelta(StatType.Rebellion,100,"test"); Assert.IsFalse(state.IsFlightSmokeEventTriggered);
            state.Stats.ApplyDelta(StatType.Rebellion,r-100,"test"); state.CaptureAdolescenceExit(10);
            state.Stats.ApplyDelta(StatType.Rebellion,expected?-100:100,"test"); state.CaptureAdolescenceExit(20);
            var restored=GameState.FromSaveData(config,JsonUtility.FromJson<GameStateSaveData>(JsonUtility.ToJson(state.ToSaveData())));
            Assert.AreEqual(expected,restored.IsFlightSmokeEventTriggered); Assert.AreEqual(r,restored.AdolescenceExitRebellion);
            Assert.IsTrue(restored.AdolescenceExitCaptured);
        }
    }
}
