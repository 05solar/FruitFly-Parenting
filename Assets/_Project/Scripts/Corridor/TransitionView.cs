using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.Corridor
{
    public sealed class TransitionView : MonoBehaviour
    {
        [SerializeField] Font font;
        Canvas hud, fade;
        Text subtitle;
        Image curtain;
        public float FadeAlpha => curtain != null ? curtain.color.a : 0;
        public string Subtitle => subtitle != null ? subtitle.text : "";
        public void Configure(Font value) => font = value;
        public void Initialize(Camera viewer)
        {
            if (curtain != null) return;
            hud = CorridorUI.Canvas("Transition Subtitle", viewer.transform, new Vector3(0,-.4f,1.5f), new Vector2(1000,170), .001f, 20);
            CorridorUI.Image(hud.transform, "Backdrop", new Vector2(1000,170), Vector2.zero, new Color(.025f,.04f,.06f,.92f));
            subtitle = CorridorUI.Text(hud.transform, "Subtitle", font, "", new Vector2(950,150), Vector2.zero, 28);
            hud.gameObject.SetActive(false);
            fade = CorridorUI.Canvas("Comfort Fade", viewer.transform, new Vector3(0,0,.5f), new Vector2(6000,6000), .001f, 1000);
            curtain = CorridorUI.Image(fade.transform, "Curtain", new Vector2(6000,6000), Vector2.zero, new Color(0,0,0,0));
        }
        public void SetFade(float value) => curtain.color = new Color(0,0,0,Mathf.Clamp01(value));
        public void ShowSubtitle(string value) { hud.gameObject.SetActive(true); subtitle.text = value; }
        public void HideSubtitle() { if (hud != null) hud.gameObject.SetActive(false); }
        void OnDestroy()
        {
            if (hud != null) Destroy(hud.gameObject);
            if (fade != null) Destroy(fade.gameObject);
        }
    }
}
