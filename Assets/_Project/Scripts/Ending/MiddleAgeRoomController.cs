using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Wheelchair;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace FruitFlyParenting.Ending
{
    public sealed class MiddleAgeRoomController : MonoBehaviour
    {
        public const string RoomId="06_MiddleAge";
        [SerializeField] GameBalanceConfig config;
        [SerializeField] Font font;
        [SerializeField] EndingDefinition[] definitions;
        [SerializeField] RoomDestination entrance;
        [SerializeField] WheelchairLocomotion chair;
        [SerializeField] RoomTransitionController incoming;
        [SerializeField] Text calendar,story;
        [SerializeField] GameObject letter,extraLetters,suitcase,key,child,magic,companions,tickets,candle,news,documents;
        [SerializeField] Transform door;
        [SerializeField] Light roomLight;
        [SerializeField] bool standalone;
        EndingPresentation presentation;
        TransitionView guidance;
        bool entered,seated,restarting;
        GameObject debtChoice;
        public float Elapsed { get; private set; }
        public bool IsQuiet=>seated && Elapsed>=config.middleAgeSeconds-config.endingQuietSeconds && !CardVisible;
        public bool CardVisible { get; private set; }
        public EndingDefinition Result { get; private set; }
        public EndingPresentation Presentation=>presentation;
        public string CheckpointPath { get; set; }
        public string ChildLine { get; private set; }
        public void Configure(GameBalanceConfig balance,Font face,EndingDefinition[] endings,RoomDestination entry,WheelchairLocomotion wheelchair,
            Text date,Text line,Transform doorHinge,Light light,GameObject[] props)
        {
            config=balance; font=face; definitions=endings; entrance=entry; chair=wheelchair; calendar=date; story=line; door=doorHinge; roomLight=light;
            letter=props[0]; extraLetters=props[1]; suitcase=props[2]; key=props[3]; child=props[4]; magic=props[5]; companions=props[6]; tickets=props[7]; candle=props[8]; news=props[9]; documents=props[10];
        }
        public void ConfigureIncoming(RoomTransitionController flow)=>incoming=flow;
        public void ConfigureStandalone()=>standalone=true;
        void OnEnable() { if(entrance!=null) entrance.Entering+=Begin; }
        void OnDisable() { if(entrance!=null) entrance.Entering-=Begin; }
        void Start()
        {
            Fly.FlyCharacterArt.InstallEnding(child);
            if(companions!=null)foreach(Transform companion in companions.transform)Fly.FlyCharacterArt.InstallEnding(companion.gameObject);
            if(standalone) { entrance.Prepare(); entrance.Enter(); }
        }
        void Begin()
        {
            if(entered) return; entered=true; CheckpointPath=CheckpointPath??incoming?.CheckpointPath;
            guidance=gameObject.AddComponent<TransitionView>(); guidance.Configure(font); guidance.Initialize(Camera.main);
            var state=GameState.Current;
            if(state?.FinalYoungAdultSnapshot==null) { guidance.ShowSubtitle("아직 청년기 종료 기록이 없어요.\n00_Bootstrap에서 새 이야기를 시작해 주세요."); return; }
            Result=EndingEvaluator.Evaluate(state.FinalYoungAdultSnapshot,definitions); ChildLine=Result.childLine;
            if(Result.condition==EndingCondition.Bankruptcy && (state.FinalYoungAdultSnapshot.VisitedItemIds.Contains("youngadult_letter") || state.FinalYoungAdultSnapshot.VisitedItemIds.Contains("youngadult_park")))
                ChildLine="손실은 남아 있지만 다시 이야기하고 싶어요. 다음 하루를 함께 생각해 줄래요?";
            state.LogEvent(Time.time,RoomId,"ending_selected",Result.endingId);
            new SaveSystem(config).SaveCheckpoint(state,"ending_selected",CheckpointPath);
            foreach(var light in Object.FindObjectsByType<Light>()) if(!light.transform.IsChildOf(transform)) light.enabled=false;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.025f,.03f,.04f);
            Camera.main.clearFlags=CameraClearFlags.SolidColor; Camera.main.backgroundColor=Color.black;
            roomLight.color=Result.lightColor; roomLight.intensity=20; calendar.text=Result.calendarText;
            letter.SetActive(Result.letterbox!=LetterboxState.Empty); extraLetters.SetActive(Result.letterbox==LetterboxState.Overflowing);
            suitcase.SetActive(Result.suitcase!=SuitcaseState.Absent); suitcase.transform.localRotation=Quaternion.Euler(0,0,Result.suitcase==SuitcaseState.Worn ? 12 : 0);
            key.SetActive(Result.handle==HandleState.Key); child.SetActive(Result.childPresent); magic.SetActive(Result.condition==EndingCondition.MagicPromise);
            companions.SetActive(Result.condition==EndingCondition.Gang); tickets.SetActive(Result.condition==EndingCondition.Success);
            candle.SetActive(Result.condition==EndingCondition.EmptyBirthday); news.SetActive(Result.condition==EndingCondition.FamousOutside); documents.SetActive(Result.condition==EndingCondition.Debt);
            presentation=gameObject.AddComponent<EndingPresentation>(); presentation.Initialize(font,config.endingGazeSeconds,Restart);
            if(Result.condition==EndingCondition.Debt) CreateDebtChoice();
            guidance.ShowSubtitle($"되돌아온 문\n{DesktopControls.MovementLabel}로 가운데 거실의 빈자리까지 다가가세요.");
        }
        void Update()
        {
            if(Result==null || CardVisible) return;
            if(!seated)
            {
                var at=transform.InverseTransformPoint(chair.transform.position); at.y=0;
                if(Vector3.Distance(at,new Vector3(12,0,9))>4) return;
                seated=true;
                // Keep user-controlled turning and head tracking while the seated scene stops forward travel.
                foreach(var provider in chair.GetComponents<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>()) provider.enabled=false;
                guidance.HideSubtitle(); story.text=Result.sceneLine;
            }
            Elapsed+=Time.deltaTime;
            float progress=Mathf.Clamp01(Elapsed/Mathf.Max(1,config.middleAgeSeconds-config.endingQuietSeconds));
            door.localRotation=Quaternion.Euler(0,Result.handle==HandleState.Closed ? 0 : Mathf.SmoothStep(0,70,progress),0);
            if(Result.condition==EndingCondition.Gang && progress>.65f) { child.SetActive(false); companions.SetActive(false); }
            if(Result.condition==EndingCondition.EmptyBirthday) candle.transform.localScale=new Vector3(1,Mathf.Lerp(1,.05f,progress),1);
            if(Result.condition==EndingCondition.MagicPromise) child.transform.localPosition=new Vector3(12,Mathf.Sin(progress*Mathf.PI)*2,17);
            if(IsQuiet) { story.text=""; if(debtChoice!=null) debtChoice.SetActive(false); }
            if(Elapsed>=config.middleAgeSeconds)
            {
                CardVisible=true; story.text="";
                foreach(var label in GetComponentsInChildren<Text>()) label.enabled=false;
                presentation.Show(GameState.Current,Result,ChildLine);
                GameState.Current.LogEvent(Time.time,RoomId,"ending_card",Result.endingId);
                new SaveSystem(config).SaveCheckpoint(GameState.Current,"ending_card",CheckpointPath);
            }
        }
        public void Restart()
        {
            if(restarting) return; restarting=true;
            var bootstrap=Object.FindAnyObjectByType<GameStateBootstrap>(); bootstrap.RestartRun();
            new SaveSystem(config).SaveCheckpoint(GameState.Current,"new_run",CheckpointPath);
            Time.timeScale=1; SceneManager.LoadSceneAsync("00_Bootstrap");
        }
        void CreateDebtChoice()
        {
            var canvas=CorridorUI.Canvas("Debt Response",transform,new Vector3(12,2,12.2f),new Vector2(1600,260),.002f,90); debtChoice=canvas.gameObject;
            string[] lines={"함께 계획을 세워 보자","대신 갚아 줄 수는 없어"};
            for(int i=0;i<2;i++)
            {
                int choice=i; var panel=UI.JournalUI.Panel(canvas.transform,"Response "+i,new Vector2(750,130),new Vector2(i==0 ? -400 : 400,0),UI.JournalUI.Sage,24);
                CorridorUI.Text(panel.transform,"Line",font,lines[i],new Vector2(720,120),Vector2.zero,34).color=Color.white;
                panel.gameObject.AddComponent<GazeAction>().Configure(()=>
                {
                    ChildLine=choice==0 ? "내 책임부터 정리할게요. 함께 계획을 생각해 줘서 고마워요." : "내가 감당할 방법을 찾아볼게요. 이야기를 들어줘서 고마워요.";
                    GameState.Current.LogEvent(Time.time,RoomId,"ending_response",choice==0 ? "plan_together" : "set_boundary");
                    new SaveSystem(config).SaveCheckpoint(GameState.Current,"ending_response",CheckpointPath); debtChoice.SetActive(false);
                },panel,config.endingGazeSeconds);
            }
        }
    }
}
