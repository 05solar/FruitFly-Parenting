using System.Collections;
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
    public class CorridorIntegrationTests
    {
        InfantRoomController infant;
        RoomTransitionController flow;
        WheelchairLocomotion chair;
        FlyAgent fly;
        NavMeshAgent agent;
        InputDevice left;
        string checkpoint;
        float oldScale;
        [UnitySetUp] public IEnumerator SetUp()
        {
            oldScale=Time.timeScale;
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            Directory.CreateDirectory("Logs");
            checkpoint=Path.GetFullPath("Logs/corridor-test-"+System.Guid.NewGuid().ToString("N")+".json");
            InputSystem.RegisterLayout(@"{""name"":""Step06Controller"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]}]}");
            left=InputSystem.AddDevice("Step06Controller"); InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);
            SceneManager.sceneLoaded+=InjectSave;
            yield return SceneManager.LoadSceneAsync("01_Infant");
            SceneManager.sceneLoaded-=InjectSave;
            infant=Object.FindAnyObjectByType<InfantRoomController>(); flow=infant.Transition;
            chair=Object.FindAnyObjectByType<WheelchairLocomotion>(); fly=Object.FindAnyObjectByType<FlyAgent>(); agent=fly.GetComponent<NavMeshAgent>();
            yield return null; Time.timeScale=10;
        }
        void InjectSave(Scene scene,LoadSceneMode mode) => Object.FindAnyObjectByType<InfantRoomController>().CheckpointPath=checkpoint;
        [UnityTearDown] public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded-=InjectSave;
            if(left!=null) InputSystem.RemoveDevice(left);
            if(flow!=null) Object.Destroy(flow.gameObject);
            if(infant!=null) Object.Destroy(infant.gameObject);
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null; Time.timeScale=oldScale;
            if(File.Exists(checkpoint)) File.Delete(checkpoint);
        }
        void Stick(Vector2 value) => InputSystem.QueueDeltaStateEvent((StickControl)left["thumbstick"],value);
        void HoldFly() { fly.enabled=false; agent.isStopped=true; agent.ResetPath(); agent.velocity=Vector3.zero; }
        IEnumerator Visit(string id)
        {
            HoldFly(); Assert.IsTrue(agent.Warp(new Vector3(12,.3f,7)));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            var slot=infant.Room.ItemSlots.Single(s=>s.ItemId=="infant_"+id);
            Assert.IsTrue(slot.TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point));
            Assert.IsTrue(agent.Warp(point+Vector3.up*agent.baseOffset));
            yield return new WaitForFixedUpdate(); yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(infant.Room.VisitedItemIds.Contains(slot.ItemId));
        }
        IEnumerator WaitPhase(TransitionPhase phase)
        {
            float deadline=Time.realtimeSinceStartup+8;
            while(flow.Phase!=phase) { Assert.Less(Time.realtimeSinceStartup,deadline,"Waiting for "+phase+", got "+flow.Phase); yield return null; }
        }
        void SteerTo(Vector3 target)
        {
            Vector3 delta=target-chair.transform.position;
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            float turn=Mathf.DeltaAngle(chair.transform.eulerAngles.y,yaw);
            Stick(new Vector2(Mathf.Clamp(turn/15,-1,1),Mathf.Abs(turn)<30 ? 1 : 0));
        }
        IEnumerator EnterCorridor()
        {
            infant.Timeline.Advance(480.1f-infant.Timeline.Elapsed); yield return null;
            // 문 개방 때 HeadToExit가 NavMesh를 다시 구동한다. 지정된 방문 기록 비교 동안만 정지한다.
            HoldFly();
            float deadline=Time.realtimeSinceStartup+10;
            while(flow.Phase==TransitionPhase.Idle)
            {
                Assert.Less(Time.realtimeSinceStartup,deadline,"Left-stick drive to source exit stalled.");
                SteerTo(new Vector3(26,0,11)); yield return null;
            }
            Stick(Vector2.zero); yield return WaitPhase(TransitionPhase.Corridor);
        }

        [UnityTest] public IEnumerator Observe_DriveCorridor_ThenEnterNextRoom_WithoutSceneLoadOrCameraJump()
        {
            yield return Visit("hug"); yield return Visit("ball"); yield return Visit("book");
            var state=GameState.Current; var handle=SceneManager.GetActiveScene().handle;
            int scenes=SceneManager.sceneCount; float began=Time.realtimeSinceStartup;
            yield return EnterCorridor();
            Assert.IsFalse(infant.Room.gameObject.activeSelf); Assert.IsTrue(flow.Corridor.gameObject.activeSelf);
            Assert.AreEqual("corridor_01",state.CurrentRoomId); Assert.GreaterOrEqual(flow.LastFadeOutSeconds,.5f);
            Vector3 stopped=chair.transform.position; Quaternion rotation=chair.transform.rotation;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.Less(Vector3.Distance(stopped,chair.transform.position),.01f); Assert.Less(Quaternion.Angle(rotation,chair.transform.rotation),.1f);
            Assert.IsTrue(chair.enabled); Assert.AreEqual(0,flow.View.FadeAlpha);
            yield return Capture("Logs/corridor-overview.png",new Vector3(24.8f,1.6f,11),new Vector3(38,1.6f,11));
            float deadline=Time.realtimeSinceStartup+12;
            while(flow.Phase==TransitionPhase.Corridor)
            {
                Assert.Less(Time.realtimeSinceStartup,deadline,"Corridor traversal stalled.");
                var before=chair.transform.position;
                SteerTo(new Vector3(40,0,11)); yield return null;
                Assert.Less(Vector3.Distance(before,chair.transform.position),.5f,"Camera/rig must not teleport.");
                Assert.That(chair.transform.position.y,Is.InRange(-.05f,.05f));
            }
            Stick(Vector2.zero); var fadePosition=chair.transform.position;
            yield return WaitPhase(TransitionPhase.Complete);
            Assert.Less(Vector3.Distance(fadePosition,chair.transform.position),.01f);
            Assert.AreSame(state,GameState.Current); Assert.AreEqual(handle,SceneManager.GetActiveScene().handle); Assert.AreEqual(scenes,SceneManager.sceneCount);
            Assert.AreEqual("03_Childhood",state.CurrentRoomId); Assert.IsTrue(flow.Destination.Entered); Assert.IsFalse(flow.Corridor.gameObject.activeSelf);
            Assert.IsTrue(chair.enabled); Assert.AreEqual(0,flow.View.FadeAlpha);
            var checkpoints=state.EventLog.Where(e=>e.eventType=="checkpoint").Select(e=>e.payload).ToArray();
            Assert.AreEqual(1,checkpoints.Count(c=>c=="corridor_start")); Assert.AreEqual(1,checkpoints.Count(c=>c=="corridor_end")); Assert.AreEqual(2,checkpoints.Count(c=>c=="room_enter"));
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint)); Assert.AreEqual("03_Childhood",saved.currentRoomId);
            CollectionAssert.AreEqual(new[]{"Recap","Narration","Silhouette"},state.EventLog.Where(e=>e.eventType=="corridor_section").Select(e=>e.payload));
            yield return Capture("Logs/childhood-entry.png",new Vector3(39,1.6f,11),new Vector3(47,1.6f,11));
            File.WriteAllText("Logs/corridor-playthrough.txt",$"Real traversal including fades (10x movement): {Time.realtimeSinceStartup-began:0.00}s\nLast fade: {flow.LastFadeOutSeconds:0.000}s\nScene count unchanged: {scenes}\nFinal room: {state.CurrentRoomId}\n");
        }

        [UnityTest] public IEnumerator RecapA_ActualCalmVisits_ShowClearFramesAndRecordedChildLines()
        {
            yield return Visit("hug"); yield return Visit("ball"); yield return Visit("book");
            yield return EnterCorridor();
            var recap=flow.Corridor.Recap.Recap;
            CollectionAssert.AreEqual(new[]{"infant_hug","infant_ball","infant_book"},recap.Memories.Select(m=>m.Experience.itemId));
            Assert.AreEqual(0,recap.InterventionCount); Assert.IsTrue(recap.Memories.All(m=>!m.WasPushedAway));
            Assert.AreEqual(0,flow.Corridor.GetComponentsInChildren<Transform>().Count(t=>t.name=="Crack"));
            Assert.IsTrue(recap.Memories.All(m=>!string.IsNullOrEmpty(m.Experience.childLine)));
            File.WriteAllText("Logs/corridor-recap-a.json",JsonUtility.ToJson(GameState.Current.ToSaveData(),true));
            yield return Capture("Logs/corridor-recap-a.png",new Vector3(26,1.65f,10.2f),new Vector3(26,1.65f,13));
        }

        [UnityTest] public IEnumerator RecapB_ActualDifferentVisitsAndPushDeparture_ChangeFramesAndCracks()
        {
            yield return Visit("blocks"); yield return Visit("learning"); yield return Visit("tablet");
            infant.Timeline.Advance(90.1f-infant.Timeline.Elapsed); yield return null;
            fly.enabled=true;
            var tablet=infant.Room.ItemSlots.Single(s=>s.ItemId=="infant_tablet");
            Vector3 away=Vector3.ProjectOnPlane(fly.transform.position-tablet.transform.position,Vector3.up).normalized;
            var receiver=fly.GetComponent<FlyContactReceiver>();
            Assert.IsTrue(receiver.ContactsEnabled,"Observation cooldown must be over before exercising a push.");
            var start=fly.transform.position;
            var region=tablet.GetComponent<SphereCollider>();
            Assert.IsTrue(Physics.ComputePenetration(fly.GetComponent<Collider>(),agent.nextPosition,fly.transform.rotation,
                region,region.transform.position,region.transform.rotation,out _,out _),"Push starts inside the tablet region.");
            receiver.ReceiveContact(PushSource.ElectricStick,-away,Time.time);
            Assert.IsTrue(fly.IsBeingPushed);
            yield return new WaitForSeconds(.5f);
            Assert.IsTrue(GameState.Current.EventLog.Any(e=>e.eventType=="item_pushed_away" && e.payload=="infant_tablet"),
                $"Push departure missing: {start:F4} -> {fly.transform.position:F4}, nav={agent.nextPosition:F4}, direction={away:F4}, pushing={fly.IsBeingPushed}");
            HoldFly();
            for(int i=0;i<3;i++) receiver.ReceiveContact(PushSource.ElectricStick,-away,Time.time);
            yield return EnterCorridor();
            var recap=flow.Corridor.Recap.Recap;
            CollectionAssert.AreEqual(new[]{"infant_tablet","infant_learning","infant_blocks"},recap.Memories.Select(m=>m.Experience.itemId));
            Assert.AreEqual(4,recap.InterventionCount); Assert.IsTrue(recap.Memories[0].WasPushedAway);
            Assert.AreEqual(3,flow.Corridor.GetComponentsInChildren<Transform>().Count(t=>t.name=="Crack"));
            Assert.IsTrue(flow.Corridor.GetComponentsInChildren<Canvas>().Where(c=>c.name.StartsWith("Memory ")).All(c=>Quaternion.Angle(c.transform.localRotation,Quaternion.identity)>3));
            File.WriteAllText("Logs/corridor-recap-b.json",JsonUtility.ToJson(GameState.Current.ToSaveData(),true));
            yield return Capture("Logs/corridor-recap-b.png",new Vector3(26,1.65f,10.2f),new Vector3(26,1.65f,13));
        }

        [UnityTest] public IEnumerator UnreadyDestination_KeepsPhysicalDoorClosed_UntilReady()
        {
            HoldFly(); flow.Destination.ContentReady=false;
            yield return EnterCorridor(); Assert.IsTrue(flow.Corridor.EndDoor.activeSelf);
            float end=Time.time+25;
            while(Time.time<end) { SteerTo(new Vector3(40,0,11)); yield return null; }
            Stick(Vector2.zero); yield return new WaitForSeconds(1);
            Assert.AreEqual(TransitionPhase.Corridor,flow.Phase); Assert.That(chair.transform.position.x,Is.InRange(37.3f,37.7f));
            Assert.IsFalse(flow.TryEnterNextRoom(chair));
            flow.Destination.ContentReady=true; yield return null; Assert.IsFalse(flow.Corridor.EndDoor.activeSelf);
            float deadline=Time.realtimeSinceStartup+5;
            while(flow.Phase==TransitionPhase.Corridor)
            { Assert.Less(Time.realtimeSinceStartup,deadline); SteerTo(new Vector3(40,0,11)); yield return null; }
            Stick(Vector2.zero); yield return WaitPhase(TransitionPhase.Complete); Assert.IsTrue(flow.Destination.Entered);
        }

        [UnityTest] public IEnumerator PushBlockedByFurniture_DoesNotInventAnAvoidedItemMemory()
        {
            yield return Visit("tablet");
            infant.Timeline.Advance(90.1f-infant.Timeline.Elapsed); yield return null;
            fly.enabled=true;
            var tablet=infant.Room.ItemSlots.Single(s=>s.ItemId=="infant_tablet");
            Vector3 away=Vector3.ProjectOnPlane(fly.transform.position-tablet.transform.position,Vector3.up).normalized;
            // 접촉 방향은 아이에서 도구를 향한다. 이 입력은 가구 쪽으로 밀어 NavMesh에 막힌다.
            fly.GetComponent<FlyContactReceiver>().ReceiveContact(PushSource.ElectricStick,away,Time.time);
            yield return new WaitForSeconds(.5f);
            Assert.Less(Vector3.Distance(fly.transform.position,tablet.transform.position),tablet.InteractionRadius);
            Assert.IsFalse(GameState.Current.EventLog.Any(e=>e.eventType=="item_pushed_away"));
        }

        [UnityTest] public IEnumerator FourMetreCorridor_AllowsFullWheelchairTurnAndPreventsReturningToOldRoom()
        {
            HoldFly(); yield return EnterCorridor();
            float target=28;
            while(chair.transform.position.x<target) { SteerTo(new Vector3(32,0,11)); yield return null; }
            Stick(Vector2.zero); yield return new WaitForSeconds(1);
            Vector3 centre=chair.transform.position;
            float yaw=chair.transform.eulerAngles.y, total=0, deadline=Time.realtimeSinceStartup+8;
            Stick(Vector2.right);
            while(total<360)
            {
                Assert.Less(Time.realtimeSinceStartup,deadline); yield return null;
                float next=chair.transform.eulerAngles.y; total+=Mathf.Abs(Mathf.DeltaAngle(yaw,next)); yaw=next;
                Assert.Less(Vector3.Distance(centre,chair.transform.position),.02f);
            }
            Stick(Vector2.zero); yield return new WaitForSeconds(1);
            var body=chair.GetComponent<CharacterController>();
            foreach(var collider in flow.Corridor.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger && c.name!="Floor"))
                Assert.IsFalse(Physics.ComputePenetration(body,chair.transform.position,chair.transform.rotation,collider,collider.transform.position,collider.transform.rotation,out _,out float depth) && depth>.02f);
            float end=Time.time+20;
            while(Time.time<end) { SteerTo(new Vector3(20,0,11)); yield return null; }
            Stick(Vector2.zero); yield return new WaitForSeconds(1);
            Assert.Greater(chair.transform.position.x,23.8f); Assert.IsFalse(infant.Room.gameObject.activeSelf);
        }

        [UnityTest] public IEnumerator AudioClipsAndCrossfade_PlayContinuously_WithoutAbruptVolumeChange()
        {
            foreach(var source in new[]{flow.Sound.Previous,flow.Sound.Next})
            {
                Assert.IsNotNull(source.clip); Assert.IsTrue(source.isPlaying); Assert.IsTrue(source.loop);
                var samples=new float[source.clip.samples]; Assert.IsTrue(source.clip.GetData(samples,0));
                Assert.Greater(samples.Max(v=>Mathf.Abs(v)),.05f);
                Assert.Less(Mathf.Abs(samples[0]-samples[samples.Length-1]),.03f,"Loop boundary must not click.");
            }
            HoldFly(); yield return EnterCorridor();
            Assert.Less(flow.Sound.Next.volume,.001f);
            float deadline=Time.realtimeSinceStartup+12;
            while(flow.Phase==TransitionPhase.Corridor)
            {
                Assert.Less(Time.realtimeSinceStartup,deadline);
                float previous=flow.Sound.Previous.volume, next=flow.Sound.Next.volume;
                SteerTo(new Vector3(40,0,11)); yield return null;
                Assert.LessOrEqual(Mathf.Abs(previous-flow.Sound.Previous.volume),Time.unscaledDeltaTime*.121f+.001f);
                Assert.LessOrEqual(Mathf.Abs(next-flow.Sound.Next.volume),Time.unscaledDeltaTime*.121f+.001f);
            }
            Stick(Vector2.zero); yield return WaitPhase(TransitionPhase.Complete); yield return new WaitForSecondsRealtime(1.1f);
            Assert.AreEqual(0,flow.Sound.Previous.volume,.001f); Assert.AreEqual(.12f,flow.Sound.Next.volume,.001f);
            Assert.IsTrue(flow.Sound.Next.isPlaying);
        }

        static IEnumerator Capture(string path, Vector3 position, Vector3 lookAt)
        {
            var go=new GameObject("Verification Camera"); var camera=go.AddComponent<Camera>(); camera.CopyFrom(Camera.main);
            camera.transform.position=position; camera.transform.LookAt(lookAt);
            var target=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var active=RenderTexture.active;
            try
            {
                camera.targetTexture=target; for(int i=0;i<4;i++) yield return null;
                RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                var colors=pixels.GetPixels(); Assert.Greater(colors.Max(c=>c.grayscale)-colors.Min(c=>c.grayscale),.1f);
                Assert.Less(colors.Count(c=>c.r>.9f && c.b>.9f && c.g<.1f),colors.Length/100);
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=null; RenderTexture.active=active; target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(go); }
        }
    }
}
