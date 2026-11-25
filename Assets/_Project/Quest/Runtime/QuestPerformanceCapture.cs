#if DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using FruitFlyParenting.Core;
using Unity.Profiling;
using UnityEngine;

namespace FruitFlyParenting.Quest
{
    // Development APK only. A CSV sample every 0.5s; missing GPU timing stays blank, never becomes a fake zero.
    public sealed class QuestPerformanceCapture : MonoBehaviour
    {
        readonly FrameTiming[] timings = new FrameTiming[1];
        ProfilerRecorder draws, triangles, memory, allocation;
        StreamWriter output;
        float sampleAt, flushAt;
        int frames, overBudget;
        float worstFrame;
        void Start()
        {
            draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            allocation = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            string path = Path.Combine(Application.persistentDataPath, "quest-performance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".csv");
            output = new StreamWriter(path);
            output.WriteLine("elapsed_s,room,cpu_ms,gpu_ms,frame_ms,draw_calls,triangles,memory_bytes,gc_bytes,refresh_hz,window_frames,over_budget_frames,worst_frame_ms");
            Debug.Log("Quest performance capture: " + path);
        }
        void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            frames++;
            float frameMs = Time.unscaledDeltaTime * 1000;
            worstFrame = Mathf.Max(worstFrame, frameMs);
            if (frameMs > 1000f / QuestRuntime.TargetHz + .1f) overBudget++;
            if (Time.unscaledTime < sampleAt) return;
            sampleAt = Time.unscaledTime + .5f;
            uint count = FrameTimingManager.GetLatestTimings(1, timings);
            string cpu = count > 0 && timings[0].cpuFrameTime > 0 ? Number(timings[0].cpuFrameTime) : "";
            string gpu = count > 0 && timings[0].gpuFrameTime > 0 ? Number(timings[0].gpuFrameTime) : "";
            output.WriteLine(string.Join(",", Number(Time.unscaledTime), GameState.Current?.CurrentRoomId ?? "loading",
                cpu, gpu, Number(frameMs), Value(draws), Value(triangles), Value(memory), Value(allocation), Number(OVRPlugin.systemDisplayFrequency), frames, overBudget, Number(worstFrame)));
            frames = 0; overBudget = 0; worstFrame = 0;
            if (Time.unscaledTime >= flushAt) { output.Flush(); flushAt = Time.unscaledTime + 5; }
        }
        static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
        static string Value(ProfilerRecorder value) => value.Valid ? value.LastValue.ToString(CultureInfo.InvariantCulture) : "";
        void OnApplicationPause(bool paused) { if (paused) output?.Flush(); }
        void OnDestroy() { output?.Dispose(); draws.Dispose(); triangles.Dispose(); memory.Dispose(); allocation.Dispose(); }
    }
}
#endif
