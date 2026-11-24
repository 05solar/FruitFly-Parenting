using System;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.Ending
{
    public sealed class EndingPresentation : MonoBehaviour
    {
        Font font;
        float gazeSeconds;
        Transform root;
        Text text;
        GameObject gallery;
        Action restart;
        GameState state;
        public string CardText { get; private set; }
        public string GalleryText { get; private set; }
        public string SelectedRoom { get; private set; }
        public bool GalleryVisible=>gallery!=null && gallery.activeSelf;
        public static readonly string[] Rooms={"00_Prologue","01_Infant","03_Childhood","04_Adolescence","05_YoungAdult","06_MiddleAge"};
        static readonly string[] Names={"기억의 방","첫 손길","놀이터와 첫 약속","갈림길","자기 방","되돌아온 문"};
        public void Initialize(Font value,float dwell,Action restartAction)
        {
            font=value; gazeSeconds=dwell; restart=restartAction;
            root=CorridorUI.Canvas("Ending Card",transform,new Vector3(12,2.1f,12.2f),new Vector2(1800,1150),.002f,100).transform;
            CorridorUI.Image(root,"Panel",new Vector2(1800,1150),Vector2.zero,new Color(.04f,.08f,.11f));
            text=CorridorUI.Text(root,"Result",font,"",new Vector2(1700,820),new Vector2(0,100),36);
            Button(root,"회고 갤러리",new Vector2(-380,-460),()=>ShowGallery());
            Button(root,"다시 시작",new Vector2(380,-460),restart);
            root.gameObject.SetActive(false);
        }
        public void Show(GameState game,EndingDefinition definition,string childLine)
        {
            state=game; var s=state.FinalYoungAdultSnapshot; var memories=EndingEvaluator.DecisiveExperiences(state.EventLog);
            string experiences=memories.Length==0 ? "수치 변화가 기록된 경험은 아직 없어요." : string.Join("\n",memories.Select((e,i)=>$"{i+1}. {e.label}  ·  S {e.stress:+0.##;-0.##;0} / R {e.rebellion:+0.##;-0.##;0} / T {e.trust:+0.##;-0.##;0}"));
            CardText=definition.title+"\n\n결정적 경험\n"+experiences+"\n\n아이의 한마디\n“"+childLine+"”\n\n개입 "+s.Interventions+"회 · 자율 방문 "+s.AutonomousVisits+"회\n버튼을 2초 바라보면 선택해요.";
            text.text=CardText; root.gameObject.SetActive(true);
        }
        GazeAction Button(Transform parent,string label,Vector2 position,Action action)
        {
            var image=CorridorUI.Image(parent,label,new Vector2(650,100),position,new Color(.12f,.22f,.25f));
            CorridorUI.Text(image.transform,"Label",font,label,new Vector2(620,90),Vector2.zero,34);
            var button=image.gameObject.AddComponent<GazeAction>(); button.Configure(action,image,gazeSeconds); return button;
        }
        public void ShowGallery()
        {
            root.gameObject.SetActive(false);
            if(gallery==null)
            {
                var canvas=CorridorUI.Canvas("Life Gallery",transform,new Vector3(12,2.1f,12.1f),new Vector2(2200,1200),.002f,100); gallery=canvas.gameObject;
                CorridorUI.Image(canvas.transform,"Panel",new Vector2(2200,1200),Vector2.zero,new Color(.04f,.08f,.11f));
                for(int i=0;i<Rooms.Length;i++)
                {
                    int index=i; float x=-900+i*360;
                    // Six room miniatures share the 24:22 footprint, with a floor, three walls and a central prop.
                    var miniature=CorridorUI.Image(canvas.transform,"Miniature "+Rooms[i],new Vector2(300,275),new Vector2(x,310),Color.HSVToRGB(i*.12f,.3f,.5f));
                    foreach(float side in new[]{-1f,1f}) CorridorUI.Image(miniature.transform,"Side Wall",new Vector2(12,275),new Vector2(side*144,0),new Color(.75f,.8f,.8f));
                    CorridorUI.Image(miniature.transform,"Back Wall",new Vector2(300,12),new Vector2(0,130),new Color(.75f,.8f,.8f));
                    CorridorUI.Image(miniature.transform,"Room Furniture",new Vector2(90,65),Vector2.zero,new Color(.8f,.65f,.45f));
                    CorridorUI.Text(miniature.transform,"Name",font,Names[i],new Vector2(290,80),new Vector2(0,-85),27);
                    var gaze=miniature.gameObject.AddComponent<GazeAction>(); gaze.Configure(()=>SelectRoom(index),miniature,gazeSeconds);
                }
                CorridorUI.Text(canvas.transform,"Summary",font,"",new Vector2(2050,500),new Vector2(0,-110),34);
                Button(canvas.transform,"엔딩 카드",new Vector2(-380,-490),()=>{ gallery.SetActive(false); root.gameObject.SetActive(true); });
                Button(canvas.transform,"다시 시작",new Vector2(380,-490),restart);
            }
            gallery.SetActive(true); SelectRoom(4);
        }
        public void SelectRoom(int index)
        {
            if(index<0 || index>=Rooms.Length) throw new ArgumentOutOfRangeException(nameof(index));
            SelectedRoom=Rooms[index]; var recap=CorridorRecapModel.Build(state.EventLog,SelectedRoom,3);
            string lines=string.Join("\n",recap.Memories.Select(m=>m.Experience.displayName+" — "+m.Experience.narrative+(m.WasPushedAway ? " (중단된 기억)" : "")));
            if(recap.Memories.Length==0) lines=index==0 && state.EventLog.Any(e=>e.eventType=="prologue_complete") ? "알이 스스로 부화했다. 수치 변화와 개입이 없던 시간." : index==5 ? text.text.Split('\n')[0] : "이 방에는 방문으로 남은 경험이 없어요.";
            if(index==4 && !string.IsNullOrEmpty(state.FinalYoungAdultSnapshot.LastLetter)) lines+="\n마지막 편지: "+state.FinalYoungAdultSnapshot.LastLetter;
            GalleryText=Names[index]+"\n"+lines+"\n이 방의 개입 "+recap.InterventionCount+"회";
            gallery.transform.Find("Summary").GetComponent<Text>().text=GalleryText;
        }
        public void Hide()=>root?.gameObject.SetActive(false);
    }
}
