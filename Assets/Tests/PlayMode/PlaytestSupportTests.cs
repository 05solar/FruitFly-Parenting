using System;
using System.Collections;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Debugging;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Tests.PlayMode
{
    public sealed class PlaytestSupportTests
    {
        string output;
        GameBalanceConfig config;
        PlaytestRecorder recorder;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            yield return EmptyScene();
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            GameState.SetCurrent(null);
            config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            output = Path.Combine(Application.temporaryCachePath, "playtest-tests-" + Guid.NewGuid().ToString("N"));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return EmptyScene();
            if (recorder != null) Object.Destroy(recorder.gameObject);
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            GameState.SetCurrent(null); Object.Destroy(config); Time.timeScale = 1;
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
        static IEnumerator EmptyScene()
        {
            var empty = SceneManager.CreateScene("Playtest isolation " + Guid.NewGuid());
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                // Keep Unity's InitTestScene: it owns the coroutine test runner.
                if (scene != empty && (scene.path.StartsWith("Assets/_Project/Scenes/", StringComparison.Ordinal) ||
                    scene.name.StartsWith("Playtest isolation ", StringComparison.Ordinal)))
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest] public IEnumerator AllFourGrowthRoomsAllowToolsImmediatelyOnEntry()
        {
            recorder=new GameObject("Test recorder").AddComponent<PlaytestRecorder>();
            recorder.Begin(output,"observer","automated_smoke");
            foreach(var scene in new[]{"01_Infant","03_Childhood","04_Adolescence","05_YoungAdult"})
            {
                yield return SceneManager.LoadSceneAsync(scene); yield return null; yield return null;
                var actor=Object.FindAnyObjectByType<FlyAgent>();
                Assert.That(actor.GetComponent<FruitFlyParenting.Tool.FlyContactReceiver>().ContactsEnabled,Is.True,scene);
                Assert.That(Object.FindAnyObjectByType<InfantRoomView>().StatusText,Does.Not.Contain("도구 사용까지"),scene);
            }
        }

        [UnityTest] public IEnumerator PrologueShowsCountdownAndAutomaticallyEntersInfantRoom()
        {
            recorder = new GameObject("Test recorder").AddComponent<PlaytestRecorder>();
            recorder.Begin(output, "observer", "automated_smoke");
            yield return SceneManager.LoadSceneAsync("00_Bootstrap"); yield return null; yield return null;
            var prologue=Object.FindAnyObjectByType<FruitFlyParenting.Ending.PrologueController>();
            var state=GameState.Current;
            StringAssert.Contains("자동 시작까지",prologue.GetComponent<FruitFlyParenting.Corridor.TransitionView>().Subtitle);
            Assert.That(GameObject.Find("Prologue Start"),Is.Not.Null);
            Time.timeScale=20;
            float deadline=Time.realtimeSinceStartup+12;
            while(SceneManager.GetActiveScene().name!="01_Infant" || prologue!=null)
            { Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline)); yield return null; }
            Assert.That(GameState.Current,Is.SameAs(state));
            Assert.That(state.EventLog.Count(e=>e.eventType=="prologue_complete"),Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<FruitFlyParenting.Wheelchair.WheelchairLocomotion>().enabled,Is.True);
            Assert.That(GameObject.Find("Prologue Start"),Is.Null);
        }

        [UnityTest] public IEnumerator RecorderIsolatesSavesAndDoesNotTreatEndingSelectionAsCompletion()
        {
            var saves = new SaveSystem(config);
            string normal = saves.GetDefaultSaveFilePath();
            byte[] before = File.Exists(normal) ? File.ReadAllBytes(normal) : null;
            recorder = new GameObject("Test recorder").AddComponent<PlaytestRecorder>();
            recorder.Begin(output, "observer", "automated_smoke");
            Assert.That(saves.GetDefaultSaveFilePath(), Is.Not.EqualTo(normal));
            var state = new GameState(config, 17); state.CurrentRoomId = "01_Infant";
            GameState.SetCurrent(state);
            state.LogEvent(1, "01_Infant", "checkpoint", "room_enter");
            state.LogEvent(2, "06_MiddleAge", "ending_selected", "1");
            saves.Save(state);
            yield return null; yield return null;
            Assert.That(recorder.Summary.endingCardReached, Is.False);
            recorder.MarkMoment();
            string folder = recorder.SessionDirectory;
            Assert.That(File.ReadAllLines(Path.Combine(folder, "events.jsonl")).Length, Is.EqualTo(2));
            state.LogEvent(3, "06_MiddleAge", "ending_card", "1");
            yield return null;
            Assert.That(recorder.Summary.endingCardReached, Is.True);
            Assert.That(recorder.Summary.humanReview, Is.EqualTo("pending"));
            Assert.That(recorder.Summary.evidenceKind, Is.EqualTo("automated_or_other_session"));
            Assert.That(recorder.Summary.markers, Is.EqualTo(1));
            yield return null;
            Assert.That(File.ReadAllLines(Path.Combine(folder, "events.jsonl")).Length, Is.EqualTo(3));
            if (before == null) Assert.That(File.Exists(normal), Is.False);
            else Assert.That(File.ReadAllBytes(normal), Is.EqualTo(before));
            Object.Destroy(recorder.gameObject); yield return null;
            Assert.That(saves.GetDefaultSaveFilePath(), Is.EqualTo(normal));
            var closed = JsonUtility.FromJson<PlaytestRecorder.Session>(File.ReadAllText(Path.Combine(folder, "session.json")));
            Assert.That(closed.status, Is.EqualTo("closed"));
            Assert.That(closed.endingCardReached, Is.True);
        }

        [UnityTest] public IEnumerator RestartCreatesSeparateEvidenceAndPauseDoesNotInflateActiveTime()
        {
            recorder = new GameObject("Test recorder").AddComponent<PlaytestRecorder>();
            recorder.Begin(output, "relationship", "automated_smoke");
            GameState.SetCurrent(new GameState(config, 17)); yield return null;
            string first = recorder.SessionDirectory;
            double active = recorder.Summary.focusedUnpausedSeconds;
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f);
            Assert.That(recorder.Summary.focusedUnpausedSeconds, Is.EqualTo(active).Within(.02));
            Assert.That(recorder.Summary.elapsedSeconds, Is.GreaterThan(.1));
            Time.timeScale = 1;
            GameState.SetCurrent(new GameState(config, 18)); yield return null;
            Assert.That(recorder.SessionDirectory, Is.Not.EqualTo(first));
            Assert.That(recorder.Summary.seed, Is.EqualTo(18));
            Assert.That(recorder.Summary.endingCardReached, Is.False);
            var previous = JsonUtility.FromJson<PlaytestRecorder.Session>(File.ReadAllText(Path.Combine(first, "session.json")));
            Assert.That(previous.status, Is.EqualTo("restarted"));
        }

        [UnityTest] public IEnumerator SpeechFollowsChildStaysDuringAnimationAndHidesOnPauseExpiryAndExit()
        {
            yield return SceneManager.LoadSceneAsync("01_Infant");
            yield return null; yield return null;
            var view = Object.FindAnyObjectByType<InfantRoomView>();
            var child = Object.FindAnyObjectByType<FlyAgent>();
            Assert.That(view, Is.Not.Null); Assert.That(child, Is.Not.Null);
            view.ShowDialogue("안아 줘…"); yield return null;
            var marker = child.transform.Find("Child Dialogue");
            Assert.That(marker, Is.Not.Null);
            var speechCanvas=marker.GetComponent<Canvas>();
            Assert.That(speechCanvas.renderMode,Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(speechCanvas.enabled, Is.True);
            Assert.That(marker.GetComponentInChildren<Text>().text,Is.EqualTo("안아 줘…"));
            Assert.That(marker.Find("Speech background").GetComponent<Image>().color.a,Is.EqualTo(.28f).Within(.001f));
            Object.FindAnyObjectByType<InfantRoomController>().enabled=false;
            child.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;
            foreach(var feedback in child.GetComponentsInChildren<FlyHesitationFeedback>()) feedback.enabled=false;
            child.enabled=false; // Cinematic sequences disable navigation, but the character still speaks.
            child.transform.position+=Vector3.right; yield return null;
            Assert.That(marker.position.x,Is.EqualTo(child.transform.position.x));
            Assert.That(marker.position.z,Is.EqualTo(child.transform.position.z));
            Assert.That(marker.position.y,Is.GreaterThan(child.GetComponent<Collider>().bounds.max.y));
            Assert.That(speechCanvas.enabled,Is.True);
            var camera=Camera.main;
            camera.transform.position=child.transform.position+new Vector3(0,1.4f,-4);
            camera.transform.LookAt(child.transform.position+Vector3.up*.8f);
            yield return null; yield return null;
            var target=new RenderTexture(1280,720,24);
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/spoken-dialogue-world.png",pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=null; RenderTexture.active=previous; target.Release(); Object.Destroy(target); Object.Destroy(pixels); }
            var comfort=FruitFlyParenting.Comfort.ComfortController.Current;
            comfort.SetMenuOpen(true); yield return null;
            Assert.That(speechCanvas.enabled,Is.False);
            comfort.SetMenuOpen(false); yield return null;
            Assert.That(speechCanvas.enabled,Is.True);
            Time.timeScale=20; yield return new WaitForSeconds(7); Time.timeScale=1; yield return null;
            Assert.That(speechCanvas.enabled,Is.False,"Expired speech: "+view.DialogueText);
            Assert.That(view.DialogueText,Is.Empty);
            Assert.That(view.StatusText, Does.Not.Contain("왼손"));
            view.HideHud();
            Assert.That(marker.gameObject.activeInHierarchy, Is.False);
            Object.FindAnyObjectByType<InfantRoomController>().enabled = false;
            Object.Destroy(view); yield return null;
            Assert.That(child.transform.Find("Child Dialogue"), Is.Null);
        }

        [UnityTest] public IEnumerator CompactHudHasOnlyCurrentValuesAndNoScreenNarrationOrLocatorInDesktopAndVr()
        {
            yield return SceneManager.LoadSceneAsync("01_Infant");
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var hud = GameObject.Find("Infant HUD").GetComponent<Canvas>();
            Assert.That(hud.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            var stats = hud.transform.Find("Adaptive content/Stat column").GetComponent<RectTransform>();
            Assert.That(stats.localScale, Is.EqualTo(Vector3.one * .65f));
            Assert.That(hud.transform.Find("Adaptive content/Narration"),Is.Null);
            Assert.That(hud.transform.Find("Adaptive content/Child Locator"),Is.Null);
            Assert.That(hud.transform.Find("Adaptive content/Chapter card"),Is.Null);
            Assert.That(stats.GetComponent<Image>(),Is.Null,"The stats must have no enclosing card.");
            float previousY=float.PositiveInfinity;
            for(int i=0;i<6;i++)
            {
                var row=stats.Find("Stat "+i).GetComponent<RectTransform>();
                Assert.That(row.anchoredPosition.x,Is.Zero);
                Assert.That(row.anchoredPosition.y,Is.LessThan(previousY)); previousY=row.anchoredPosition.y;
                Assert.That(row.GetComponent<Image>(),Is.Null,"Each bar must remain separate from a card.");
                Assert.That(row.Find("Fill").GetComponent<RectTransform>().sizeDelta.y,Is.EqualTo(14));
                Assert.That(row.Find("Value").GetComponent<Text>().text,Does.Match("^[0-9]+$"));
            }
            Rect Bounds(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
            }
            foreach(var rect in new[] {stats})
            {
                var bounds=Bounds(rect);
                Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(0)); Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(0));
                Assert.That(bounds.xMax,Is.LessThanOrEqualTo(Screen.width)); Assert.That(bounds.yMax,Is.LessThanOrEqualTo(Screen.height));
            }
            Assert.That(stats.GetComponentsInChildren<Text>().Length,Is.EqualTo(13));
            foreach(var text in stats.GetComponentsInChildren<Text>())
            {
                Assert.That(text.fontSize,Is.GreaterThanOrEqualTo(18));
                Assert.That(text.cachedTextGenerator.vertexCount,Is.GreaterThan(0),"A CJK line must actually generate visible glyphs: "+text.name);
            }
            var controller=InputSystem.AddDevice<XRController>();
            try
            {
                yield return null; yield return null;
                Assert.That(hud.renderMode,Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(hud.transform.localScale.x,Is.EqualTo(.001f).Within(.00001));
                var speech=Object.FindAnyObjectByType<FlyAgent>().transform.Find("Child Dialogue").GetComponent<Canvas>();
                Assert.That(speech.renderMode,Is.EqualTo(RenderMode.WorldSpace));
            }
            finally { InputSystem.RemoveDevice(controller); }
            yield return null; yield return null;
            Assert.That(hud.renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        }
    }
}
