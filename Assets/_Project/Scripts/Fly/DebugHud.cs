using System.Globalization;
using System.Text;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FruitFlyParenting.Fly
{
    // World-space overlay for both eyes. Never allocates UI or reads keys in a release player.
    public sealed class DebugHud : MonoBehaviour
    {
        public static bool IsAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
        FlyAgent fly;
        GameBalanceConfig config;
        Canvas canvas;
        Text label;
        float refreshAt;
        public bool Visible => canvas != null && canvas.gameObject.activeSelf;
        public string DisplayText => label != null ? label.text : string.Empty;
        public void Configure(FlyAgent target, GameBalanceConfig balance) { fly = target; config = balance; }
        void Update()
        {
            if (!IsAvailable || fly == null) return;
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) SetVisible(!Visible);
            if (Visible && Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + config.debugHudRefreshSeconds;
                label.text = Describe(fly.CurrentDestinationId, fly.LastChoice, GameState.Current?.RunSeed ?? 0);
            }
        }
        public void SetVisible(bool visible)
        {
            if (!IsAvailable) return;
            if (visible && canvas == null)
            {
                var camera = Camera.main;
                if (camera == null) return;
                var go = new GameObject("Preference Debug HUD", typeof(RectTransform), typeof(Canvas), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(camera.transform, false); rect.localPosition = new Vector3(0,.25f,1.5f);
                rect.localScale = Vector3.one * .001f; rect.sizeDelta = new Vector2(1400,700);
                canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; canvas.sortingOrder = 30;
                var panel = go.GetComponent<Image>(); panel.color = new Color(.025f,.04f,.06f,.96f); panel.raycastTarget = false;
                var text = new GameObject("Choice Reasons", typeof(RectTransform), typeof(Text));
                var textRect = (RectTransform)text.transform; textRect.SetParent(rect, false);
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(24,24); textRect.offsetMax = new Vector2(-24,-24);
                label = text.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 25; label.color = new Color(.85f,.95f,1f); label.raycastTarget = false;
                label.alignment = TextAnchor.UpperLeft; label.supportRichText = false;
            }
            if (canvas != null) { canvas.gameObject.SetActive(visible); refreshAt = 0; }
        }
        public static string Describe(string destination, ItemChoiceDecision decision, int seed)
        {
            var text = new StringBuilder("PREFERENCE DEBUG  |  F8: hide\n");
            text.Append("RunSeed: ").Append(seed).Append("   Destination: ").Append(destination).Append('\n');
            if (decision == null) return text.Append("No choice yet.").ToString();
            text.AppendFormat(CultureInfo.InvariantCulture, "Last choice: {0}   Exploration: {1:P0}   Uniform phase: {2}\n",
                decision.SelectedId, decision.ExplorationFloor, decision.UniformPhase);
            text.Append("score = base + preference - avoidance + need + noise\n\n");
            foreach (var candidate in decision.Candidates)
                text.AppendFormat(CultureInfo.InvariantCulture,
                    "{0}  P={1:P1}  score={2:F2}\n  base {3:F2} + pref {4:F2} - avoid {5:F2} + need {6:F2} + noise {7:F2}\n",
                    candidate.Id, candidate.Probability, candidate.Score, candidate.BaseAttraction, candidate.Preference,
                    candidate.Avoidance, candidate.Need, candidate.Noise);
            return text.ToString();
        }
        void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
        void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    }
}
