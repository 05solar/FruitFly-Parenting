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
        Text text, titleText, gallerySummary, totals, selectionHint;
        readonly Text[] memoryLabels=new Text[3], memoryEffects=new Text[3];
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
            UI.JournalUI.Icon(root,6,new Vector2(0,490),55,UI.JournalUI.Sage);
            titleText=CorridorUI.Text(root,"Title",font,"",new Vector2(1640,90),new Vector2(0,390),56);
            CorridorUI.Text(root,"Memories heading",font,"결정적 경험 3개",new Vector2(1580,52),new Vector2(0,302),30);
            for(int i=0;i<3;i++)
            {
                var row=UI.JournalUI.Panel(root,"Memory row "+i,new Vector2(1580,104),new Vector2(0,210-i*128),UI.JournalUI.Soft,18).transform;
                memoryLabels[i]=UI.JournalUI.Text(row,"Experience",font,"",new Vector2(1480,48),new Vector2(0,21),32);
                memoryLabels[i].resizeTextForBestFit=true; memoryLabels[i].resizeTextMinSize=26; memoryLabels[i].resizeTextMaxSize=32;
                memoryEffects[i]=UI.JournalUI.Text(row,"Changes",font,"",new Vector2(1480,42),new Vector2(0,-25),26,UI.JournalUI.Muted);
            }
            CorridorUI.Text(root,"Child heading",font,"대사",new Vector2(1580,44),new Vector2(0,-130),24).color=UI.JournalUI.Sage;
            text=CorridorUI.Text(root,"Result",font,"",new Vector2(1480,110),new Vector2(0,-203),36);
            text.resizeTextForBestFit=true; text.resizeTextMinSize=30; text.resizeTextMaxSize=36;
            totals=CorridorUI.Text(root,"Totals",font,"",new Vector2(1580,48),new Vector2(0,-303),28);
            selectionHint=CorridorUI.Text(root,"Selection hint",font,"",new Vector2(1580,40),new Vector2(0,-350),22);
            Button(root,"회고 갤러리",new Vector2(-380,-460),()=>ShowGallery());
            Button(root,"다시 시작",new Vector2(380,-460),restart);
            UI.JournalUI.Adapt(root.GetComponent<Canvas>(),new Vector2(1800,1150),Vector2.one*.5f,Vector2.zero);
            root.gameObject.SetActive(false);
        }
        public void Show(GameState game,EndingDefinition definition,string childLine)
        {
            state=game; var s=state.FinalYoungAdultSnapshot; var memories=EndingEvaluator.DecisiveExperiences(state.EventLog);
            string experiences=memories.Length==0 ? "수치 변화가 기록된 경험은 아직 없어요." : string.Join("\n",memories.Select((e,i)=>$"{i+1}. {e.label}  ·  S {e.stress:+0.##;-0.##;0} / R {e.rebellion:+0.##;-0.##;0} / T {e.trust:+0.##;-0.##;0}"));
            string hint=Wheelchair.DesktopControls.Current!=null && Wheelchair.DesktopControls.Current.IsDesktopMode ? "아래 버튼을 클릭해 다음 이야기를 선택하세요." : "버튼을 2초 바라보면 선택해요.";
            string body="결정적 경험\n"+experiences+"\n\n아이의 한마디\n“"+childLine+"”\n\n개입 "+s.Interventions+"회 · 자율 방문 "+s.AutonomousVisits+"회\n"+hint;
            CardText=definition.title+"\n\n"+body;
            titleText.text=definition.title; text.text="“"+childLine+"”";
            totals.text=$"개입 {s.Interventions}회   ·   자율 방문 {s.AutonomousVisits}회"; selectionHint.text=hint;
            for(int i=0;i<3;i++)
            {
                memoryLabels[i].text=i<memories.Length ? $"{i+1:00}   {memories[i].label}" : i==0 ? "아직 기록된 경험이 없어요." : "";
                memoryEffects[i].text=i<memories.Length ? $"스트레스 {memories[i].stress:+0.##;-0.##;0}     반항 {memories[i].rebellion:+0.##;-0.##;0}     신뢰 {memories[i].trust:+0.##;-0.##;0}" : "";
            }
            root.gameObject.SetActive(true);
        }
        GazeAction Button(Transform parent,string label,Vector2 position,Action action)
        {
            var image=UI.JournalUI.Panel(parent,label,new Vector2(650,100),position,UI.JournalUI.Sage,22);
            CorridorUI.Text(image.transform,"Label",font,label,new Vector2(620,90),Vector2.zero,34).color=Color.white;
            var button=image.gameObject.AddComponent<GazeAction>(); button.Configure(action,image,gazeSeconds); return button;
        }
        public void ShowGallery()
        {
            root.gameObject.SetActive(false);
            if(gallery==null)
            {
                var canvas=CorridorUI.Canvas("Life Gallery",transform,new Vector3(12,2.1f,12.1f),new Vector2(1500,1100),.002f,100); gallery=canvas.gameObject;
                CorridorUI.Image(canvas.transform,"Panel",new Vector2(1500,1100),Vector2.zero,UI.JournalUI.Paper);
                CorridorUI.Text(canvas.transform,"Title",font,"플레이 기록",new Vector2(1380,80),new Vector2(0,450),44);
                for(int i=0;i<Rooms.Length;i++)
                {
                    int index=i; float x=-550+i%3*275, y=245-i/3*320;
                    // Six room miniatures share the 24:22 footprint, with a floor, three walls and a central prop.
                    var miniature=UI.JournalUI.Panel(canvas.transform,"Miniature "+Rooms[i],new Vector2(250,275),new Vector2(x,y),Color.HSVToRGB(i*.12f,.15f,.86f),16);
                    foreach(float side in new[]{-1f,1f}) CorridorUI.Image(miniature.transform,"Side Wall",new Vector2(10,210),new Vector2(side*110,25),new Color(.75f,.8f,.8f));
                    CorridorUI.Image(miniature.transform,"Back Wall",new Vector2(225,10),new Vector2(0,125),new Color(.75f,.8f,.8f));
                    CorridorUI.Image(miniature.transform,"Room Furniture",new Vector2(90,65),Vector2.zero,new Color(.8f,.65f,.45f));
                    CorridorUI.Text(miniature.transform,"Name",font,Names[i],new Vector2(230,80),new Vector2(0,-85),29);
                    var gaze=miniature.gameObject.AddComponent<GazeAction>(); gaze.Configure(()=>SelectRoom(index),miniature,gazeSeconds);
                }
                UI.JournalUI.Panel(canvas.transform,"Memory paper",new Vector2(530,720),new Vector2(425,20),Color.white,20);
                gallerySummary=CorridorUI.Text(canvas.transform,"Summary",font,"",new Vector2(470,660),new Vector2(425,20),29);
                gallerySummary.alignment=TextAnchor.UpperLeft;
                Button(canvas.transform,"엔딩 카드",new Vector2(-360,-440),()=>{ gallery.SetActive(false); root.gameObject.SetActive(true); });
                Button(canvas.transform,"다시 시작",new Vector2(360,-440),restart);
                UI.JournalUI.Adapt(canvas,new Vector2(1500,1100),Vector2.one*.5f,Vector2.zero);
            }
            gallery.SetActive(true); SelectRoom(4);
        }
        public void SelectRoom(int index)
        {
            if(index<0 || index>=Rooms.Length) throw new ArgumentOutOfRangeException(nameof(index));
            SelectedRoom=Rooms[index]; var recap=CorridorRecapModel.Build(state.EventLog,SelectedRoom,3);
            string lines=string.Join("\n",recap.Memories.Select(m=>m.Experience.displayName+" — "+m.Experience.narrative+(m.WasPushedAway ? " (중단된 기억)" : "")));
            if(recap.Memories.Length==0) lines=index==0 && state.EventLog.Any(e=>e.eventType=="prologue_complete") ? "알이 스스로 부화했다. 수치 변화와 개입이 없던 시간." : index==5 ? titleText.text : "이 방에는 방문으로 남은 경험이 없어요.";
            if(index==4 && !string.IsNullOrEmpty(state.FinalYoungAdultSnapshot.LastLetter)) lines+="\n마지막 편지: "+state.FinalYoungAdultSnapshot.LastLetter;
            GalleryText=Names[index]+"\n"+lines+"\n이 방의 개입 "+recap.InterventionCount+"회";
            gallerySummary.text=GalleryText;
        }
        public void Hide()=>root?.gameObject.SetActive(false);
    }
}
