using System.Collections.Generic;
using FruitFlyParenting.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.Room
{
    // 양안에서 보이는 카메라 자식 World Space UI. 포인터/버튼은 사용하지 않는다.
    public sealed class InfantRoomView : MonoBehaviour
    {
        [SerializeField] Font font;
        Text status, narrative;
        Canvas hud;
        Camera viewer;
        readonly List<Transform> labels = new List<Transform>();
        float narrativeUntil;
        string roomTitle = "첫 손길";
        int itemCount = 9;
        public string StatusText => status != null ? status.text : string.Empty;
        public string NarrativeText => narrative != null ? narrative.text : string.Empty;
        public void Configure(Font textFont) => font = textFont;

        public void Initialize(Camera camera, RoomController room, string title = "첫 손길")
        {
            roomTitle = title; itemCount = room.ItemSlots.Count;
            viewer = camera;
            hud = CanvasAt("Infant HUD", camera.transform, new Vector3(0f, -0.42f, 1.5f), new Vector2(1100, 250), 10);
            Panel(hud.transform, new Color(0.04f, 0.07f, 0.1f, 0.88f));
            status = Label(hud.transform, "Status", new Vector2(1060, 140), new Vector2(0, 45), 28);
            narrative = Label(hud.transform, "Narrative", new Vector2(1060, 85), new Vector2(0, -72), 26);
            foreach (var slot in room.ItemSlots)
            {
                var plate = CanvasAt("Item Name", slot.transform, new Vector3(0, 1.8f, 0), new Vector2(600, 100), 0);
                plate.transform.localScale = Vector3.one * 0.002f;
                Panel(plate.transform, new Color(0.06f, 0.12f, 0.18f, 0.95f));
                var text = Label(plate.transform, "Name", new Vector2(580, 90), Vector2.zero, 32);
                text.text = slot.Definition.displayName + "\n" + string.Join(" · ", slot.Definition.tags);
                labels.Add(plate.transform);
            }
        }

        public void Refresh(RoomTimeline clock, GameState state, int visited, string guidanceOverride = null)
        {
            string guidance = !clock.ToolsEnabled ? $"잠시 지켜봐 주세요 · 개입까지 {Mathf.CeilToInt(clock.ToolCooldownRemaining)}초" :
                clock.ExitOnly ? "이제 복도로 갈 시간이에요 · 왼손 스틱으로 동쪽 문까지 이동하세요" :
                clock.DoorOpen ? "동쪽 문이 열렸어요 · 왼손 스틱으로 출구를 통과하세요" : "왼손: 이동·회전  |  오른손: 전기 봉 접촉";
            if (guidanceOverride != null) guidance = guidanceOverride;
            var s = state.Stats;
            status.text = $"{roomTitle}  {Mathf.FloorToInt(clock.Elapsed / 60):00}:{Mathf.FloorToInt(clock.Elapsed % 60):00}  ·  경험 {visited}/{itemCount}\n{guidance}\n" +
                $"스트레스 {s.GetValue(StatType.Stress):0} · 지능 {s.GetValue(StatType.Intelligence):0} · 건강 {s.GetValue(StatType.Health):0} · 인기 {s.GetValue(StatType.Popularity):0} · 사랑 {s.GetValue(StatType.Love):0} · 반항 {s.GetValue(StatType.Rebellion):0}";
            if (Time.time > narrativeUntil) narrative.text = string.Empty;
        }

        public void ShowNarrative(string line) { narrative.text = line; narrativeUntil = Time.time + 8f; }
        public void HideHud() { if (hud != null) hud.gameObject.SetActive(false); }
        void OnDestroy() { if (hud != null) Destroy(hud.gameObject); }

        void LateUpdate()
        {
            if (viewer == null) return;
            foreach (var label in labels)
                if (label != null) label.rotation = Quaternion.LookRotation(label.position - viewer.transform.position, Vector3.up);
        }

        Canvas CanvasAt(string name, Transform parent, Vector3 position, Vector2 size, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.localPosition = position; rect.localScale = Vector3.one * 0.001f; rect.sizeDelta = size;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = viewer; canvas.sortingOrder = order;
            return canvas;
        }
        static Image Panel(Transform parent, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }
        Text Label(Transform parent, string name, Vector2 size, Vector2 position, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white; text.raycastTarget = false; text.supportRichText = false;
            return text;
        }
    }
}
