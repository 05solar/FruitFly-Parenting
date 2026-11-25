using System;
using System.Collections;
using System.Linq;
using FruitFlyParenting.Comfort;
using FruitFlyParenting.Core;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Tool;
using FruitFlyParenting.Wheelchair;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Tests.PlayMode
{
    public sealed class DesktopControlsTests
    {
        Keyboard keys; Mouse mouse; XRController controller;
        DesktopControls desktop; WheelchairLocomotion chair; ComfortController comfort;
        string key;
        InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif
        void Loaded(Scene scene, LoadSceneMode mode)
        { foreach(var value in Object.FindObjectsByType<ComfortController>()) value.UsePreferencesKey(key); }
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1; key="desktop-test-"+Guid.NewGuid();
            previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keys=InputSystem.AddDevice<Keyboard>(); mouse=InputSystem.AddDevice<Mouse>();
            SceneManager.sceneLoaded+=Loaded;
            foreach(var state in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(state.gameObject);
            yield return null; yield return SceneManager.LoadSceneAsync("03_RoomShellTest"); yield return null; yield return null;
            Object.FindAnyObjectByType<FlyAgent>().gameObject.SetActive(false);
            chair=Object.FindAnyObjectByType<WheelchairLocomotion>(); desktop=chair.GetComponent<DesktopControls>(); comfort=chair.GetComponent<ComfortController>();
            Assert.That(desktop.IsDesktopMode,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(comfort!=null && comfort.MenuOpen) comfort.SetMenuOpen(false);
            // Stop the real room before disposing the persistent state used by the prologue test.
            if(SceneManager.GetActiveScene().name!="03_RoomShellTest")
            {
                yield return SceneManager.LoadSceneAsync("03_RoomShellTest"); yield return null;
                desktop=Object.FindAnyObjectByType<DesktopControls>();
            }
            SceneManager.sceneLoaded-=Loaded;
            if(controller!=null) InputSystem.RemoveDevice(controller);
            InputSystem.RemoveDevice(keys); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
#endif
            PlayerPrefs.DeleteKey(key); Time.timeScale=1; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if(desktop!=null) Object.Destroy(desktop.gameObject);
            foreach(var state in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(state.gameObject);
            yield return null;
        }
        [UnityTest] public IEnumerator EnterStartsPrologueOnceAndRespectsSettingsPause()
        {
            yield return SceneManager.LoadSceneAsync("00_Bootstrap"); yield return null; yield return null;
            chair=Object.FindAnyObjectByType<WheelchairLocomotion>(); desktop=chair.GetComponent<DesktopControls>(); comfort=chair.GetComponent<ComfortController>();
            var prologue=Object.FindAnyObjectByType<PrologueController>();
            var state=GameState.Current;
            comfort.SetMenuOpen(true);
            float elapsed=prologue.Elapsed;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Enter)); yield return null; yield return null;
            Assert.That(prologue.Elapsed,Is.EqualTo(elapsed));
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("00_Bootstrap"));
            InputSystem.QueueStateEvent(keys,new KeyboardState()); yield return null;
            comfort.SetMenuOpen(false);
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Enter)); yield return null; yield return null;
            prologue.RequestStart(); // Repeated input must not launch a second scene transition.
            InputSystem.QueueStateEvent(keys,new KeyboardState());
            float deadline=Time.realtimeSinceStartup+10;
            while(SceneManager.GetActiveScene().name!="01_Infant" || prologue!=null)
            { Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline)); yield return null; }
            Assert.That(state.EventLog.Count(e=>e.eventType=="prologue_complete"),Is.EqualTo(1));
            Assert.That(GameState.Current,Is.SameAs(state));
            Assert.That(Object.FindAnyObjectByType<WheelchairLocomotion>().enabled,Is.True);
        }

        [UnityTest] public IEnumerator KeyboardMovesTurnsStopsAndRespectsWallsAndPause()
        {
            var start=chair.transform.position;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.UpArrow)); yield return new WaitForSeconds(1);
            Assert.That(Vector3.Distance(start,chair.transform.position),Is.GreaterThan(.9f),"Desktop travel should accelerate at four times the VR rate.");
            InputSystem.QueueStateEvent(keys,new KeyboardState()); yield return null; yield return null;
            start=chair.transform.position; yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(start,chair.transform.position),Is.LessThan(.001f));
            var rotation=chair.transform.rotation;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.RightArrow)); yield return new WaitForSeconds(1);
            Assert.That(Quaternion.Angle(rotation,chair.transform.rotation),Is.GreaterThan(10));
            comfort.SetMenuOpen(true); start=chair.transform.position; rotation=chair.transform.rotation;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W,Key.D)); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(chair.transform.position,Is.EqualTo(start)); Assert.That(Quaternion.Angle(rotation,chair.transform.rotation),Is.LessThan(.01f));
            comfort.SetMenuOpen(false);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=chair.transform.position+chair.transform.forward*.8f+Vector3.up;
            wall.transform.rotation=chair.transform.rotation; wall.transform.localScale=new Vector3(4,2,.2f);
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W)); yield return new WaitForSeconds(1.5f);
            Assert.That(Vector3.Distance(start,chair.transform.position),Is.LessThan(.45f));
            Object.Destroy(wall);
        }
        [UnityTest] public IEnumerator EquipmentShowsLeftJoystickAndBatonWithoutChangingPhysics()
        {
            var art=chair.GetComponent<PlayerEquipmentArt>();
            Assert.That(art,Is.Not.Null);Assert.That(art.Joystick,Is.Not.Null);
            var visual=chair.transform.Find(PlayerEquipmentArt.ChairName);
            Assert.That(visual.GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(chair.GetComponent<CharacterController>().radius,Is.EqualTo(.4f).Within(.001f));
            var viewport=Camera.main.WorldToViewportPoint(art.Joystick.TransformPoint(new Vector3(0,.12f,0)));
            Assert.That(viewport.z,Is.GreaterThan(.05f));
            Assert.That(viewport.x,Is.InRange(.1f,.48f),"Joystick must be on the left in the seated forward view.");
            Assert.That(viewport.y,Is.InRange(.08f,.65f),"Joystick must remain above the bottom of the screen.");
            var neutral=art.Joystick.localRotation;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.UpArrow));yield return new WaitForSeconds(.25f);
            Assert.That(Quaternion.Angle(neutral,art.Joystick.localRotation),Is.GreaterThan(10));
            InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(.3f);
            Assert.That(Quaternion.Angle(neutral,art.Joystick.localRotation),Is.LessThan(1));
            var tool=chair.GetComponentInChildren<LoveStickTool>(true);
            Assert.That(tool.GetComponent<Collider>().enabled,Is.False,"Idle art must not activate contact.");
            var baton=tool.transform.parent.Find("Electric Baton Art");
            Assert.That(baton,Is.Not.Null);Assert.That(baton.GetComponentsInChildren<Collider>(),Is.Empty);
            var tip=Camera.main.WorldToViewportPoint(baton.TransformPoint(new Vector3(0,0,.52f)));
            Assert.That(tip.x,Is.InRange(.5f,.95f));Assert.That(tip.y,Is.InRange(.1f,.8f));
            int count=chair.GetComponentsInChildren<Renderer>().Length;art.Install();
            Assert.That(chair.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(count));
            var camera=Camera.main;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;
            var target=new RenderTexture(1600,900,24);camera.targetTexture=target;camera.aspect=1600f/900f;
            yield return null;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            System.IO.File.WriteAllBytes("Logs/player-equipment-seated.png",image.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=oldTarget;camera.aspect=oldAspect;
            Object.Destroy(image);Object.Destroy(target);
        }
        [UnityTest] public IEnumerator RightDragLooksWithoutSteeringAndEscapeReleasesMouse()
        {
            var camera=Camera.main; var body=chair.transform.rotation; var view=camera.transform.localRotation;
            InputSystem.QueueStateEvent(mouse,new MouseState {delta=new Vector2(200,-100)}.WithButton(MouseButton.Right));
            yield return null; yield return null;
            Assert.That(Quaternion.Angle(view,camera.transform.localRotation),Is.GreaterThan(5));
            Assert.That(Quaternion.Angle(body,chair.transform.rotation),Is.LessThan(.01f));
            Assert.That(desktop.Looking,Is.True);
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Escape)); yield return null; yield return null;
            Assert.That(desktop.Looking,Is.False); Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
        }
        [UnityTest] public IEnumerator ClicksSettingsWithoutActivatingTheTool()
        {
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.F9)); yield return null; yield return null;
            Assert.That(comfort.MenuOpen,Is.False,"F9 no longer opens settings.");
            InputSystem.QueueStateEvent(keys,new KeyboardState());
            yield return new WaitForSecondsRealtime(1.1f);
            var launcher=GameObject.Find("Comfort launcher").GetComponentInChildren<GazeAction>();
            var launcherPosition=RectTransformUtility.WorldToScreenPoint(null,launcher.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=launcherPosition}.WithButton(MouseButton.Left)); yield return null; yield return null;
            Assert.That(comfort.MenuOpen,Is.True); Assert.That(desktop.ToolExtended,Is.False);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=launcherPosition}); yield return null;
            yield return new WaitForSecondsRealtime(1.1f);
            var button=GameObject.Find("Comfort settings").GetComponentsInChildren<GazeAction>().Single(a=>a.name.StartsWith("회전 느리게"));
            var buttonCanvas=button.GetComponentInParent<UnityEngine.Canvas>();
            Vector2 position=RectTransformUtility.WorldToScreenPoint(buttonCanvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : Camera.main,button.transform.position);
            float before=comfort.Preferences.turnSpeed;
            InputSystem.QueueStateEvent(mouse,new MouseState {position=position}.WithButton(MouseButton.Left)); yield return null; yield return null;
            Assert.That(comfort.Preferences.turnSpeed,Is.EqualTo(before-5)); Assert.That(desktop.ToolExtended,Is.False);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=position}); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState {position=position}.WithButton(MouseButton.Left)); yield return null; yield return null;
            Assert.That(comfort.Preferences.turnSpeed,Is.EqualTo(before-10),"Clicking the same button again must not require moving off it.");
        }
        [UnityTest] public IEnumerator MouseToolMakesOnePhysicalContactPerPress()
        {
            var fly=Object.FindObjectsByType<FlyAgent>(FindObjectsInactive.Include).First();
            fly.gameObject.SetActive(true); fly.enabled=false;
            var nav=fly.GetComponent<UnityEngine.AI.NavMeshAgent>(); nav.enabled=false;
            var tool=chair.GetComponentInChildren<LoveStickTool>(true);
            var position=new Vector2(Screen.width*.7f,Screen.height*.6f);
            int count=GameState.Current.Relationship.TotalInterventionCount;
            InputSystem.QueueStateEvent(mouse,new MouseState {position=position}.WithButton(MouseButton.Left));
            yield return new WaitForSeconds(.4f);
            var body=fly.GetComponent<Collider>();
            fly.transform.position += tool.transform.position-body.bounds.center; Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(GameState.Current.Relationship.TotalInterventionCount,Is.EqualTo(count+1));
            yield return new WaitForSeconds(.1f);
            Assert.That(GameState.Current.Relationship.TotalInterventionCount,Is.EqualTo(count+1));
            InputSystem.QueueStateEvent(mouse,new MouseState {position=position}); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState {position=position}.WithButton(MouseButton.Left));
            yield return new WaitForSeconds(.4f);
            fly.transform.position += tool.transform.position-body.bounds.center; Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(GameState.Current.Relationship.TotalInterventionCount,Is.EqualTo(count+2));
        }
        [UnityTest] public IEnumerator ToolIsContactBasedAndVrDevicesTakePriorityOverKeyboard()
        {
            var move=chair.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>();
            float desktopSpeed=move.moveSpeed;
            var tool=chair.GetComponentInChildren<LoveStickTool>(true); var tracked=Camera.main.GetComponent<TrackedPoseDriver>();
            Assert.That(tool.GetComponent<Collider>().enabled,Is.False); Assert.That(tracked.enabled,Is.False);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width*.7f,Screen.height*.6f)}.WithButton(MouseButton.Left));
            yield return new WaitForSeconds(.3f); Assert.That(desktop.ToolExtended,Is.True); Assert.That(tool.enabled,Is.True);
            InputSystem.QueueStateEvent(mouse,new MouseState()); yield return null; yield return null;
            Assert.That(tool.enabled,Is.False); Assert.That(desktop.ToolExtended,Is.False);
            controller=InputSystem.AddDevice<XRController>(); yield return null; yield return null;
            Assert.That(desktop.IsDesktopMode,Is.False); Assert.That(tracked.enabled,Is.True); Assert.That(tool.enabled,Is.True);
            Assert.That(move.moveSpeed,Is.EqualTo(desktopSpeed*.25f).Within(.001f),"XR restores the original travel speed.");
            var start=chair.transform.position;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.UpArrow)); yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(start,chair.transform.position),Is.LessThan(.001f));
            InputSystem.RemoveDevice(controller); controller=null; yield return null; yield return null;
            Assert.That(desktop.IsDesktopMode,Is.True);
            Assert.That(move.moveSpeed,Is.EqualTo(desktopSpeed).Within(.001f));
        }
    }
}
