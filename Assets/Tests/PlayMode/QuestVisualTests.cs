#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using FruitFlyParenting.Comfort;
using FruitFlyParenting.Core;
using FruitFlyParenting.Ending;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Tests.PlayMode
{
    public sealed class QuestVisualTests
    {
        [UnityTest] public IEnumerator QuestLightingPreservesAllFiveRoomsAndNarrativeLight()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/_Project/Settings/URP-Quest.asset");
            Assert.That(pipeline, Is.Not.Null, "Run Setup 15 before Quest visual checks.");
            var previous = QualitySettings.renderPipeline;
            string key = "FruitFlyParenting.QuestTest." + Guid.NewGuid().ToString("N");
            string checkpoint = Path.GetFullPath("Logs/quest-visual-checkpoint.json");
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                Type.GetType("FruitFlyParenting.Editor.QuestSceneProcessor, Assembly-CSharp-Editor", true)
                    .GetMethod("Apply").Invoke(null, new object[] { scene });
                foreach (var settings in Object.FindObjectsByType<ComfortController>()) settings.UsePreferencesKey(key);
                foreach (var component in Object.FindObjectsByType<MonoBehaviour>())
                {
                    var property = component.GetType().GetProperty("CheckpointPath");
                    if (property?.CanWrite == true && property.PropertyType == typeof(string)) property.SetValue(component, checkpoint);
                }
                if (scene.name == "06_MiddleAge") GameState.Current.CaptureFinalYoungAdultSnapshot(480);
            }
            SceneManager.sceneLoaded += Loaded;
            try
            {
                Directory.CreateDirectory("Logs"); QualitySettings.renderPipeline = pipeline;
                foreach (string name in new[] { "01_Infant", "03_Childhood", "04_Adolescence", "05_YoungAdult", "06_MiddleAge" })
                {
                    yield return SceneManager.LoadSceneAsync(name); yield return new WaitForSeconds(.3f);
                    if (name == "06_MiddleAge")
                    {
                        Assert.That(Object.FindAnyObjectByType<MiddleAgeRoomController>().Result, Is.Not.Null);
                        Assert.That(GameObject.Find("Living Room Light").GetComponent<Light>().enabled, Is.True);
                    }
                    var camera = Camera.main; var target = new RenderTexture(960, 600, 24);
                    var pixels = new Texture2D(960, 600, TextureFormat.RGB24, false);
                    var active = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = target; yield return null; camera.Render(); RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, 960, 600), 0, 0); pixels.Apply();
                        var colors = pixels.GetPixels();
                        Assert.That(colors.Count(c => c.maxColorComponent > .08f), Is.GreaterThan(colors.Length / 10), name + " is too dark");
                        Assert.That(colors.Count(c => c.r > .9f && c.b > .9f && c.g < .1f), Is.LessThan(100), name + " has a shader error");
                        File.WriteAllBytes("Logs/quest-" + name + ".png", pixels.EncodeToPNG());
                    }
                    finally { camera.targetTexture = null; RenderTexture.active = active; target.Release(); Object.Destroy(target); Object.Destroy(pixels); }
                }
            }
            finally
            {
                SceneManager.sceneLoaded -= Loaded; QualitySettings.renderPipeline = previous;
                PlayerPrefs.DeleteKey(key);
            }
            yield return SceneManager.LoadSceneAsync("03_RoomShellTest");
            if (File.Exists(checkpoint)) File.Delete(checkpoint);
        }
    }
}
#endif
