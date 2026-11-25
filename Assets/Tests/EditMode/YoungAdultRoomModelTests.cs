using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class YoungAdultRoomModelTests
    {
        GameBalanceConfig config;
        GameState state;
        YoungAdultItemEffects effects;
        [SetUp] public void SetUp()
        { config=ScriptableObject.CreateInstance<GameBalanceConfig>(); state=new GameState(config,11); effects=new YoungAdultItemEffects(config,state); }
        [TearDown] public void TearDown()=>Object.DestroyImmediate(config);
        static ItemDefinition Item(string id)=>AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/YoungAdult/youngadult_"+id+".asset");
        [TestCase("interview",11,3,-2,8,0,0,0,YoungAdultFlags.None)]
        [TestCase("studio",5,10,0,2,2,0,0,YoungAdultFlags.None)]
        [TestCase("stadium",9,0,10,6,0,0,0,YoungAdultFlags.None)]
        [TestCase("workshop",2,2,3,4,5,0,0,YoungAdultFlags.None)]
        [TestCase("dinner",-5,0,1,6,10,0,0,YoungAdultFlags.None)]
        [TestCase("investment",12,1,-3,3,-3,0,0,YoungAdultFlags.HighRiskContract)]
        [TestCase("park",-10,1,6,0,3,0,0,YoungAdultFlags.None)]
        [TestCase("letter",-4,1,0,0,9,0,0,YoungAdultFlags.None)]
        [TestCase("startup",25,14,-12,20,-15,-6,0,YoungAdultFlags.HighRiskContract)]
        [TestCase("selection",23,0,25,15,-10,-5,0,YoungAdultFlags.None)]
        [TestCase("independence",-20,2,3,7,-22,-18,20,YoungAdultFlags.ContactBlocked)]
        [TestCase("quit",-25,-5,9,6,-13,-8,14,YoungAdultFlags.Resigned)]
        [TestCase("gambling",25,-12,-18,-22,-20,-12,0,YoungAdultFlags.GamblingBankruptcy)]
        public void SavedItems_ApplySpec55ExactlyOnce(string id,float s,float i,float h,float p,float l,float t,float r,YoungAdultFlags flags)
        {
            var item=Item(id); Assert.IsNotNull(item); item.ValidateTags(); Assert.AreEqual(t!=0,item.isExtreme);
            Assert.AreEqual(flags,item.youngAdultFlags);
            Assert.IsTrue(effects.Visit(item,true,true,0).EffectsApplied);
            Assert.IsFalse(effects.Visit(item,false,true,1).EffectsApplied); Assert.IsFalse(effects.Visit(item,true,true,2).EffectsApplied);
            float[] expected={s,i,h,p,l}; for(int n=0;n<5;n++) Assert.AreEqual(50+expected[n],state.Stats.GetValue((StatType)n));
            Assert.AreEqual(50+t+(id=="letter" ? config.autonomousParentTrustGain : 0),state.Relationship.Trust); Assert.AreEqual(r,state.Stats.GetValue(StatType.Rebellion));
            Assert.AreEqual(flags,state.ToSaveData().youngAdultFlags); Assert.IsFalse(state.HasDelinquencyRecord);
        }
        [Test] public void BothContracts_SetOneStickyFlag_RecoveryDoesNotClearBankruptcyOrContract()
        {
            foreach(string id in new[]{"investment","startup","gambling","park","letter"}) effects.Visit(Item(id),true,true,0);
            Assert.IsTrue(state.HasHighRiskContract); Assert.IsTrue(state.HasGamblingBankruptcy);
            Assert.AreEqual(2,state.EventLog.Count(e=>e.eventType=="youngadult_flags_set"));
            StringAssert.Contains("잃은",state.LastYoungAdultLetter);
        }
        [Test] public void LetterVisitedBeforeRupture_CanReconnectOnAutonomousRevisit_OnlyOnce()
        {
            effects.Visit(Item("letter"),true,true,0); effects.Visit(Item("independence"),true,true,1);
            var before=state.ToSaveData();
            effects.Visit(Item("letter"),false,true,2,new ItemVisitContext(true,CoercionKind.None)); Assert.IsTrue(state.HasContactBlocked);
            state.Relationship.RecordIntervention(3);
            effects.Visit(Item("letter"),false,true,4); Assert.IsTrue(state.HasContactBlocked);
            effects.Visit(Item("letter"),false,true,14); Assert.IsFalse(state.HasContactBlocked); Assert.AreEqual(42+config.autonomousParentTrustGain,state.Relationship.Trust);
            effects.Visit(Item("letter"),false,true,15); effects.Visit(Item("independence"),false,true,16);
            Assert.IsFalse(state.HasContactBlocked); Assert.AreEqual(42+config.autonomousParentTrustGain,state.Relationship.Trust);
            CollectionAssert.AreEqual(before.statValues,state.ToSaveData().statValues);
            Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="youngadult_reconnect"));
        }
        [Test] public void WorkshopRevisit_ClearsActiveResignation_WithoutRepeatingStats()
        {
            effects.Visit(Item("workshop"),true,true,0); effects.Visit(Item("quit"),true,true,1);
            var before=state.ToSaveData(); var result=effects.Visit(Item("workshop"),false,true,2);
            Assert.IsFalse(state.HasResigned); StringAssert.Contains("새 일",result.Narrative);
            effects.Visit(Item("workshop"),false,true,3); effects.Visit(Item("quit"),false,true,4);
            Assert.IsFalse(state.HasResigned); CollectionAssert.AreEqual(before.statValues,state.ToSaveData().statValues);
            Assert.AreEqual(before.trust,state.Relationship.Trust); Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="youngadult_new_job"));
        }
        [Test] public void ExpiredEffects_CannotCreateOrRecoverFlags()
        {
            foreach(string id in new[]{"investment","startup","independence","quit","gambling"}) effects.Visit(Item(id),true,false,601);
            Assert.AreEqual(YoungAdultFlags.None,state.ToSaveData().youngAdultFlags);
            state.ApplyYoungAdultFlags(YoungAdultFlags.ContactBlocked|YoungAdultFlags.Resigned,"test",602);
            effects.Visit(Item("letter"),true,false,603); effects.Visit(Item("workshop"),true,false,604);
            Assert.IsTrue(state.HasContactBlocked); Assert.IsTrue(state.HasResigned); Assert.AreEqual(50,state.Relationship.Trust);
        }
        [TestCase(29.99f,"혼자 있고 싶어")] [TestCase(30,"시간 되면 한번 보자")] [TestCase(69.99f,"시간 되면 한번 보자")] [TestCase(70,"내일 내가")]
        public void LetterUsesThreeTrustBands(float trust,string text)=>StringAssert.Contains(text,YoungAdultItemEffects.LetterLine(trust,config));
        [Test] public void Resistance_ClearsScoreAndPausesForFifteenSeconds_LastContactExtendsIt()
        {
            var machine=new ComplianceStateMachine(config,new FlyMoodState(config));
            machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,0); Assert.AreEqual(2,machine.Score);
            machine.Suspend(1,15); Assert.AreEqual(0,machine.Score);
            Assert.AreEqual(ComplianceContactResult.Ignored,machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,15.99f));
            machine.Suspend(15,15); Assert.IsTrue(machine.IsSuspended(29.99f)); Assert.IsFalse(machine.IsSuspended(30));
            Assert.AreEqual(ComplianceContactResult.Accumulated,machine.RecordContact(PushSource.ElectricStick,Vector3.right,0,30)); Assert.AreEqual(2,machine.Score);
            machine.Suspend(31,15); machine.EnterRoom(); Assert.IsFalse(machine.IsSuspended(31)); Assert.AreEqual(0,machine.Score);
        }
        [Test] public void Snapshot_IsImmutableDeepCopiedAndRoundTripsAllEndingInputs()
        {
            state.RecordDelinquency("adolescence_theft",0); state.Stats.ApplyDelta(StatType.Rebellion,60,"test"); state.CaptureAdolescenceExit(1);
            foreach(string id in new[]{"interview","letter","investment","independence","quit","gambling"}) effects.Visit(Item(id),true,true,2);
            state.CaptureFinalYoungAdultSnapshot(480); var snapshot=state.FinalYoungAdultSnapshot; var before=state.ToSaveData();
            Assert.IsTrue(snapshot.DelinquencyRecord && snapshot.GamblingBankruptcy && snapshot.HighRiskContract && snapshot.ContactBlocked && snapshot.Resigned);
            Assert.IsTrue(snapshot.AdolescenceFlightSmoke); Assert.AreEqual(60,snapshot.AdolescenceExitRebellion);
            Assert.IsTrue(snapshot.AchievementVisited); Assert.AreEqual(6,snapshot.VisitedItemIds.Count);
            CollectionAssert.AreEqual(before.statValues,new[]{snapshot.Stress,snapshot.Intelligence,snapshot.Health,snapshot.Popularity,snapshot.Love,snapshot.Rebellion});
            Assert.AreEqual(before.trust,snapshot.Trust); Assert.AreEqual(state.LastYoungAdultLetter,snapshot.LastLetter);
            effects.Visit(Item("letter"),false,true,481); effects.Visit(Item("workshop"),true,true,482); state.CaptureFinalYoungAdultSnapshot(490);
            Assert.AreSame(snapshot,state.FinalYoungAdultSnapshot); Assert.IsTrue(snapshot.ContactBlocked && snapshot.Resigned);
            var loaded=GameState.FromSaveData(config,JsonUtility.FromJson<GameStateSaveData>(JsonUtility.ToJson(state.ToSaveData())));
            Assert.AreEqual(snapshot.ToLogString(),loaded.FinalYoungAdultSnapshot.ToLogString()); Assert.AreNotSame(snapshot,before.finalYoungAdultSnapshot);
            Assert.AreEqual(480,loaded.FinalYoungAdultSnapshot.CapturedAt); CollectionAssert.AreEqual(snapshot.VisitedItemIds,loaded.FinalYoungAdultSnapshot.VisitedItemIds);
            Assert.AreEqual(snapshot.ToLogString(),state.EventLog.Single(e=>e.eventType=="youngadult_final_snapshot").payload);
            Assert.IsFalse(loaded.HasContactBlocked); Assert.IsTrue(loaded.FinalYoungAdultSnapshot.ContactBlocked);
        }
        [Test] public void OlderSaveWithoutYoungAdultFields_LoadsWithoutInventingAnEnding()
        {
            var old=JsonUtility.FromJson<GameStateSaveData>("{\"trust\":50,\"statTypes\":[],\"statValues\":[],\"eventLog\":[]}"); var loaded=GameState.FromSaveData(config,old);
            Assert.IsNull(loaded.FinalYoungAdultSnapshot); Assert.AreEqual(YoungAdultFlags.None,loaded.ToSaveData().youngAdultFlags);
        }
        [Test] public void ThirteenFootprintsAreNonOverlappingAndNormalOffsetsStayWithinOneMetre()
        {
            var entries=YoungAdultCatalog.Entries; Assert.AreEqual(13,entries.Length); Assert.AreEqual(5,entries.Count(e=>e.Extreme));
            Vector2[] canonical={new Vector2(5,17),new Vector2(12,18),new Vector2(19,17),new Vector2(5,5),new Vector2(12,4),new Vector2(19,5),new Vector2(21,14),new Vector2(3,14)};
            for(int i=0;i<8;i++) Assert.LessOrEqual(Vector2.Distance(canonical[i],entries[i].Position),1);
            for(int a=0;a<entries.Length;a++) for(int b=a+1;b<entries.Length;b++)
            { var d=entries[a].Position-entries[b].Position; Assert.IsTrue(Mathf.Abs(d.x)>=2.5f || Mathf.Abs(d.y)>=2.5f,entries[a].Id+" / "+entries[b].Id); }
        }
    }
}
