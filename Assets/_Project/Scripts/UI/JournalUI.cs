using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.UI
{
    // Original, resolution-independent UI artwork. No downloaded textures or extra packages.
    public static class JournalUI
    {
        public static readonly Color Paper = new Color32(255, 250, 240, 250);
        public static readonly Color Ink = new Color32(37, 57, 59, 255);
        public static readonly Color Muted = new Color32(92, 108, 103, 255);
        public static readonly Color Sage = new Color32(54, 112, 91, 255);
        public static readonly Color Soft = new Color32(231, 237, 225, 255);
        public static readonly Color[] StatColors = {
            new Color32(181, 99, 78, 255), new Color32(75, 125, 166, 255), new Color32(67, 133, 104, 255),
            new Color32(165, 126, 54, 255), new Color32(173, 98, 126, 255), new Color32(130, 110, 160, 255)
        };
        public static readonly string[] StatNames = { "스트레스", "지능", "건강", "인기", "사랑", "반항" };

        public static void OutlineText(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .95f); outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 at)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = at;
            return rect;
        }
        public static void Dock(RectTransform rect, Vector2 anchor, Vector2 offset)
        { rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = offset; }

        public static Image Panel(Transform parent, string name, Vector2 size, Vector2 at, Color color, float radius = 20, bool shadow = false)
        {
            if (shadow)
            {
                var shade = Rect(parent, name + " shadow", size + new Vector2(4, 4), at + new Vector2(0, -5));
                var backdrop = shade.gameObject.AddComponent<JournalPanel>();
                backdrop.Radius = radius + 2; backdrop.color = new Color(.08f, .16f, .15f, .18f); backdrop.raycastTarget = false;
            }
            var rect = Rect(parent, name, size, at);
            var image = rect.gameObject.AddComponent<JournalPanel>();
            image.Radius = radius; image.color = color; image.raycastTarget = false;
            return image;
        }
        public static Text Text(Transform parent, string name, Font font, string value, Vector2 size, Vector2 at, int fontSize, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = Rect(parent, name, size, at);
            var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = fontSize;
            text.text = value; text.color = color ?? Ink; text.alignment = align;
            text.supportRichText = false; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // CJK fonts include tall ascenders/descenders: a short single-line box must not drop the entire line.
            text.verticalOverflow = size.y < fontSize * 1.7f ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            text.lineSpacing = 1.15f;
            return text;
        }
        public static JournalIcon Icon(Transform parent, int kind, Vector2 at, float size, Color color)
        {
            var rect = Rect(parent, "Journal icon " + kind, Vector2.one * size, at);
            var icon = rect.gameObject.AddComponent<JournalIcon>(); icon.Kind = kind; icon.color = color; icon.raycastTarget = false; return icon;
        }
        public static void Adapt(Canvas canvas, Vector2 size, Vector2 anchor, Vector2 margin, bool fill = false, bool hideForMenu = false)
        { canvas.gameObject.AddComponent<AdaptiveJournalCanvas>().Configure(size, anchor, margin, fill, hideForMenu); }
    }

    public sealed class JournalPanel : Image
    {
        public float Radius = 20;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = GetPixelAdjustedRect();
            float r = Mathf.Clamp(Radius, 0, Mathf.Min(rect.width, rect.height) * .5f);
            vh.AddVert(rect.center, color, Vector2.zero);
            const int segments = 8;
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r,
                    corner < 2 ? rect.yMax - r : rect.yMin + r);
                for (int step = 0; step <= segments; step++)
                {
                    float angle = (corner * 90f + step * 90f / segments) * Mathf.Deg2Rad;
                    vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, color, Vector2.zero);
                }
            }
            int count = 4 * (segments + 1);
            for (int i = 0; i < count; i++) vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
        }
    }

    public sealed class JournalIcon : MaskableGraphic
    {
        public int Kind;
        void Polygon(VertexHelper vh, params Vector2[] points)
        {
            int start = vh.currentVertCount;
            var rect = rectTransform.rect;
            foreach (var p in points) vh.AddVert(rect.center + Vector2.Scale(p, rect.size) * .5f, color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
        void Line(VertexHelper vh, Vector2 a, Vector2 b, float width = .12f)
        {
            var d = (b - a).normalized; var n = new Vector2(-d.y, d.x) * width * .5f;
            Polygon(vh, a + n, b + n, b - n, a - n);
        }
        void Disc(VertexHelper vh, Vector2 center, Vector2 radius)
        {
            var points = new Vector2[24];
            for (int i = 0; i < points.Length; i++) { float a = i * Mathf.PI * 2 / points.Length; points[i] = center + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), radius); }
            Polygon(vh, points);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (Kind)
            {
                case 0:
                    Disc(vh, Vector2.zero, Vector2.one * .36f);
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); Line(vh, d * .55f, d * .83f); } break;
                case 1:
                    Polygon(vh, new Vector2(-.8f,.65f), new Vector2(-.1f,.4f), new Vector2(-.1f,-.7f), new Vector2(-.8f,-.45f));
                    Polygon(vh, new Vector2(.1f,.4f), new Vector2(.8f,.65f), new Vector2(.8f,-.45f), new Vector2(.1f,-.7f)); break;
                case 2: Line(vh, new Vector2(-.7f,0),new Vector2(.7f,0),.38f); Line(vh,new Vector2(0,-.7f),new Vector2(0,.7f),.38f); break;
                case 3:
                    for (int i = 0; i < 5; i++) { float a = (90 + i * 72) * Mathf.Deg2Rad; float b = a + Mathf.PI / 5; float c = a + Mathf.PI * 2 / 5;
                        Polygon(vh, Vector2.zero, new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.9f, new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.4f, new Vector2(Mathf.Cos(c),Mathf.Sin(c))*.9f); } break;
                case 4:
                    Disc(vh,new Vector2(-.35f,.26f),Vector2.one*.43f); Disc(vh,new Vector2(.35f,.26f),Vector2.one*.43f);
                    Polygon(vh,new Vector2(-.75f,.1f),new Vector2(.75f,.1f),new Vector2(0,-.8f)); break;
                case 5: Polygon(vh,new Vector2(.2f,.95f),new Vector2(-.65f,-.15f),new Vector2(-.05f,-.15f),new Vector2(-.2f,-.95f),new Vector2(.7f,.2f),new Vector2(.05f,.2f)); break;
                default:
                    Disc(vh,new Vector2(-.4f,.25f),new Vector2(.45f,.28f)); Disc(vh,new Vector2(.4f,.25f),new Vector2(.45f,.28f));
                    Disc(vh,new Vector2(0,-.2f),new Vector2(.24f,.6f)); Disc(vh,new Vector2(0,.52f),Vector2.one*.25f);
                    Line(vh,new Vector2(-.1f,.65f),new Vector2(-.35f,.95f)); Line(vh,new Vector2(.1f,.65f),new Vector2(.35f,.95f)); break;
            }
        }
    }
}
