using System.Collections;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Wheelchair;
using UnityEngine;
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
            view.ShowSubtitle("초파리를 행복하게 만들어주세요.");
            foreach(var wall in outlines) wall.SetActive(false);
        }
        void Update()
        {
            if(loading) return; Elapsed+=Time.deltaTime;
            heartbeat.intensity=4+3*Mathf.Pow(Mathf.Max(0,Mathf.Sin(Elapsed*5)),3);
            for(int i=0;i<outlines.Length;i++) outlines[i].SetActive(Elapsed>=config.prologueSeconds*(i+1)/(outlines.Length+2));
            if(Elapsed>config.prologueSeconds-10)
            {
                egg.localScale=Vector3.Lerp(new Vector3(.5f,.7f,.5f),new Vector3(.7f,.35f,.7f),Mathf.Clamp01((Elapsed-config.prologueSeconds+10)/8));
                hatchling.SetActive(Elapsed>=config.prologueSeconds-6);
                egg.gameObject.SetActive(Elapsed<config.prologueSeconds-4);
                view.ShowSubtitle("아이가 스스로 알을 깨고 나와요.\n영유아 방에 도착하면 왼손 스틱으로 이동할 수 있어요.");
            }
            if(Elapsed>=config.prologueSeconds) StartCoroutine(EnterInfant());
        }
        IEnumerator EnterInfant()
        {
            loading=true;
            for(float t=0;t<config.roomFadeSeconds;t+=Time.unscaledDeltaTime) { view.SetFade(t/config.roomFadeSeconds); yield return null; }
            view.SetFade(1); GameState.Current.LogEvent(Time.time,"00_Prologue","prologue_complete","hatched");
            DontDestroyOnLoad(gameObject);
            yield return SceneManager.LoadSceneAsync("01_Infant");
            var arrival=gameObject.AddComponent<TransitionView>(); arrival.Configure(font); arrival.Initialize(Camera.main); arrival.SetFade(1);
            for(float t=0;t<config.roomFadeSeconds;t+=Time.unscaledDeltaTime) { arrival.SetFade(1-t/config.roomFadeSeconds); yield return null; }
            Destroy(gameObject);
        }
    }
}
