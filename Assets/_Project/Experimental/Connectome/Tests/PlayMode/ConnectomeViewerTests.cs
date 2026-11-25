#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FruitFlyParenting.Connectome.Tests
{
    public sealed class ConnectomeViewerTests
    {
        [UnityTest] public IEnumerator BothViewsRenderWithPersistentCreditAndDisclaimer()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/Experimental_ConnectomeViewer.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var viewer = Object.FindAnyObjectByType<ConnectomeViewer>();
            Assert.That(viewer, Is.Not.Null);
            Assert.That(viewer.NodeCount, Is.EqualTo(17));
            Assert.That(viewer.EdgeCount, Is.EqualTo(16));
            CheckNotice(viewer);
            yield return Capture(viewer, "step13-neurons.png");
            var button = viewer.GetComponentsInChildren<Button>().Single(b => b.name.StartsWith("기억 모티브"));
            button.onClick.Invoke(); yield return null;
            Assert.That(viewer.IsMotif, Is.True);
            Assert.That(viewer.NodeCount, Is.EqualTo(5));
            Assert.That(viewer.EdgeCount, Is.EqualTo(4));
            CheckNotice(viewer);
            yield return Capture(viewer, "step13-motif.png");
            viewer.TogglePause(); Assert.That(viewer.IsPaused, Is.True);
            var pulse = viewer.GetComponentsInChildren<Transform>().First(t => t.name == "Authored pulse");
            Vector3 position = pulse.localPosition;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(pulse.localPosition, Is.EqualTo(position));
            for (int i = 0; i < 8; i++) { viewer.ShowMotif(i % 2 == 0); yield return null; }
            Assert.That(viewer.GetComponentsInChildren<Canvas>().Count(c => c.renderMode == RenderMode.ScreenSpaceOverlay), Is.EqualTo(1));
            Assert.That(viewer.GetComponentsInChildren<Transform>().Count(t => t.name == "Selected graph"), Is.EqualTo(1));
            CheckNotice(viewer);
        }
        static void CheckNotice(ConnectomeViewer viewer)
        {
            Assert.That(viewer.CreditText.text, Is.EqualTo(ConnectomeViewer.Credit));
            Assert.That(viewer.DisclaimerText.text, Is.EqualTo(ConnectomeViewer.Disclaimer));
            Assert.That(viewer.Overlay.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            foreach (var text in new[] { viewer.CreditText, viewer.DisclaimerText })
            {
                Assert.That(text.isActiveAndEnabled, Is.True);
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height));
                var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                Assert.That(corners.All(p => p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height), Is.True);
            }
        }
        static IEnumerator Capture(ConnectomeViewer viewer, string name)
        {
            // Batch mode has no presented backbuffer. Render the same canvas through
            // the camera for a GPU readback, then restore the actual overlay mode.
            var camera = Camera.main;
            var target = new RenderTexture(1440, 900, 24);
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                viewer.Overlay.renderMode = RenderMode.ScreenSpaceCamera;
                viewer.Overlay.worldCamera = camera; viewer.Overlay.planeDistance = 1;
                Canvas.ForceUpdateCanvases(); yield return null;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels();
                Assert.That(colors.Count(c => c.grayscale > .65f), Is.GreaterThan(1500), "Readable text must render.");
                Assert.That(colors.Count(c => c.r > .9f && c.b > .9f && c.g < .1f), Is.LessThan(100), "No missing pink shaders.");
                Assert.That(pixels.GetPixels(120, 300, 1200, 360).Count(c => c.b > .3f || c.r > .5f),
                    Is.GreaterThan(100), "Graph nodes and edges must render in the central region.");
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                viewer.Overlay.renderMode = RenderMode.ScreenSpaceOverlay;
                viewer.Overlay.worldCamera = null; Canvas.ForceUpdateCanvases();
                target.Release(); Object.Destroy(target); Object.Destroy(pixels);
            }
        }
    }
}
#endif
