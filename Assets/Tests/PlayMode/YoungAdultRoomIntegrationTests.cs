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
    public class YoungAdultRoomIntegrationTests
    {
        YoungAdultRoomController adult;
        FlyAgent fly;
        NavMeshAgent agent;
        InputDevice left;
        string checkpoint;
        float scale;
        [UnitySetUp] public IEnumerator SetUp()
        {
            scale=Time.timeScale;
            Directory.CreateDirectory("Logs"); checkpoint=Path.GetFullPath("Logs/youngadult-test-"+System.Guid.NewGuid().ToString("N")+".json");
            InputSystem.RegisterLayout(@"{""name"":""Step11Controller"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]}]}");
            left=InputSystem.AddDevice("Step11Controller"); InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);
            yield return Load();
        }
        IEnumerator Load()
        {
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            SceneManager.sceneLoaded+=Inject; yield return SceneManager.LoadSceneAsync("05_YoungAdult"); SceneManager.sceneLoaded-=Inject;
            adult=Object.FindAnyObjectByType<YoungAdultRoomController>(); fly=adult.Fly; agent=fly.GetComponent<NavMeshAgent>();
            yield return null; Time.timeScale=10; Hold();
        }
        void Inject(Scene scene,LoadSceneMode mode)
        {
            Object.FindAnyObjectByType<YoungAdultRoomController>().CheckpointPath=checkpoint;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded-=Inject; if(left!=null) InputSystem.RemoveDevice(left);
            if(adult!=null) { Object.Destroy(adult.Transition.Destination.gameObject); Object.Destroy(adult.Transition.Corridor.gameObject); Object.Destroy(adult.Transition.gameObject); Object.Destroy(adult.gameObject); }
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null; Time.timeScale=scale; if(File.Exists(checkpoint)) File.Delete(checkpoint);
        }
        void Hold() { fly.enabled=false; agent.isStopped=true; agent.ResetPath(); agent.velocity=Vector3.zero; }
        ItemSlot Slot(string id) => adult.Room.ItemSlots.Single(s=>s.ItemId=="youngadult_"+id);
        IEnumerator Approach(string id)
        {
            Hold(); Assert.IsTrue(agent.Warp(new Vector3(12,.3f,9))); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsTrue(Slot(id).TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point));
            Assert.IsTrue(agent.Warp(point+Vector3.up*agent.baseOffset)); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        }
        IEnumerator Visit(string id)
        { yield return Approach(id); yield return new WaitForSeconds(Slot(id).Definition.isExtreme ? 4.2f : 2.2f); Assert.IsTrue(adult.Room.VisitedItemIds.Contains("youngadult_"+id)); if(adult.Gambling.IsPlaying) { float deadline=Time.realtimeSinceStartup+5; while(adult.Gambling.IsPlaying) { Assert.Less(Time.realtimeSinceStartup,deadline); yield return null; } } }
        [UnityTest] public IEnumerator ThirteenActualVisits_ApplyAllValuesAndFlags_WithoutRepeatedGamblingLoss()
        {
            int training=0; float expectedR=0,expectedTrust=50;
            foreach(var entry in YoungAdultCatalog.Entries)
            {
                var before=GameState.Current.ToSaveData();
                bool repeated=(entry.Stats[1]>=5 || entry.Stats[2]>=5) && ++training>=3 && training<=5;
                yield return Visit(entry.Id.Substring("youngadult_".Length));
                for(int i=0;i<5;i++)
                {
                    float expected=Mathf.Clamp(before.statValues[i]+entry.Stats[i],0,100);
                    if(i==0 && repeated) expected=Mathf.Clamp(expected+3,0,100);
                    Assert.AreEqual(expected,GameState.Current.Stats.GetValue((StatType)i),entry.Id+" stat "+i);
                }
                expectedR+=entry.Rebellion+(repeated ? 4 : 0); expectedTrust+=entry.Trust+(entry.Id=="youngadult_letter" ? 8f : 0);
                Assert.AreEqual(expectedR,GameState.Current.Stats.GetValue(StatType.Rebellion)); Assert.AreEqual(expectedTrust,GameState.Current.Relationship.Trust);
            }
            var state=GameState.Current; var all=state.ToSaveData();
            yield return Visit("gambling"); CollectionAssert.AreEqual(all.statValues,state.ToSaveData().statValues);
            yield return Visit("park"); Assert.IsTrue(state.HasGamblingBankruptcy);
            Assert.AreEqual(13,adult.Room.VisitedItemIds.Count); Assert.AreEqual(46,state.Stats.GetValue(StatType.Rebellion)); Assert.AreEqual(9,state.Relationship.Trust);
            Assert.AreEqual((YoungAdultFlags)15,state.ToSaveData().youngAdultFlags);
            Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="gambling_montage_completed"));
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint)); Assert.IsFalse(saved.finalYoungAdultSnapshot!=null && saved.finalYoungAdultSnapshot.IsCaptured);
            File.WriteAllText("Logs/youngadult-all-visits.json",JsonUtility.ToJson(saved,true));
        }
        [UnityTest] public IEnumerator Gambling_DelaysLossUntilMontageEnds_ClosesOpenDoor_AndFinishesAcrossTenMinuteDeadline()
        {
            adult.Timeline.Advance(590-adult.Timeline.Elapsed); yield return null; Hold();
            var state=GameState.Current; var before=state.ToSaveData(); Assert.IsTrue(adult.Transition.SourceExitOpen); Assert.IsNull(state.FinalYoungAdultSnapshot);
            yield return Approach("gambling"); yield return new WaitForSeconds(4.2f);
            Assert.AreEqual(GamblingPhase.SmallWin,adult.Gambling.Phase); Assert.IsFalse(agent.enabled);
            Assert.IsFalse(adult.Transition.SourceExitOpen); Assert.IsFalse(fly.GetComponent<FlyContactReceiver>().ContactsEnabled);
            CollectionAssert.AreEqual(before.statValues,state.ToSaveData().statValues); Assert.IsFalse(state.HasGamblingBankruptcy);
            adult.Timeline.Advance(600.1f-adult.Timeline.Elapsed); yield return new WaitForSeconds(2.1f);
            Assert.AreEqual(GamblingPhase.PassingMonths,adult.Gambling.Phase); Assert.GreaterOrEqual(adult.Gambling.CalendarMonth,1);
            yield return Capture("Logs/youngadult-gambling-calendar.png",new Vector3(3,3,16),new Vector3(3,3,20));
            Assert.IsFalse(state.HasGamblingBankruptcy); float deadline=Time.realtimeSinceStartup+5;
            while(adult.Gambling.Phase!=GamblingPhase.Loss) { Assert.Less(Time.realtimeSinceStartup,deadline); yield return null; }
            Assert.IsFalse(state.HasGamblingBankruptcy);
            while(adult.Gambling.IsPlaying) { Assert.Less(Time.realtimeSinceStartup,deadline); yield return null; }
            Assert.AreEqual(75,state.Stats.GetValue(StatType.Stress)); Assert.AreEqual(38,state.Relationship.Trust); Assert.IsTrue(state.HasGamblingBankruptcy);
            Assert.IsTrue(agent.enabled); Assert.IsTrue(adult.Transition.SourceExitOpen); Assert.IsFalse(adult.Gambling.Play());
            var after=state.ToSaveData(); yield return Visit("gambling"); CollectionAssert.AreEqual(after.statValues,state.ToSaveData().statValues);
            Assert.IsNull(state.FinalYoungAdultSnapshot); Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="gambling_montage_started"));
            yield return Capture("Logs/youngadult-gambling-loss.png",new Vector3(3,3,16),new Vector3(3,3,20));
        }
        [UnityTest] public IEnumerator LowTrust_ContactsPreserveOriginalActivityForFifteenSeconds_WithoutScoreOrPush()
        {
            adult.Timeline.Advance(90.1f-adult.Timeline.Elapsed); yield return null;
            var state=GameState.Current; state.Relationship.AdjustTrust(-21,"test"); var receiver=fly.GetComponent<FlyContactReceiver>();
            fly.enabled=true; fly.PickAndMoveToNext(); string original=fly.CurrentDestinationId; Assert.IsNotNull(original);
            var position=fly.transform.position;
            for(int i=0;i<3;i++)
            {
                receiver.ReceiveContact(PushSource.ElectricStick,Vector3.right,Time.time);
                Assert.IsTrue(fly.IsProtectingActivity); Assert.IsFalse(fly.IsBeingPushed); Assert.IsFalse(fly.IsComplying); Assert.AreEqual(0,receiver.Compliance.Score);
                fly.PickAndMoveToNext(); Assert.AreEqual(original,fly.CurrentDestinationId); yield return new WaitForSeconds(1.1f);
            }
            Assert.Greater(Vector3.Distance(position,fly.transform.position),.1f); Assert.AreEqual(3,state.Relationship.TotalInterventionCount);
            float until=receiver.Compliance.SuspendedUntil; state.Relationship.AdjustTrust(100,"test");
            receiver.ReceiveContact(PushSource.ElectricStick,Vector3.right,Time.time); Assert.AreEqual(until,receiver.Compliance.SuspendedUntil);
            adult.Timeline.Advance(480.1f-adult.Timeline.Elapsed); yield return null; Assert.AreEqual(original,fly.CurrentDestinationId);
            while(Time.time<until-.3f) { Assert.AreEqual(original,fly.CurrentDestinationId); Assert.AreEqual(0,receiver.Compliance.Score); yield return null; }
            Assert.IsTrue(fly.IsProtectingActivity); yield return new WaitForSeconds(.5f); Assert.IsFalse(fly.IsProtectingActivity);
            Assert.AreEqual(RoomController.ExitDestinationId,fly.CurrentDestinationId);
            Assert.AreEqual(0,state.EventLog.Count(e=>e.eventType=="compliance_enter"));
        }
        [UnityTest] public IEnumerator TrustAtThirtyUsesNormalContact_HighTrustComplianceReturnsToPreferenceChoice()
        {
            adult.Timeline.Advance(90.1f-adult.Timeline.Elapsed); yield return null;
            var state=GameState.Current; var receiver=fly.GetComponent<FlyContactReceiver>(); state.Relationship.AdjustTrust(-20,"test");
            receiver.ReceiveContact(PushSource.ElectricStick,Vector3.right,Time.time); Assert.IsFalse(receiver.Compliance.IsSuspended(Time.time)); Assert.AreEqual(2,receiver.Compliance.Score);
            yield return new WaitForSeconds(1.1f); receiver.Compliance.EnterRoom(); state.Relationship.AdjustTrust(100,"test"); state.Stats.ApplyDelta(StatType.Rebellion,100,"test");
            fly.enabled=true;
            for(int i=0;i<4;i++) { receiver.ReceiveContact(PushSource.ElectricStick,Vector3.right,Time.time); yield return new WaitForSeconds(1.1f); }
            Assert.IsTrue(fly.IsComplying); Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="compliance_enter"));
            receiver.CompleteItemVisit(new ItemVisitContext(true,CoercionKind.None)); Assert.IsFalse(fly.IsComplying);
            fly.PickAndMoveToNext(); Assert.IsNotNull(fly.LastChoice); Assert.Greater(fly.LastChoice.Candidates.Count,1);
            Assert.IsTrue(fly.LastChoice.Candidates.All(c=>c.Probability>0));
        }
        [UnityTest] public IEnumerator LetterAndWorkshopRevisits_RecoverOnlyOnce_RecentContactPreventsAutonomousReconnect()
        {
            var state=GameState.Current; yield return Visit("letter"); yield return Visit("workshop"); yield return Visit("independence"); yield return Visit("quit");
            var before=state.ToSaveData(); state.Relationship.RecordIntervention(Time.time);
            yield return Visit("letter"); Assert.IsTrue(state.HasContactBlocked); Assert.AreEqual(24+8f,state.Relationship.Trust);
            yield return new WaitForSeconds(10.1f); yield return Visit("letter"); Assert.IsFalse(state.HasContactBlocked); Assert.AreEqual(34+8f,state.Relationship.Trust);
            yield return Visit("workshop"); Assert.IsFalse(state.HasResigned);
            yield return Visit("letter"); yield return Visit("workshop"); CollectionAssert.AreEqual(before.statValues,state.ToSaveData().statValues);
            Assert.AreEqual(34+8f,state.Relationship.Trust); Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="youngadult_reconnect")); Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="youngadult_new_job"));
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint)); Assert.AreEqual(YoungAdultFlags.None,saved.youngAdultFlags); Assert.AreEqual(34+8f,saved.trust);
        }
        [UnityTest] public IEnumerator SavedNavMesh_IsOneConnectedFloor_AllThirteenSlotsReachable()
        {
            var mesh=NavMesh.CalculateTriangulation(); Assert.IsNotEmpty(mesh.indices);
            for(int i=0;i<mesh.indices.Length;i+=3)
            {
                var center=(mesh.vertices[mesh.indices[i]]+mesh.vertices[mesh.indices[i+1]]+mesh.vertices[mesh.indices[i+2]])/3;
                Assert.Less(center.y,.2f); var path=new NavMeshPath(); Assert.IsTrue(NavMesh.CalculatePath(agent.nextPosition,center,NavMesh.AllAreas,path)); Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
            }
            Assert.AreEqual(13,adult.Room.ItemSlots.Count); foreach(var slot in adult.Room.ItemSlots) Assert.IsTrue(slot.TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out _),slot.ItemId);
            Assert.IsTrue(agent.Warp(new Vector3(6,.3f,11))); agent.isStopped=false; agent.SetDestination(new Vector3(18,0,11));
            float deadline=Time.realtimeSinceStartup+7; while(fly.transform.position.x<17.5f) { Assert.Less(Time.realtimeSinceStartup,deadline); yield return null; }
            Hold(); File.WriteAllText("Logs/youngadult-navmesh.txt",$"{mesh.indices.Length/3} triangles; 1 connected floor; 13 reachable items\n");
            yield return Capture("Logs/youngadult-overview.png",new Vector3(12,21,-1),new Vector3(12,0,11));
        }
        [UnityTest] public IEnumerator ActualExit_CapturesAllEndingInputsOnce_ShowsLastLetterAndChildOpeningDoor_ThenPersistsInMiddleAge()
        {
            var state=GameState.Current; state.RecordDelinquency("adolescence_theft",Time.time);
            foreach(string id in new[]{"investment","independence","quit","gambling","letter","workshop"}) yield return Visit(id);
            Assert.IsTrue(state.HasHighRiskContract && state.HasGamblingBankruptcy); Assert.IsFalse(state.HasContactBlocked || state.HasResigned);
            adult.Timeline.Advance(360.1f-adult.Timeline.Elapsed); yield return null; adult.Timeline.Advance(1); yield return null;
            Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="youngadult_tomorrow"));
            adult.Timeline.Advance(480.1f-adult.Timeline.Elapsed); yield return null; Hold(); Assert.IsNull(state.FinalYoungAdultSnapshot);
            var flow=adult.Transition; var chair=Object.FindAnyObjectByType<WheelchairLocomotion>(); float deadline=Time.realtimeSinceStartup+12;
            while(flow.Phase==TransitionPhase.Idle) { Assert.Less(Time.realtimeSinceStartup,deadline); Drive(chair,new Vector3(26,0,11)); yield return null; }
            Stick(Vector2.zero); yield return Phase(flow,TransitionPhase.Corridor);
            var snapshot=state.FinalYoungAdultSnapshot; Assert.IsNotNull(snapshot);
            CollectionAssert.AreEqual(state.ToSaveData().statValues,new[]{snapshot.Stress,snapshot.Intelligence,snapshot.Health,snapshot.Popularity,snapshot.Love,snapshot.Rebellion});
            Assert.AreEqual(state.Relationship.Trust,snapshot.Trust); Assert.AreEqual(state.LastYoungAdultLetter,snapshot.LastLetter);
            Assert.IsTrue(snapshot.DelinquencyRecord && snapshot.GamblingBankruptcy && snapshot.HighRiskContract); Assert.IsFalse(snapshot.ContactBlocked || snapshot.Resigned);
            Assert.AreEqual(snapshot.ToLogString(),state.EventLog.Single(e=>e.eventType=="youngadult_final_snapshot").payload);
            Assert.AreEqual(snapshot.LastLetter,flow.Corridor.Recap.YoungAdultLetterLine);
            yield return Capture("Logs/youngadult-corridor-letter.png",new Vector3(31,1.7f,11.9f),new Vector3(31,1.7f,9));
            deadline=Time.realtimeSinceStartup+12;
            while(chair.transform.position.x<35) { Assert.Less(Time.realtimeSinceStartup,deadline); Drive(chair,new Vector3(35.3f,0,11)); yield return null; }
            Stick(Vector2.zero); yield return new WaitForSecondsRealtime(3.2f);
            var door=flow.Corridor.GetComponentInChildren<YoungAdultCorridorDoor>(); Assert.IsNotNull(door); Assert.AreEqual(70,door.Angle,.01f);
            Assert.IsFalse(flow.Corridor.transform.Find("Child Silhouette Body").gameObject.activeSelf);
            Assert.Less(door.transform.Find("Door").TransformPoint(new Vector3(470,0,0)).z,door.transform.position.z,"Door must open into the corridor, not through its wall.");
            yield return Capture("Logs/youngadult-corridor-door.png",new Vector3(36,1.6f,10),new Vector3(36,1.6f,13));
            deadline=Time.realtimeSinceStartup+12;
            while(flow.Phase==TransitionPhase.Corridor) { Assert.Less(Time.realtimeSinceStartup,deadline); Drive(chair,new Vector3(40,0,11)); yield return null; }
            Stick(Vector2.zero); yield return Phase(flow,TransitionPhase.Complete); Assert.AreEqual("06_MiddleAge",state.CurrentRoomId);
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint)); Assert.AreEqual(snapshot.ToLogString(),saved.finalYoungAdultSnapshot.ToLogString());
            File.WriteAllText("Logs/youngadult-exit.json",JsonUtility.ToJson(saved,true));
            state.Stats.ApplyDelta(StatType.Stress,-50,"test"); state.CaptureFinalYoungAdultSnapshot(Time.time); Assert.AreSame(snapshot,state.FinalYoungAdultSnapshot);
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
