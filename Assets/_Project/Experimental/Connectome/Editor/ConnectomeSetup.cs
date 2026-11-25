using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FruitFlyParenting.Connectome.Editor
{
    public static class ConnectomeSetup
    {
        public const string Root = "Assets/_Project/Experimental/Connectome/";
        public const string ScenePath = "Assets/_Project/Scenes/Experimental_ConnectomeViewer.unity";

        [MenuItem("Fruit Fly Parenting/Experimental/13 Create Connectome Viewer")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/_Project/Scenes");
            AssetDatabase.Refresh();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Art/ConnectomeUnlit.shader");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Connectome.mat");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, Root + "Art/Connectome.mat");
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Independent desktop camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.018f, .028f, .055f); camera.fieldOfView = 46;
            camera.nearClipPlane = .1f; camera.farClipPlane = 60; camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.transform.position = new Vector3(0, 0, -17.5f);
            var viewer = new GameObject("Independent Connectome Viewer").AddComponent<ConnectomeViewer>();
            viewer.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Data/connectome-v783.json"),
                AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Data/memory-motif-v783.json"),
                AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/NotoSansCJKkr-Regular.otf"), material, camera);
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save viewer scene.");
            AssetDatabase.SaveAssets();
            VerifyMainIsolation();
            Debug.Log("Step13: independent desktop scene created; main build list unchanged.");
        }

        public static void VerifyMainIsolation()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if ((scenes.Length == 0 && !File.Exists("ProjectSettings/ConnectomeStandalone.txt")) || scenes.Contains(ScenePath))
                throw new InvalidOperationException("The experimental scene must not be in the main build list.");
            var leaked = AssetDatabase.GetDependencies(scenes, true).Where(p => p.StartsWith(Root)).ToArray();
            if (leaked.Length != 0) throw new InvalidOperationException("Main scenes reference experimental assets: " + string.Join(", ", leaked));
            var data = AssetDatabase.FindAssets("", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath);
            if (data.Any(p => p.Contains("/Resources/") || p.Contains("/StreamingAssets/")))
                throw new InvalidOperationException("Experimental assets must not be globally included.");
        }

        [MenuItem("Fruit Fly Parenting/Experimental/13 Build Windows Viewer (isolated)")]
        public static void LaunchIsolatedBuild()
        {
            string script = Path.GetFullPath("scripts/Build-Connectome.ps1");
            if (!File.Exists(script)) throw new FileNotFoundException("Run scripts/Build-Connectome.ps1 from the source repository.");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = "powershell.exe", UseShellExecute = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\""
            });
            Debug.Log("Building the isolated viewer; see Logs/connectome-build-run.log.");
        }

        public static void PrepareStandalone()
        {
            if (!File.Exists("ProjectSettings/ConnectomeStandalone.txt"))
                throw new InvalidOperationException("PrepareStandalone is only for a generated isolated project.");
            PlayerSettings.productName = "FlyWire Connectome Experiment";
            PlayerSettings.companyName = "Fruit Fly Parenting Experiments";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/Settings/URP-Default.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            CreateScene();
        }

        public static void BuildViewer()
        {
            if (!File.Exists("ProjectSettings/ConnectomeStandalone.txt"))
                throw new InvalidOperationException("Use scripts/Build-Connectome.ps1 to exclude unrelated XR runtime initializers.");
            BuildOne(true);
        }

        public static void VerifyMainBuild() => BuildOne(false);

        static void BuildOne(bool experiment)
        {
            VerifyMainIsolation();
            string name = experiment ? "ConnectomeExperiment" : "MainIsolationCheck";
            string output = "Builds/Step13/" + name + "/" + name + ".exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = experiment ? new[] { ScenePath } : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.DetailedBuildReport,
                extraScriptingDefines = experiment ? new[] { "CONNECTOME_EXPERIMENT" } : Array.Empty<string>()
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(name + " build failed.");
            var packed = report.packedAssets.SelectMany(p => p.contents).Select(a => a.sourceAssetPath).Distinct().OrderBy(p => p).ToArray();
            var experimentalAssets = packed.Where(p => p.StartsWith(Root) || p == ScenePath).ToArray();
            string managedPath = Path.ChangeExtension(output, null) + "_Data/Managed/FruitFlyParenting.Connectome.dll";
            bool containsAssembly = File.Exists(managedPath);
            if (!experiment && (experimentalAssets.Length != 0 || containsAssembly))
                throw new InvalidOperationException("Experimental data/code leaked into the main player.");
            if (experiment && (!experimentalAssets.Contains(Root + "Data/connectome-v783.json")
                || !experimentalAssets.Contains(Root + "Data/memory-motif-v783.json")))
                throw new InvalidOperationException("Viewer build is missing graph assets.");
            if (experiment)
            {
                var graph = ConnectomeGraph.Parse(File.ReadAllText(Root + "Data/connectome-v783.json"));
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "CONNECTOME-NOTICE.txt"),
                    ConnectomeViewer.Credit + "\n\n" + graph.attribution + "\n\n" + ConnectomeViewer.Boundary + "\n"
                    + ConnectomeViewer.Disclaimer + "\n\nData sources:\n"
                    + string.Join("\n", graph.sources.Select(s => s.url + "\nSHA-256: " + s.sha256))
                    + "\n\nFont: Noto Sans CJK KR, SIL OFL 1.1 (see FONT-OFL.txt).\n");
                File.Copy("Assets/_Project/Art/Fonts/OFL.txt", Path.Combine(Path.GetDirectoryName(output), "FONT-OFL.txt"), true);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/step13-" + name + "-build.json", JsonUtility.ToJson(new BuildEvidence {
                build = name, result = report.summary.result.ToString(), bytes = (long)report.summary.totalSize,
                experimentalAssets = experimentalAssets, containsConnectomeAssembly = containsAssembly,
                scenes = experiment ? new[] { ScenePath } : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray()
            }, true));
            Debug.Log("Step13 verified: " + name + ", experimental assets=" + experimentalAssets.Length);
        }
        [Serializable] sealed class BuildEvidence
        {
            public string build, result;
            public long bytes;
            public bool containsConnectomeAssembly;
            public string[] scenes, experimentalAssets;
        }
    }
}
