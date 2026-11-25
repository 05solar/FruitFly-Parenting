using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Tests.EditMode
{
    public sealed class QuestConfigurationTests
    {
        [Test] public void AndroidHasSeparateQualityAndControllerOnlyOpenXR()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/Settings/URP-Quest.asset");
            Assert.That(pipeline, Is.Not.Null);
            Assert.That(pipeline.msaaSampleCount, Is.EqualTo(4)); Assert.That(pipeline.shadowDistance, Is.EqualTo(12));
            Assert.That(pipeline.supportsHDR, Is.False); Assert.That(pipeline.additionalLightsRenderingMode, Is.EqualTo(LightRenderingMode.PerPixel));
            Assert.That(pipeline.maxAdditionalLightsCount, Is.EqualTo(1));
            Assert.That(pipeline, Is.Not.SameAs(GraphicsSettings.defaultRenderPipeline));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android), Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Is.EqualTo(new[] { GraphicsDeviceType.Vulkan }));
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            Assert.That(xr.renderMode, Is.EqualTo(OpenXRSettings.RenderMode.SinglePassInstanced));
            var enabled = xr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name).ToArray();
            Assert.That(enabled, Does.Contain("MetaQuestFeature")); Assert.That(enabled, Does.Contain("OculusTouchControllerProfile"));
            Assert.That(enabled.Any(n => n.Contains("HandTracking") || n.Contains("Mock")), Is.False);
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
            int android = -1;
            for (int i = 0; i < defaults.arraySize; i++)
            { var pair = defaults.GetArrayElementAtIndex(i); if (pair.FindPropertyRelative("first").stringValue == "Android") android = pair.FindPropertyRelative("second").intValue; }
            Assert.That(QualitySettings.names[android], Is.EqualTo("Quest 72Hz"));
        }
        [Test] public void BuildProcessorStripsDebugWithoutMovingRigOrStoryProps()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Wall_North";
                var actor = GameObject.CreatePrimitive(PrimitiveType.Cube); actor.name = "Child Silhouette Body";
                actor.transform.position = new Vector3(3, 2, 1);
                actor.AddComponent<FruitFlyParenting.Room.RoomCoordinateGizmos>(); actor.AddComponent<FruitFlyParenting.Fly.DebugHud>();
                var lamp = new GameObject("Section Light").AddComponent<Light>(); lamp.type = LightType.Point;
                var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
                var storyLight = new GameObject("Living Room Light").AddComponent<Light>(); storyLight.type = LightType.Point;
                var type = Type.GetType("FruitFlyParenting.Editor.QuestSceneProcessor, Assembly-CSharp-Editor", true);
                type.GetMethod("Apply").Invoke(null, new object[] { scene });
                Assert.That(actor.transform.position, Is.EqualTo(new Vector3(3, 2, 1)));
                Assert.That(GameObjectUtility.GetStaticEditorFlags(actor), Is.EqualTo((StaticEditorFlags)0));
                Assert.That(GameObjectUtility.GetStaticEditorFlags(wall) & StaticEditorFlags.BatchingStatic, Is.Not.EqualTo((StaticEditorFlags)0));
                Assert.That(actor.GetComponent<FruitFlyParenting.Fly.DebugHud>(), Is.Null);
                Assert.That(actor.GetComponent<FruitFlyParenting.Room.RoomCoordinateGizmos>(), Is.Null);
                Assert.That(lamp.enabled, Is.False); Assert.That(sun.enabled, Is.True);
                Assert.That(storyLight.enabled, Is.True);
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
        [Test] public void AudioOverridesKeepShortCueResponsiveAndLoopsCompressed()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.ContainsSampleSettingsOverride("Android"), Is.True, path);
                var settings = importer.GetOverrideSampleSettings("Android");
                bool shortCue = path.EndsWith("ItemApproach.wav");
                Assert.That(settings.compressionFormat, Is.EqualTo(shortCue ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis), path);
                Assert.That(settings.loadType, Is.EqualTo(shortCue ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory), path);
            }
        }
    }
}
