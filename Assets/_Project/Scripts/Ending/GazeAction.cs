using System;
using System.Collections.Generic;
using FruitFlyParenting.Wheelchair;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FruitFlyParenting.Ending
{
    // Head-tracked gaze works with the existing rig: no extra controller button or camera steering.
    public sealed class GazeAction : MonoBehaviour
    {
        static readonly List<GazeAction> active = new List<GazeAction>();
        Action action;
        Image background;
        float elapsed,seconds,armedAt;
        bool fired;
        bool usableWhilePaused;
        Color normalColor = UI.JournalUI.Sage;
        public float Progress=>Mathf.Clamp01(elapsed/seconds);
        public void Configure(Action callback,Image panel,float duration,bool allowWhilePaused=false) { action=callback; background=panel; if(panel!=null) normalColor=panel.color; seconds=Mathf.Max(.2f,duration); usableWhilePaused=allowWhilePaused; }
        void OnEnable() { if(!active.Contains(this)) active.Add(this); elapsed=0; fired=false; armedAt=Time.unscaledTime+1; }
        void OnDisable() => active.Remove(this);
        public static GazeAction PointerTarget(Ray ray)
        {
            GazeAction nearest=null; float closest=12; int overlayOrder=int.MinValue;
            foreach(var button in active)
            {
                if(button==null || !button.isActiveAndEnabled || (Comfort.ComfortController.IsPaused && !button.usableWhilePaused)) continue;
                var rect=(RectTransform)button.transform;
                var canvas=button.GetComponentInParent<Canvas>();
                if(canvas!=null && canvas.renderMode==RenderMode.ScreenSpaceOverlay)
                {
                    if(canvas.enabled && Mouse.current!=null && canvas.sortingOrder>=overlayOrder &&
                        RectTransformUtility.RectangleContainsScreenPoint(rect,Mouse.current.position.ReadValue(),null))
                    { nearest=button; overlayOrder=canvas.sortingOrder; }
                    continue;
                }
                if(overlayOrder!=int.MinValue) continue;
                if(new Plane(rect.forward,rect.position).Raycast(ray,out float distance) && distance>0 && distance<closest && rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(distance))))
                { closest=distance; nearest=button; }
            }
            return nearest;
        }
        public void Activate() { if(!isActiveAndEnabled || fired || (Comfort.ComfortController.IsPaused && !usableWhilePaused)) return; fired=true; action?.Invoke(); }
        void Update()
        {
            if(Comfort.ComfortController.IsPaused && !usableWhilePaused) { elapsed=0; return; }
            var camera=Camera.main; if(camera==null || Time.unscaledTime<armedAt) return;
            bool desktop=DesktopControls.Current!=null && DesktopControls.Current.IsDesktopMode;
            var ray=desktop ? DesktopControls.InteractionRay(camera) : new Ray(camera.transform.position,camera.transform.forward);
            bool focused=PointerTarget(ray)==this;
            if(desktop)
            {
                elapsed=0;
                if(background!=null) background.color=focused ? Color.Lerp(normalColor,Color.white,.2f) : normalColor;
                if(focused && Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame) { fired=false; Activate(); }
                return;
            }
            if(!focused) { elapsed=0; fired=false; } else if(!fired) elapsed+=Time.unscaledDeltaTime;
            if(background!=null) background.color=Color.Lerp(normalColor,Color.white,Progress*.3f);
            if(elapsed>=seconds) Activate();
        }
    }
}
