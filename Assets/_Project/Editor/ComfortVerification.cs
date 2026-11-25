using System;
using System.IO;
using System.Linq;
using FruitFlyParenting.Comfort;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FruitFlyParenting.Editor
{
    public static class ComfortVerification
    {
        const string Key = "FruitFlyParenting.Step14.ProcessPersistenceProbe";
        // Separate editor processes verify disk persistence without modifying the player's settings key.
        public static void WritePreferences()
        {
            new ComfortPreferences { turnSpeed = 35, vignette = false, heightOffset = .15f }.Save(Key);
            Debug.Log("Step14 settings probe written to disk.");
        }
        public static void ReadPreferencesAndBuild()
        {
            var value = ComfortPreferences.Load(Key);
            try
            {
                if (value.turnSpeed != 35 || value.vignette || Mathf.Abs(value.heightOffset - .15f) > .0001f)
                    throw new InvalidOperationException("Settings did not persist across editor processes.");
            }
            finally { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
            var output = "Builds/Step14/FruitFlyParenting.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.DetailedBuildReport
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Step14 player build failed.");
            var assets = report.packedAssets.SelectMany(p => p.contents).Select(p => p.sourceAssetPath).Distinct().ToArray();
            if (!assets.Contains("Assets/_Project/Art/Shaders/ComfortOverlay.shader") || assets.Any(p => p.Contains("/Experimental/Connectome/")))
                throw new InvalidOperationException("Comfort shader missing or experiment leaked into main build.");
            File.WriteAllText("Logs/step14-build.json", JsonUtility.ToJson(new Evidence {
                result = report.summary.result.ToString(), bytes = (long)report.summary.totalSize,
                processPersistencePassed = true, comfortShaderIncluded = true, experimentAssets = 0
            }, true));
        }
        [Serializable] sealed class Evidence
        {
            public string result;
            public long bytes;
            public bool processPersistencePassed, comfortShaderIncluded;
            public int experimentAssets;
        }
    }
}
