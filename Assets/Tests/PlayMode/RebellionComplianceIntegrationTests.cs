using System.Collections;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FruitFlyParenting.Tests.PlayMode
{
    public class RebellionComplianceIntegrationTests
    {
        FlyAgent fly;
        FlyContactReceiver receiver;
        NavMeshAgent agent;
        GameBalanceConfig config;
        float oldScale;
        [UnitySetUp] public IEnumerator SetUp()
        {
            oldScale=Time.timeScale; Time.timeScale=4f;
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            yield return SceneManager.LoadSceneAsync("03_RoomShellTest"); yield return null;
            fly=Object.FindAnyObjectByType<FlyAgent>(); agent=fly.GetComponent<NavMeshAgent>(); receiver=fly.GetComponent<FlyContactReceiver>();
            config=ScriptableObject.CreateInstance<GameBalanceConfig>();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale=oldScale; Object.Destroy(config);
            foreach(var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
        }
        IEnumerator AccumulateAt(Vector3 position, Vector3 direction, int count)
        {
            // Hold the body only while accumulating so the navigation assertion has a known start.
            fly.enabled=false;
            for(int i=0;i<count;i++)
            {
                Assert.IsTrue(agent.Warp(position)); agent.ResetPath(); agent.isStopped=true;
                receiver.ReceiveContact(PushSource.ElectricStick,-direction,Time.time);
                if(i<count-1) yield return new WaitForSeconds(1.05f);
            }
            fly.enabled=true;
        }
        [UnityTest] public IEnumerator R100FourStickContacts_EnterMoveThenTimeoutToAutonomy_WithExactCosts()
        {
            var state=GameState.Current; state.Stats.ApplyDelta(StatType.Rebellion,100,"test");
            var start=new Vector3(12,.3f,11);
            yield return AccumulateAt(start,Vector3.right,4);
            Assert.AreEqual(ComplianceState.Complying,receiver.Compliance.State); Assert.IsTrue(fly.IsComplying);
            Assert.IsFalse(receiver.Usage.Mood.IsAvoiding(Time.time));
            Assert.AreEqual(64.35f,state.Stats.GetValue(StatType.Stress),.001f);
            Assert.AreEqual(100,state.Stats.GetValue(StatType.Rebellion));
            Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="compliance_enter"));
            yield return new WaitForSeconds(1.1f);
            Assert.Greater(fly.transform.position.x,start.x+.5f);
            receiver.ReceiveContact(PushSource.ElectricStick,Vector3.left,Time.time);
            Assert.AreEqual(68.5f,state.Stats.GetValue(StatType.Stress),.001f);
            Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="compliance_contact"));
            yield return new WaitForSeconds(.1f);
            receiver.ReceiveContact(PushSource.ElectricStick,Vector3.left,Time.time);
            Assert.AreEqual(71.9f,state.Stats.GetValue(StatType.Stress),.001f);
            Assert.AreEqual(1,state.EventLog.Count(e=>e.eventType=="compliance_contact"),"Rapid retouch shocks but cannot charge compliance twice.");
            yield return new WaitForSeconds(8.1f);
            Assert.AreEqual(ComplianceState.Free,receiver.Compliance.State); Assert.IsFalse(fly.IsComplying);
            Assert.IsFalse(agent.isStopped); Assert.IsNotEmpty(fly.CurrentDestinationId);
        }
        [UnityTest] public IEnumerator DirectedArrivalRequiresDwell_LogsForcedVisit_RevisitDoesNotReapply()
        {
            var room=Object.FindAnyObjectByType<RoomController>();
            var slot=room.ItemSlots.Single(s=>s.ItemId=="slot_nc");
            var item=ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId="training-test"; item.statDeltas=new[]{new ItemStatDelta(StatType.Intelligence,5)};
            slot.AssignDefinition(item); room.ResetVisits();
            var effects=new ItemEffectApplier(config,GameState.Current,"03_RoomShellTest");
            int firstCount=0,revisitCount=0;
            slot.OnFirstVisit+=()=>{firstCount++;effects.Visit(item,true,true,Time.time,slot.LastVisitContext);};
            slot.OnRevisit+=()=>{revisitCount++;effects.Visit(item,false,true,Time.time,slot.LastVisitContext);};
            yield return AccumulateAt(new Vector3(12,.3f,14),Vector3.forward,3);
            Assert.AreEqual(ComplianceState.Complying,receiver.Compliance.State);
            Assert.AreEqual(0,firstCount); Assert.AreEqual(50,GameState.Current.Stats.GetValue(StatType.Intelligence));
            float deadline=Time.time+7.5f;
            while(firstCount==0 && Time.time<deadline) yield return null;
            Assert.AreEqual(1,firstCount); Assert.AreEqual(55,GameState.Current.Stats.GetValue(StatType.Intelligence));
            Assert.AreEqual(11,GameState.Current.Stats.GetValue(StatType.Rebellion)); // entry +6, strong coercion +5
            Assert.AreEqual(1,GameState.Current.EventLog.Count(e=>e.eventType=="coerced_training"));
            Assert.AreEqual(ComplianceState.Free,receiver.Compliance.State); Assert.IsFalse(fly.IsComplying);
            Assert.IsTrue(GameState.Current.EventLog.Any(e=>e.eventType=="forced_visit" && e.payload.Contains("first=True")));
            yield return AccumulateAt(new Vector3(12,.3f,14),Vector3.forward,3);
            deadline=Time.time+7.5f;
            while(revisitCount==0 && Time.time<deadline) yield return null;
            Assert.AreEqual(1,revisitCount); Assert.AreEqual(55,GameState.Current.Stats.GetValue(StatType.Intelligence));
            Assert.AreEqual(17,GameState.Current.Stats.GetValue(StatType.Rebellion)); // only another entry +6
            Assert.AreEqual(ComplianceState.Free,receiver.Compliance.State);
            Assert.IsTrue(GameState.Current.EventLog.Any(e=>e.eventType=="forced_visit" && e.payload.Contains("first=False")));
            Object.Destroy(item);
        }
        [UnityTest] public IEnumerator WallObstructionChoosesReachableFallback_AndAgentActuallyMoves()
        {
            yield return AccumulateAt(new Vector3(22,.3f,8),Vector3.right,3);
            Assert.IsTrue(fly.UsedComplianceFallback);
            Assert.IsTrue(fly.IsComplying);
            var path=new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(agent.nextPosition,fly.ComplianceDestination,agent.areaMask,path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
            float before=fly.transform.position.x;
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(fly.transform.position.x,before+.1f); Assert.Less(fly.transform.position.x,24f);
            Assert.AreEqual(NavMeshPathStatus.PathComplete,agent.pathStatus);
            yield return Capture("Logs/compliance-wall.png");
        }
        [UnityTest] public IEnumerator DoorOpeningDefersExitUntilComplianceReleases()
        {
            yield return AccumulateAt(new Vector3(12,.3f,11),Vector3.right,3);
            var room=Object.FindAnyObjectByType<RoomController>();
            room.ConfigureExit(true,false,new Vector3(23,0,12.1f),3);
            var directed=fly.ComplianceDestination;
            fly.HeadToExit();
            Assert.IsTrue(fly.IsComplying);
            Assert.Less(Vector3.Distance(directed,agent.destination),.01f);
            yield return new WaitForSeconds(8.1f);
            Assert.IsFalse(fly.IsComplying);
            Assert.AreEqual(RoomController.ExitDestinationId,fly.CurrentDestinationId);
            Assert.IsFalse(agent.isStopped);
        }
        static IEnumerator Capture(string path)
        {
            yield return null;
            var go=new GameObject("Compliance verification camera",typeof(Camera)); var camera=go.GetComponent<Camera>();
            camera.transform.position=new Vector3(20,8,4); camera.transform.LookAt(new Vector3(22,0,9));
            var target=new RenderTexture(960,540,24); var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            var active=RenderTexture.active;
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,960,540),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=null; RenderTexture.active=active; target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(go); }
        }
    }
}
