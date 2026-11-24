using System.Collections;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Room;
using FruitFlyParenting.Wheelchair;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FruitFlyParenting.Tests.PlayMode
{
    public class EndingIntegrationTests
    {
        InputDevice left;
        string checkpoint;
        float previousScale;
        GameState injected;
        GameBalanceConfig config;
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousScale=Time.timeScale; config=ScriptableObject.CreateInstance<GameBalanceConfig>();
            foreach(var b in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(b.gameObject);
            yield return null; GameState.SetCurrent(null); Directory.CreateDirectory("Logs"); checkpoint=Path.GetFullPath("Logs/ending-test-"+System.Guid.NewGuid().ToString("N")+".json");
            InputSystem.RegisterLayout(@"{""name"":""Step12Controller"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]}]}");
            left=InputSystem.AddDevice("Step12Controller"); InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);
            SceneManager.sceneLoaded+=Inject;
        }
        void Inject(Scene scene,LoadSceneMode mode)
        {
            if(injected!=null) { GameState.SetCurrent(injected); injected=null; }
            var infant=Object.FindAnyObjectByType<InfantRoomController>(); if(infant!=null) infant.CheckpointPath=checkpoint;
            var middle=Object.FindAnyObjectByType<MiddleAgeRoomController>(); if(middle!=null) middle.CheckpointPath=checkpoint;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded-=Inject; Stick(Vector2.zero); InputSystem.RemoveDevice(left);
            foreach(var b in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(b.gameObject);
            foreach(var p in Object.FindObjectsByType<PrologueController>()) Object.Destroy(p.gameObject);
            yield return SceneManager.LoadSceneAsync("03_RoomShellTest");
            foreach(var b in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(b.gameObject);
            yield return null; GameState.SetCurrent(null); Time.timeScale=previousScale; Object.Destroy(config);
            if(File.Exists(checkpoint)) File.Delete(checkpoint);
        }
        void Stick(Vector2 value)=>InputSystem.QueueDeltaStateEvent((StickControl)left["thumbstick"],value);
        void Drive(WheelchairLocomotion chair,Vector3 target)
        { var d=target-chair.transform.position; float turn=Mathf.DeltaAngle(chair.transform.eulerAngles.y,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg); Stick(new Vector2(Mathf.Clamp(turn/15,-1,1),Mathf.Abs(turn)<30 ? 1 : 0)); }
        static IEnumerator Until(System.Func<bool> predicate,float seconds=10)
        { float deadline=Time.realtimeSinceStartup+seconds; while(!predicate()) { Assert.Less(Time.realtimeSinceStartup,deadline); yield return null; } }
        GameState Fixture(int ending)
        {
            var s=new GameState(config,1200+ending); s.CurrentRoomId=MiddleAgeRoomController.RoomId;
            s.Stats.ApplyDelta(StatType.Health,10,"fixture");
            if(ending==1) s.ApplyYoungAdultFlags(YoungAdultFlags.GamblingBankruptcy,"youngadult_gambling",0);
            if(ending==2)
            {
                s.Stats.ApplyDelta(StatType.Love,50,"fixture"); s.Relationship.AdjustTrust(30,"fixture");
                foreach(string id in new[]{"infant_hug","infant_ball","youngadult_dinner"}) s.LogEvent(0,id.StartsWith("youngadult") ? YoungAdultRoomController.RoomId : InfantRoomController.RoomId,"item_first",id,
                    new ItemExperience { itemId=id,displayName=id=="infant_hug" ? "푹신한 포옹 의자" : id=="infant_ball" ? "친구 곁 공놀이" : "친구·연인의 저녁 식탁",tags=new[]{"관계"},autonomous=true,narrative="함께 보낸 시간" });
            }
            if(ending==6) { s.Stats.ApplyDelta(StatType.Stress,25,"fixture"); s.ApplyYoungAdultFlags(YoungAdultFlags.HighRiskContract,"test",0); }
            if(ending==7) { s.Relationship.AdjustTrust(-30,"fixture"); s.ApplyYoungAdultFlags(YoungAdultFlags.ContactBlocked,"test",0); }
            using(s.TrackImpact(1,YoungAdultRoomController.RoomId,"visit","결정적 방문")) s.Stats.ApplyDelta(StatType.Stress,-5,"test");
            s.CaptureFinalYoungAdultSnapshot(480); return s;
        }
        IEnumerator LoadEnding(int priority)
        { injected=Fixture(priority); yield return SceneManager.LoadSceneAsync("06_MiddleAge"); yield return null; Time.timeScale=20; }
        [UnityTest] public IEnumerator ThreeDistinctPresentations_KeepFinalState_AndLastFifteenSecondsQuiet()
        {
            foreach(int ending in new[]{1,2,7})
            {
                yield return LoadEnding(ending); var middle=Object.FindAnyObjectByType<MiddleAgeRoomController>(); Assert.AreEqual(ending,middle.Result.priority);
                var before=GameState.Current.ToSaveData();
                Assert.AreEqual(ending!=7,middle.transform.Find("Adult Child").gameObject.activeSelf);
                Assert.AreEqual(ending==2,middle.transform.Find("Adult Child/Star Costume").gameObject.activeSelf);
                Assert.AreEqual(ending==7,middle.transform.Find("Birthday Candle").gameObject.activeSelf);
                yield return Until(()=>middle.IsQuiet,15); float quietAt=middle.Elapsed; var camera=Camera.main.transform; var at=camera.position; var rotation=camera.rotation;
                Assert.AreEqual("",middle.transform.Find("Ending Scene Board/Ending Scene").GetComponent<UnityEngine.UI.Text>().text); Assert.IsFalse(middle.CardVisible);
                yield return new WaitForSeconds(10); Assert.IsTrue(middle.IsQuiet); Assert.Less(Vector3.Distance(at,camera.position),.001f); Assert.Less(Quaternion.Angle(rotation,camera.rotation),.001f);
                yield return Capture("Logs/ending-scene-"+ending+".png",new Vector3(12,3,8),new Vector3(12,1.5f,16));
                yield return Until(()=>middle.CardVisible,5); Assert.GreaterOrEqual(middle.Elapsed-quietAt,14.5f);
                StringAssert.Contains(middle.Result.title,middle.Presentation.CardText); StringAssert.Contains("결정적 방문",middle.Presentation.CardText);
                CollectionAssert.AreEqual(before.statValues,GameState.Current.ToSaveData().statValues); Assert.AreEqual(before.trust,GameState.Current.Relationship.Trust);
                if(ending==2) yield return Capture("Logs/ending-card-magic.png",new Vector3(12,2.1f,8),new Vector3(12,2.1f,12.2f));
            }
        }
        [UnityTest] public IEnumerator GazeOpensGallery_SelectionsReuseActualRecaps_AndRestartClearsRun()
        {
            yield return LoadEnding(2); var middle=Object.FindAnyObjectByType<MiddleAgeRoomController>(); yield return Until(()=>middle.CardVisible,16);
            var camera=Camera.main; var button=middle.transform.Find("Ending Card/회고 갤러리");
            var ray=new Ray(camera.transform.position,button.position-camera.transform.position);
            Assert.IsFalse(Physics.RaycastAll(ray,Vector3.Distance(camera.transform.position,button.position)).Any(h=>h.collider.name=="Small Table"),"The table must not hide the gallery button.");
            camera.transform.LookAt(button);
            yield return Until(()=>middle.Presentation.GalleryVisible,5);
            Assert.AreEqual(6,middle.transform.Find("Life Gallery").GetComponentsInChildren<GazeAction>().Count(g=>g.name.StartsWith("Miniature")));
            for(int i=0;i<6;i++)
            {
                middle.Presentation.SelectRoom(i); var recap=CorridorRecapModel.Build(GameState.Current.EventLog,EndingPresentation.Rooms[i],3);
                foreach(var memory in recap.Memories) StringAssert.Contains(memory.Experience.narrative,middle.Presentation.GalleryText);
            }
            middle.Presentation.SelectRoom(1); yield return Capture("Logs/ending-gallery.png",new Vector3(12,2.1f,8),new Vector3(12,2.1f,12.1f));
            int oldSeed=GameState.Current.RunSeed;
            middle.transform.Find("Life Gallery/다시 시작").GetComponent<GazeAction>().Activate();
            yield return Until(()=>SceneManager.GetActiveScene().name=="00_Bootstrap"); yield return null;
            Assert.AreNotEqual(oldSeed,GameState.Current.RunSeed); Assert.IsNull(GameState.Current.FinalYoungAdultSnapshot);
            Assert.AreEqual(0,GameState.Current.Relationship.TotalInterventionCount); Assert.AreEqual(0,GameState.Current.Relationship.AutonomousVisitCount);
            Assert.AreEqual(YoungAdultFlags.None,GameState.Current.ToSaveData().youngAdultFlags); Assert.IsFalse(GameState.Current.HasDelinquencyRecord);
            Assert.AreEqual(50,GameState.Current.Relationship.Trust); Assert.AreEqual(0,GameState.Current.Stats.GetValue(StatType.Rebellion));
            Assert.IsFalse(GameState.Current.EventLog.Any(e=>e.eventType=="item_first"));
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint)); Assert.AreEqual(GameState.Current.RunSeed,saved.runSeed);
        }
        [UnityTest] public IEnumerator FullRun_PrologueThroughFourRoomsAndCorridors_ToEndingCardAndGallery()
        {
            yield return SceneManager.LoadSceneAsync("00_Bootstrap"); yield return null;
            var prologue=Object.FindAnyObjectByType<PrologueController>(); var state=GameState.Current; var initial=state.ToSaveData();
            var cameraAt=Camera.main.transform.position; Stick(Vector2.up); Time.timeScale=20;
            yield return new WaitForSeconds(30); Assert.IsFalse(Object.FindAnyObjectByType<WheelchairLocomotion>().enabled);
            Assert.Less(Vector3.Distance(cameraAt,Camera.main.transform.position),.001f); CollectionAssert.AreEqual(initial.statValues,state.ToSaveData().statValues);
            Assert.AreEqual(0,state.Relationship.TotalInterventionCount);
            yield return Capture("Logs/prologue-memory-room.png",new Vector3(12,2,8),new Vector3(12,1.2f,12)); Stick(Vector2.zero);
            yield return Until(()=>SceneManager.GetActiveScene().name=="01_Infant",8); yield return Until(()=>Object.FindAnyObjectByType<PrologueController>()==null,5); Time.timeScale=20;
            Assert.AreSame(state,GameState.Current); Assert.IsTrue(state.EventLog.Any(e=>e.eventType=="prologue_complete"));
            var chair=Object.FindAnyObjectByType<WheelchairLocomotion>();
            for(int roomIndex=0;roomIndex<4;roomIndex++)
            {
                RoomTransitionController flow=roomIndex==0 ? Object.FindAnyObjectByType<InfantRoomController>().Transition :
                    roomIndex==1 ? Object.FindAnyObjectByType<ChildhoodRoomController>().Transition : roomIndex==2 ? Object.FindAnyObjectByType<AdolescenceRoomController>().Transition : Object.FindAnyObjectByType<YoungAdultRoomController>().Transition;
                yield return Until(()=>flow.SourceExitOpen,45);
                float origin=roomIndex*38,deadline=Time.realtimeSinceStartup+12;
                while(flow.Phase==TransitionPhase.Idle) { Assert.Less(Time.realtimeSinceStartup,deadline); Drive(chair,new Vector3(origin+26,0,11)); yield return null; }
                Stick(Vector2.zero); yield return Until(()=>flow.Phase==TransitionPhase.Corridor);
                Assert.Greater(flow.Corridor.Recap.Recap.Memories.Length,0);
                deadline=Time.realtimeSinceStartup+12;
                while(flow.Phase==TransitionPhase.Corridor) { Assert.Less(Time.realtimeSinceStartup,deadline); Drive(chair,new Vector3(origin+40,0,11)); yield return null; }
                Stick(Vector2.zero); yield return Until(()=>flow.Phase==TransitionPhase.Complete);
            }
            Assert.AreEqual("06_MiddleAge",state.CurrentRoomId); var middle=Object.FindAnyObjectByType<MiddleAgeRoomController>();
            float arriveDeadline=Time.realtimeSinceStartup+12;
            while(middle.Elapsed==0) { Assert.Less(Time.realtimeSinceStartup,arriveDeadline); Drive(chair,new Vector3(164,0,9)); yield return null; }
            Stick(Vector2.zero); yield return Until(()=>middle.CardVisible,16);
            Assert.IsTrue(state.FinalYoungAdultSnapshot.IsCaptured); Assert.IsNotNull(middle.Result);
            Assert.AreEqual(4,state.EventLog.Count(e=>e.eventType=="room_exit_snapshot"));
            foreach(var impact in EndingEvaluator.DecisiveExperiences(state.EventLog)) StringAssert.Contains(impact.label,middle.Presentation.CardText);
            middle.Presentation.ShowGallery(); for(int i=1;i<=4;i++) { middle.Presentation.SelectRoom(i); Assert.IsFalse(middle.Presentation.GalleryText.Contains("경험이 없어요")); }
            var saved=JsonUtility.FromJson<GameStateSaveData>(File.ReadAllText(checkpoint)); Assert.AreEqual(state.FinalYoungAdultSnapshot.ToLogString(),saved.finalYoungAdultSnapshot.ToLogString());
            File.WriteAllText("Logs/ending-full-playthrough.json",JsonUtility.ToJson(saved,true));
            File.WriteAllText("Logs/ending-full-playthrough.txt",string.Join("\n",state.EventLog.Where(e=>e.eventType=="room_exit_snapshot").Select(e=>e.roomId+" "+e.payload))+"\nEnding: "+middle.Result.title);
        }
        [UnityTest] public IEnumerator DebtResponseChangesOnlyDialogue_AndQuietPeriodClosesOptionalChoices()
        {
            yield return LoadEnding(6); var middle=Object.FindAnyObjectByType<MiddleAgeRoomController>(); Assert.AreEqual(6,middle.Result.priority);
            var before=GameState.Current.ToSaveData(); middle.transform.Find("Debt Response/Response 1").GetComponent<GazeAction>().Activate();
            StringAssert.Contains("감당할 방법",middle.ChildLine); Assert.IsFalse(middle.transform.Find("Debt Response").gameObject.activeSelf);
            CollectionAssert.AreEqual(before.statValues,GameState.Current.ToSaveData().statValues); Assert.AreEqual(before.trust,GameState.Current.Relationship.Trust);
            Assert.AreEqual(1,GameState.Current.EventLog.Count(e=>e.eventType=="ending_response"));
            yield return Until(()=>middle.CardVisible,16); StringAssert.Contains(middle.ChildLine,middle.Presentation.CardText);
        }
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
