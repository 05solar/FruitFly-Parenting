using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using UnityEngine;

namespace FruitFlyParenting.Corridor
{
    public sealed class CorridorRecapController : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] Transform[] anchors;
        [SerializeField] GameBalanceConfig config;
        public CorridorRecap Recap { get; private set; }
        public string PaperAirplaneLine { get; private set; } = string.Empty;
        public float ParentPhotoShare { get; private set; }
        public float ShadowSeparation { get; private set; }
        public string YoungAdultLetterLine { get; private set; } = string.Empty;
        public void Configure(Font textFont, Transform[] frames, GameBalanceConfig balance)
        { font = textFont; anchors = frames; config = balance; }

        public void Show(GameState state, string previousRoomId)
        {
            Recap = CorridorRecapModel.Build(state.EventLog, previousRoomId, config.corridorRecapCount);
            if(previousRoomId==AdolescenceRoomController.RoomId) DrawAdolescence(state.Relationship.Trust);
            if(previousRoomId==YoungAdultRoomController.RoomId) DrawYoungAdult(state);
            PaperAirplaneLine = previousRoomId == ChildhoodRoomController.RoomId ?
                PaperAirplaneEvent.CorridorLine(state.ChildhoodAirplaneOutcome,Recap.HadInterruptedActivity) : string.Empty;
            if (!string.IsNullOrEmpty(PaperAirplaneLine))
            {
                var old = transform.Find("Paper Airplane Memory");
                if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
                var board = CorridorUI.Canvas("Paper Airplane Memory",transform,new Vector3(6,1.65f,-1.96f),new Vector2(1500,520),.002f);
                board.transform.localRotation = Quaternion.Euler(0,180,0);
                CorridorUI.Image(board.transform,"Backdrop",new Vector2(1500,520),Vector2.zero,new Color(.08f,.16f,.19f));
                CorridorUI.Text(board.transform,"Paper memory",font,"종이비행기에 남은 약속\n"+PaperAirplaneLine,new Vector2(1400,380),new Vector2(0,-30),34);
                bool repaired = state.ChildhoodAirplaneOutcome == PaperAirplaneOutcome.ApologizeAndRepair;
                var wing = CorridorUI.Image(board.transform,"Paper wings",new Vector2(200,36),new Vector2(0,205),repaired?new Color(.75f,.95f,.8f):new Color(1,.78f,.5f));
                wing.transform.localRotation = Quaternion.Euler(0,0,repaired?0:18);
            }
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
        void DrawYoungAdult(GameState state)
        {
            // This stage replaces the shared child/play-block silhouette with the child opening their own door.
            foreach(string name in new[]{"Child Silhouette Body","Child Silhouette Head","Play Blocks"})
            { var shared=transform.Find(name); if(shared!=null) shared.gameObject.SetActive(false); }
            YoungAdultLetterLine=state.FinalYoungAdultSnapshot?.LastLetter??state.LastYoungAdultLetter;
            var old=transform.Find("Young Adult Memories"); if(old!=null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            var root=new GameObject("Young Adult Memories").transform; root.SetParent(transform,false);
            var letter=CorridorUI.Canvas("Last Letter",root,new Vector3(7,1.7f,-1.96f),new Vector2(1800,650),.002f);
            letter.transform.localRotation=Quaternion.Euler(0,180,0);
            CorridorUI.Image(letter.transform,"Paper",new Vector2(1800,650),Vector2.zero,new Color(.15f,.20f,.22f));
            CorridorUI.Text(letter.transform,"Letter",font,string.IsNullOrEmpty(YoungAdultLetterLine) ? "아직 받은 편지는 없어요.\n아이가 직접 여는 문을 기다려요." : "마지막으로 받은 편지\n"+YoungAdultLetterLine,new Vector2(1700,570),Vector2.zero,38);
            var board=CorridorUI.Canvas("Child Opens Door",root,new Vector3(12,1.6f,1.96f),new Vector2(1400,1100),.002f);
            CorridorUI.Image(board.transform,"Backdrop",new Vector2(1400,1100),Vector2.zero,new Color(.77f,.78f,.7f));
            CorridorUI.Image(board.transform,"Open Doorway",new Vector2(470,780),new Vector2(285,0),new Color(.97f,.87f,.61f));
            var door=CorridorUI.Image(board.transform,"Door",new Vector2(470,780),new Vector2(50,0),new Color(.68f,.46f,.29f));
            door.rectTransform.pivot=new Vector2(0,.5f);
            CorridorUI.Image(door.transform,"Handle",new Vector2(30,70),new Vector2(170,0),new Color(.95f,.78f,.4f));
            CorridorUI.Image(board.transform,"Adult Child",new Vector2(160,410),new Vector2(-230,-80),new Color(.1f,.22f,.25f));
            CorridorUI.Image(board.transform,"Child Head",new Vector2(160,160),new Vector2(-230,200),new Color(.1f,.22f,.25f));
            CorridorUI.Image(board.transform,"Child Reaching Hand",new Vector2(270,55),new Vector2(-50,40),new Color(.1f,.22f,.25f));
            var title=CorridorUI.Text(board.transform,"Own Door",font,"“이번 문은 내가 열어 볼게요.”",new Vector2(1300,150),new Vector2(0,470),38);
            title.color=new Color(.1f,.22f,.25f);
            board.gameObject.AddComponent<YoungAdultCorridorDoor>().Configure(door.transform);
        }
        void DrawAdolescence(float trust)
        {
            ParentPhotoShare=.6f*Mathf.Clamp01(trust/100f);
            ShadowSeparation=.45f+1.2f*(1-Mathf.Clamp01(trust/100f));
            var old=transform.Find("Adolescence Wall Art"); if(old!=null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            var art=new GameObject("Adolescence Wall Art").transform; art.SetParent(transform,false);
            for(int i=0;i<3;i++)
            {
                var board=CorridorUI.Canvas("Changing Photo "+i,art,new Vector3(5.5f+i*2.3f,1.7f,-1.96f),new Vector2(1000,600),.002f);
                board.transform.localRotation=Quaternion.Euler(0,180,0);
                CorridorUI.Image(board.transform,"Photo Frame",new Vector2(1000,600),Vector2.zero,new Color(.08f,.14f,.19f));
                float parent=ParentPhotoShare*(1-i*.2f),friends=1-parent;
                for(int person=-1;person<=1;person+=2)
                {
                    CorridorUI.Image(board.transform,"Parent Photo",new Vector2(150,240),new Vector2(person*190,20),new Color(.95f,.77f,.46f,parent));
                    CorridorUI.Image(board.transform,"Friend Photo",new Vector2(150,180),new Vector2(person*95,20),new Color(.48f,.8f,.87f,friends));
                }
                for(int mark=0;mark<3;mark++)
                {
                    var stroke=CorridorUI.Image(board.transform,"Child Graffiti",new Vector2(500,12),new Vector2(0,130+mark*35),new Color(.85f,.6f,.8f,friends));
                    stroke.transform.localRotation=Quaternion.Euler(0,0,mark%2==0 ? 12 : -12);
                }
                CorridorUI.Text(board.transform,"Caption",font,"함께 찍은 사진 위에\n내 글씨와 친구들의 얼굴이 늘었어요.",new Vector2(940,160),new Vector2(0,-200),30);
            }
            var shadows=CorridorUI.Canvas("Two Shadows",art,new Vector3(12.1f,1.4f,1.96f),new Vector2(1800,1100),.002f);
            for(int person=-1;person<=1;person+=2)
                CorridorUI.Image(shadows.transform,"Shadow",new Vector2(180,600),new Vector2(person*ShadowSeparation*250,0),new Color(.07f,.11f,.15f));
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
