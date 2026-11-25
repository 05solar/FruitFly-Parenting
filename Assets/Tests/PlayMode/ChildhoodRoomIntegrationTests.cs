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
    public class ChildhoodRoomIntegrationTests
    {
        ChildhoodRoomController child;
        FlyAgent fly;
        NavMeshAgent agent;
        InputDevice left;
        string checkpoint;
        float scale;
        [UnitySetUp] public IEnumerator SetUp()
        {
            scale=Time.timeScale;
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            Directory.CreateDirectory("Logs"); checkpoint=Path.GetFullPath("Logs/childhood-test-"+System.Guid.NewGuid().ToString("N")+".json");
            InputSystem.RegisterLayout(@"{""name"":""Step09Controller"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]}]}");
            left=InputSystem.AddDevice("Step09Controller"); InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);
            SceneManager.sceneLoaded+=Inject;
            yield return SceneManager.LoadSceneAsync("03_Childhood"); SceneManager.sceneLoaded-=Inject;
            child=Object.FindAnyObjectByType<ChildhoodRoomController>(); fly=child.Fly; agent=fly.GetComponent<NavMeshAgent>();
            yield return null; Time.timeScale=10; Hold();
        }
        void Inject(Scene scene,LoadSceneMode mode) => Object.FindAnyObjectByType<ChildhoodRoomController>().CheckpointPath=checkpoint;
        [UnityTearDown] public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded-=Inject; if(left!=null) InputSystem.RemoveDevice(left);
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null; Time.timeScale=scale; if(File.Exists(checkpoint)) File.Delete(checkpoint);
        }
        void Hold() { fly.enabled=false; agent.isStopped=true; agent.ResetPath(); agent.velocity=Vector3.zero; }
        ItemSlot Slot(string id) => child.Room.ItemSlots.Single(s=>s.ItemId=="childhood_"+id);
        IEnumerator Approach(string id)
        {
            Hold(); Assert.IsTrue(agent.Warp(new Vector3(12,.3f,9)));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsTrue(Slot(id).TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point));
            Assert.IsTrue(agent.Warp(point+Vector3.up*agent.baseOffset));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        }
        IEnumerator Visit(string id)
        { yield return Approach(id); yield return new WaitForSeconds(Slot(id).Definition.isExtreme ? 4.2f : 2.2f); Assert.IsTrue(child.Room.VisitedItemIds.Contains("childhood_"+id)); }

        [UnityTest] public IEnumerator AllTwelveActualVisits_StackTrainingButNeverRepeatExtremeTrustOrRebellion()
        {
            int training=0; float expectedR=0,expectedTrust=50;
            foreach(var e in ChildhoodCatalog.Entries)
            {
                var before=GameState.Current.ToSaveData();
                bool repeated=(e.Stats[1]>=5 || e.Stats[2]>=5) && ++training>=3 && training<=5;
                yield return Visit(e.Id.Substring("childhood_".Length));
                for(int i=0;i<5;i++) Assert.AreEqual(Mathf.Clamp(before.statValues[i]+e.Stats[i]+(i==0 && repeated ? 3 : 0),0,100),GameState.Current.Stats.GetValue((StatType)i),e.Id+" stat "+i);
                expectedR+=e.Rebellion+(repeated ? 4 : 0); expectedTrust+=e.Trust+(e.Id=="childhood_family" ? 8f : 0);
                Assert.AreEqual(expectedR,GameState.Current.Stats.GetValue(StatType.Rebellion),e.Id);
                Assert.AreEqual(expectedTrust,GameState.Current.Relationship.Trust,e.Id);
            }
            yield return Visit("hideout"); yield return Visit("nightgame");
            Assert.AreEqual(12,child.Room.VisitedItemIds.Count); Assert.AreEqual(39,GameState.Current.Stats.GetValue(StatType.Rebellion));
            Assert.AreEqual(33+8f,GameState.Current.Relationship.Trust); Assert.IsFalse(GameState.Current.IsFlightSmokeEventTriggered);
            Assert.AreEqual(3,GameState.Current.EventLog.Count(e=>e.eventType=="repeated_training"));
            File.WriteAllText("Logs/childhood-all-visits.json",JsonUtility.ToJson(GameState.Current.ToSaveData(),true));
        }
        [UnityTest] public IEnumerator ConnectedNavMesh_AllSlotsReachable_AndAgentCrossesClassroomPlayground()
        {
            Assert.AreEqual(12,child.Room.ItemSlots.Count);
            var triangles=NavMesh.CalculateTriangulation(); Assert.IsNotEmpty(triangles.indices);
            for(int i=0;i<triangles.indices.Length;i+=3)
            {
                var center=(triangles.vertices[triangles.indices[i]]+triangles.vertices[triangles.indices[i+1]]+triangles.vertices[triangles.indices[i+2]])/3;
                Assert.Less(center.y,.2f,"No furniture-top navigation islands.");
                var path=new NavMeshPath(); Assert.IsTrue(NavMesh.CalculatePath(agent.nextPosition,center,NavMesh.AllAreas,path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status,"Unreachable triangle at "+center);
            }
            File.WriteAllText("Logs/childhood-navmesh.txt",$"Triangles: {triangles.indices.Length/3}; connected floor regions: 1; slots: 12\n");
            foreach(var slot in child.Room.ItemSlots)
            {
                Assert.IsTrue(slot.TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point),slot.ItemId);
                var path=new NavMeshPath(); Assert.IsTrue(NavMesh.CalculatePath(agent.nextPosition,point,NavMesh.AllAreas,path)); Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
            }
            var furniture=child.Room.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Furniture_")).ToArray();
            Assert.AreEqual(12,furniture.Length);
            for(int a=0;a<furniture.Length;a++) for(int b=a+1;b<furniture.Length;b++) Assert.IsFalse(furniture[a].bounds.Intersects(furniture[b].bounds));
            Assert.IsTrue(agent.Warp(new Vector3(6,.3f,11))); agent.isStopped=false; Assert.IsTrue(agent.SetDestination(new Vector3(18,0,11)));
            float deadline=Time.realtimeSinceStartup+6;
            while(fly.transform.position.x<17.5f) { Assert.Less(Time.realtimeSinceStartup,deadline); yield return null; }
            agent.isStopped=true;
            Assert.IsTrue(child.View.StatusText.Contains("/12"));
            yield return Capture("Logs/childhood-overview.png",new Vector3(12,21,-1),new Vector3(12,0,11));
        }
        [UnityTest] public IEnumerator ExtremeHesitation_StopsMotionWithoutReplacingChoice_ThenAllowsPhysicalPush()
        {
            yield return Approach("hideout"); fly.enabled=true;
            string chosen=fly.CurrentDestinationId; var state=GameState.Current.ToSaveData();
            // A sinusoidal sway legitimately crosses zero. Observe a window rather than one arbitrary frame.
            float until=Time.time+.5f,maxSway=0;
            while(Time.time<until) { yield return null; maxSway=Mathf.Max(maxSway,Quaternion.Angle(Quaternion.identity,fly.transform.Find("Childhood Appearance").localRotation)); }
            Assert.IsTrue(fly.IsHesitating); Assert.IsTrue(agent.isStopped); Assert.AreEqual(chosen,fly.CurrentDestinationId);
            Assert.IsFalse(child.Room.VisitedItemIds.Contains("childhood_hideout")); CollectionAssert.AreEqual(state.statValues,GameState.Current.ToSaveData().statValues);
            Assert.Greater(maxSway,.05f);
            child.Timeline.Advance(90.1f-child.Timeline.Elapsed); yield return null;
            Vector3 away=(fly.transform.position-Slot("hideout").transform.position).normalized;
            fly.GetComponent<FlyContactReceiver>().ReceiveContact(PushSource.ElectricStick,-away,Time.time);
            Assert.IsTrue(fly.IsBeingPushed); Assert.IsFalse(fly.IsHesitating);
            yield return new WaitForSeconds(.6f); Assert.IsFalse(child.Room.VisitedItemIds.Contains("childhood_hideout"));
        }
        [UnityTest] public IEnumerator Airplane_Repair_FromActualCraftVisit() { yield return PaperBranch("craft",PaperAirplaneOutcome.ApologizeAndRepair); }
        [UnityTest] public IEnumerator Airplane_Postpone_FromActualHideoutAndLowTrust() { yield return PaperBranch("hideout",PaperAirplaneOutcome.PostponeApology); }
        [UnityTest] public IEnumerator Airplane_Tomorrow_WithoutQualifyingVisits() { yield return PaperBranch(null,PaperAirplaneOutcome.TalkTomorrow); }
        IEnumerator PaperBranch(string visit,PaperAirplaneOutcome expected)
        {
            if(visit!=null) yield return Visit(visit);
            if(visit=="hideout") GameState.Current.Relationship.AdjustTrust(-10,"test");
            var before=GameState.Current.ToSaveData(); child.Timeline.Advance(210-child.Timeline.Elapsed); yield return null;
            Assert.AreEqual(expected,child.Airplane.Outcome); Assert.AreEqual(expected,child.AirplaneView.Outcome);
            Assert.AreEqual(PaperAirplaneEvent.SceneLine(expected),child.AirplaneView.Caption);
            CollectionAssert.AreEqual(before.statValues,GameState.Current.ToSaveData().statValues); Assert.AreEqual(before.trust,GameState.Current.Relationship.Trust);
            yield return new WaitForSeconds(6.2f);
            Assert.AreEqual(expected==PaperAirplaneOutcome.ApologizeAndRepair ? .03f : .44f,child.AirplaneView.WingSeparation,.005f);
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint)); Assert.AreEqual(expected,saved.childhoodAirplaneOutcome);
            yield return Capture("Logs/childhood-airplane-"+expected+".png",new Vector3(12,2,8),new Vector3(12,1.8f,12.5f));
        }
        [UnityTest] public IEnumerator CorridorTwo_ActualDrivePreservesStressSnapshotsAndShowsAirplaneMemory()
        {
            var state=GameState.Current; state.Stats.ApplyDelta(StatType.Stress,30,"test"); state.CaptureRoomExitStress(LifeStage.Infant,0);
            state.Stats.ApplyDelta(StatType.Stress,-8,"test"); child.Timeline.Advance(480.1f-child.Timeline.Elapsed); yield return null; Hold();
            Assert.IsFalse(state.ChildhoodExitStressCaptured,"Door open is not the room exit.");
            var flow=child.Transition; var chair=Object.FindAnyObjectByType<WheelchairLocomotion>();
            float deadline=Time.realtimeSinceStartup+12;
            while(flow.Phase==TransitionPhase.Idle) { Assert.Less(Time.realtimeSinceStartup,deadline); Drive(chair,new Vector3(26,0,11)); yield return null; }
            Stick(Vector2.zero); yield return Phase(flow,TransitionPhase.Corridor);
            Assert.AreEqual(80,state.InfantExitStress); Assert.AreEqual(72,state.ChildhoodExitStress); Assert.IsTrue(state.IsPressureOutfitEligible);
            Assert.AreEqual(PaperAirplaneEvent.CorridorLine(PaperAirplaneOutcome.TalkTomorrow,false),flow.Corridor.Recap.PaperAirplaneLine);
            Assert.IsNotNull(flow.Corridor.transform.Find("Paper Airplane Memory"));
            yield return Capture("Logs/childhood-corridor-two.png",new Vector3(30,1.7f,11.8f),new Vector3(30,1.7f,9));
            deadline=Time.realtimeSinceStartup+12;
            while(flow.Phase==TransitionPhase.Corridor)
            {
                Assert.Less(Time.realtimeSinceStartup,deadline); var before=chair.transform.position;
                Drive(chair,new Vector3(40,0,11)); yield return null; Assert.Less(Vector3.Distance(before,chair.transform.position),.5f);
            }
            Stick(Vector2.zero); yield return Phase(flow,TransitionPhase.Complete);
            Assert.AreSame(state,GameState.Current); Assert.AreEqual("04_Adolescence",state.CurrentRoomId);
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint));
            Assert.AreEqual(80,saved.infantExitStress); Assert.AreEqual(72,saved.childhoodExitStress);
            Assert.AreEqual(PaperAirplaneOutcome.TalkTomorrow,saved.childhoodAirplaneOutcome);
            File.WriteAllText("Logs/childhood-exit.json",JsonUtility.ToJson(saved,true));
        }
        void Stick(Vector2 value) => InputSystem.QueueDeltaStateEvent((StickControl)left["thumbstick"],value);
        void Drive(WheelchairLocomotion chair,Vector3 target)
        {
            var delta=target-chair.transform.position; float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            float turn=Mathf.DeltaAngle(chair.transform.eulerAngles.y,yaw); Stick(new Vector2(Mathf.Clamp(turn/15,-1,1),Mathf.Abs(turn)<30 ? 1 : 0));
        }
        static IEnumerator Phase(RoomTransitionController flow,TransitionPhase phase)
        { float deadline=Time.realtimeSinceStartup+8; while(flow.Phase!=phase) { Assert.Less(Time.realtimeSinceStartup,deadline,flow.Phase.ToString()); yield return null; } }
        static IEnumerator Capture(string path,Vector3 at,Vector3 target)
        {
            var main=Camera.main; var oldPosition=main.transform.position; var oldRotation=main.transform.rotation;
            var texture=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false); var previous=RenderTexture.active;
            try
            {
                main.transform.position=at; main.transform.LookAt(target); yield return null;
                main.targetTexture=texture; main.Render(); RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                var colors=pixels.GetPixels(); Assert.Greater(colors.Max(c=>c.grayscale)-colors.Min(c=>c.grayscale),.1f);
                Assert.Less(colors.Count(c=>c.r>.9f && c.b>.9f && c.g<.1f),colors.Length/100);
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally { main.targetTexture=null; RenderTexture.active=previous; main.transform.SetPositionAndRotation(oldPosition,oldRotation); texture.Release(); Object.Destroy(texture); Object.Destroy(pixels); }
        }
    }
}
