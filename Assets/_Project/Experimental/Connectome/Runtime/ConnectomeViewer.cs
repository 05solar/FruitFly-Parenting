using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FruitFlyParenting.Connectome
{
    public sealed class ConnectomeViewer : MonoBehaviour
    {
        public const string Credit = "FlyWire Consortium, Nature 2024 · FAFB v783 · CC BY-NC 4.0";
        public const string Disclaimer = "이 시각화는 창작된 게임 연출이며 실제 초파리의 행동을 재현하지 않습니다";
        public const string Boundary = "실제 데이터: 선택 뉴런의 연결·시냅스 수 / 창작: 배치·색·빛의 움직임";
        [SerializeField] TextAsset neuronData, motifData;
        [SerializeField] Font font;
        [SerializeField] Material graphMaterial;
        [SerializeField] Camera viewerCamera;
        readonly List<Material> materials = new List<Material>();
        readonly List<Transform> labels = new List<Transform>();
        readonly List<Pulse> pulses = new List<Pulse>();
        Transform graphRoot;
        Canvas overlay;
        Text summary, detail, modeButton, pauseButton;
        float phase, yaw, pitch;
        bool paused;
        public bool IsMotif { get; private set; }
        public ConnectomeGraph Graph { get; private set; }
        public int NodeCount => Graph?.nodes.Length ?? 0;
        public int EdgeCount => Graph?.edges.Length ?? 0;
        public Canvas Overlay => overlay;
        public Text DisclaimerText { get; private set; }
        public Text CreditText { get; private set; }
        public bool IsPaused => paused;
        struct Pulse { public Transform marker; public Vector3 a, b; public float offset; }
        static readonly Color[] Palette = {
            new Color(.22f,.86f,.83f), new Color(.32f,.67f,1), new Color(.74f,.55f,1),
            new Color(1,.67f,.35f), new Color(1,.42f,.55f)
        };

        public void Configure(TextAsset neurons, TextAsset motif, Font displayFont, Material material, Camera camera)
        { neuronData = neurons; motifData = motif; font = displayFont; graphMaterial = material; viewerCamera = camera; }

        void Start()
        {
            CreateOverlay(); // Source/disclaimer remain visible even if graph validation fails.
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var material = new Material(graphMaterial) { color = Palette[i] };
                    materials.Add(material);
                }
                ShowMotif(false);
            }
            catch (Exception error)
            {
                summary.text = "데이터를 열 수 없습니다";
                detail.text = error.Message;
                Debug.LogException(error, this);
                enabled = false;
            }
        }

        public void ShowMotif(bool motif)
        {
            var parsed = ConnectomeGraph.Parse((motif ? motifData : neuronData).text);
            if (graphRoot != null) { graphRoot.gameObject.SetActive(false); Destroy(graphRoot.gameObject); }
            pulses.Clear(); labels.Clear(); phase = 0;
            graphRoot = new GameObject("Selected graph").transform;
            graphRoot.SetParent(transform, false);
            Graph = parsed; IsMotif = motif;
            var positions = new Dictionary<string, Vector3>();
            var counts = new int[5]; var index = new int[5];
            foreach (var node in Graph.nodes) counts[node.stage]++;
            foreach (var node in Graph.nodes)
            {
                int row = index[node.stage]++;
                var position = new Vector3((node.stage - 2) * 3.05f,
                    (row - (counts[node.stage] - 1) * .5f) * .85f,
                    motif ? Mathf.Sin(node.stage * 1.4f) * .6f : (row % 2 == 0 ? -.3f : .3f));
                positions.Add(node.id, position);
                Sphere("Node " + node.id, position, motif ? .42f : .22f, materials[node.stage]);
                Label((motif ? node.label + " · " + node.members.Length : node.side + " · " + node.id.Substring(node.id.Length - 6)),
                    position + Vector3.up * .29f, motif ? .026f : .018f);
            }
            var titles = new[] { "01  후각 입력", "02  ALPN", "03  KC / 연합", "04  MBON", "05  하행 출력" };
            for (int i = 0; i < 5; i++)
                Label(titles[i], new Vector3((i - 2) * 3.05f, 3.3f, 0), .022f).color = Palette[i];
            for (int i = 0; i < Graph.edges.Length; i++)
            {
                var edge = Graph.edges[i]; var a = positions[edge.source]; var b = positions[edge.target];
                int stage = Array.Find(Graph.nodes, n => n.id == edge.source).stage;
                float width = .018f + .065f * edge.visualWeight;
                Line("Edge " + edge.source + " to " + edge.target, new[] { a, b }, width, materials[stage]);
                var direction = (b - a).normalized;
                var normal = Vector3.Cross(direction, Vector3.forward).normalized;
                var tip = Vector3.Lerp(a, b, .7f);
                Line("Direction arrow", new[] { tip - direction * .18f + normal * .09f, tip,
                    tip - direction * .18f - normal * .09f }, .035f, materials[stage]);
                var marker = Sphere("Authored pulse", a, .085f, materials[stage]);
                pulses.Add(new Pulse { marker = marker, a = a, b = b, offset = i * .137f });
                if (motif) Label(edge.synapses + " synapses", (a + b) * .5f + Vector3.down * .3f, .022f);
            }
            summary.text = motif ? "기억 모티브  /  선택 경로의 단계별 합산" : "실제 연결  /  후각 입력에서 하행 출력까지";
            detail.text = $"{Graph.nodes.Length} nodes  ·  {Graph.edges.Length} directed edges  ·  "
                + (motif ? "5개 단계로 축약한 독립 연출" : "6개의 선별 경로 / 선 굵기 = log(1 + 시냅스 수)");
            modeButton.text = motif ? "실제 연결 보기  [M]" : "기억 모티브 보기  [M]";
            ResetView();
        }

        public void TogglePause() { paused = !paused; pauseButton.text = paused ? "빛 재생  [Space]" : "빛 멈춤  [Space]"; }
        public void ResetView() { yaw = pitch = 0; viewerCamera.transform.position = new Vector3(0, 0, -17.5f); viewerCamera.transform.rotation = Quaternion.identity; }

        void Update()
        {
            if (Graph == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.mKey.wasPressedThisFrame) ShowMotif(!IsMotif);
                if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
                if (keyboard.rKey.wasPressedThisFrame) ResetView();
                yaw += ((keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0)) * Time.unscaledDeltaTime * 30;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    yaw += mouse.delta.ReadValue().x * .13f;
                    pitch -= mouse.delta.ReadValue().y * .13f;
                }
                float distance = Mathf.Clamp(-viewerCamera.transform.position.z - mouse.scroll.ReadValue().y * .008f, 13, 24);
                viewerCamera.transform.position = new Vector3(0, 0, -distance);
            }
            pitch = Mathf.Clamp(pitch, -35, 35); yaw = Mathf.Clamp(yaw, -55, 55);
            graphRoot.localRotation = Quaternion.Euler(pitch, yaw, 0);
            foreach (var label in labels) label.rotation = viewerCamera.transform.rotation;
            if (!paused) phase += Time.unscaledDeltaTime * .22f;
            foreach (var pulse in pulses) pulse.marker.localPosition = Vector3.Lerp(pulse.a, pulse.b, Mathf.Repeat(phase + pulse.offset, 1));
        }

        Transform Sphere(string name, Vector3 position, float size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name;
            go.transform.SetParent(graphRoot, false); go.transform.localPosition = position; go.transform.localScale = Vector3.one * size;
            Destroy(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }
        void Line(string name, Vector3[] points, float width, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(graphRoot, false);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = false;
            line.sharedMaterial = material; line.positionCount = points.Length; line.SetPositions(points);
            line.startWidth = line.endWidth = width; line.numCapVertices = 3;
        }
        Text Label(string value, Vector3 position, float scale)
        {
            var go = new GameObject("Graph label", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(graphRoot, false); go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = viewerCamera;
            var text = MakeText(go.transform, value, 17, Color.white, TextAnchor.MiddleCenter);
            text.rectTransform.sizeDelta = new Vector2(170, 36);
            labels.Add(go.transform);
            return text;
        }
        void CreateOverlay()
        {
            var go = new GameObject("Persistent source and disclaimer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false); overlay = go.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            var top = Panel("Header", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -162), Vector2.zero);
            At(top, "CONNECTOME  /  실험 13", 28, new Vector2(32, -26), new Vector2(1300, 42));
            summary = At(top, "선택 그래프를 준비하는 중", 20, new Vector2(32, -73), new Vector2(1300, 34));
            detail = At(top, "", 17, new Vector2(32, -110), new Vector2(1300, 30));
            var footer = Panel("Source footer", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 178));
            CreditText = At(footer, Credit, 19, new Vector2(32, -18), new Vector2(1360, 30));
            At(footer, Boundary, 18, new Vector2(32, -53), new Vector2(1360, 32));
            DisclaimerText = At(footer, Disclaimer, 21, new Vector2(32, -93), new Vector2(1360, 38));
            foreach (var text in footer.GetComponentsInChildren<Text>())
            {
                var rect = text.rectTransform;
                rect.anchorMax = new Vector2(1, 1);
                rect.offsetMax = new Vector2(-32, rect.offsetMax.y);
            }
            At(footer, "비상업 실험 · 하행 뉴런은 근육 운동뉴런이 아닙니다 · 빛은 신경활동 측정값이 아닙니다", 16,
                new Vector2(32, -139), new Vector2(1360, 28));
            modeButton = Button("기억 모티브 보기  [M]", new Vector2(32, 204), () => ShowMotif(!IsMotif));
            pauseButton = Button("빛 멈춤  [Space]", new Vector2(340, 204), TogglePause);
            Button("시점 초기화  [R]", new Vector2(648, 204), ResetView);
            var help = MakeText(overlay.transform, "우클릭 드래그 / ← → : 회전     휠 : 확대", 16, new Color(.65f,.73f,.82f), TextAnchor.MiddleRight);
            help.rectTransform.anchorMin = help.rectTransform.anchorMax = new Vector2(1, 0);
            help.rectTransform.pivot = new Vector2(1, 0); help.rectTransform.anchoredPosition = new Vector2(-32, 260); help.rectTransform.sizeDelta = new Vector2(600, 30);
            if (EventSystem.current == null)
            {
                var events = new GameObject("Viewer event system", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        RectTransform Panel(string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(overlay.transform, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = offsetMin; r.offsetMax = offsetMax;
            go.GetComponent<Image>().color = new Color(.025f,.045f,.08f,.97f); return r;
        }
        Text At(Transform parent, string value, int size, Vector2 position, Vector2 dimensions)
        {
            var text = MakeText(parent, value, size, Color.white, TextAnchor.MiddleLeft);
            var r = text.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = position; r.sizeDelta = dimensions; return text;
        }
        Text MakeText(Transform parent, string value, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = color; text.text = value;
            text.alignment = alignment; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        Text Button(string value, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(overlay.transform, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero;
            r.anchoredPosition = position; r.sizeDelta = new Vector2(286, 44);
            go.GetComponent<Image>().color = new Color(.12f,.2f,.29f); go.GetComponent<Button>().onClick.AddListener(action);
            var text = MakeText(go.transform, value, 18, Color.white, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero; return text;
        }
        void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
