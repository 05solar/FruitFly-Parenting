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
    public class AdolescenceRoomIntegrationTests
    {
        AdolescenceRoomController teen;
        FlyAgent fly;
        NavMeshAgent agent;
        InputDevice left;
        string checkpoint;
        float scale;
        bool hidden;
        [UnitySetUp] public IEnumerator SetUp()
        {
            scale=Time.timeScale; hidden=false;
            Directory.CreateDirectory("Logs"); checkpoint=Path.GetFullPath("Logs/adolescence-test-"+System.Guid.NewGuid().ToString("N")+".json");
            InputSystem.RegisterLayout(@"{""name"":""Step10Controller"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]}]}");
            left=InputSystem.AddDevice("Step10Controller"); InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);
            yield return Load(false);
        }
        IEnumerator Load(bool hiddenCostume)
        {
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null; hidden=hiddenCostume;
            SceneManager.sceneLoaded+=Inject; yield return SceneManager.LoadSceneAsync("04_Adolescence"); SceneManager.sceneLoaded-=Inject;
            teen=Object.FindAnyObjectByType<AdolescenceRoomController>(); fly=teen.Fly; agent=fly.GetComponent<NavMeshAgent>();
            yield return null; Time.timeScale=10; Hold();
        }
        void Inject(Scene scene,LoadSceneMode mode)
        {
            Object.FindAnyObjectByType<AdolescenceRoomController>().CheckpointPath=checkpoint;
            if(!hidden) return;
            var state=GameState.Current;
            state.Stats.ApplyDelta(StatType.Stress,30,"test"); state.CaptureRoomExitStress(LifeStage.Infant,0);
            state.Stats.ApplyDelta(StatType.Stress,-8,"test"); state.CaptureRoomExitStress(LifeStage.Childhood,0);
            state.Stats.ApplyDelta(StatType.Stress,-22,"test");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded-=Inject; if(left!=null) InputSystem.RemoveDevice(left);
            if(teen!=null) { Object.Destroy(teen.Transition.Destination.gameObject); Object.Destroy(teen.Transition.Corridor.gameObject); Object.Destroy(teen.Transition.gameObject); Object.Destroy(teen.gameObject); }
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null; Time.timeScale=scale; if(File.Exists(checkpoint)) File.Delete(checkpoint);
        }
        void Hold() { fly.enabled=false; agent.isStopped=true; agent.ResetPath(); agent.velocity=Vector3.zero; }
        ItemSlot Slot(string id) => teen.Room.ItemSlots.Single(s=>s.ItemId=="adolescence_"+id);
        IEnumerator Approach(string id)
        {
            Hold(); Assert.IsTrue(agent.Warp(new Vector3(12,.3f,9))); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsTrue(Slot(id).TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point));
            Assert.IsTrue(agent.Warp(point+Vector3.up*agent.baseOffset)); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        }
        IEnumerator Visit(string id)
        { yield return Approach(id); yield return new WaitForSeconds(Slot(id).Definition.isExtreme ? 4.2f : 2.2f); Assert.IsTrue(teen.Room.VisitedItemIds.Contains("adolescence_"+id)); }
        [UnityTest] public IEnumerator ThirteenActualVisits_ApplyValuesAndTheftRecord_RecoveryDoesNotClearIt()
        {
            int training=0; float expectedR=0,expectedTrust=50;
            foreach(var entry in AdolescenceCatalog.Entries)
            {
                var before=GameState.Current.ToSaveData();
                bool repeated=(entry.Stats[1]>=5 || entry.Stats[2]>=5) && ++training>=3 && training<=5;
                yield return Visit(entry.Id.Substring("adolescence_".Length));
                for(int i=0;i<5;i++)
                {
                    float expected=Mathf.Clamp(before.statValues[i]+entry.Stats[i],0,100);
                    if(i==0 && repeated) expected=Mathf.Clamp(expected+3,0,100);
                    Assert.AreEqual(expected,GameState.Current.Stats.GetValue((StatType)i),entry.Id+" stat "+i);
                }
                expectedR+=entry.Rebellion+(repeated ? 4 : 0); expectedTrust+=entry.Trust+(entry.Id=="adolescence_parents" ? 8f : 0);
                Assert.AreEqual(expectedR,GameState.Current.Stats.GetValue(StatType.Rebellion)); Assert.AreEqual(expectedTrust,GameState.Current.Relationship.Trust);
                Assert.AreEqual(entry.Delinquency,GameState.Current.HasDelinquencyRecord,"Only the final theft item sets the record.");
            }
            yield return Visit("theft"); yield return Visit("counsel");
            Assert.AreEqual(13,teen.Room.VisitedItemIds.Count); Assert.AreEqual(70,GameState.Current.Stats.GetValue(StatType.Rebellion));
            Assert.AreEqual(17+8f,GameState.Current.Relationship.Trust); Assert.IsTrue(GameState.Current.HasDelinquencyRecord);
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint)); Assert.IsTrue(saved.delinquencyRecord);
            Assert.IsFalse(saved.adolescenceExitCaptured); File.WriteAllText("Logs/adolescence-all-visits.json",JsonUtility.ToJson(saved,true));
        }
        [UnityTest] public IEnumerator SavedNavMesh_IsOneConnectedFloor_AllThirteenSlotsReachable()
        {
            var mesh=NavMesh.CalculateTriangulation(); Assert.IsNotEmpty(mesh.indices);
            for(int i=0;i<mesh.indices.Length;i+=3)
            {
                var center=(mesh.vertices[mesh.indices[i]]+mesh.vertices[mesh.indices[i+1]]+mesh.vertices[mesh.indices[i+2]])/3;
                Assert.Less(center.y,.2f); var path=new NavMeshPath(); Assert.IsTrue(NavMesh.CalculatePath(agent.nextPosition,center,NavMesh.AllAreas,path)); Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
            }
            Assert.AreEqual(13,teen.Room.ItemSlots.Count); foreach(var slot in teen.Room.ItemSlots) Assert.IsTrue(slot.TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out _),slot.ItemId);
            Assert.IsTrue(agent.Warp(new Vector3(6,.3f,11))); agent.isStopped=false; agent.SetDestination(new Vector3(18,0,11));
            float deadline=Time.realtimeSinceStartup+7; while(fly.transform.position.x<17.5f) { Assert.Less(Time.realtimeSinceStartup,deadline); yield return null; }
            Hold(); File.WriteAllText("Logs/adolescence-navmesh.txt",$"{mesh.indices.Length/3} triangles; 1 connected floor; 13 reachable items\n");
            yield return Capture("Logs/adolescence-overview.png",new Vector3(12,21,-1),new Vector3(12,0,11));
        }
        [UnityTest] public IEnumerator LowTrust_FirstConversationRefusesButSecondApproachCanVisit()
        {
            GameState.Current.Relationship.AdjustTrust(-20,"test"); yield return Approach("parents"); fly.enabled=true;
            yield return new WaitForSeconds(.5f); Assert.IsTrue(teen.Narrative.RefusedConversation); Assert.IsTrue(fly.IsHesitating);
            Assert.IsFalse(teen.Room.VisitedItemIds.Contains("adolescence_parents")); Assert.AreEqual(50,GameState.Current.Stats.GetValue(StatType.Love));
            yield return new WaitForSeconds(2); yield return Visit("parents");
            Assert.AreEqual(59,GameState.Current.Stats.GetValue(StatType.Love)); Assert.AreEqual(1,GameState.Current.EventLog.Count(e=>e.eventType=="conversation_refused"));
        }
        [UnityTest] public IEnumerator UniformAndHiddenCostume_UseEntrySnapshotsOnly()
        {
            Assert.IsFalse(teen.Costume.Hidden); Assert.IsTrue(fly.transform.Find("Adolescence Appearance/School Uniform").gameObject.activeSelf);
            yield return Capture("Logs/adolescence-uniform.png",fly.transform.position+new Vector3(0,1,2),fly.transform.position+Vector3.up*.3f);
            yield return Load(true); Assert.IsTrue(teen.Costume.Hidden); Assert.IsFalse(fly.transform.Find("Adolescence Appearance/School Uniform").gameObject.activeSelf);
            Assert.AreEqual(50,GameState.Current.Stats.GetValue(StatType.Stress)); Assert.AreEqual(13,teen.Room.GetAllItemIds().Count);
            GameState.Current.Stats.ApplyDelta(StatType.Stress,-50,"test"); yield return null; Assert.IsTrue(teen.Costume.Hidden);
            yield return Capture("Logs/adolescence-hidden.png",fly.transform.position+new Vector3(0,1,2),fly.transform.position+Vector3.up*.3f);
        }
        [UnityTest] public IEnumerator ActualAutonomousChoicesAndTwoPushDepartures_ProducePreferenceDialogue()
        {
            teen.Timeline.Advance(90.1f-teen.Timeline.Elapsed); yield return null;
            for(int n=0;n<40 && teen.Narrative.PreferredItemId==null;n++) fly.PickAndMoveToNext();
            Assert.IsNotNull(teen.Narrative.PreferredItemId);
            string id=teen.Narrative.PreferredItemId.Substring("adolescence_".Length);
            GameState.Current.Stats.ApplyDelta(StatType.Rebellion,40,"test");
            for(int episode=0;episode<2;episode++)
            {
                for(int n=0;n<1000 && fly.CurrentDestinationId!="adolescence_"+id;n++) fly.PickAndMoveToNext();
                Assert.AreEqual("adolescence_"+id,fly.CurrentDestinationId);
                yield return Visit(id); fly.enabled=true;
                Vector3 away=Vector3.ProjectOnPlane(fly.transform.position-Slot(id).transform.position,Vector3.up).normalized;
                fly.GetComponent<FlyContactReceiver>().ReceiveContact(PushSource.ElectricStick,-away,Time.time);
                yield return new WaitForSeconds(.6f); Hold();
            }
            Assert.AreEqual(2,teen.Narrative.BlockCount);
            Assert.IsTrue(GameState.Current.EventLog.Any(e=>e.eventType=="blocked_preference" && e.payload.Contains("이번엔 내가 골라 볼게")));
        }
        [UnityTest] public IEnumerator WaitingWindow_ActualToolUseRestartsQuietPeriod()
        {
            teen.Timeline.Advance(390-teen.Timeline.Elapsed); yield return null;
            Assert.Greater(teen.Room.RestAttractionBonus,0); Assert.IsFalse(teen.Narrative.Waited);
            teen.Timeline.Advance(20); yield return null;
            fly.GetComponent<FlyContactReceiver>().ReceiveContact(PushSource.ElectricStick,Vector3.forward,Time.time);
            teen.Timeline.Advance(28); yield return null; Assert.IsFalse(teen.Narrative.Waited);
            teen.Timeline.Advance(2.5f); yield return null; Assert.IsTrue(teen.Narrative.Waited);
            Assert.AreEqual(1,GameState.Current.EventLog.Count(e=>e.eventType=="waiting_choice" && e.payload.Contains("기다려 줘서")));
        }
        [UnityTest] public IEnumerator ExitAt59_OpensCorridorWithoutFlight_AndSnapshotCannotBeChanged()
        {
            GameState.Current.Stats.ApplyDelta(StatType.Rebellion,59,"test"); teen.Timeline.Advance(480.1f-teen.Timeline.Elapsed); yield return null;
            Assert.AreEqual(AdolescenceExitPhase.None,teen.ExitSequence.Phase); Assert.IsTrue(teen.Transition.SourceExitOpen);
            Assert.AreEqual(59,GameState.Current.AdolescenceExitRebellion); Assert.IsFalse(GameState.Current.IsFlightSmokeEventTriggered);
            GameState.Current.Stats.ApplyDelta(StatType.Rebellion,1,"test"); yield return null;
            Assert.IsFalse(teen.ExitSequence.Play()); Assert.AreEqual(59,GameState.Current.AdolescenceExitRebellion);
        }
        [UnityTest] public IEnumerator ExitAt60_FliesSmokesExtinguishesOnce_WithoutMovingCamera_ThenDrivesCorridorThree()
        {
            yield return Load(true);
            var state=GameState.Current; state.Stats.ApplyDelta(StatType.Rebellion,60,"test");
            var chair=Object.FindAnyObjectByType<WheelchairLocomotion>(); var camera=Camera.main;
            var cameraAt=camera.transform.position; var cameraRotation=camera.transform.rotation;
            var before=state.ToSaveData(); teen.Timeline.Advance(480.1f-teen.Timeline.Elapsed); yield return null;
            Assert.IsTrue(teen.ExitSequence.IsPlaying); Assert.IsFalse(teen.Transition.SourceExitOpen); Assert.IsFalse(chair.enabled);
            Assert.IsTrue(teen.Costume.Hidden); bool sawSmoke=false,sawFlight=false;
            float start=Time.time,deadline=Time.realtimeSinceStartup+8;
            Stick(Vector2.up);
            while(teen.ExitSequence.IsPlaying)
            {
                Assert.Less(Time.realtimeSinceStartup,deadline);
                Assert.Less(Vector3.Distance(cameraAt,camera.transform.position),.001f); Assert.Less(Quaternion.Angle(cameraRotation,camera.transform.rotation),.01f);
                StringAssert.Contains("장면 진행 중",teen.View.StatusText);
                if(teen.ExitSequence.WingsVisible && fly.transform.position.y>2 && !sawFlight)
                {
                    sawFlight=true;
                    yield return Capture("Logs/adolescence-flight.png",new Vector3(12,4,7),fly.transform.position);
                }
                if(teen.ExitSequence.SmokeVisible && !sawSmoke)
                {
                    sawSmoke=true; Stick(Vector2.zero);
                    yield return Capture("Logs/adolescence-window.png",new Vector3(20,2.4f,17.5f),new Vector3(20,2.2f,20.4f));
                }
                yield return null;
            }
            Stick(Vector2.zero); Assert.IsTrue(sawFlight); Assert.IsTrue(sawSmoke); Assert.That(Time.time-start,Is.InRange(11.7f,12.5f));
            Assert.IsFalse(teen.ExitSequence.SmokeVisible); Assert.IsFalse(teen.ExitSequence.WingsVisible); Assert.IsTrue(agent.isOnNavMesh); Assert.IsTrue(chair.enabled);
            CollectionAssert.AreEqual(before.statValues,state.ToSaveData().statValues);
            Assert.IsFalse(teen.ExitSequence.Play()); Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="flight_smoke_started"));
            state.Stats.ApplyDelta(StatType.Rebellion,-59,"test"); Assert.AreEqual(60,state.AdolescenceExitRebellion);
            var flow=teen.Transition; Hold(); state.Relationship.AdjustTrust(-30,"test");
            deadline=Time.realtimeSinceStartup+12;
            while(flow.Phase==TransitionPhase.Idle) { Assert.Less(Time.realtimeSinceStartup,deadline); Drive(chair,new Vector3(26,0,11)); yield return null; }
            Stick(Vector2.zero); yield return Phase(flow,TransitionPhase.Corridor);
            Assert.AreEqual(.12f,flow.Corridor.Recap.ParentPhotoShare,.001f); float distance=flow.Corridor.Recap.ShadowSeparation;
            yield return Capture("Logs/adolescence-corridor-low-trust.png",new Vector3(31,1.7f,11.9f),new Vector3(31,1.7f,9));
            state.Relationship.AdjustTrust(60,"test"); flow.Corridor.Recap.Show(state,AdolescenceRoomController.RoomId); yield return null;
            Assert.AreEqual(.48f,flow.Corridor.Recap.ParentPhotoShare,.001f); Assert.Less(flow.Corridor.Recap.ShadowSeparation,distance);
            yield return Capture("Logs/adolescence-corridor-high-trust.png",new Vector3(31,1.7f,11.9f),new Vector3(31,1.7f,9));
            deadline=Time.realtimeSinceStartup+12;
            while(flow.Phase==TransitionPhase.Corridor) { Assert.Less(Time.realtimeSinceStartup,deadline); Drive(chair,new Vector3(40,0,11)); yield return null; }
            Stick(Vector2.zero); yield return Phase(flow,TransitionPhase.Complete);
            Assert.AreEqual("05_YoungAdult",state.CurrentRoomId); var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint));
            Assert.AreEqual(60,saved.adolescenceExitRebellion); Assert.IsTrue(saved.flightSmokeEventTriggered); Assert.IsTrue(saved.adolescenceExitEventPlayed); Assert.IsTrue(saved.adolescenceHiddenCostume);
            Assert.IsFalse(saved.delinquencyRecord,"Flying does not invent a theft record."); File.WriteAllText("Logs/adolescence-exit.json",JsonUtility.ToJson(saved,true));
        }
        void Stick(Vector2 value) => InputSystem.QueueDeltaStateEvent((StickControl)left["thumbstick"],value);
        void Drive(WheelchairLocomotion chair,Vector3 target)
        { var d=target-chair.transform.position; float turn=Mathf.DeltaAngle(chair.transform.eulerAngles.y,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg); Stick(new Vector2(Mathf.Clamp(turn/15,-1,1),Mathf.Abs(turn)<30 ? 1 : 0)); }
        static IEnumerator Phase(RoomTransitionController flow,TransitionPhase phase)
        { float deadline=Time.realtimeSinceStartup+8; while(flow.Phase!=phase) { Assert.Less(Time.realtimeSinceStartup,deadline); yield return null; } }
        static IEnumerator Capture(string path,Vector3 at,Vector3 target)
        {
            var main=Camera.main; var oldPosition=main.transform.position; var oldRotation=main.transform.rotation;
            var texture=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false); var previous=RenderTexture.active;
            try
            {
                main.transform.position=at; main.transform.LookAt(target); yield return null; main.targetTexture=texture; main.Render(); RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply(); var colors=pixels.GetPixels();
                Assert.Greater(colors.Max(c=>c.grayscale)-colors.Min(c=>c.grayscale),.1f); Assert.Less(colors.Count(c=>c.r>.9f && c.b>.9f && c.g<.1f),colors.Length/100);
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally { main.targetTexture=null; RenderTexture.active=previous; main.transform.SetPositionAndRotation(oldPosition,oldRotation); texture.Release(); Object.Destroy(texture); Object.Destroy(pixels); }
        }
    }
}
