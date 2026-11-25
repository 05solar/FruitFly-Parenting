using System.Collections;
using UnityEngine;

namespace FruitFlyParenting.Quest
{
    public sealed class QuestRuntime : MonoBehaviour
    {
        public const int TargetHz = 72;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var root = new GameObject("Quest runtime");
            DontDestroyOnLoad(root); root.AddComponent<QuestRuntime>();
#if DEVELOPMENT_BUILD
            root.AddComponent<QuestPerformanceCapture>();
#endif
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = TargetHz;
            // Request through the installed Meta OpenXR provider once its session is ready.
            for (int attempt = 0; attempt < 30; attempt++)
            {
                if (OVRPlugin.initialized)
                {
                    foreach (float rate in OVRPlugin.systemDisplayFrequenciesAvailable)
                        if (Mathf.Abs(rate - TargetHz) < .1f)
                        { OVRPlugin.systemDisplayFrequency = TargetHz; yield break; }
                }
                yield return new WaitForSecondsRealtime(1);
            }
            Debug.LogWarning("Quest: 72 Hz request was not supported/initialized; check the actual refresh rate with Metrics.");
        }
    }
}
