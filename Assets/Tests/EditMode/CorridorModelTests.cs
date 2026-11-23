using System.Collections.Generic;
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
    public class CorridorModelTests
    {
        [TestCase(LifeStage.Prologue,LifeStage.Infant,true)]
        [TestCase(LifeStage.Infant,LifeStage.Childhood,true)]
        [TestCase(LifeStage.Childhood,LifeStage.Adolescence,true)]
        [TestCase(LifeStage.Adolescence,LifeStage.YoungAdult,true)]
        [TestCase(LifeStage.YoungAdult,LifeStage.MiddleAge,true)]
        [TestCase(LifeStage.Infant,LifeStage.Infant,false)]
        [TestCase(LifeStage.Childhood,LifeStage.Infant,false)]
        [TestCase(LifeStage.Infant,LifeStage.YoungAdult,false)]
        [TestCase(LifeStage.MiddleAge,LifeStage.Prologue,false)]
        public void Routes_OnlyAllowAdjacentForwardLifeStages(LifeStage from, LifeStage to, bool allowed)
        { Assert.AreEqual(allowed,RoomTransitionSequence.Allows(from,to)); }

        [Test] public void SavedRoutesAndShell_HaveExpectedDimensionsAndUniqueNextStages()
        {
            var routes = AssetDatabase.FindAssets("t:RoomTransitionDefinition").Select(g=>AssetDatabase.LoadAssetAtPath<RoomTransitionDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            Assert.AreEqual(5,routes.Length); Assert.AreEqual(5,routes.Select(r=>r.fromStage).Distinct().Count());
            foreach (var route in routes)
            {
                Assert.IsTrue(RoomTransitionSequence.Allows(route.fromStage,route.toStage));
                Assert.IsNotEmpty(route.narration); Assert.IsNotNull(route.previousRoomSound); Assert.IsNotNull(route.nextRoomSound);
            }
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Rooms/CorridorShell.prefab").GetComponent<CorridorShell>();
            Assert.AreEqual(4,shell.Width); Assert.AreEqual(14,shell.Length);
            Assert.AreEqual(CorridorSection.Recap,shell.SectionAt(new Vector3(3.99f,0,0)));
            Assert.AreEqual(CorridorSection.Narration,shell.SectionAt(new Vector3(4,0,0)));
            Assert.AreEqual(CorridorSection.Silhouette,shell.SectionAt(new Vector3(10,0,0)));
        }

        [Test] public void FadeSequence_UsesMinimumHalfSecond_AndReadinessGate()
        {
            var model = new RoomTransitionSequence(.1f);
            Assert.IsFalse(model.Begin(LifeStage.Infant,LifeStage.MiddleAge));
            Assert.IsTrue(model.Begin(LifeStage.Infant,LifeStage.Childhood));
            Assert.IsFalse(model.Begin(LifeStage.Infant,LifeStage.Childhood));
            Assert.IsFalse(model.MovementAllowed); model.Advance(.3f);
            Assert.AreEqual(TransitionPhase.FadingToCorridor,model.Phase); Assert.AreEqual(.6f,model.FadeAlpha,.001f);
            model.Advance(.2f); Assert.AreEqual(TransitionPhase.RevealingCorridor,model.Phase); Assert.AreEqual(1,model.FadeAlpha);
            model.Advance(.5f); Assert.AreEqual(TransitionPhase.Corridor,model.Phase); Assert.IsTrue(model.MovementAllowed);
            model.Advance(100); Assert.AreEqual(TransitionPhase.Corridor,model.Phase,"Time alone must never leave the corridor.");
            Assert.IsFalse(model.BeginNextRoom(false)); Assert.IsTrue(model.BeginNextRoom(true));
            model.Advance(.5f); Assert.AreEqual(TransitionPhase.RevealingRoom,model.Phase);
            model.Advance(.5f); Assert.AreEqual(TransitionPhase.Complete,model.Phase);
            Assert.IsFalse(model.BeginNextRoom(true)); Assert.IsTrue(model.MovementAllowed);
        }

        static EventLogEntry Visit(string room, string id, float magnitude, string line = "기억") => new EventLogEntry
        {
            roomId = room,eventType = "item_first",payload = id,
            experience = new ItemExperience { itemId = id,displayName = id,narrative = "narrative-"+id,childLine = line,actualChangeMagnitude = magnitude }
        };
        [Test] public void Recap_SelectsActualLargestChanges_TiesUseVisitOrder_AndNeverAddsUnvisitedItems()
        {
            var log = new List<EventLogEntry> { Visit("infant","book",10),Visit("other","wrong",100),Visit("infant","hug",18),
                Visit("infant","tablet",30),Visit("infant","ball",18),Visit("infant","book",90),
                new EventLogEntry { roomId="infant",eventType="item_revisit",payload="unvisited",experience=new ItemExperience { itemId="unvisited",actualChangeMagnitude=100 } } };
            var recap = CorridorRecapModel.Build(log,"infant",3);
            CollectionAssert.AreEqual(new[]{"tablet","hug","ball"},recap.Memories.Select(m=>m.Experience.itemId));
            Assert.AreEqual(0,recap.InterventionCount);
            Assert.IsEmpty(CorridorRecapModel.Build(log,"missing",3).Memories);
        }
        [Test] public void Recap_OnlyUsesLatestRoomEntry_AndMarksActualPushHistory()
        {
            var log = new List<EventLogEntry> { Visit("infant","old",90), new EventLogEntry {roomId="infant",eventType="contact"},
                new EventLogEntry {roomId="infant",eventType="checkpoint",payload="room_enter"}, Visit("infant","new",5,"이번에 내가 골랐어요."),
                new EventLogEntry {roomId="infant",eventType="contact"},new EventLogEntry {roomId="other",eventType="contact"},
                new EventLogEntry {roomId="infant",eventType="item_pushed_away",payload="new"} };
            var recap = CorridorRecapModel.Build(log,"infant",3);
            Assert.AreEqual(1,recap.Memories.Length); Assert.IsTrue(recap.Memories[0].WasPushedAway);
            Assert.AreEqual("이번에 내가 골랐어요.",recap.Memories[0].Experience.childLine); Assert.AreEqual(1,recap.InterventionCount);
        }
        [Test] public void VisitSnapshot_SurvivesSaveRoundTripAndLaterAssetChanges_UsesClampedMagnitude()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                var state = new GameState(config,123); state.Stats.ApplyDelta(StatType.Love,49,"setup");
                item.itemId="one"; item.displayName="처음 이름"; item.tags=new[]{"관계"}; item.childReflectionLine="내가 골랐어요.";
                item.narrativeLines=new[]{"처음 경험"}; item.statDeltas=new[]{new ItemStatDelta(StatType.Love,12)};
                new ItemEffectApplier(config,state,"infant").Visit(item,true,true,3);
                item.displayName="changed"; item.tags[0]="changed"; item.childReflectionLine="changed";
                var restored = GameState.FromSaveData(config,JsonUtility.FromJson<GameStateSaveData>(JsonUtility.ToJson(state.ToSaveData())));
                var snapshot = CorridorRecapModel.Build(restored.EventLog,"infant",3).Memories[0].Experience;
                Assert.AreEqual("처음 이름",snapshot.displayName); Assert.AreEqual("관계",snapshot.tags[0]);
                Assert.AreEqual("내가 골랐어요.",snapshot.childLine); Assert.AreEqual(1,snapshot.actualChangeMagnitude);
            }
            finally { Object.DestroyImmediate(item); Object.DestroyImmediate(config); }
        }
        [Test] public void AudioMix_IsContinuousAcrossSections_AndOnlyIntroducesNextSoundAfterRecap()
        {
            var start = CorridorAudioMix.At(0,4,14); Assert.AreEqual(1,start.Previous); Assert.AreEqual(0,start.Next);
            var last = start;
            for (int i=1;i<=1400;i++)
            {
                var current = CorridorAudioMix.At(i*.01f,4,14);
                Assert.LessOrEqual(current.Previous,last.Previous+.00001f); Assert.GreaterOrEqual(current.Next,last.Next-.00001f);
                Assert.Less(Mathf.Abs(current.Previous-last.Previous),.003f); Assert.Less(Mathf.Abs(current.Next-last.Next),.003f);
                Assert.Greater(current.Previous+current.Next,.59f);
                if (i<=400) Assert.AreEqual(0,current.Next);
                last=current;
            }
            Assert.AreEqual(0,last.Previous); Assert.AreEqual(1,last.Next);
        }
    }
}
