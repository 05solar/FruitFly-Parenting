using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class AdolescenceRoomModelTests
    {
        GameBalanceConfig config;
        GameState state;
        [SetUp] public void SetUp() { config=ScriptableObject.CreateInstance<GameBalanceConfig>(); state=new GameState(config,10); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(config);
        static ItemDefinition Item(string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/Items/Adolescence/adolescence_"+id+".asset");
        [TestCase("exam",12,12,-3,0,-2,0,0,false)]
        [TestCase("club",2,6,0,6,4,0,0,false)]
        [TestCase("stadium",7,1,11,5,0,0,0,false)]
        [TestCase("counsel",-10,1,1,0,5,0,0,false)]
        [TestCase("stars",-6,2,0,8,6,0,0,false)]
        [TestCase("nightgame",-4,-2,-7,5,1,0,0,false)]
        [TestCase("job",5,3,4,3,1,0,0,false)]
        [TestCase("parents",-5,0,0,0,9,0,0,false)]
        [TestCase("camp",24,25,-15,-5,-9,-5,0,false)]
        [TestCase("training",20,0,23,8,-8,-4,0,false)]
        [TestCase("performance",13,3,-5,20,-10,-7,18,false)]
        [TestCase("dayoff",-22,-12,3,5,-12,-9,20,false)]
        [TestCase("theft",-10,-7,-2,15,-18,-8,20,true)]
        public void SavedItems_MatchSpec54_TheftAloneRecordsDelinquency(string id,float s,float i,float h,float p,float l,float t,float r,bool delinquency)
        {
            var item=Item(id); Assert.IsNotNull(item); item.ValidateTags(); Assert.AreEqual(t!=0,item.isExtreme);
            var effects=new ItemEffectApplier(config,state,AdolescenceRoomController.RoomId);
            Assert.IsTrue(effects.Visit(item,true,true,0).EffectsApplied);
            effects.Visit(item,false,true,1); effects.Visit(item,true,true,2);
            float[] expected={s,i,h,p,l}; for(int n=0;n<5;n++) Assert.AreEqual(50+expected[n],state.Stats.GetValue((StatType)n));
            Assert.AreEqual(50+t+(id=="parents" ? config.autonomousParentTrustGain : 0),state.Relationship.Trust); Assert.AreEqual(r,state.Stats.GetValue(StatType.Rebellion));
            Assert.AreEqual(delinquency,state.HasDelinquencyRecord); Assert.IsFalse(state.IsFlightSmokeEventTriggered);
            Assert.AreEqual(delinquency ? 1 : 0,state.EventLog.Count(e=>e.eventType=="delinquency_record"));
        }
        [TestCase(69,70,false)] [TestCase(70,69,false)] [TestCase(70,70,true)]
        [TestCase(69,79,false)] [TestCase(69,81,true)] [TestCase(100,50,true)]
        public void CostumeBoundaries(float infant,float child,bool expected)
            => Assert.AreEqual(expected,CostumeEvaluator.ShouldWearHiddenCostume(infant,child));
        [Test] public void CostumeOnlyDifference_LeavesAllStatsFlagsProbabilitiesAndSeededChoicesIdentical()
        {
            var data=state.ToSaveData(); data.adolescenceHiddenCostume=true; data.adolescenceCostumeCaptured=true;
            var dressed=GameState.FromSaveData(config,data);
            var candidates=AdolescenceCatalog.Entries.Select(e=>new ItemChoiceCandidate(e.Id,e.Tags)).ToArray();
            var a=new ItemChoiceModel(config,state,new SystemRandomSource(state.Random));
            var b=new ItemChoiceModel(config,dressed,new SystemRandomSource(dressed.Random));
            CollectionAssert.AreEqual(a.Evaluate(candidates,null,LifeStage.Adolescence,0).Candidates.Select(c=>c.Probability),
                b.Evaluate(candidates,null,LifeStage.Adolescence,0).Candidates.Select(c=>c.Probability));
            string previous=null;
            for(int n=0;n<1000;n++)
            {
                string choice=a.ChooseNextItem(candidates,previous,LifeStage.Adolescence,n).SelectedId;
                Assert.AreEqual(choice,b.ChooseNextItem(candidates,previous,LifeStage.Adolescence,n).SelectedId); previous=choice;
            }
            CollectionAssert.AreEqual(state.ToSaveData().statValues,dressed.ToSaveData().statValues);
            Assert.AreEqual(state.Relationship.Trust,dressed.Relationship.Trust); Assert.AreEqual(state.HasDelinquencyRecord,dressed.HasDelinquencyRecord);
            Assert.AreEqual(state.IsFlightSmokeEventTriggered,dressed.IsFlightSmokeEventTriggered);
            Assert.AreEqual(13,a.Evaluate(candidates,null,LifeStage.Adolescence,0).Candidates.Count);
        }
        [TestCase(59,false)] [TestCase(59.99f,false)] [TestCase(60,true)]
        public void ExitSnapshotAndPresentation_AreOnceOnlyAndPersist(float rebellion,bool triggered)
        {
            state.Stats.ApplyDelta(StatType.Rebellion,rebellion,"test"); state.CaptureAdolescenceExit(480);
            Assert.AreEqual(triggered,state.IsFlightSmokeEventTriggered);
            Assert.AreEqual(triggered,state.TryBeginAdolescenceExitEvent(480)); Assert.IsFalse(state.TryBeginAdolescenceExitEvent(481));
            state.Stats.ApplyDelta(StatType.Rebellion,100,"test"); state.CaptureAdolescenceExit(490);
            state.RecordDelinquency("adolescence_theft",491);
            var loaded=GameState.FromSaveData(config,JsonUtility.FromJson<GameStateSaveData>(JsonUtility.ToJson(state.ToSaveData())));
            Assert.AreEqual(rebellion,loaded.AdolescenceExitRebellion); Assert.AreEqual(triggered,loaded.IsFlightSmokeEventTriggered);
            Assert.AreEqual(triggered,loaded.AdolescenceExitEventPlayed); Assert.IsTrue(loaded.HasDelinquencyRecord);
            Assert.IsFalse(loaded.TryBeginAdolescenceExitEvent(500));
        }
        [Test] public void ExpiredTheftVisitAndRecoveryItems_DoNotCreateOrClearRecord()
        {
            var effects=new ItemEffectApplier(config,state,"teen"); effects.Visit(Item("theft"),true,false,601); Assert.IsFalse(state.HasDelinquencyRecord);
            effects=new ItemEffectApplier(config,state,"new"); effects.Visit(Item("theft"),true,true,0);
            effects.Visit(Item("counsel"),true,true,1); effects.Visit(Item("parents"),true,true,2); Assert.IsTrue(state.HasDelinquencyRecord);
        }
        [TestCase(39.99f,true)] [TestCase(40,false)]
        public void ConversationRefusal_OnlyFirstLowTrustAttempt(float trust,bool expected)
        {
            var story=new AdolescenceNarrative(config); Assert.AreEqual(expected,story.RefuseConversation(trust));
            if(expected) Assert.IsFalse(story.RefuseConversation(0));
        }
        [TestCase(34.99f,"시키는 대로 할게")] [TestCase(35,"내가 고를래")]
        public void ThirdAutonomousChoice_ThenTwoRelatedConfirmedBlocks_SelectsDialogue(float rebellion,string line)
        {
            var story=new AdolescenceNarrative(config);
            Assert.IsNull(story.Approach(Item("club"))); Assert.IsNull(story.Approach(Item("club")));
            Assert.IsNotNull(story.Approach(Item("club"))); Assert.IsNull(story.Approach(Item("club")));
            Assert.IsNull(story.Block(new[]{"소비"},rebellion)); Assert.AreEqual(0,story.BlockCount);
            Assert.IsNull(story.Block(new[]{"창작"},rebellion)); StringAssert.Contains(line,story.Block(new[]{"관계"},rebellion));
            Assert.IsNull(story.Block(new[]{"관계"},rebellion)); Assert.AreEqual(2,story.BlockCount);
        }
        [Test] public void WaitingCountsThirtySecondsInsideLastNinety_ResetsOnEveryToolUse()
        {
            var story=new AdolescenceNarrative(config); Assert.IsNull(story.Advance(389.99f));
            Assert.IsNotNull(story.Advance(390)); Assert.IsNull(story.Advance(419.99f));
            story.ToolUsed(419.99f); Assert.IsNull(story.Advance(449.98f)); Assert.IsNotNull(story.Advance(449.99f));
            Assert.IsTrue(story.Waited); Assert.IsNull(story.Advance(470));
            var late=new AdolescenceNarrative(config); late.ToolUsed(475); Assert.IsNull(late.Advance(505)); Assert.IsFalse(late.Waited);
        }
        [Test] public void ThirteenFootprintsAndWallClearances_DoNotOverlap()
        {
            var entries=AdolescenceCatalog.Entries; Assert.AreEqual(13,entries.Length); Assert.AreEqual(5,entries.Count(e=>e.Extreme));
            foreach(var entry in entries) { Assert.That(entry.Position.x,Is.InRange(2,22)); Assert.That(entry.Position.y,Is.InRange(2,20)); }
            for(int a=0;a<entries.Length;a++) for(int b=a+1;b<entries.Length;b++)
            { var d=entries[a].Position-entries[b].Position; Assert.IsTrue(Mathf.Abs(d.x)>=2.5f || Mathf.Abs(d.y)>=2.5f,entries[a].Id+" / "+entries[b].Id); }
        }
        [Test] public void WaitingCompletion_IsNotLostWhenFrameCrossesDoorDeadline()
        {
            var story=new AdolescenceNarrative(config); story.ToolUsed(450);
            story.Advance(479.99f); Assert.IsFalse(story.Waited);
            Assert.IsNotNull(story.Advance(480.02f)); Assert.IsTrue(story.Waited);
            var shortWait=new AdolescenceNarrative(config); shortWait.ToolUsed(450.01f);
            shortWait.Advance(480.02f); Assert.IsFalse(shortWait.Waited);
        }
    }
}
