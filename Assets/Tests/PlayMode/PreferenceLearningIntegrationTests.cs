using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FruitFlyParenting.Tests.PlayMode
{
    public class PreferenceLearningIntegrationTests
    {
        FlyAgent fly;
        NavMeshAgent agent;
        FlyContactReceiver receiver;
        RoomController room;
        float previousScale;
        readonly List<ItemDefinition> items = new List<ItemDefinition>();
        Keyboard keyboard;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousScale=Time.timeScale; Time.timeScale=4;
            previousBackground=InputSystem.settings.backgroundBehavior;
            previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null; yield return SceneManager.LoadSceneAsync("03_RoomShellTest"); yield return null;
            fly=Object.FindAnyObjectByType<FlyAgent>(); agent=fly.GetComponent<NavMeshAgent>(); receiver=fly.GetComponent<FlyContactReceiver>();
            room=Object.FindAnyObjectByType<RoomController>();
            for(int i=0;i<room.ItemSlots.Count;i++)
            {
                var item=ScriptableObject.CreateInstance<ItemDefinition>(); items.Add(item);
                item.itemId=room.ItemSlots[i].ItemId;
                item.tags=new[]{i==1 ? TagDefinitions.Play : TagDefinitions.Study};
                room.ItemSlots[i].AssignDefinition(item);
            }
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
            Time.timeScale=previousScale;
            foreach(var item in items) Object.Destroy(item); items.Clear();
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
        }
        IEnumerator HoldAtNorthItem()
        {
            fly.enabled=false; agent.isStopped=true; agent.ResetPath();
            var slot=room.ItemSlots.Single(s=>s.ItemId=="slot_nc");
            Assert.IsTrue(slot.TryGetNavigationPoint(new Vector3(12,0,14),agent.agentTypeID,out var point));
            Assert.IsTrue(agent.Warp(point+Vector3.up*agent.baseOffset));
            yield return new WaitForFixedUpdate(); yield return new WaitForSeconds(2.2f);
        }
        [UnityTest] public IEnumerator RealPushDepartureLearnsTag_ContactWithoutDepartureDoesNot()
        {
            yield return HoldAtNorthItem();
            receiver.ReceiveContact(PushSource.ElectricStick,Vector3.back,Time.time); // Push into the furniture: no departure.
            fly.enabled=true; yield return new WaitForSeconds(.4f);
            Assert.AreEqual(0,GameState.Current.Preferences.GetAvoidance(TagDefinitions.Play));
            yield return HoldAtNorthItem();
            receiver.ReceiveContact(PushSource.ElectricStick,Vector3.forward,Time.time); // Push south, out of the interaction region.
            fly.enabled=true; yield return new WaitForSeconds(.4f);
            Assert.Greater(GameState.Current.Preferences.GetAvoidance(TagDefinitions.Play),0);
            var learning=GameState.Current.EventLog.Where(e=>e.eventType=="learning_avoidance").ToArray();
            Assert.AreEqual(1,learning.Length); Assert.AreEqual("pushed_away",learning[0].learning.outcome);
            Assert.AreEqual("slot_nc",learning[0].learning.originalItemId);
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(1,GameState.Current.EventLog.Count(e=>e.eventType=="learning_avoidance"));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/preference-runtime-example.json",JsonUtility.ToJson(GameState.Current.ToSaveData(),true));
        }
        [UnityTest] public IEnumerator AlternateActualDwellConfirmsApproachRedirect_ThenRoomResetClearsPendingOnly()
        {
            fly.enabled=false; agent.isStopped=true; agent.ResetPath();
            Assert.IsTrue(agent.Warp(new Vector3(12,.3f,11)));
            var intended=room.ItemSlots.Single(s=>s.ItemId==fly.CurrentDestinationId);
            receiver.ReceiveContact(PushSource.Wheelchair,Vector3.left,Time.time);
            var other=room.ItemSlots.First(s=>s.ItemId!=intended.ItemId);
            Assert.IsTrue(other.TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point));
            Assert.IsTrue(agent.Warp(point+Vector3.up*agent.baseOffset));
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(other.HasConfirmedCurrentStay);
            // A contact before the new item's dwell confirms must retain the original autonomous goal.
            receiver.ReceiveContact(PushSource.Wheelchair,Vector3.left,Time.time);
            yield return new WaitForSeconds(2.2f);
            var learned=GameState.Current.EventLog.Single(e=>e.eventType=="learning_avoidance");
            Assert.AreEqual("redirected_arrival",learned.learning.outcome); Assert.AreEqual(other.ItemId,learned.learning.outcomeItemId);
            Assert.AreEqual(intended.ItemId,learned.learning.originalItemId); Assert.AreEqual(2,learned.learning.interventionCount);
            float memory=GameState.Current.Preferences.GetAvoidance(intended.Definition.tags[0]); Assert.Greater(memory,0);
            EventBus.RaiseRoomEntered("next-room");
            Assert.AreEqual(memory,GameState.Current.Preferences.GetAvoidance(intended.Definition.tags[0]));
            Assert.IsFalse(fly.Learning.ConfirmArrival("next-room","anything",Time.time));
        }
        [UnityTest] public IEnumerator ActualChoiceFiltersUnreachableSlotsAndUsesCrossRoomTagMemory()
        {
            fly.enabled=false; agent.isStopped=true;
            var outside=new GameObject("Unreachable item",typeof(SphereCollider),typeof(ItemSlot));
            outside.transform.SetParent(room.transform); outside.transform.position=new Vector3(100,0,100);
            outside.GetComponent<ItemSlot>().ConfigureForBuilder("unreachable",1.2f,2.5f,2);
            room.RefreshItemSlots(); room.ConfigureLifeStage(LifeStage.Childhood);
            GameState.Current.Preferences.LearnAvoidance(new[]{TagDefinitions.Play},1000);
            for(int i=0;i<5;i++)
            {
                string previous=fly.CurrentDestinationId; fly.PickAndMoveToNext();
                Assert.AreNotEqual(previous,fly.CurrentDestinationId);
                Assert.IsFalse(fly.LastChoice.Candidates.Any(c=>c.Id=="unreachable"));
                Assert.IsFalse(fly.LastChoice.UniformPhase); Assert.AreEqual(.55f,fly.LastChoice.ExplorationFloor);
                var toy=fly.LastChoice.Candidates.FirstOrDefault(c=>c.Id=="slot_nc");
                if(toy!=null) { Assert.AreEqual(1000,toy.Avoidance); Assert.Greater(toy.Probability,0); }
            }
            Object.Destroy(outside);
            yield return null;
            Assert.AreEqual(NavMeshPathStatus.PathComplete,agent.pathStatus);
        }
        [UnityTest] public IEnumerator F8ShowsCandidateBreakdownWithoutChangingChoice_AndRendersOverlay()
        {
            fly.enabled=false; agent.isStopped=true;
            room.ConfigureLifeStage(LifeStage.Childhood); fly.PickAndMoveToNext(); agent.isStopped=true;
            var hud=fly.GetComponent<DebugHud>(); Assert.IsNotNull(hud); Assert.IsFalse(hud.Visible);
            var choice=fly.LastChoice;
            keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F8)); yield return null; yield return null;
            Assert.IsTrue(keyboard.f8Key.isPressed,"The virtual key must reach the background test player.");
            Assert.IsTrue(hud.Visible); StringAssert.Contains("RunSeed:",hud.DisplayText);
            StringAssert.Contains("base",hud.DisplayText); StringAssert.Contains("avoid",hud.DisplayText);
            Assert.AreSame(choice,fly.LastChoice);
            yield return Capture("Logs/preference-debug-hud.png");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F8)); yield return null; yield return null;
            Assert.IsFalse(hud.Visible);
        }
        static IEnumerator Capture(string filename)
        {
            yield return null;
            var camera=Camera.main; var original=camera.targetTexture; var active=RenderTexture.active;
            var target=new RenderTexture(1600,1000,24); var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes(filename,pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=original; RenderTexture.active=active; target.Release(); Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
