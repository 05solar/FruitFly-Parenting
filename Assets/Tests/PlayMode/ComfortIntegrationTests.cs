using System;
using System.Collections;
using System.IO;
using System.Linq;
using FruitFlyParenting.Comfort;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Wheelchair;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Tests.PlayMode
{
    public sealed class ComfortIntegrationTests
    {
        InputDevice left;
        ComfortController comfort;
        WheelchairLocomotion chair;
        string key;
        void Inject(Scene scene, LoadSceneMode mode)
        {
            foreach (var value in Object.FindObjectsByType<ComfortController>()) value.UsePreferencesKey(key);
            foreach (var room in Object.FindObjectsByType<ChildhoodRoomController>()) room.CheckpointPath = "Logs/" + key + ".json";
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            key = "step14-play-" + Guid.NewGuid(); Time.timeScale = 1;
            InputSystem.RegisterLayout(@"{""name"":""ComfortTestController"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]}]}");
            left = InputSystem.AddDevice("ComfortTestController"); InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
            foreach (var b in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(b.gameObject);
            yield return null;
            SceneManager.sceneLoaded += Inject;
            yield return SceneManager.LoadSceneAsync("03_RoomShellTest"); yield return null;
            comfort = Object.FindAnyObjectByType<ComfortController>(); chair = Object.FindAnyObjectByType<WheelchairLocomotion>();
            Assert.That(comfort, Is.Not.Null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= Inject;
            if (comfort != null && comfort.MenuOpen) comfort.SetMenuOpen(false);
            if (left != null) InputSystem.RemoveDevice(left);
            PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); Time.timeScale = 1;
            if (File.Exists("Logs/" + key + ".json")) File.Delete("Logs/" + key + ".json");
            foreach (var b in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(b.gameObject);
            yield return null;
        }
        void Stick(Vector2 value) => InputSystem.QueueDeltaStateEvent((StickControl)left["thumbstick"], value);

        [UnityTest] public IEnumerator MovingVignetteNeutralStopAndTurnPreferenceWorkThroughActualProviders()
        {
            Object.FindAnyObjectByType<FlyAgent>().gameObject.SetActive(false);
            Stick(Vector2.up); yield return new WaitForSeconds(.8f);
            Assert.That(comfort.VignetteAlpha, Is.GreaterThan(.1f));
            Stick(Vector2.zero); yield return null; yield return null;
            var position = chair.transform.position; var rotation = chair.transform.rotation;
            yield return new WaitForSeconds(.3f);
            Assert.That(Vector3.Distance(position, chair.transform.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rotation, chair.transform.rotation), Is.LessThan(.01f));
            comfort.ChangeTurn(-15); Assert.That(chair.ComfortTurnSpeed, Is.EqualTo(15));
            comfort.ToggleVignette(); Stick(Vector2.right); yield return new WaitForSeconds(1);
            Assert.That(comfort.VignetteAlpha, Is.Zero);
            Assert.That(Quaternion.Angle(rotation, chair.transform.rotation), Is.InRange(10, 17));
            Stick(Vector2.zero);
        }
        [UnityTest] public IEnumerator WorldAnchoredGazeMenuPausesAndHeightChangesOnlyUnderBlackCurtain()
        {
            Object.FindAnyObjectByType<FlyAgent>().gameObject.SetActive(false);
            comfort.SetMenuOpen(true); Assert.That(Time.timeScale, Is.Zero);
            var panel = GameObject.Find("Comfort settings"); var at = panel.transform.position;
            var camera = Camera.main; var oldRotation = camera.transform.rotation;
            camera.transform.Rotate(0, 10, 0); yield return null;
            Assert.That(panel.transform.position, Is.EqualTo(at), "Head-gaze UI must not follow head rotation.");
            camera.transform.rotation = oldRotation;
            var slower = panel.GetComponentsInChildren<GazeAction>().Single(g => g.name.StartsWith("회전 느리게"));
            slower.Activate(); Assert.That(comfort.Preferences.turnSpeed, Is.EqualTo(25));
            yield return Capture("Logs/comfort-settings.png");
            var origin = chair.GetComponentInChildren<XROrigin>(); var before = origin.transform.localPosition;
            var rootBefore = chair.transform.position; var rootRotation = chair.transform.rotation;
            comfort.ChangeHeight(.05f); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(origin.transform.localPosition, Is.EqualTo(before));
            float deadline = Time.realtimeSinceStartup + 2;
            while (origin.transform.localPosition == before) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            Assert.That(comfort.HeightFadeAlpha, Is.EqualTo(1));
            while (comfort.AdjustingHeight) yield return null;
            Assert.That(origin.transform.localPosition.y - before.y, Is.EqualTo(.05f).Within(.001f));
            Assert.That(chair.transform.position, Is.EqualTo(rootBefore)); Assert.That(chair.transform.rotation, Is.EqualTo(rootRotation));
            comfort.SetMenuOpen(false); Assert.That(Time.timeScale, Is.EqualTo(1));
            var saved = ComfortPreferences.Load(key); Assert.That(saved.turnSpeed, Is.EqualTo(25)); Assert.That(saved.heightOffset, Is.EqualTo(.05f));
            yield return SceneManager.LoadSceneAsync("03_RoomShellTest"); yield return null;
            comfort = Object.FindAnyObjectByType<ComfortController>(); Assert.That(comfort.Preferences.turnSpeed, Is.EqualTo(25));
            Assert.That(comfort.Preferences.heightOffset, Is.EqualTo(.05f));
        }
        [UnityTest] public IEnumerator ChangesUseActualDeltasAndShockHasTextAwayFromCentre()
        {
            GameState.Current.Stats.ApplyDelta(StatType.Stress, 100, "test");
            GameState.Current.Stats.ApplyDelta(StatType.Health, -3, "test"); yield return null;
            StringAssert.Contains("▲", comfort.PopupText); StringAssert.Contains("+50.0", comfort.PopupText);
            StringAssert.Contains("▼", comfort.PopupText); Assert.That(Mathf.Abs(comfort.PopupTransform.localPosition.x), Is.GreaterThan(.4f));
            var fly = Object.FindAnyObjectByType<FlyAgent>(); EventBus.RaiseElectricShockVisual(fly.transform.position);
            StringAssert.Contains("전기 봉", comfort.Caption);
            yield return Capture("Logs/comfort-feedback.png");
        }
        [UnityTest] public IEnumerator EverySlotCuesBeforeEntryAndVisitedLampDimsWithoutReplay()
        {
            var fly = Object.FindAnyObjectByType<FlyAgent>(); fly.enabled = false;
            var agent = fly.GetComponent<NavMeshAgent>(); agent.enabled = false;
            var slot = Object.FindObjectsByType<ItemSlot>().First(); var f = slot.GetComponent<ItemApproachFeedback>();
            fly.transform.position = slot.transform.position + Vector3.right * (slot.InteractionRadius + 1);
            yield return new WaitForSeconds(.15f);
            Assert.That(f.Approaching, Is.True); int count = f.CueCount; Assert.That(count, Is.GreaterThan(0));
            float intensity = f.LampIntensity; yield return new WaitForSeconds(.1f); Assert.That(f.CueCount, Is.EqualTo(count));
            slot.GetComponentInParent<RoomController>().TryMarkFirstVisit(slot.ItemId); yield return new WaitForSeconds(.15f);
            Assert.That(f.LampIntensity, Is.LessThan(intensity * .3f));
        }
        [UnityTest] public IEnumerator OpaqueFadeCoversObjectsCloserThanTheCurtain()
        {
            var camera = Camera.main;
            var go = new GameObject("Fade verification"); var view = go.AddComponent<TransitionView>();
            view.Configure(comfort.PopupTransform.GetComponentInChildren<UnityEngine.UI.Text>().font);
            view.Initialize(camera); view.SetFade(1);
            var nearObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nearObject.transform.SetParent(camera.transform, false); nearObject.transform.localPosition = new Vector3(0, 0, .2f);
            nearObject.transform.localScale = Vector3.one * .1f;
            var target = new RenderTexture(320, 200, 24); var pixels = new Texture2D(320, 200, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; yield return null; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 320, 200), 0, 0); pixels.Apply();
                Assert.That(pixels.GetPixels().Max(c => c.grayscale), Is.LessThan(.01f), "Near hands/tools must not break the opaque fade.");
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(nearObject); Object.Destroy(go);
            }
        }
        [UnityTest] public IEnumerator HesitationAnimationShowsChildSpeechInsteadOfScreenNarration()
        {
            yield return SceneManager.LoadSceneAsync("03_Childhood"); yield return null;
            comfort = Object.FindAnyObjectByType<ComfortController>();
            var fly = Object.FindAnyObjectByType<FlyAgent>();
            fly.RefuseApproach(0); yield return null;
            fly.RefuseApproach(2); yield return null; yield return null;
            Assert.That(Object.FindAnyObjectByType<InfantRoomView>().DialogueText,Is.EqualTo("음…"));
            Assert.That(comfort.Caption,Is.Empty);
        }
        static IEnumerator Capture(string path)
        {
            var camera = Camera.main; var target = new RenderTexture(1440, 900, 24);
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; yield return null; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); pixels.Apply();
                Assert.That(pixels.GetPixels().Count(c => c.r > .9f && c.b > .9f && c.g < .1f), Is.LessThan(100));
                Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
