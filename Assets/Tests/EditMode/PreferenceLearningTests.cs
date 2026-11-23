using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Tests.EditMode
{
    public class PreferenceLearningTests
    {
        GameBalanceConfig config;
        GameState state;
        readonly List<ItemDefinition> items = new List<ItemDefinition>();
        [SetUp] public void SetUp() { config=ScriptableObject.CreateInstance<GameBalanceConfig>(); state=new GameState(config,808); }
        [TearDown] public void TearDown() { foreach(var item in items) Object.DestroyImmediate(item); items.Clear(); Object.DestroyImmediate(config); }
        ItemDefinition Item(string id, params string[] tags)
        {
            var item=ScriptableObject.CreateInstance<ItemDefinition>(); items.Add(item); item.itemId=id; item.tags=tags; return item;
        }
        ItemChoiceModel Model(GameState value=null) => new ItemChoiceModel(config,value??state,new SystemRandomSource(new System.Random(808)));
        ItemChoiceCandidate[] Candidates() => new[]{new ItemChoiceCandidate("toy",new[]{TagDefinitions.Play}),
            new ItemChoiceCandidate("book",new[]{TagDefinitions.Study}),new ItemChoiceCandidate("bed",new[]{TagDefinitions.Rest})};

        [Test] public void TagsAreRestricted_ExistingAssetsValid_DuplicatesDoNotMultiplyLearning()
        {
            Assert.AreEqual(9,TagDefinitions.All.Count);
            Assert.Throws<ArgumentException>(()=>new ItemChoiceCandidate("invalid",new[]{"typo"}));
            foreach(var guid in AssetDatabase.FindAssets("t:ItemDefinition"))
                Assert.DoesNotThrow(()=>AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)).ValidateTags());
            state.Preferences.LearnPreference(new[]{TagDefinitions.Play,TagDefinitions.Play},1);
            Assert.AreEqual(1,state.Preferences.GetPreference(TagDefinitions.Play));
            Assert.AreEqual(0,state.Preferences.GetAvoidance(TagDefinitions.Play));
        }
        [TestCase(LifeStage.Infant,.65f)] [TestCase(LifeStage.Childhood,.55f)]
        [TestCase(LifeStage.Adolescence,.45f)] [TestCase(LifeStage.YoungAdult,.35f)]
        public void StageFloorsMatchConfig_AndAvoidance1000StillHasPositiveProbability(LifeStage stage,float floor)
        {
            state.Preferences.LearnAvoidance(new[]{TagDefinitions.Play},1000);
            var decision=Model().Evaluate(Candidates(),null,stage,120);
            Assert.AreEqual(floor,decision.ExplorationFloor);
            Assert.AreEqual(floor/3d,decision.Candidates[0].Probability,1e-8);
            Assert.Greater(decision.Candidates[0].Probability,0);
            Assert.AreEqual(1,decision.Candidates.Sum(c=>c.Probability),1e-12);
        }
        [Test] public void FloorsReadTunedValuesAndGlobalMinimum()
        {
            config.infantExplorationFloor=.81f; config.childhoodExplorationFloor=.72f;
            config.adolescenceExplorationFloor=.63f; config.youngAdultExplorationFloor=.54f;
            var model=Model();
            CollectionAssert.AreEqual(new[]{.81f,.72f,.63f,.54f},new[]{LifeStage.Infant,LifeStage.Childhood,LifeStage.Adolescence,LifeStage.YoungAdult}.Select(model.ExplorationFloor));
            config.youngAdultExplorationFloor=.1f; Assert.AreEqual(.35f,model.ExplorationFloor(LifeStage.YoungAdult));
        }
        [Test] public void EqualScoresAreUniform_StableSoftmaxHandlesLargeScores()
        {
            var decision=Model().Evaluate(Candidates(),null,LifeStage.YoungAdult,200);
            foreach(var candidate in decision.Candidates) { Assert.AreEqual(1d/3,candidate.Probability,1e-12); Assert.AreEqual(1d/3,candidate.SoftmaxProbability,1e-12); }
            decision=Model().Evaluate(new[]{new ItemChoiceCandidate("a",attraction:10000),new ItemChoiceCandidate("b",attraction:10001)},null,LifeStage.YoungAdult,200);
            Assert.AreEqual(.268941421,decision.Candidates[0].SoftmaxProbability,1e-8);
            Assert.IsFalse(double.IsNaN(decision.Candidates[1].Probability));
        }
        [Test] public void NeedUsesCurrentStressAndHealth_AndScoreBreakdownMatchesFormula()
        {
            state.Stats.ApplyDelta(StatType.Stress,50,"test"); state.Stats.ApplyDelta(StatType.Health,-50,"test");
            state.Preferences.LearnPreference(new[]{TagDefinitions.Rest},2); state.Preferences.LearnAvoidance(new[]{TagDefinitions.Rest},.5f);
            var options=new[]{new ItemChoiceCandidate("rest",new[]{TagDefinitions.Rest},1),new ItemChoiceCandidate("exercise",new[]{TagDefinitions.Exercise})};
            var decision=Model().Evaluate(options,null,LifeStage.YoungAdult,120);
            Assert.AreEqual(4,decision.Candidates[0].Score); Assert.AreEqual(1.5,decision.Candidates[0].Need);
            Assert.AreEqual(1,decision.Candidates[1].Need);
        }
        [Test] public void InfantFirstTwoMinutesIgnoreBias_WhileLaterStagesUseMemoryImmediately()
        {
            state.Preferences.LearnAvoidance(new[]{TagDefinitions.Play},1000);
            var model=Model(); var decision=model.Evaluate(Candidates(),null,LifeStage.Infant,119.99f,true);
            Assert.IsTrue(decision.UniformPhase); foreach(var candidate in decision.Candidates) Assert.AreEqual(1d/3,candidate.Probability,1e-12);
            Assert.IsFalse(model.Evaluate(Candidates(),null,LifeStage.Infant,120).UniformPhase);
            Assert.Less(model.Evaluate(Candidates(),null,LifeStage.Childhood,0).Candidates[0].Probability,1d/3);
        }
        [Test] public void EligibilityPrecedesSoftmax_OnlyOneCandidateCanRepeat_ExitKeepsThreeTickets()
        {
            var choices=new[]{new ItemChoiceCandidate("current"),new ItemChoiceCandidate("blocked",reachable:false),new ItemChoiceCandidate("next")};
            Assert.AreEqual("next",Model().ChooseNextItem(choices,"current",LifeStage.YoungAdult,200).SelectedId);
            Assert.AreEqual("current",Model().ChooseNextItem(choices.Take(2).ToArray(),"current",LifeStage.YoungAdult,200).SelectedId);
            Assert.Throws<ArgumentException>(()=>Model().Evaluate(new[]{choices[1]},null,LifeStage.Infant,0));
            var decision=Model().Evaluate(new[]{new ItemChoiceCandidate("item"),new ItemChoiceCandidate("exit",tickets:3)},null,LifeStage.Infant,0);
            Assert.AreEqual(.25,decision.Candidates[0].Probability); Assert.AreEqual(.75,decision.Candidates[1].Probability);
        }
        [TestCase(5.99f,true)] [TestCase(6f,true)] [TestCase(6.01f,false)]
        public void AvoidanceRequiresDifferentArrivalWithinSixSeconds(float elapsed,bool expected)
        {
            var tracker=new PreferenceLearningTracker(config,state); var item=Item("toy",TagDefinitions.Play);
            tracker.RecordContact("room",item,Vector3.forward,PushSource.ElectricStick,10,75);
            Assert.AreEqual(0,state.Preferences.GetAvoidance(TagDefinitions.Play));
            Assert.AreEqual(expected,tracker.ConfirmArrival("room","different",10+elapsed));
            Assert.AreEqual(expected ? .6f : 0f,state.Preferences.GetAvoidance(TagDefinitions.Play),.0001f);
            if(expected)
            {
                var e=state.EventLog.Single(); Assert.AreEqual("room",e.roomId); Assert.AreEqual(Vector3.forward,e.learning.approachDirection);
                Assert.AreEqual(1,e.learning.interventionCount); Assert.AreEqual(75,e.learning.stress); Assert.AreEqual("redirected_arrival",e.learning.outcome);
                Assert.IsFalse(tracker.ConfirmArrival("room","third",10+elapsed));
            }
        }
        [Test] public void NoContactSameArrivalExpiredContactAndRoomChangesCannotTeachAvoidance()
        {
            var tracker=new PreferenceLearningTracker(config,state); var item=Item("toy",TagDefinitions.Play);
            Assert.IsFalse(tracker.ConfirmArrival("room","other",0));
            tracker.RecordContact("room",item,Vector3.right,PushSource.Wheelchair,0,50);
            Assert.IsFalse(tracker.ConfirmArrival("room","toy",1));
            tracker.RecordContact("room",item,Vector3.right,PushSource.Wheelchair,2,50);
            Assert.IsFalse(tracker.ConfirmArrival("next-room","other",3));
            tracker.RecordContact("room",item,Vector3.right,PushSource.Wheelchair,4,50);
            tracker.Reset(); Assert.IsFalse(tracker.ConfirmPushedAway("room","toy",5));
            Assert.AreEqual(0,state.Preferences.GetAvoidance(TagDefinitions.Play)); Assert.IsEmpty(state.EventLog);
        }
        [Test] public void ActualPushDepartureTeachesOnce_WithRecentContactsAndNoExtraTrustCost()
        {
            var tracker=new PreferenceLearningTracker(config,state); var item=Item("toy",TagDefinitions.Play);
            tracker.RecordContact("room",item,Vector3.forward,PushSource.ElectricStick,0,52);
            tracker.RecordContact("room",item,Vector3.forward,PushSource.Wheelchair,7,53);
            tracker.RecordContact("room",item,Vector3.forward,PushSource.ElectricStick,8,56);
            Assert.IsFalse(tracker.ConfirmPushedAway("room","wrong",8));
            Assert.IsTrue(tracker.ConfirmPushedAway("room","toy",8));
            Assert.AreEqual(.9f,state.Preferences.GetAvoidance(TagDefinitions.Play),.0001f);
            Assert.AreEqual(2,state.EventLog.Single().learning.interventionCount); Assert.AreEqual(50,state.Relationship.Trust);
            Assert.IsFalse(tracker.ConfirmArrival("room","different",9));
        }
        [TestCase(-6f,4f,.4f)] [TestCase(8f,-4f,-.5f)]
        public void VisitLearningUsesActualBaseChanges_OnceOnly(float stress,float health,float expected)
        {
            var item=Item("experience",TagDefinitions.Rest);
            item.statDeltas=new[]{new ItemStatDelta(StatType.Stress,stress),new ItemStatDelta(StatType.Health,health)};
            var effects=new ItemEffectApplier(config,state,"room");
            effects.Visit(item,true,true,0); effects.Visit(item,false,true,1); effects.Visit(item,true,true,2);
            Assert.AreEqual(expected,state.Preferences.GetPreference(TagDefinitions.Rest),.0001f);
            Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="learning_preference"));
        }
        [Test] public void SaturatedStatsRebellionAndDisabledEffectsCannotGrantFictitiousEnjoyment()
        {
            var item=Item("cap",TagDefinitions.Study); item.statDeltas=new[]{new ItemStatDelta(StatType.Intelligence,10),new ItemStatDelta(StatType.Rebellion,10)};
            state.Stats.ApplyDelta(StatType.Intelligence,50,"test");
            new ItemEffectApplier(config,state,"room").Visit(item,true,true,0);
            var expired=Item("expired",TagDefinitions.Rest); expired.statDeltas=new[]{new ItemStatDelta(StatType.Stress,-10)};
            new ItemEffectApplier(config,state,"room").Visit(expired,true,false,1);
            Assert.AreEqual(0,state.Preferences.GetPreference(TagDefinitions.Study)); Assert.AreEqual(0,state.Preferences.GetPreference(TagDefinitions.Rest));
            Assert.IsFalse(state.EventLog.Any(e=>e.eventType=="learning_preference"));
        }
        [Test] public void MemoryAndLearningSnapshotsSurviveSaveAndGeneralizeToNewItemIds_OldSavesDefaultToZero()
        {
            var tracker=new PreferenceLearningTracker(config,state); var item=Item("infant-toy",TagDefinitions.Play);
            tracker.RecordContact("infant",item,Vector3.right,PushSource.ElectricStick,0,60); tracker.ConfirmPushedAway("infant",item.itemId,1);
            state.Preferences.LearnPreference(new[]{TagDefinitions.Play},.4f); state.CurrentRoomId="childhood";
            var restored=GameState.FromSaveData(config,JsonUtility.FromJson<GameStateSaveData>(JsonUtility.ToJson(state.ToSaveData())));
            Assert.AreEqual(.6f,restored.Preferences.GetAvoidance(TagDefinitions.Play)); Assert.AreEqual(.4f,restored.Preferences.GetPreference(TagDefinitions.Play));
            var candidate=new ItemChoiceCandidate("childhood-game",new[]{TagDefinitions.Play});
            Assert.AreEqual(.6,Model(restored).Evaluate(new[]{candidate},null,LifeStage.Childhood,0).Candidates[0].Avoidance,1e-6);
            Assert.AreEqual("infant-toy",restored.EventLog.Single().learning.originalItemId);
            var old=state.ToSaveData(); old.tagMemory=null;
            Assert.AreEqual(0,GameState.FromSaveData(config,old).Preferences.GetAvoidance(TagDefinitions.Play));
        }
        [Test] public void SameSeedComparisonShowsAvoidanceFrequencyChangeAndKeepsVariety()
        {
            var quiet=new GameState(config,808); var blocked=new GameState(config,808);
            var tracker=new PreferenceLearningTracker(config,blocked); var item=Item("infant-toy",TagDefinitions.Play);
            for(int i=0;i<12;i++) { tracker.RecordContact("infant",item,Vector3.forward,PushSource.ElectricStick,i*10,65); tracker.ConfirmPushedAway("infant",item.itemId,i*10+1); }
            var candidates=new[]{new ItemChoiceCandidate("childhood-toy",new[]{TagDefinitions.Play}),new ItemChoiceCandidate("new-book",new[]{TagDefinitions.Study}),new ItemChoiceCandidate("new-bed",new[]{TagDefinitions.Rest}),new ItemChoiceCandidate("new-sport",new[]{TagDefinitions.Exercise})};
            int[] Run(GameState s)
            {
                var model=new ItemChoiceModel(config,s,new SystemRandomSource(new System.Random(s.RunSeed)));
                var counts=new int[candidates.Length]; string previous=null;
                for(int n=0;n<20000;n++)
                {
                    string next=model.ChooseNextItem(candidates,previous,LifeStage.Childhood,0).SelectedId;
                    Assert.AreNotEqual(previous,next); counts[Array.FindIndex(candidates,c=>c.Id==next)]++; previous=next;
                }
                return counts;
            }
            var a=Run(quiet); var b=Run(blocked); CollectionAssert.AreEqual(a,Run(new GameState(config,808)));
            Assert.Greater(a[0]-b[0],800); Assert.Greater(b[0],1000);
            foreach(int count in a) Assert.Greater(count,3000);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/preference-comparison.txt",$"RunSeed=808; choices=20000; childhood floor=0.55; no immediate repeats\nquiet: {string.Join(",",a)}\nblocked: {string.Join(",",b)}\navoidance={blocked.Preferences.GetAvoidance(TagDefinitions.Play)}\n");
            File.WriteAllText("Logs/preference-learning-example.json",JsonUtility.ToJson(blocked.ToSaveData(),true));
        }
        [Test] public void InspectingDebugReasonsDoesNotConsumeRandomness()
        {
            var first=Model(); var second=Model();
            for(int i=0;i<50;i++)
            {
                var expected=first.ChooseNextItem(Candidates(),null,LifeStage.YoungAdult,120);
                var actual=second.ChooseNextItem(Candidates(),null,LifeStage.YoungAdult,120);
                for(int n=0;n<10;n++) StringAssert.Contains("RunSeed: 808",DebugHud.Describe(actual.SelectedId,actual,808));
                Assert.AreEqual(expected.SelectedId,actual.SelectedId);
            }
        }
    }
}
