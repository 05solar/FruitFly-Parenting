using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.Corridor
{
    public static class CorridorUI
    {
        public static Canvas Canvas(string name, Transform parent, Vector3 position, Vector2 size, float scale, int order = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.localPosition = position; rect.localScale = Vector3.one * scale; rect.sizeDelta = size;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = order;
            return canvas;
        }
        public static Image Image(Transform parent, string name, Vector2 size, Vector2 at, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = at;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        public static Text Text(Transform parent, string name, Font font, string value, Vector2 size, Vector2 at, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = at;
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; text.supportRichText = false;
            return text;
        }
    }
}
