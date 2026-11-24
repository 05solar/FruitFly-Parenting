using System;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FruitFlyParenting.Tests.EditMode
{
    public class EndingEvaluatorTests
    {
        GameBalanceConfig config;
        EndingDefinition[] definitions;
        [SetUp] public void SetUp()
        {
            config=ScriptableObject.CreateInstance<GameBalanceConfig>();
            definitions=Enumerable.Range(1,14).Select(i=>AssetDatabase.LoadAssetAtPath<EndingDefinition>("Assets/_Project/ScriptableObjects/Endings/ending_"+i.ToString("00")+".asset")).ToArray();
        }
        [TearDown] public void TearDown()=>Object.DestroyImmediate(config);
        GameState State(float s=50,float i=50,float h=60,float p=50,float l=50,float r=0,float t=50,YoungAdultFlags flags=YoungAdultFlags.None,
            bool delinquency=false,string items="",int interventions=0,bool autonomous=true)
        {
            var state=new GameState(config,12); state.CurrentRoomId=YoungAdultRoomController.RoomId;
            float[] values={s,i,h,p,l,r}; for(int n=0;n<6;n++) state.Stats.ApplyDelta((StatType)n,values[n]-state.Stats.GetValue((StatType)n),"fixture");
            state.Relationship.AdjustTrust(t-50,"fixture"); state.ApplyYoungAdultFlags(flags,"fixture",0);
            if(delinquency) state.RecordDelinquency("adolescence_theft",0);
            for(int n=0;n<interventions;n++) state.Relationship.RecordIntervention(0);
            foreach(string id in items.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries))
            {
                string folder=id.StartsWith("youngadult_") ? "YoungAdult" : id.StartsWith("childhood_") ? "Childhood" : id.StartsWith("adolescence_") ? "Adolescence" : "Infant";
                var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/"+folder+"/"+id+".asset"); Assert.IsNotNull(item,id);
                state.LogEvent(0,folder=="YoungAdult" ? YoungAdultRoomController.RoomId : folder,"item_first",id,new ItemExperience { itemId=id,tags=item.tags,autonomous=autonomous });
            }
            return state;
        }
        EndingDefinition Evaluate(GameState state) { state.CaptureFinalYoungAdultSnapshot(480); return EndingEvaluator.Evaluate(state.FinalYoungAdultSnapshot,definitions.Reverse()); }
        GameState Fixture(int priority)=>priority switch
        {
            1=>State(flags:YoungAdultFlags.GamblingBankruptcy),
            2=>State(l:100,t:70,items:"infant_hug,infant_ball,youngadult_dinner"),
            3=>State(r:75,p:60,t:45,delinquency:true,flags:YoungAdultFlags.Resigned),
            4=>State(i:75,l:60,t:60,items:"youngadult_interview,youngadult_letter"),
            5=>State(l:60,t:60,items:"childhood_walk,youngadult_park"),
            6=>State(s:70,t:59,flags:YoungAdultFlags.HighRiskContract),
            7=>State(t:24,flags:YoungAdultFlags.ContactBlocked),
            8=>State(i:75,t:39,items:"youngadult_interview"),
            9=>State(h:54),
            10=>State(h:75,t:45,items:"youngadult_stadium"),
            11=>State(l:60,t:60),
            12=>State(i:65,t:40,items:"infant_blocks,youngadult_studio"),
            13=>State(s:80,t:40),
            _=>State()
        };
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)]
        public void AllFourteenEndings_AreReachableWithIndependentFixtures(int expected)
        {
            var result=Evaluate(Fixture(expected)); Assert.AreEqual(expected,result.priority); Assert.AreEqual(EndingCatalog.Titles[expected-1],result.title);
            Assert.IsNotEmpty(result.calendarText); Assert.IsNotEmpty(result.sceneLine); Assert.IsNotEmpty(result.childLine);
        }
        [Test] public void SpecExamples_LoveAloneIsNotMagic_HighIntelligenceLowTrust_FamilyWithoutWealth()
        {
            Assert.AreEqual(14,Evaluate(State(l:100,t:50,items:"infant_hug,infant_ball,youngadult_dinner")).priority);
            Assert.AreEqual(8,Evaluate(State(i:80,t:30,items:"youngadult_interview,youngadult_letter")).priority);
            Assert.AreEqual(11,Evaluate(State(l:70,t:80)).priority);
        }
        [Test] public void BankruptcyWinsAgainstEveryOtherEligibleEnding_AndPriorityIsNotArrayOrder()
        {
            for(int n=2;n<=14;n++) { var state=Fixture(n); state.ApplyYoungAdultFlags(YoungAdultFlags.GamblingBankruptcy,"test",1); Assert.AreEqual(1,Evaluate(state).priority); }
            Assert.AreEqual(2,Evaluate(State(i:80,h:80,l:100,t:80,items:"infant_hug,infant_ball,youngadult_dinner,youngadult_interview,youngadult_letter,childhood_walk")).priority);
            Assert.AreEqual(4,Evaluate(State(i:80,l:80,t:80,items:"youngadult_interview,youngadult_letter,childhood_walk,youngadult_park")).priority);
        }
        [TestCase(74.99f,79.99f,79.99f,false)] [TestCase(75,50,50,true)] [TestCase(50,80,50,true)] [TestCase(50,50,80,true)]
        public void SuccessThresholdsRequireAYoungAdultAchievement(float intelligence,float popularity,float health,bool expected)
        {
            var a=State(i:intelligence,p:popularity,h:health,items:"youngadult_interview"); a.CaptureFinalYoungAdultSnapshot(0);
            Assert.AreEqual(expected,new EndingEvaluationContext(a.FinalYoungAdultSnapshot).Successful);
            var b=State(i:intelligence,p:popularity,h:health,items:"adolescence_exam"); b.CaptureFinalYoungAdultSnapshot(0); Assert.IsFalse(new EndingEvaluationContext(b.FinalYoungAdultSnapshot).Successful);
        }
        [TestCase(69.99f,50,true)] [TestCase(70,50,false)] [TestCase(50,49.99f,false)]
        public void StabilityBoundaries(float stress,float health,bool expected)
        { var s=State(s:stress,h:health); s.CaptureFinalYoungAdultSnapshot(0); Assert.AreEqual(expected,new EndingEvaluationContext(s.FinalYoungAdultSnapshot).Stable); }
        [TestCase(59.99f,60,false)] [TestCase(60,59.99f,false)] [TestCase(60,60,true)]
        public void RelationshipBoundaries(float trust,float love,bool expected)
        { var s=State(t:trust,l:love); s.CaptureFinalYoungAdultSnapshot(0); Assert.AreEqual(expected,new EndingEvaluationContext(s.FinalYoungAdultSnapshot).GoodRelationship); }
        [TestCase(39.99f,false)] [TestCase(40,true)] [TestCase(59,true)] [TestCase(59.01f,false)]
        public void OwnCityUsesInclusiveFortyToFiftyNine(float trust,bool expected)
        { var s=State(i:65,t:trust,items:"infant_blocks,youngadult_studio"); s.CaptureFinalYoungAdultSnapshot(0); Assert.AreEqual(expected,EndingEvaluator.Matches(EndingCondition.OwnCity,new EndingEvaluationContext(s.FinalYoungAdultSnapshot))); }
        [Test] public void HiddenEndingRequiresAutonomousRelationshipExperiences_AndDistinctFirstVisits()
        {
            var forced=State(l:100,t:80,items:"infant_hug,infant_ball,youngadult_dinner",autonomous:false); Assert.AreNotEqual(2,Evaluate(forced).priority);
            var repeated=State(l:100,t:80,items:"youngadult_dinner,youngadult_dinner,youngadult_dinner"); Assert.AreNotEqual(2,Evaluate(repeated).priority);
            Assert.AreEqual(1,repeated.FinalYoungAdultSnapshot.AutonomousRelationshipVisits);
        }
        [Test] public void LifetimeMealAndCreativeMappings_IgnoreRevisits_AndSnapshotsStayFrozen()
        {
            var s=State(items:"childhood_walk,childhood_family,youngadult_park,youngadult_dinner,infant_blocks,youngadult_studio");
            s.CaptureFinalYoungAdultSnapshot(0); var snap=s.FinalYoungAdultSnapshot;
            Assert.AreEqual(4,snap.RestMealVisits); Assert.AreEqual(2,snap.CreativeVisits);
            s.LogEvent(1,YoungAdultRoomController.RoomId,"item_revisit","youngadult_letter",new ItemExperience { itemId="youngadult_letter",tags=new[]{"창작"},autonomous=true });
            s.Relationship.AdjustTrust(-50,"test"); Assert.AreEqual(50,snap.Trust); Assert.AreEqual(2,snap.CreativeVisits);
            var restored=GameState.FromSaveData(config,JsonUtility.FromJson<GameStateSaveData>(JsonUtility.ToJson(s.ToSaveData())));
            Assert.AreEqual(4,restored.FinalYoungAdultSnapshot.RestMealVisits); Assert.AreEqual(2,restored.FinalYoungAdultSnapshot.CreativeVisits);
        }
        [Test] public void EmptyBirthdayThresholdAndGangActiveFlags_DoNotConfuseFlightWithDelinquency()
        {
            Assert.AreEqual(7,Evaluate(State(t:24.99f,interventions:25)).priority); Assert.AreEqual(14,Evaluate(State(t:25,interventions:25)).priority);
            Assert.AreEqual(14,Evaluate(State(t:24,interventions:24)).priority);
            var s=State(r:100,p:80,t:40,flags:YoungAdultFlags.ContactBlocked); s.CaptureAdolescenceExit(0); Assert.IsTrue(s.IsFlightSmokeEventTriggered); Assert.AreNotEqual(3,Evaluate(s).priority);
            Assert.AreEqual(3,Evaluate(State(r:75,p:60,t:45,delinquency:true,flags:YoungAdultFlags.ContactBlocked)).priority);
        }
        [Test] public void DecisiveExperiences_UseActualClampedRST_StableTies_NestedScopesOnlyOnce()
        {
            var s=State(s:95,r:95,t:95);
            using(s.TrackImpact(0,"a","first","첫 경험")) { s.Stats.ApplyDelta(StatType.Stress,20,"test"); s.Relationship.AdjustTrust(20,"test"); }
            using(s.TrackImpact(1,"b","second","둘째")) { using(s.TrackImpact(1,"b","inner","중복 금지")) s.Stats.ApplyDelta(StatType.Rebellion,-20,"test"); }
            using(s.TrackImpact(2,"c","third","셋째")) s.Relationship.AdjustTrust(-20,"test");
            using(s.TrackImpact(3,"d","fourth","넷째")) s.Stats.ApplyDelta(StatType.Stress,-1,"test");
            var top=EndingEvaluator.DecisiveExperiences(s.EventLog);
            CollectionAssert.AreEqual(new[]{"둘째","셋째","첫 경험"},top.Select(e=>e.label)); Assert.AreEqual(10,top[2].Magnitude);
            Assert.AreEqual(4,s.EventLog.Count(e=>e.eventType=="experience_impact")); top[0].stress=999;
            Assert.AreEqual(20,EndingEvaluator.DecisiveExperiences(s.EventLog)[0].Magnitude);
        }
        [Test] public void RealItemAndRecoveryEffects_RecordAutonomyAndSingleImpact()
        {
            var state=new GameState(config,1); var effects=new YoungAdultItemEffects(config,state);
            ItemDefinition Item(string id)=>AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/YoungAdult/youngadult_"+id+".asset");
            effects.Visit(Item("independence"),true,true,0); effects.Visit(Item("letter"),true,true,11); effects.Visit(Item("letter"),false,true,12);
            Assert.AreEqual(2,state.EventLog.Count(e=>e.eventType=="experience_impact"));
            var impact=state.EventLog.Last(e=>e.eventType=="experience_impact").impact; Assert.AreEqual(-4,impact.stress); Assert.AreEqual(10,impact.trust);
            Assert.IsTrue(state.EventLog.Last(e=>e.eventType=="item_first").experience.autonomous);
            Assert.AreEqual(42,state.Relationship.Trust);
        }
        [Test] public void MissingSnapshotAndIncompleteCatalogAreRejectedInsteadOfInventingDefaultEnding()
        {
            Assert.Throws<ArgumentException>(()=>EndingEvaluator.Evaluate(null,definitions));
            var state=State(); state.CaptureFinalYoungAdultSnapshot(0);
            Assert.Throws<ArgumentException>(()=>EndingEvaluator.Evaluate(state.FinalYoungAdultSnapshot,definitions.Take(13)));
        }
    }
}
