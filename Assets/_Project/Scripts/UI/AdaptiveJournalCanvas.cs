using FruitFlyParenting.Comfort;
using FruitFlyParenting.Wheelchair;
using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.UI
{
    // Keeps VR panels in world space and gives desktop panels predictable pixel sizing.
    [DefaultExecutionOrder(200)]
    public sealed class AdaptiveJournalCanvas : MonoBehaviour
    {
        Canvas canvas;
        RectTransform content;
        Vector2 size, anchor, margin;
        bool fill, hideForMenu, initialized, desktop;
        Transform originalParent;
        Vector3 position, scale;
        Quaternion rotation;
        public RectTransform Content => content;
        public void Configure(Vector2 dimensions, Vector2 dock, Vector2 offset, bool fillScreen, bool hideWhenMenu)
        { size = dimensions; anchor = dock; margin = offset; fill = fillScreen; hideForMenu = hideWhenMenu; }
        void Start() => Initialize();
        void Initialize()
        {
            if (initialized) return; initialized = true;
            canvas = GetComponent<Canvas>(); originalParent = transform.parent;
            position = transform.localPosition; rotation = transform.localRotation; scale = transform.localScale;
            content = JournalUI.Rect(transform, "Adaptive content", size, Vector2.zero);
            while (transform.childCount > 1) transform.GetChild(0).SetParent(content, false);
            content.SetAsFirstSibling();
            ApplyMode(DesktopControls.Current != null && DesktopControls.Current.IsDesktopMode);
        }
        void LateUpdate()
        {
            Initialize();
            bool pc = DesktopControls.Current != null && DesktopControls.Current.IsDesktopMode;
            if (pc != desktop) ApplyMode(pc);
            if (desktop) LayoutDesktop();
            if (hideForMenu) canvas.enabled = !ComfortController.IsPaused;
        }
        void ApplyMode(bool pc)
        {
            desktop = pc;
            if (pc)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
                scaler.enabled = true; scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
                LayoutDesktop();
            }
            else
            {
                var scaler = GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.scaleFactor = 1;
                if (transform.parent == originalParent) { transform.localPosition = position; transform.localRotation = rotation; }
                transform.localScale = scale;
                ((RectTransform)transform).sizeDelta = size;
                content.anchorMin = content.anchorMax = content.pivot = Vector2.one * .5f;
                content.anchoredPosition = Vector2.zero; content.sizeDelta = size; content.localScale = Vector3.one;
            }
        }
        void LayoutDesktop()
        {
            var screen = ((RectTransform)transform).rect.size;
            if (fill)
            {
                content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.pivot = Vector2.one * .5f;
                content.offsetMin = content.offsetMax = Vector2.zero; content.localScale = Vector3.one;
            }
            else
            {
                JournalUI.Dock(content, anchor, margin); content.sizeDelta = size;
                // Dialogs and end cards retain their layout while fitting small windows.
                float fit = Mathf.Min(1, (screen.x - 48) / size.x, (screen.y - 64) / size.y);
                content.localScale = Vector3.one * Mathf.Max(.1f, fit);
            }
        }
    }
}
