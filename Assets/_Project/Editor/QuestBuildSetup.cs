using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;

namespace FruitFlyParenting.Editor
{
    public static class QuestBuildSetup
    {
        public const string QualityName = "Quest 72Hz";
        public const string PipelinePath = "Assets/_Project/Settings/URP-Quest.asset";
        public const string RendererPath = "Assets/_Project/Settings/URP-Quest_Renderer.asset";

        [MenuItem("Fruit Fly Parenting/Setup 15 Quest Android")]
        public static void Configure()
        {
            ConfigureRendering();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.fruitflyparenting.game");
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            PlayerSettings.enableFrameTimingStats = true;
            ConfigureBatching();
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;
            ConfigureOpenXR();
            ConfigureImports();
            AssetDatabase.SaveAssets();
            Debug.Log("Step15 Quest Android settings saved; standalone OpenXR features and PC quality preserved.");
        }

        static void ConfigureBatching()
        {
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var entries = player.FindProperty("m_BuildTargetBatching");
            SerializedProperty android = null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("m_BuildTarget").stringValue == "Android") android = entry;
            }
            if (android == null) { int index = entries.arraySize++; android = entries.GetArrayElementAtIndex(index); }
            android.FindPropertyRelative("m_BuildTarget").stringValue = "Android";
            android.FindPropertyRelative("m_StaticBatching").boolValue = true;
            android.FindPropertyRelative("m_DynamicBatching").boolValue = false;
            player.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureRendering()
        {
            if (!File.Exists(RendererPath)) AssetDatabase.CopyAsset("Assets/_Project/Settings/URP-Default_Renderer.asset", RendererPath);
            if (!File.Exists(PipelinePath)) AssetDatabase.CopyAsset("Assets/_Project/Settings/URP-Default.asset", PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            renderer.rendererFeatures.Clear();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var data = new SerializedObject(pipeline);
            data.FindProperty("m_RendererDataList").arraySize = 1;
            data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            data.FindProperty("m_DefaultRendererIndex").intValue = 0;
            data.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1;
            data.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 1;
            data.FindProperty("m_SoftShadowsSupported").boolValue = false;
            data.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1;
            pipeline.shadowDistance = 12;
            pipeline.mainLightShadowmapResolution = 1024;
            pipeline.shadowCascadeCount = 1;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.useSRPBatcher = true;
            EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(pipeline);

            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            int index = Array.IndexOf(QualitySettings.names, QualityName);
            if (index < 0) { index = levels.arraySize; levels.InsertArrayElementAtIndex(index); }
            var level = levels.GetArrayElementAtIndex(index);
            level.FindPropertyRelative("name").stringValue = QualityName;
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            level.FindPropertyRelative("antiAliasing").intValue = 4;
            level.FindPropertyRelative("vSyncCount").intValue = 0;
            level.FindPropertyRelative("shadowDistance").floatValue = 12;
            level.FindPropertyRelative("pixelLightCount").intValue = 1;
            var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
            excluded.arraySize = 1; excluded.GetArrayElementAtIndex(0).stringValue = "Standalone";
            for (int i = 0; i < levels.arraySize; i++)
            {
                if (i == index) continue;
                var platforms = levels.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms");
                bool found = false;
                for (int j = 0; j < platforms.arraySize; j++) found |= platforms.GetArrayElementAtIndex(j).stringValue == "Android";
                if (!found) { int end = platforms.arraySize++; platforms.GetArrayElementAtIndex(end).stringValue = "Android"; }
            }
            var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
            // Serialized maps are key/value pairs, not fields named after their keys.
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var pair = defaults.GetArrayElementAtIndex(i);
                if (pair.FindPropertyRelative("first").stringValue == "Android") pair.FindPropertyRelative("second").intValue = index;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureOpenXR()
        {
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android))
                throw new InvalidOperationException("Android OpenXR loader could not be assigned.");
            general.InitManagerOnStart = true; general.Manager.automaticLoading = false; general.Manager.automaticRunning = false;
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null) throw new InvalidOperationException("Install Android Build Support before Setup 15.");
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            // Controller-only main game: do not inherit optional hand tracking, space warp or test runtimes.
            string[] enabled = { "MetaQuestFeature", "MetaXRFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile" };
            foreach (var feature in settings.GetFeatures())
            {
                feature.enabled = enabled.Contains(feature.GetType().Name);
                if (feature.GetType().Name == "MetaQuestFeature")
                {
                    var featureData = new SerializedObject(feature);
                    var devices = featureData.FindProperty("targetDevices");
                    for (int i = 0; i < devices.arraySize; i++)
                    {
                        var device = devices.GetArrayElementAtIndex(i);
                        device.FindPropertyRelative("enabled").boolValue = device.FindPropertyRelative("manifestName").stringValue != "quest";
                    }
                    featureData.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(feature);
            }
            if (!settings.GetFeatures().Any(f => f.enabled && f.GetType().Name == "MetaQuestFeature"))
                throw new InvalidOperationException("Meta Quest Support feature is unavailable.");
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(general.Manager); EditorUtility.SetDirty(general); EditorUtility.SetDirty(perTarget);
        }

        static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art" }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer == null) continue;
                var setting = importer.GetPlatformTextureSettings("Android");
                setting.overridden = true; setting.format = TextureImporterFormat.ASTC_6x6; setting.maxTextureSize = 1024;
                importer.SetPlatformTextureSettings(setting); importer.SaveAndReimport();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var setting = importer.GetOverrideSampleSettings("Android");
                bool shortCue = Path.GetFileNameWithoutExtension(path) == "ItemApproach";
                setting.compressionFormat = shortCue ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
                setting.loadType = shortCue ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                setting.quality = .5f; setting.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; setting.sampleRateOverride = 22050;
                importer.SetOverrideSampleSettings("Android", setting); importer.SaveAndReimport();
            }
        }

        [MenuItem("Fruit Fly Parenting/Build 15 Quest Release APK")]
        public static void BuildRelease() => Build(false);
        [MenuItem("Fruit Fly Parenting/Build 15 Quest Profiling APK")]
        public static void BuildDevelopment() => Build(true);
        public static void BuildBoth() { Build(false); Build(true); }
        static void Build(bool development)
        {
            Configure();
            QuestPerformanceAudit.Run();
            Directory.CreateDirectory("Builds/Step15"); Directory.CreateDirectory("Logs");
            int previousQuality = QualitySettings.GetQualityLevel();
            BuildReport report;
            try
            {
                // Vendor validation may adjust the current level's VSync. Keep it on the Android level.
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, QualityName), true);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = "Builds/Step15/FruitFlyParenting" + (development ? "-profiling" : "") + ".apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.DetailedBuildReport | (development ? BuildOptions.Development : BuildOptions.None)
                });
            }
            finally { QualitySettings.SetQualityLevel(previousQuality, true); }
            var assets = report.packedAssets.SelectMany(p => p.contents).Select(p => p.sourceAssetPath).Distinct().ToArray();
            var evidence = new BuildEvidence { result = report.summary.result.ToString(), bytes = (long)report.summary.totalSize,
                development = development, experimentalAssets = assets.Count(p => p.Contains("/Experimental/Connectome/")),
                comfortShaderIncluded = assets.Contains("Assets/_Project/Art/Shaders/ComfortOverlay.shader") };
            File.WriteAllText("Logs/step15-build" + (development ? "-profiling" : "") + ".json", JsonUtility.ToJson(evidence, true));
            if (report.summary.result != BuildResult.Succeeded || evidence.experimentalAssets != 0 || !evidence.comfortShaderIncluded)
                throw new BuildFailedException("Quest build or main-game asset isolation failed; inspect BuildReport.");
        }
        [Serializable] sealed class BuildEvidence
        { public string result; public long bytes; public bool development, comfortShaderIncluded; public int experimentalAssets; }
    }
}
