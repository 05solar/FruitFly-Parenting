using System;
using UnityEngine;
using UnityEngine.UI;

namespace FruitFlyParenting.Ending
{
    // Head-tracked gaze works with the existing rig: no extra controller button or camera steering.
    public sealed class GazeAction : MonoBehaviour
    {
        Action action;
        Image background;
        float elapsed,seconds,armedAt;
        bool fired;
        public float Progress=>Mathf.Clamp01(elapsed/seconds);
        public void Configure(Action callback,Image panel,float duration) { action=callback; background=panel; seconds=Mathf.Max(.2f,duration); }
        void OnEnable() { elapsed=0; fired=false; armedAt=Time.unscaledTime+1; }
        public void Activate() { if(!isActiveAndEnabled || fired) return; fired=true; action?.Invoke(); }
        void Update()
        {
            var camera=Camera.main; if(camera==null || Time.unscaledTime<armedAt) return;
            var rect=(RectTransform)transform; var ray=new Ray(camera.transform.position,camera.transform.forward);
            bool focused=new Plane(rect.forward,rect.position).Raycast(ray,out float distance) && distance>0 && distance<12 && rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(distance)));
            if(!focused) { elapsed=0; fired=false; } else if(!fired) elapsed+=Time.unscaledDeltaTime;
            if(background!=null) background.color=Color.Lerp(new Color(.12f,.22f,.25f),new Color(.36f,.57f,.5f),Progress);
            if(elapsed>=seconds) Activate();
        }
    }
}
