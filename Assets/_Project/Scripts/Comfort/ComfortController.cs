using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Wheelchair;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace FruitFlyParenting.Comfort
{
    [DefaultExecutionOrder(-230)]
    public sealed class ComfortController : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] Material overlayMaterial;
        [SerializeField] string preferencesKey = ComfortPreferences.DefaultKey;
        WheelchairLocomotion chair;
        XROrigin origin;
        Camera viewer;
        Vector3 originBase;
        Canvas menu, launcher, feedback, captions, vignetteCanvas, fadeCanvas;
        Text values, popup, caption, menuHelp, launcherText;
        Image vignetteImage, curtain;
        Texture2D vignetteTexture;
        Sprite vignetteSprite;
        InputAction menuInput;
        GameState observed;
        readonly Dictionary<StatType, float> deltas = new Dictionary<StatType, float>();
        float previousTimeScale, popupUntil, captionUntil;
        bool adjustingHeight;
        string menuMessage;
        float menuMessageUntil;
        public static ComfortController Current { get; private set; }
        public static bool IsPaused => Current != null && Current.MenuOpen;
        public Material OverlayMaterial => overlayMaterial;
        public Font InterfaceFont => font;
        public ComfortPreferences Preferences { get; private set; }
        public bool MenuOpen { get; private set; }
        public float VignetteAlpha => vignetteImage != null ? vignetteImage.color.a : 0;
        public float HeightFadeAlpha => curtain != null ? curtain.color.a : 0;
        public bool AdjustingHeight => adjustingHeight;
        public string PopupText => popup != null ? popup.text : "";
        public string Caption => caption != null ? caption.text : "";
        public Transform PopupTransform => feedback.transform;
        public void Configure(Font value, Material overlay) { font = value; overlayMaterial = overlay; }
        public void UsePreferencesKey(string key) => preferencesKey = key;

        void Start()
        {
            chair = GetComponent<WheelchairLocomotion>(); origin = GetComponentInChildren<XROrigin>();
            viewer = origin.Camera; originBase = origin.transform.localPosition;
            Preferences = ComfortPreferences.Load(preferencesKey); Current = this;
            origin.transform.localPosition = originBase + Vector3.up * Preferences.heightOffset;
            chair.ComfortTurnSpeed = Preferences.turnSpeed;
            BuildUI();
            menuInput = new InputAction("Comfort settings", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
            menuInput.Enable();
        }
        void Update()
        {
            if (Preferences == null) return;
            if (menuInput.WasPressedThisFrame() && !adjustingHeight) SetMenuOpen(!MenuOpen);
            chair.ComfortInputBlocked = MenuOpen || adjustingHeight;
            if (!ReferenceEquals(observed, GameState.Current))
            {
                if (observed != null) observed.Stats.OnStatChanged -= StatChanged;
                observed = GameState.Current; deltas.Clear();
                if (observed != null) observed.Stats.OnStatChanged += StatChanged;
            }
            float strength = Mathf.Max(chair.ActualVelocity.magnitude / .8f, Mathf.Abs(chair.CommandedTurnSpeed) / Mathf.Max(1, Preferences.turnSpeed));
            float target = Preferences.vignette && chair.enabled && !MenuOpen ? Mathf.Clamp01(strength) * .65f : 0;
            vignetteImage.color = new Color(0, 0, 0, Mathf.MoveTowards(VignetteAlpha, target, Time.unscaledDeltaTime * 3));
            feedback.gameObject.SetActive(!MenuOpen && Time.unscaledTime < popupUntil);
            if (Time.unscaledTime >= popupUntil) deltas.Clear();
            captions.gameObject.SetActive(!MenuOpen && Time.unscaledTime < captionUntil);
            if (MenuOpen) values.text = $"회전 {Preferences.turnSpeed:0}°/초   ·   시야 축소 {(Preferences.vignette ? "켜짐" : "꺼짐")}\n높이 보정 {Preferences.heightOffset:+0.00;-0.00;0.00} m   ·   자막 항상 켜짐";
            if (MenuOpen && Time.unscaledTime < menuMessageUntil) values.text = menuMessage;
            menuHelp.text = DesktopControls.Current != null && DesktopControls.Current.IsDesktopMode
                ? "버튼을 클릭해 조절하세요 · 돌아가기를 누르면 게임을 계속합니다" : "버튼을 2초 바라보세요 · 왼손 이동 / 오른손 전기 봉";
            launcherText.text = DesktopControls.Current != null && DesktopControls.Current.IsDesktopMode
                ? "설정" : "설정 · 왼손 Y";
        }
        public void SetMenuOpen(bool open)
        {
            if (menu == null || adjustingHeight || open == MenuOpen) return;
            if (open && FindObjectsByType<TransitionView>().Any(v => v.FadeAlpha > .001f)) return;
            MenuOpen = open;
            if (open)
            {
                previousTimeScale = Time.timeScale; Time.timeScale = 0;
                // Anchor in the world once: head-gaze must move across fixed buttons.
                menu.transform.SetParent(null, true);
                menu.transform.SetPositionAndRotation(viewer.transform.position + viewer.transform.forward * 1.5f, viewer.transform.rotation);
            }
            else Time.timeScale = previousTimeScale;
            menu.gameObject.SetActive(open); launcher.gameObject.SetActive(!open);
            chair.ComfortInputBlocked = open;
        }
        public void ChangeTurn(float amount)
        {
            Preferences.turnSpeed += amount; Preferences.Save(preferencesKey); chair.ComfortTurnSpeed = Preferences.turnSpeed;
        }
        public void ToggleVignette() { Preferences.vignette = !Preferences.vignette; Preferences.Save(preferencesKey); }
        public void ChangeHeight(float amount) => SetHeight(Preferences.heightOffset + amount);
        public void CalibrateHeight()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) || !tracked)
            { menuMessage = "자동 보정에는 헤드셋 추적이 필요해요.\n추적을 확인하거나 높이 ±5 cm 버튼을 사용하세요."; menuMessageUntil = Time.unscaledTime + 5; return; }
            float measured = transform.InverseTransformPoint(viewer.transform.position).y;
            SetHeight(ComfortPreferences.CalibratedOffset(Preferences.heightOffset, measured));
        }
        public void SetHeight(float value)
        {
            if (!MenuOpen || adjustingHeight || float.IsNaN(value) || float.IsInfinity(value)) return;
            StartCoroutine(ApplyHeight(Mathf.Clamp(value, -ComfortPreferences.HeightLimit, ComfortPreferences.HeightLimit)));
        }
        IEnumerator ApplyHeight(float value)
        {
            adjustingHeight = true;
            for (float t = 0; t < .5f; t += Time.unscaledDeltaTime) { curtain.color = new Color(0, 0, 0, t / .5f); yield return null; }
            curtain.color = Color.black;
            Preferences.heightOffset = value; Preferences.Save(preferencesKey);
            // User-requested vertical calibration only, under an opaque curtain.
            // The wheelchair root, tracked local pose and yaw are never moved here.
            origin.transform.localPosition = originBase + Vector3.up * value;
            yield return null;
            for (float t = 0; t < .5f; t += Time.unscaledDeltaTime) { curtain.color = new Color(0, 0, 0, 1 - t / .5f); yield return null; }
            curtain.color = Color.clear; adjustingHeight = false;
        }
        public static void Announce(string line)
        {
            if (Current == null || Current.caption == null) return;
            Current.caption.text = line; Current.captionUntil = Time.unscaledTime + 5;
        }
        void StatChanged(StatType type, float oldValue, float newValue, string reason)
        {
            float delta = newValue - oldValue; if (Mathf.Abs(delta) < .001f) return;
            if (Time.unscaledTime >= popupUntil) deltas.Clear();
            deltas[type] = (deltas.TryGetValue(type, out float value) ? value : 0) + delta;
            popupUntil = Time.unscaledTime + 4;
            popup.text = string.Join("\n", deltas.OrderBy(p => p.Key).Select(p => FormatDelta(p.Key, p.Value)));
        }
        public static string FormatDelta(StatType type, float delta)
        {
            string name = new[] { "스트레스", "지능", "건강", "인기", "사랑", "반항" }[(int)type];
            bool beneficial = type == StatType.Stress || type == StatType.Rebellion ? delta < 0 : delta > 0;
            return $"<color={(beneficial ? "#77E4B1" : "#FFA18D")}>{(delta >= 0 ? "▲" : "▼")} {name} {delta:+0.0;-0.0;0.0}</color>";
        }
        Canvas Board(string name, Vector3 at, Vector2 size, int order) => CorridorUI.Canvas(name, viewer.transform, at, size, .001f, order);
        void BuildUI()
        {
            launcher = Board("Comfort launcher", new Vector3(-.48f, .34f, 1.5f), new Vector2(180, 50), 100);
            launcher.transform.SetParent(transform, false); launcher.transform.localPosition = new Vector3(-.55f, 1.05f, .9f);
            launcher.transform.localScale = Vector3.one * .001f;
            ActionButton(launcher.transform, "설정", Vector2.zero, new Vector2(180, 50), () => SetMenuOpen(true), 22);
            launcherText = launcher.GetComponentInChildren<Text>();
            UI.JournalUI.Adapt(launcher, new Vector2(180,50), new Vector2(1,1), new Vector2(-16,-16));
            menu = Board("Comfort settings", new Vector3(0, 0, 1.5f), new Vector2(1000, 720), 150);
            CorridorUI.Image(menu.transform, "Background", new Vector2(1000, 720), Vector2.zero, new Color(.025f,.045f,.07f,.98f));
            CorridorUI.Text(menu.transform, "Title", font, "설정", new Vector2(950, 70), new Vector2(0, 300), 38);
            values = CorridorUI.Text(menu.transform, "Values", font, "", new Vector2(940, 90), new Vector2(0, 210), 28);
            ActionButton(menu.transform, "회전 느리게 −5°", new Vector2(-240, 110), new Vector2(400, 65), () => ChangeTurn(-5));
            ActionButton(menu.transform, "회전 빠르게 +5°", new Vector2(240, 110), new Vector2(400, 65), () => ChangeTurn(5));
            ActionButton(menu.transform, "이동 중 시야 축소 켜기 / 끄기", new Vector2(0, 25), new Vector2(880, 65), ToggleVignette);
            ActionButton(menu.transform, "높이 낮추기 −5 cm", new Vector2(-240, -60), new Vector2(400, 65), () => ChangeHeight(-.05f));
            ActionButton(menu.transform, "높이 높이기 +5 cm", new Vector2(240, -60), new Vector2(400, 65), () => ChangeHeight(.05f));
            ActionButton(menu.transform, "1.20 m 기준 자동 보정 (범위 ±35 cm)", new Vector2(0, -145), new Vector2(880, 65), CalibrateHeight);
            ActionButton(menu.transform, "돌아가기", new Vector2(0, -240), new Vector2(880, 65), () => SetMenuOpen(false));
            menuHelp = CorridorUI.Text(menu.transform, "Help", font, "", new Vector2(950, 45), new Vector2(0, -315), 22);
            UI.JournalUI.Adapt(menu, new Vector2(1000,720), Vector2.one*.5f, Vector2.zero);
            menu.gameObject.SetActive(false);
            feedback = Board("Peripheral stat changes", new Vector3(-.62f, .08f, 1.5f), new Vector2(380, 240), 90);
            popup = CorridorUI.Text(feedback.transform, "Delta arrows", font, "", new Vector2(360, 230), Vector2.zero, 27); popup.supportRichText = true;
            popup.color = Color.white; UI.JournalUI.OutlineText(popup);
            UI.JournalUI.Adapt(feedback, new Vector2(380,240), new Vector2(1,.5f), new Vector2(-24,0), false, true);
            captions = Board("Accessible event captions", new Vector3(0, -.21f, 1.5f), new Vector2(760, 70), 80);
            UI.JournalUI.Panel(captions.transform, "Black background", new Vector2(760, 70), Vector2.zero, Color.black, 0);
            caption = UI.JournalUI.Text(captions.transform, "Caption", font, "", new Vector2(720, 65), Vector2.zero, 25, Color.white, TextAnchor.MiddleCenter);
            UI.JournalUI.Adapt(captions, new Vector2(760,70), new Vector2(.5f,0), new Vector2(0,236), false, true);
            vignetteCanvas = Board("Movement vignette", new Vector3(0, 0, .7f), new Vector2(3000, 3000), 50);
            vignetteImage = CorridorUI.Image(vignetteCanvas.transform, "Soft periphery", new Vector2(3000, 3000), Vector2.zero, Color.clear);
            vignetteImage.material = overlayMaterial;
            vignetteTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false); var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float radius = new Vector2((x - 127.5f) / 127.5f, (y - 127.5f) / 127.5f).magnitude;
                pixels[y * 256 + x] = new Color(0, 0, 0, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.32f, .65f, radius)));
            }
            vignetteTexture.SetPixels(pixels); vignetteTexture.Apply(); vignetteTexture.wrapMode = TextureWrapMode.Clamp;
            vignetteSprite = Sprite.Create(vignetteTexture, new Rect(0, 0, 256, 256), Vector2.one * .5f);
            vignetteImage.sprite = vignetteSprite;
            fadeCanvas = Board("Height calibration curtain", new Vector3(0, 0, .5f), new Vector2(6000, 6000), 1200);
            curtain = CorridorUI.Image(fadeCanvas.transform, "Curtain", new Vector2(6000, 6000), Vector2.zero, Color.clear);
            curtain.material = overlayMaterial;
            UI.JournalUI.Adapt(fadeCanvas, new Vector2(6000,6000), Vector2.one*.5f, Vector2.zero, true);
        }
        void ActionButton(Transform parent, string label, Vector2 at, Vector2 size, Action action, int fontSize = 27)
        {
            var image = UI.JournalUI.Panel(parent, label, size, at, new Color32(25,28,32,255), 2);
            var text = CorridorUI.Text(image.transform, "Label", font, label, size - new Vector2(12, 4), Vector2.zero, fontSize);
            text.color = Color.white;
            image.gameObject.AddComponent<GazeAction>().Configure(() => { if (!adjustingHeight) action(); }, image, 2, true);
        }
        void OnDestroy()
        {
            if (MenuOpen) Time.timeScale = previousTimeScale;
            if (Current == this) Current = null;
            if (observed != null) observed.Stats.OnStatChanged -= StatChanged;
            menuInput?.Dispose();
            foreach (var canvas in new[] { menu, launcher, feedback, captions, vignetteCanvas, fadeCanvas }) if (canvas != null) Destroy(canvas.gameObject);
            if (vignetteSprite != null) Destroy(vignetteSprite);
            if (vignetteTexture != null) Destroy(vignetteTexture);
        }
    }
}
