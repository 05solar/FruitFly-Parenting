using System.Collections;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Wheelchair;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace FruitFlyParenting.Ending
{
    public sealed class PrologueController : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] Font font;
        [SerializeField] GameObject[] outlines;
        [SerializeField] Transform egg;
        [SerializeField] Light heartbeat;
        [SerializeField] GameObject hatchling;
        TransitionView view;
        Canvas startCanvas;
        UnityEngine.UI.Text startLabel;
        bool loading;
        public float Elapsed { get; private set; }
        public void Configure(GameBalanceConfig balance,Font face,GameObject[] walls,Transform shell,Light light,GameObject child)
        { config=balance; font=face; outlines=walls; egg=shell; heartbeat=light; hatchling=child; }
        void Start()
        {
            Time.timeScale=1;
            var chair=Object.FindAnyObjectByType<WheelchairLocomotion>(); chair.enabled=false;
            foreach(var provider in chair.GetComponents<LocomotionProvider>()) provider.enabled=false;
            foreach(var renderer in chair.GetComponentsInChildren<Renderer>()) renderer.enabled=false;
            Camera.main.clearFlags=CameraClearFlags.SolidColor; Camera.main.backgroundColor=Color.black;
            hatchling.SetActive(false);
            GameState.Current.CurrentRoomId="00_Prologue"; GameState.Current.LogEvent(Time.time,"00_Prologue","prologue_start","no_interventions");
            view=gameObject.AddComponent<TransitionView>(); view.Configure(font); view.Initialize(Camera.main);
            startCanvas=CorridorUI.Canvas("Prologue Start",Camera.main.transform,new Vector3(0,-.15f,1.5f),new Vector2(280,52),.001f,41);
            var button=UI.JournalUI.Panel(startCanvas.transform,"Start game",new Vector2(280,52),Vector2.zero,Color.black,0);
            startLabel=UI.JournalUI.Text(button.transform,"Label",font,"게임 시작",new Vector2(260,44),Vector2.zero,24,Color.white,TextAnchor.MiddleCenter);
            button.gameObject.AddComponent<GazeAction>().Configure(RequestStart,button,2);
            UI.JournalUI.Adapt(startCanvas,new Vector2(280,52),new Vector2(.5f,0),new Vector2(0,242),false,true);
            ShowProgress();
            foreach(var wall in outlines) wall.SetActive(false);
        }
        void Update()
        {
            if(loading) return; Elapsed+=Time.deltaTime;
            bool desktop=DesktopControls.Current!=null && DesktopControls.Current.IsDesktopMode;
            startLabel.text=desktop ? "게임 시작 · Enter" : "게임 시작";
            if(desktop && (Application.isFocused || Application.isBatchMode) && Keyboard.current!=null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            { RequestStart(); if(loading) return; }
            ShowProgress();
            heartbeat.intensity=4+3*Mathf.Pow(Mathf.Max(0,Mathf.Sin(Elapsed*5)),3);
            for(int i=0;i<outlines.Length;i++) outlines[i].SetActive(Elapsed>=config.prologueSeconds*(i+1)/(outlines.Length+2));
            if(Elapsed>config.prologueSeconds-10)
            {
                egg.localScale=Vector3.Lerp(new Vector3(.5f,.7f,.5f),new Vector3(.7f,.35f,.7f),Mathf.Clamp01((Elapsed-config.prologueSeconds+10)/8));
                hatchling.SetActive(Elapsed>=config.prologueSeconds-6);
                egg.gameObject.SetActive(Elapsed<config.prologueSeconds-4);
                view.ShowSubtitle($"아이가 스스로 알을 깨고 나와요.\n영유아 방에 도착하면 {DesktopControls.MovementLabel}로 이동할 수 있어요.");
            }
            if(Elapsed>=config.prologueSeconds) StartCoroutine(EnterInfant(false));
        }
        void ShowProgress()
        {
            view.ShowSubtitle($"초파리를 행복하게 만들어주세요.\n자동 시작까지 {Mathf.CeilToInt(Mathf.Max(0,config.prologueSeconds-Elapsed))}초");
        }
        public void RequestStart()
        {
            if(loading || Comfort.ComfortController.IsPaused || Time.timeScale<=0) return;
            StartCoroutine(EnterInfant(true));
        }
        IEnumerator EnterInfant(bool skipped)
        {
            loading=true;
            if(startCanvas!=null) startCanvas.gameObject.SetActive(false);
            float duration=Mathf.Max(.5f,config.roomFadeSeconds);
            for(float t=0;t<duration;t+=Comfort.ComfortController.IsPaused ? 0 : Time.unscaledDeltaTime) { view.SetFade(t/duration); yield return null; }
            view.SetFade(1); GameState.Current.LogEvent(Time.time,"00_Prologue","prologue_complete",skipped ? "skipped" : "hatched");
            DontDestroyOnLoad(gameObject);
            yield return SceneManager.LoadSceneAsync("01_Infant");
            var arrival=gameObject.AddComponent<TransitionView>(); arrival.Configure(font); arrival.Initialize(Camera.main); arrival.SetFade(1);
            for(float t=0;t<duration;t+=Comfort.ComfortController.IsPaused ? 0 : Time.unscaledDeltaTime) { arrival.SetFade(1-t/duration); yield return null; }
            Destroy(gameObject);
        }
        void OnDestroy() { if(startCanvas!=null) Destroy(startCanvas.gameObject); }
    }
}
