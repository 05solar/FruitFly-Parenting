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
using FruitFlyParenting.Wheelchair;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FruitFlyParenting.Tests.PlayMode
{
    public class InfantRoomIntegrationTests
    {
        InfantRoomController flow;
        FlyAgent fly;
        NavMeshAgent agent;
        WheelchairLocomotion chair;
        InputDevice left;
        string checkpoint;
        float oldScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldScale = Time.timeScale;
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            Directory.CreateDirectory("Logs");
            checkpoint = Path.GetFullPath("Logs/infant-test-checkpoint-" + System.Guid.NewGuid().ToString("N") + ".json");
            InputSystem.RegisterLayout(@"{""name"":""Step05Controller"",""extend"":""XRController"",""controls"":[
                {""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]}]}");
            left = InputSystem.AddDevice("Step05Controller");
            InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
            SceneManager.sceneLoaded += SetCheckpointPath;
            yield return SceneManager.LoadSceneAsync("01_Infant");
            SceneManager.sceneLoaded -= SetCheckpointPath;
            flow = Object.FindAnyObjectByType<InfantRoomController>();
            fly = Object.FindAnyObjectByType<FlyAgent>();
            agent = fly.GetComponent<NavMeshAgent>();
            chair = Object.FindAnyObjectByType<WheelchairLocomotion>();
            yield return null;
            Assert.IsNotNull(flow.Timeline);
            Assert.IsTrue(File.Exists(checkpoint), "Room entry must save without touching the user's default save.");
        }
        void SetCheckpointPath(Scene scene, LoadSceneMode mode)
        {
            var controller = Object.FindAnyObjectByType<InfantRoomController>();
            if (controller != null) controller.CheckpointPath = checkpoint;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= SetCheckpointPath;
            if (left != null) InputSystem.RemoveDevice(left);
            if (flow != null) Object.Destroy(flow.gameObject);
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            Time.timeScale = oldScale;
            if (File.Exists(checkpoint)) File.Delete(checkpoint);
        }
        void Stick(Vector2 value) => InputSystem.QueueDeltaStateEvent((StickControl)left["thumbstick"], value);
        [UnityTest]
        public IEnumerator DownloadedFlyArt_CoversGrowthStages_PreservesPhysics_AndKeepsDialogueAboveWings()
        {
            HoldFly();
            var actors=Object.FindObjectsByType<FlyAgent>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Assert.That(actors.Length,Is.GreaterThanOrEqualTo(4));
            foreach(var actor in actors)
            {
                int colliders=actor.GetComponentsInChildren<Collider>(true).Length;
                float radius=actor.GetComponent<NavMeshAgent>().radius;
                var art=actor.GetComponent<FlyCharacterArt>() ?? actor.gameObject.AddComponent<FlyCharacterArt>();
                var oldBody=actor.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Fly Placeholder" || r.name=="Body" || r.name=="Pacifier").ToArray();
                art.Install(actor.Stage);
                Assert.That(art.Visual,Is.Not.Null,actor.Stage.ToString());
                Assert.That(art.VisualStage,Is.EqualTo(actor.Stage));
                Assert.That(art.Visual.GetComponentsInChildren<Collider>(true),Is.Empty);
                Assert.That(actor.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(colliders));
                Assert.That(actor.GetComponent<NavMeshAgent>().radius,Is.EqualTo(radius));
                foreach(var bodyRenderer in oldBody)if(!bodyRenderer.transform.IsChildOf(art.Visual))Assert.That(bodyRenderer.enabled,Is.False,"Legacy body overlaps fly model");
                var wings=art.Visual.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Wing Pivot")).ToArray();
                Assert.That(wings.Length,Is.EqualTo(2));
                int count=actor.GetComponentsInChildren<Renderer>(true).Length;art.Install(actor.Stage);
                Assert.That(actor.GetComponentsInChildren<Renderer>(true).Length,Is.EqualTo(count));
                foreach(var renderer in art.Visual.GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)Assert.That(material.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
            }
            var current=fly.GetComponent<FlyCharacterArt>();
            var wing=current.Visual.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("Wing Pivot"));
            var rotation=wing.localRotation;yield return new WaitForSeconds(.18f);
            Assert.That(Quaternion.Angle(rotation,wing.localRotation),Is.GreaterThan(.1f));
            var view=flow.GetComponent<InfantRoomView>();view.ShowDialogue("우와! 날개다!");yield return null;yield return null;
            var dialogue=fly.transform.Find("Child Dialogue");Assert.That(dialogue,Is.Not.Null);
            Assert.That(dialogue.position.y,Is.GreaterThan(current.TopY));
            var review=new GameObject("Fly review").AddComponent<Camera>();
            review.transform.position=fly.transform.position+new Vector3(.9f,.55f,1.35f);review.transform.LookAt(fly.transform.position+Vector3.up*.05f);
            review.fieldOfView=45;review.nearClipPlane=.03f;review.aspect=16f/9f;
            var target=new RenderTexture(1600,900,24);review.targetTexture=target;yield return null;dialogue.gameObject.SetActive(false);review.Render();review.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;var picture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            picture.ReadPixels(new Rect(0,0,1600,900),0,0);picture.Apply();File.WriteAllBytes("Logs/fly-character-in-game.png",picture.EncodeToPNG());
            RenderTexture.active=previous;review.targetTexture=null;dialogue.gameObject.SetActive(true);Object.Destroy(picture);Object.Destroy(target);Object.Destroy(review.gameObject);
        }
        void HoldFly()
        { fly.enabled = false; agent.isStopped = true; agent.ResetPath(); }
        IEnumerator LeaveItem()
        {
            Assert.IsTrue(agent.Warp(new Vector3(12f,.3f,7f)));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        }
        IEnumerator EnterItem(ItemSlot slot)
        {
            Assert.IsTrue(slot.TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point));
            Assert.IsTrue(agent.Warp(point + Vector3.up * agent.baseOffset));
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(2.2f);
        }

        [UnityTest]
        public IEnumerator NineSavedItems_ApplyExactEffectsOnce_AndCheckpointEveryFirstVisit()
        {
            Time.timeScale = 10f;
            HoldFly();
            Assert.AreEqual(9, flow.Room.ItemSlots.Count);
            var expectedIds = new[] { "book","blocks","learning","hug","ball","snack","bed","crawl","tablet" };
            for (int n=0;n<9;n++)
            {
                var slot = flow.Room.ItemSlots.Single(s => s.ItemId == "infant_" + expectedIds[n]);
                Vector2 at = RoomCoordinates.ItemSlots[n].pos;
                Assert.AreEqual(new Vector3(at.x,.3f,at.y),slot.transform.position);
                yield return LeaveItem();
                var before = Enumerable.Range(0,5).Select(i=>GameState.Current.Stats.GetValue((StatType)i)).ToArray();
                yield return EnterItem(slot);
                Assert.IsTrue(flow.Room.VisitedItemIds.Contains(slot.ItemId));
                foreach (var delta in slot.Definition.statDeltas)
                {
                    // 07: training visits #3/#4/#5 are learning, ball and bed in this fixed itinerary.
                    float repeated = delta.stat == StatType.Stress && (n == 2 || n == 4 || n == 6) ? 3f : 0f;
                    Assert.AreEqual(Mathf.Clamp(Mathf.Clamp(before[(int)delta.stat]+delta.amount,0,100)+repeated,0,100),GameState.Current.Stats.GetValue(delta.stat),.001f,slot.ItemId);
                }
                string narrative = flow.View.DialogueText;
                var after = Enumerable.Range(0,5).Select(i=>GameState.Current.Stats.GetValue((StatType)i)).ToArray();
                yield return LeaveItem(); yield return EnterItem(slot);
                Assert.AreNotEqual(narrative,flow.View.DialogueText);
                for (int i=0;i<5;i++) Assert.AreEqual(after[i],GameState.Current.Stats.GetValue((StatType)i));
                Assert.AreEqual(1,GameState.Current.Relationship.GetItemRevisitCount(slot.ItemId));
            }
            var logs = GameState.Current.EventLog.Where(e=>e.eventType=="item_first").ToArray();
            Assert.AreEqual(9,logs.Length);
            Assert.AreEqual(9,GameState.Current.EventLog.Count(e=>e.eventType=="checkpoint" && e.payload.StartsWith("item_first:")));
            var saved = JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint));
            CollectionAssert.AreEqual(Enumerable.Range(0,6).Select(i=>GameState.Current.Stats.GetValue((StatType)i)).ToArray(),saved.statValues);
            Assert.AreEqual(9,GameState.Current.EventLog.Count(e=>e.eventType=="learning_preference"));
            Assert.AreEqual(9,saved.eventLog.Count(e=>e.eventType=="learning_preference"));
            Assert.IsTrue(saved.tagMemory.Any(entry=>entry.preference!=0));
            File.WriteAllText("Logs/preference-infant-checkpoint.json",JsonUtility.ToJson(saved,true));
            File.WriteAllLines("Logs/infant-first-visits.txt",logs.Select(e=>$"{e.timestamp:0.00}s {e.payload}"));
        }

        [UnityTest]
        public IEnumerator RoomEntryImmediatelyAllowsWheelchairAndElectricContact()
        {
            HoldFly();
            // 정지 요청이 NavMesh/Transform에 반영된 다음 접촉 전 기준 위치를 기록한다.
            agent.velocity = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return null;
            var contact = fly.GetComponent<FlyContactReceiver>();
            Assert.IsTrue(contact.ContactsEnabled);
            Assert.Less(flow.Timeline.Elapsed,5f);
            contact.ReceiveContact(PushSource.Wheelchair,Vector3.left,Time.time);
            Assert.IsTrue(fly.IsBeingPushed);
            Assert.AreEqual(1,GameState.Current.Relationship.TotalInterventionCount);
            Assert.AreEqual(0,fly.GetComponent<FlyShockFeedback>().ShockCount);
            StringAssert.DoesNotContain("도구 사용까지",flow.View.StatusText);
            float stress = GameState.Current.Stats.GetValue(StatType.Stress);
            contact.ReceiveContact(PushSource.ElectricStick,Vector3.left,Time.time);
            Assert.IsTrue(fly.IsBeingPushed);
            Assert.AreEqual(2,GameState.Current.Relationship.TotalInterventionCount);
            Assert.AreEqual(stress+2.4f,GameState.Current.Stats.GetValue(StatType.Stress),.001f);
            Assert.AreEqual(1,fly.GetComponent<FlyShockFeedback>().ShockCount);
        }

        [UnityTest]
        public IEnumerator DoorAndEffectDeadlines_ChangeActualScene_AndExitBecomesOnlyGoal()
        {
            HoldFly();
            flow.Timeline.Advance(419f-flow.Timeline.Elapsed);
            yield return null;
            Assert.IsTrue(flow.DoorBlocking); Assert.IsFalse(flow.TryBeginExit(chair));
            flow.Timeline.Advance(480.1f-flow.Timeline.Elapsed);
            yield return null;
            Assert.IsFalse(flow.DoorBlocking);
            Assert.AreEqual(RoomController.ExitDestinationId,fly.CurrentDestinationId);
            Assert.AreEqual(3,flow.Room.GetDestinationIds().Count(id=>id==RoomController.ExitDestinationId));
            Assert.IsFalse(flow.TryBeginExit(chair),"Opening the door must not teleport a stationary player.");
            flow.Timeline.Advance(600.1f-flow.Timeline.Elapsed);
            yield return null;
            Assert.IsTrue(flow.Room.GetDestinationIds().All(id=>id==RoomController.ExitDestinationId));
            HoldFly();
            Time.timeScale = 10f;
            var before = Enumerable.Range(0,5).Select(i=>GameState.Current.Stats.GetValue((StatType)i)).ToArray();
            yield return EnterItem(flow.Room.ItemSlots.Single(s=>s.ItemId=="infant_book"));
            Assert.IsTrue(flow.Room.VisitedItemIds.Contains("infant_book"));
            for (int i=0;i<5;i++) Assert.AreEqual(before[i],GameState.Current.Stats.GetValue((StatType)i));
            StringAssert.Contains("effects=False",GameState.Current.EventLog.Single(e=>e.eventType=="item_first").payload);
        }

        [UnityTest]
        public IEnumerator ClosedDoor_BlocksWheelchairDrivingThroughBoundary()
        {
            Time.timeScale = 10f;
            fly.gameObject.SetActive(false);
            Stick(Vector2.up);
            yield return new WaitForSeconds(25f);
            Stick(Vector2.zero);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(flow.DoorBlocking);
            Assert.That(chair.transform.position.x,Is.InRange(23.3f,23.6f));
            Assert.AreEqual(InfantRoomPhase.InRoom,flow.Phase);
        }

        [UnityTest]
        public IEnumerator TabletRoutes_AreConnected_AndTraversableWithoutFurniturePenetration()
        {
            Time.timeScale = 10f;
            fly.enabled = false;
            var body = fly.GetComponent<CapsuleCollider>();
            var obstacles = flow.Room.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger).ToArray();
            foreach (string id in new[]{"infant_tablet","infant_learning","infant_tablet","infant_book"})
            {
                var slot = flow.Room.ItemSlots.Single(s=>s.ItemId==id);
                Assert.IsTrue(slot.TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point));
                agent.isStopped=false; Assert.IsTrue(agent.SetDestination(point));
                yield return null;
                float deadline=Time.time+25f;
                while (agent.pathPending || agent.remainingDistance>.1f)
                {
                    Assert.Less(Time.time,deadline,"Route stalled: "+id);
                    foreach (var obstacle in obstacles)
                        Assert.IsFalse(Physics.ComputePenetration(body,fly.transform.position,fly.transform.rotation,
                            obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth) && depth>.02f,obstacle.name);
                    yield return null;
                }
                Assert.AreEqual(NavMeshPathStatus.PathComplete,agent.pathStatus);
                yield return new WaitForSeconds(2.2f);
            }
        }

        [UnityTest]
        public IEnumerator FullPlaythrough_ObserveThenDrive_FadesInRealTime_AndSavesCorridor()
        {
            float began = Time.realtimeSinceStartup;
            Vector3 initial = chair.transform.position;
            Time.timeScale = 20f;
            yield return Capture("Logs/infant-room.png");
            float timeout = Time.realtimeSinceStartup+65f;
            while (flow.Timeline.Elapsed<90f)
            { Assert.Less(Time.realtimeSinceStartup,timeout); yield return null; }
            Assert.GreaterOrEqual(GameState.Current.Relationship.AutonomousVisitCount,2,"Protected observation must record two free choices.");
            Assert.GreaterOrEqual(flow.Room.VisitedItemIds.Count,2);
            while (flow.Timeline.Elapsed<481f)
            { Assert.Less(Time.realtimeSinceStartup,timeout); yield return null; }
            Assert.Less(Vector3.Distance(initial,chair.transform.position),.05f);
            Assert.AreEqual(0,GameState.Current.Relationship.TotalInterventionCount);
            Assert.AreEqual(InfantRoomPhase.InRoom,flow.Phase);
            Assert.IsFalse(flow.DoorBlocking);
            int observed = flow.Room.VisitedItemIds.Count;
            Stick(Vector2.up);
            timeout = Time.realtimeSinceStartup+8f;
            while (flow.Phase==InfantRoomPhase.InRoom)
            { Assert.Less(Time.realtimeSinceStartup,timeout,"Left-stick drive did not reach the exit."); yield return null; }
            Stick(Vector2.zero);
            Assert.AreEqual(InfantRoomPhase.Fading,flow.Phase);
            Vector3 duringFade = chair.transform.position;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(InfantRoomPhase.Fading,flow.Phase);
            Assert.That(flow.Transition.View.FadeAlpha,Is.GreaterThan(0f).And.LessThan(1f));
            while (flow.Transition.Phase!=TransitionPhase.Corridor)
            { Assert.Less(Time.realtimeSinceStartup,timeout); yield return null; }
            Assert.GreaterOrEqual(flow.LastFadeRealSeconds,.5f);
            Assert.AreEqual(0f,flow.Transition.View.FadeAlpha);
            Assert.AreEqual(InfantRoomPhase.Exited,flow.Phase);
            Assert.Less(Vector3.Distance(duringFade,chair.transform.position),.01f);
            Assert.AreEqual("corridor_01",GameState.Current.CurrentRoomId);
            var saved = JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint));
            Assert.AreEqual("corridor_01",saved.currentRoomId);
            Assert.AreEqual("corridor_start",saved.eventLog.Last().payload);
            yield return Capture("Logs/infant-corridor.png");
            string report = $"Game duration: {flow.Timeline.Elapsed:0.00}s\nReal duration (20x test): {Time.realtimeSinceStartup-began:0.00}s\n"+
                $"Observed distinct items: {observed}\nActual fade: {flow.LastFadeRealSeconds:0.000}s\n";
            File.WriteAllText("Logs/infant-playthrough.txt",report);
            Debug.Log(report);
        }

        static IEnumerator Capture(string path)
        {
            var camera = Camera.main;
            var target = new RenderTexture(1280,720,24);
            var pixels = new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                for(int i=0;i<5;i++) yield return null;
                RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                var colors=pixels.GetPixels();
                Assert.Greater(colors.Max(c=>c.grayscale)-colors.Min(c=>c.grayscale),.05f);
                Assert.Less(colors.Count(c=>c.r>.9f && c.b>.9f && c.g<.1f),colors.Length/100);
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=previous; RenderTexture.active=active; target.Release(); Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
