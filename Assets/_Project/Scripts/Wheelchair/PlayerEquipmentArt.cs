using FruitFlyParenting.Core;
using FruitFlyParenting.Tool;
using UnityEngine;

namespace FruitFlyParenting.Wheelchair
{
    // Presentation follows the existing rig and tool. It never creates physics or moves the camera.
    public sealed class PlayerEquipmentArt : MonoBehaviour
    {
        public const string ChairName = "Powered Wheelchair Art";
        public Transform Joystick { get; private set; }
        WheelchairLocomotion chair;
        LoveStickTool tool;
        Transform stickArt;
        LineRenderer[] arcs;
        float contactUntil;
        float nextArc;
        Quaternion neutralJoystick;

        void Start() => Install();
        public void Install()
        {
            if (transform.Find(ChairName) != null) return;
            chair = GetComponent<WheelchairLocomotion>();
            var source = Resources.Load<GameObject>("PlayerEquipment/PoweredChair");
            var baton = Resources.Load<GameObject>("PlayerEquipment/ElectricBaton");
            if (source == null || baton == null) return;
            foreach (Transform child in transform)
                if (child.name == "Seat" || child.name == "Back" || child.name == "Wheel" || child.name == "Bumper")
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var art = Instantiate(source,transform,false); art.name = ChairName;
            Joystick = art.transform.Find("Left Console/Joystick Pivot");
            neutralJoystick = Joystick.localRotation;
            tool = GetComponentInChildren<LoveStickTool>(true);
            if (tool == null) return;
            var parent = tool.transform.parent;
            foreach(var renderer in parent.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            stickArt = Instantiate(baton,parent,false).transform;
            stickArt.name = "Electric Baton Art";
            arcs = stickArt.GetComponentsInChildren<LineRenderer>(true);
        }
        void OnEnable() => EventBus.ElectricShockVisual += Contact;
        void OnDisable() => EventBus.ElectricShockVisual -= Contact;
        void Contact(Vector3 position) { if(tool != null && Vector3.Distance(tool.transform.position,position)<2f) contactUntil=Time.unscaledTime+.2f; }
        void LateUpdate()
        {
            if (chair == null || Joystick == null) return;
            var desktop = GetComponent<DesktopControls>();
            Vector2 input = desktop != null && desktop.IsDesktopMode ? desktop.MoveInput :
                chair.LeftStick != null ? chair.LeftStick.action.ReadValue<Vector2>() : Vector2.zero;
            if (!chair.enabled || chair.ComfortInputBlocked || Time.timeScale <= 0) input=Vector2.zero;
            Joystick.localRotation=Quaternion.Slerp(Joystick.localRotation,
                neutralJoystick*Quaternion.Euler(input.y*16,0,-input.x*16),1-Mathf.Exp(-16*Time.unscaledDeltaTime));
            if(arcs==null || Time.unscaledTime<nextArc || Time.timeScale<=0)return;
            nextArc=Time.unscaledTime+.09f;
            for(int a=0;a<arcs.Length;a++)
            {
                var arc=arcs[a]; arc.widthMultiplier=Time.unscaledTime<contactUntil ? .006f : .0024f;
                for(int i=0;i<arc.positionCount;i++)
                {
                    float t=i/(float)(arc.positionCount-1);
                    float wave=Mathf.Sin(t*21+Time.unscaledTime*8+a*2)*.008f*Mathf.Sin(t*Mathf.PI);
                    arc.SetPosition(i,new Vector3(Mathf.Lerp(-.027f,.027f,t),wave+a*.011f,.522f+Mathf.Sin(t*Mathf.PI)*.025f));
                }
            }
        }
    }
}
