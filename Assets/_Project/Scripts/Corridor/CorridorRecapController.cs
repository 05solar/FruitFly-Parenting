using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Corridor
{
    public sealed class CorridorRecapController : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] Transform[] anchors;
        [SerializeField] GameBalanceConfig config;
        public CorridorRecap Recap { get; private set; }
        public void Configure(Font textFont, Transform[] frames, GameBalanceConfig balance)
        { font = textFont; anchors = frames; config = balance; }

        public void Show(GameState state, string previousRoomId)
        {
            Recap = CorridorRecapModel.Build(state.EventLog, previousRoomId, config.corridorRecapCount);
            foreach (var anchor in anchors)
                foreach (Transform child in anchor) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            for (int i = 0; i < Recap.Memories.Length && i < anchors.Length; i++)
            {
                var memory = Recap.Memories[i]; var experience = memory.Experience;
                var card = CorridorUI.Canvas("Memory " + experience.itemId, anchors[i], Vector3.zero, new Vector2(740,520), .002f);
                // 흔들리는 카메라/사진 애니메이션 대신 정적인 기울기와 끊어진 윤곽을 사용한다.
                if (Recap.InterventionCount >= config.corridorUneasyContactThreshold)
                    card.transform.localRotation = Quaternion.Euler(0,0,(i % 2 == 0 ? 1 : -1) * config.corridorFrameTiltDegrees);
                CorridorUI.Image(card.transform,"Frame",new Vector2(740,520),Vector2.zero,new Color(.08f,.13f,.18f));
                CorridorUI.Image(card.transform,"Photo",new Vector2(690,230),new Vector2(0,100),new Color(.68f,.76f,.72f));
                DrawSilhouette(card.transform, experience.tags ?? new string[0], memory.WasPushedAway);
                CorridorUI.Text(card.transform,"Item",font,experience.displayName,new Vector2(690,65),new Vector2(0,-50),32);
                CorridorUI.Text(card.transform,"Narrative",font,experience.narrative,new Vector2(680,80),new Vector2(0,-117),25);
                string quote = string.IsNullOrEmpty(experience.childLine) ? "함께 머물렀던 기억" : "“" + experience.childLine + "”";
                CorridorUI.Text(card.transform,"Child Line",font,quote,new Vector2(680,80),new Vector2(0,-202),27);
                if (memory.WasPushedAway)
                {
                    for (int j=0;j<3;j++)
                    {
                        var crack = CorridorUI.Image(card.transform,"Crack",new Vector2(6,95),new Vector2(-20+j*17,170-j*65),new Color(.12f,.16f,.2f));
                        crack.transform.localRotation = Quaternion.Euler(0,0,j % 2 == 0 ? 25 : -30);
                    }
                }
            }
            if (Recap.Memories.Length == 0)
            {
                var empty = CorridorUI.Canvas("Quiet Memory",anchors[0],Vector3.zero,new Vector2(740,260),.002f);
                CorridorUI.Image(empty.transform,"Frame",new Vector2(740,260),Vector2.zero,new Color(.08f,.13f,.18f));
                CorridorUI.Text(empty.transform,"No Recorded Visit",font,"아직 사진으로 남은 경험은 없어요.\n함께 지나온 시간은 남아 있어요.",new Vector2(680,220),Vector2.zero,30);
            }
        }
        static void DrawSilhouette(Transform parent, string[] tags, bool faded)
        {
            var ink = faded ? new Color(.39f,.45f,.45f) : new Color(.14f,.24f,.28f);
            if (tags.Contains("공부"))
            {
                CorridorUI.Image(parent,"Book Left",new Vector2(95,140),new Vector2(-50,100),ink);
                CorridorUI.Image(parent,"Book Right",new Vector2(95,140),new Vector2(50,100),ink);
            }
            else if (tags.Contains("소비"))
            {
                CorridorUI.Image(parent,"Screen",new Vector2(220,155),new Vector2(0,100),ink);
                CorridorUI.Image(parent,"Screen Light",new Vector2(185,120),new Vector2(0,100),new Color(.5f,.59f,.58f));
            }
            else if (tags.Contains("창작") || tags.Contains("운동"))
            {
                for (int j=0;j<3;j++) CorridorUI.Image(parent,"Blocks",new Vector2(60,60),new Vector2((j-1)*70,70+j%2*60),ink);
            }
            else
            {
                CorridorUI.Image(parent,"Cushion",new Vector2(220,70),new Vector2(0,60),ink);
                CorridorUI.Image(parent,"Backrest",new Vector2(220,65),new Vector2(0,130),ink);
            }
        }
    }
}
