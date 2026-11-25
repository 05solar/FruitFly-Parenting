using FruitFlyParenting.Comfort;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FruitFlyParenting.UI
{
    public sealed class DesktopJournalHelp : MonoBehaviour
    {
        RectTransform expanded;
        Text footer;
        bool open;
        public void Build(Font font)
        {
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440,900); scaler.matchWidthOrHeight = .5f;
            var strip = JournalUI.Rect(transform,"Control shortcuts",new Vector2(960,32),Vector2.zero);
            JournalUI.Dock(strip,new Vector2(.5f,0),new Vector2(0,10));
            footer = JournalUI.Text(strip,"Shortcuts",font,"",new Vector2(924,32),Vector2.zero,18,Color.white,TextAnchor.MiddleCenter);
            JournalUI.OutlineText(footer);
            var root = JournalUI.Rect(transform,"Expanded controls",new Vector2(510,330),Vector2.zero);
            JournalUI.Dock(root,new Vector2(1,1),new Vector2(-28,-126)); expanded=root;
            JournalUI.Panel(root,"Help background",new Vector2(510,330),Vector2.zero,Color.black,0);
            JournalUI.Text(root,"Title",font,"조작법",new Vector2(454,40),new Vector2(0,119),28,Color.white);
            string[] keys={"방향키 / WASD","마우스 오른쪽 드래그","마우스 왼쪽 버튼","Esc","우측 상단 설정"};
            string[] actions={"이동 · 회전","시점 회전","도구 사용","마우스 잠금 해제","설정 · 일시정지"};
            for(int i=0;i<keys.Length;i++)
            {
                float y=62-i*44;
                JournalUI.Text(root,"Key",font,keys[i],new Vector2(216,36),new Vector2(-114,y),19,Color.white);
                JournalUI.Text(root,"Action",font,actions[i],new Vector2(250,36),new Vector2(115,y),18,Color.white);
            }
            root.gameObject.SetActive(false);
        }
        void Update()
        {
            if(Keyboard.current!=null && Keyboard.current.f1Key.wasPressedThisFrame) open=!open;
            expanded.gameObject.SetActive(open && !ComfortController.IsPaused);
            footer.text = ComfortController.IsPaused ? "일시정지  ·  돌아가기를 눌러 계속" :
                "WASD / 방향키  이동     ·     우클릭 드래그  시점     ·     F1  도움말";
            if(Debugging.PlaytestRecorder.Current!=null && !ComfortController.IsPaused) footer.text+="     ·     F6 / F7  기록";
        }
    }
}
