using System.Collections.Generic;
using FruitFlyParenting.Core;
using FruitFlyParenting.Fly;
using FruitFlyParenting.UI;
using FruitFlyParenting.Wheelchair;
using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.Room
{
    public sealed class InfantRoomView : MonoBehaviour
    {
        [SerializeField] Font font;
        Canvas hud, childMarker;
        Camera viewer;
        RoomController observedRoom;
        FlyAgent child;
        float headOffset=.4f;
        Text details, guidance, speech;
        readonly Text[] values = new Text[6];
        readonly RectTransform[] bars = new RectTransform[6];
        readonly List<Transform> labels = new List<Transform>();
        readonly List<float> labelScales = new List<float>();
        float narrativeUntil;
        string roomTitle = "첫 손길", statusText = "", narrativeText = "";
        int itemCount;
        RectTransform statColumn;
        readonly Vector3[] boundsCorners = new Vector3[4];
        public string StatusText => statusText;
        public string DialogueText => narrativeText;
        public void Configure(Font textFont) => font = textFont;

        public void Initialize(Camera camera, RoomController room, string title = "첫 손길")
        {
            viewer = camera; observedRoom = room; roomTitle = title; itemCount = room.ItemSlots.Count;
            hud = CanvasAt("Infant HUD", camera.transform, new Vector3(0, 0, 1.6f), new Vector2(1440, 900), .001f, 30);
            var stats = JournalUI.Rect(hud.transform, "Stat column", new Vector2(252, 500), Vector2.zero);
            statColumn = stats;
            JournalUI.Dock(stats, new Vector2(0, .5f), new Vector2(24, 20));
            stats.localScale = Vector3.one * .65f;
            for (int i = 0; i < 6; i++)
            {
                var row = JournalUI.Rect(stats, "Stat " + i, new Vector2(252, 64), new Vector2(0, 210 - i * 74));
                HudText(row, "Name", JournalUI.StatNames[i], new Vector2(140, 34), new Vector2(-48, 17), 22);
                values[i] = HudText(row, "Value", "0", new Vector2(104, 34), new Vector2(66, 17), 22, TextAnchor.MiddleRight);
                JournalUI.Panel(row, "Track", new Vector2(236, 14), new Vector2(0, -16), new Color(0, 0, 0, .85f), 0);
                bars[i] = (RectTransform)JournalUI.Panel(row, "Fill", new Vector2(118, 14), new Vector2(-118, -16), JournalUI.StatColors[i], 0).transform;
                bars[i].pivot = new Vector2(0, .5f);
            }
            details = HudText(stats, "Room details", "", new Vector2(252, 32), new Vector2(0, -225), 18);
            guidance = HudText(hud.transform, "Guidance", "", new Vector2(860, 36), Vector2.zero, 20, TextAnchor.MiddleCenter);
            JournalUI.Dock(guidance.rectTransform, new Vector2(.5f, 0), new Vector2(0, 48));
            JournalUI.Adapt(hud, new Vector2(1440, 900), Vector2.one * .5f, Vector2.zero, true, true);

            foreach (var slot in room.ItemSlots)
            {
                bool recovery = false;
                foreach (var delta in slot.Definition.statDeltas)
                    if (!slot.Definition.isExtreme && !slot.Definition.recordsDelinquency && delta.stat == StatType.Stress && delta.amount <= -5) recovery = true;
                var plate = CanvasAt("Item Name", slot.transform, new Vector3(0, 1.85f, 0), new Vector2(540, 116), .0022f, 0);
                var name = HudText(plate.transform, "Name", slot.Definition.displayName, new Vector2(480, 53), new Vector2(4, 22), 34, TextAnchor.MiddleCenter);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 26; name.resizeTextMaxSize = 34;
                HudText(plate.transform, "Category", recovery ? "스트레스 감소 · " + string.Join(" · ", slot.Definition.tags) : string.Join(" · ", slot.Definition.tags),
                    new Vector2(480, 36), new Vector2(4, -29), 25, TextAnchor.MiddleCenter);
                labels.Add(plate.transform); labelScales.Add(.0022f);
            }
        }

        Text HudText(Transform parent, string name, string value, Vector2 size, Vector2 at, int fontSize, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var text = JournalUI.Text(parent, name, font, value, size, at, fontSize, Color.white, align);
            JournalUI.OutlineText(text); return text;
        }
        public void Refresh(RoomTimeline clock, GameState state, int visited, string guidanceOverride = null)
        {
            string instruction = clock.ExitOnly || clock.DoorOpen ? "동쪽 출구 개방 · 다음 방으로 이동" : "";
            if (guidanceOverride != null) instruction = guidanceOverride;
            details.text = $"{Mathf.FloorToInt(clock.Elapsed / 60):00}:{Mathf.FloorToInt(clock.Elapsed % 60):00} · 경험 {visited}/{itemCount}";
            guidance.text = instruction;
            statusText = roomTitle + " · " + details.text + "\n" + instruction;
            for (int i = 0; i < 6; i++)
            {
                float value = state.Stats.GetValue((StatType)i);
                values[i].text = Mathf.RoundToInt(value).ToString(); bars[i].sizeDelta = new Vector2(236 * Mathf.Clamp01(value / 100), 14);
                statusText += $" · {JournalUI.StatNames[i]} {value:0}";
            }
            if (Time.time > narrativeUntil)
            {
                narrativeText = "";
            }
        }
        public void ShowDialogue(string line)
        { narrativeText = line ?? ""; narrativeUntil = Time.time + Mathf.Clamp(narrativeText.Length * .16f, 3.5f, 6f); }
        public static bool SpeakFor(FlyAgent actor, string line)
        {
            if(actor==null) return false;
            foreach(var view in Object.FindObjectsByType<InfantRoomView>())
                if(view.observedRoom!=null && view.observedRoom.Stage==actor.Stage && view.hud!=null && view.hud.gameObject.activeInHierarchy)
                { view.ShowDialogue(line); return true; }
            return false;
        }
        public void HideHud() { if (hud != null) hud.gameObject.SetActive(false); if (childMarker != null) childMarker.gameObject.SetActive(false); }
        void OnDestroy() { if (hud != null) Destroy(hud.gameObject); if (childMarker != null) Destroy(childMarker.gameObject); }
        void LateUpdate()
        {
            if (viewer == null) return;
            for (int i = 0; i < labels.Count; i++) if (labels[i] != null)
            {
                var label = labels[i]; label.rotation = Quaternion.LookRotation(label.position - viewer.transform.position, Vector3.up);
                // Keep distant labels legible without allowing them to grow without bound.
                float distance = Vector3.Distance(label.position, viewer.transform.position);
                label.localScale = Vector3.one * labelScales[i] * Mathf.Clamp(distance / 4, 1, 3);
                // Reserve the HUD area without putting an opaque card behind the stat column.
                bool show = true;
                if (hud != null && hud.enabled && hud.gameObject.activeSelf && hud.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    var labelBounds = ScreenBounds((RectTransform)label, viewer);
                    show = viewer.WorldToScreenPoint(label.position).z > 0 && !labelBounds.Overlaps(ScreenBounds(statColumn, null));
                }
                if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            }
            if (hud == null || !hud.gameObject.activeSelf) return;
            if (child == null || !child.gameObject.activeInHierarchy)
            {
                if (childMarker != null) Destroy(childMarker.gameObject); child = null;
                foreach (var actor in FlyAgent.SceneActors)
                    if (actor != null && actor.isActiveAndEnabled && actor.Stage == observedRoom.Stage) { child = actor; break; }
                if (child != null)
                {
                    childMarker = CanvasAt("Child Dialogue", child.transform, Vector3.up * 1.1f, new Vector2(360, 100), .002f, 2);
                    JournalUI.Panel(childMarker.transform, "Speech background", new Vector2(360, 100), Vector2.zero, new Color(0, 0, 0, .28f), 8);
                    speech = HudText(childMarker.transform, "Speech", "", new Vector2(332, 86), Vector2.zero, 24, TextAnchor.MiddleCenter);
                }
            }
            if (childMarker == null) return;
            if (Time.time > narrativeUntil) narrativeText = "";
            if(speech.text!=narrativeText)
            {
                speech.text=narrativeText;
                float width=Mathf.Clamp(speech.preferredWidth+28,100,360);
                speech.rectTransform.sizeDelta=new Vector2(width-28,160);
                float height=Mathf.Max(48,speech.preferredHeight+18);
                ((RectTransform)childMarker.transform).sizeDelta=new Vector2(width,height);
                ((RectTransform)childMarker.transform.Find("Speech background")).sizeDelta=new Vector2(width,height);
                speech.rectTransform.sizeDelta=new Vector2(width-28,height-18);
            }
            childMarker.enabled = !string.IsNullOrEmpty(narrativeText) && !Comfort.ComfortController.IsPaused;
            childMarker.transform.rotation = Quaternion.LookRotation(childMarker.transform.position - viewer.transform.position, Vector3.up);
            float speechDistance = Vector3.Distance(viewer.transform.position, childMarker.transform.position);
            float scale = .0035f * Mathf.Clamp(speechDistance / 4f, 1f, 3f);
            childMarker.transform.localScale = Vector3.one * scale / Mathf.Max(.01f, child.transform.lossyScale.x);
            var body=child.GetComponent<Collider>();
            if(body!=null && body.enabled) headOffset=body.bounds.max.y-child.transform.position.y;
            var character=child.GetComponent<FlyCharacterArt>();
            if(character!=null && character.Visual!=null)headOffset=Mathf.Max(headOffset,character.TopY-child.transform.position.y);
            childMarker.transform.position=child.transform.position+Vector3.up*(headOffset+.1f+((RectTransform)childMarker.transform).rect.height*scale*.5f);
        }
        Canvas CanvasAt(string name, Transform parent, Vector3 at, Vector2 size, float scale, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.localPosition = at; rect.localScale = Vector3.one * scale; rect.sizeDelta = size;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = viewer; canvas.sortingOrder = order; return canvas;
        }
        Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            rect.GetWorldCorners(boundsCorners);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in boundsCorners)
            {
                Vector2 point = camera != null ? (Vector2)camera.WorldToScreenPoint(corner) : (Vector2)corner;
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
