using System;
using System.IO;
using System.Text;
using FruitFlyParenting.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FruitFlyParenting.Debugging
{
    // Opt-in evidence collection, never an automatic claim of human acceptance.
    public sealed class PlaytestRecorder : MonoBehaviour
    {
        [Serializable] public sealed class Session
        {
            public string evidenceKind = "unreviewed_player_session";
            public string style, source, startedUtc, updatedUtc, buildGuid, version, platform, roomId, status;
            public int seed, eventCount, markers;
            public double elapsedSeconds, focusedUnpausedSeconds, endingCardSeconds = -1;
            public bool endingCardReached, nonRealtimeSpeedObserved;
            public string humanReview = "pending";
        }
        [Serializable] sealed class Observation
        {
            public string kind, utc, roomId;
            public double elapsedSeconds;
            public float gameTime;
        }

        public static PlaytestRecorder Current { get; private set; }
        public static string IsolatedSavePath => Current != null ? Current.savePath : null;
        public string SessionDirectory { get; private set; }
        public Session Summary { get; private set; }
        string root, savePath, style, source;
        GameState observed;
        int eventCursor;
        double started, previous, nextFlush;
        bool failed, finished, previousActive;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void FromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-playtest") < 0) return;
            var recorder = new GameObject("Playtest Recorder").AddComponent<PlaytestRecorder>();
            recorder.Begin(Argument(args, "-playtest-output", Path.Combine(Application.persistentDataPath, "PlaytestReports")),
                Argument(args, "-playtest-style", "observer"), Argument(args, "-playtest-source", "player"));
        }
        static string Argument(string[] args, string key, string fallback)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        public void Begin(string outputDirectory, string playStyle, string evidenceSource)
        {
            if (Current != null) throw new InvalidOperationException("A playtest recorder is already active.");
            // Fail before redirecting saves if the destination cannot be created.
            root = Path.Combine(Path.GetFullPath(outputDirectory), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(root);
            style = playStyle; source = evidenceSource;
            savePath = Path.Combine(root, "savegame.json");
            Current = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("Playtest recording: " + root + " (human review pending)");
        }

        void Update()
        {
            if (Current != this || failed) return;
            try
            {
                var state = GameState.Current;
                if (state == null) return;
                if (!ReferenceEquals(state, observed))
                {
                    if (observed != null) Finish("restarted");
                    StartSession(state);
                }
                double now = Time.realtimeSinceStartupAsDouble;
                Summary.elapsedSeconds = now - started;
                bool active = Application.isFocused && Time.timeScale > 0;
                if (previousActive && active) Summary.focusedUnpausedSeconds += now - previous;
                previousActive = active;
                previous = now;
                if (Time.timeScale != 0 && !Mathf.Approximately(Time.timeScale, 1)) Summary.nonRealtimeSpeedObserved = true;
                DrainEvents();
                var keyboard = Keyboard.current;
                if (Application.isFocused && keyboard != null)
                {
                    if (keyboard.f6Key.wasPressedThisFrame) MarkMoment();
                    if (keyboard.f7Key.wasPressedThisFrame)
                    {
                        Observe("screenshot_requested");
                        ScreenCapture.CaptureScreenshot(Path.Combine(SessionDirectory, "screen-" + DateTime.UtcNow.ToString("HHmmss-fff") + ".png"));
                    }
                }
                if (now >= nextFlush) { Flush(); nextFlush = now + 10; }
            }
            catch (Exception e) { failed = true; Debug.LogError("Playtest recording failed; gameplay continues: " + e.Message); }
        }

        void StartSession(GameState state)
        {
            observed = state; eventCursor = 0;
            finished = false; previousActive = false;
            started = previous = Time.realtimeSinceStartupAsDouble; nextFlush = started;
            SessionDirectory = Path.Combine(root, "run-" + DateTime.UtcNow.ToString("HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(SessionDirectory);
            Summary = new Session { style = style, source = source, startedUtc = DateTime.UtcNow.ToString("O"),
                seed = state.RunSeed, buildGuid = Application.buildGUID, version = Application.version,
                platform = Application.platform.ToString(), status = "recording" };
            if (source != "player") Summary.evidenceKind = "automated_or_other_session";
            var bootstrap = FindAnyObjectByType<GameStateBootstrap>();
            if (bootstrap != null) File.WriteAllText(Path.Combine(SessionDirectory, "balance-config.json"), JsonUtility.ToJson(bootstrap.Config, true), Utf8);
            string manifest = Path.Combine(Path.GetDirectoryName(Application.dataPath), "build-manifest.json");
            if (File.Exists(manifest)) File.Copy(manifest, Path.Combine(SessionDirectory, "build-manifest.json"));
            Observe("session_started");
        }

        void DrainEvents()
        {
            var buffer = new StringBuilder();
            while (eventCursor < observed.EventLog.Count)
            {
                var entry = observed.EventLog[eventCursor++];
                buffer.AppendLine(JsonUtility.ToJson(entry));
                if (entry.eventType == "ending_card" && !Summary.endingCardReached)
                {
                    Summary.endingCardReached = true;
                    Summary.endingCardSeconds = Time.realtimeSinceStartupAsDouble - started;
                }
            }
            if (buffer.Length > 0) File.AppendAllText(Path.Combine(SessionDirectory, "events.jsonl"), buffer.ToString(), Utf8);
            Summary.eventCount = eventCursor;
        }
        public void MarkMoment()
        {
            if (Summary == null || failed) return;
            Summary.markers++; Observe("remembered_moment"); Flush();
            Debug.Log("Playtest: remembered moment " + Summary.markers);
        }
        void Observe(string kind)
        {
            if (SessionDirectory == null) return;
            var value = new Observation { kind = kind, utc = DateTime.UtcNow.ToString("O"), roomId = observed?.CurrentRoomId,
                elapsedSeconds = Time.realtimeSinceStartupAsDouble - started, gameTime = Time.time };
            File.AppendAllText(Path.Combine(SessionDirectory, "observations.jsonl"), JsonUtility.ToJson(value) + "\n", Utf8);
        }
        void Flush()
        {
            Summary.updatedUtc = DateTime.UtcNow.ToString("O"); Summary.roomId = observed.CurrentRoomId;
            File.WriteAllText(Path.Combine(SessionDirectory, "session.json"), JsonUtility.ToJson(Summary, true), Utf8);
            File.WriteAllText(Path.Combine(SessionDirectory, "state.json"), JsonUtility.ToJson(observed.ToSaveData(), true), Utf8);
        }
        void Finish(string status)
        {
            if (Summary == null || failed || finished) return;
            Summary.elapsedSeconds = Time.realtimeSinceStartupAsDouble - started;
            Summary.status = status; DrainEvents(); Observe(status); Flush();
            finished = true;
        }
        void OnApplicationFocus(bool focused) => SafeObserve(focused ? "focus_gained" : "focus_lost");
        void SafeObserve(string kind)
        {
            if (failed) return;
            try { Observe(kind); } catch (Exception e) { failed = true; Debug.LogError("Playtest recording failed: " + e.Message); }
        }
        void OnDestroy()
        {
            if (Current != this) return;
            try { Finish("closed"); } catch (Exception e) { Debug.LogError("Playtest final write failed: " + e.Message); }
            Current = null;
        }
        void OnApplicationQuit()
        {
            if (Current != this) return;
            try { Finish("closed"); } catch (Exception e) { Debug.LogError("Playtest final write failed: " + e.Message); }
        }
    }
}
