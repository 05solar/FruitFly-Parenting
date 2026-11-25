using System;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Tool;
using FruitFlyParenting.Wheelchair;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace FruitFlyParenting.Room
{
    public enum AdolescenceExitPhase { None, Flying, AtWindow, Extinguishing, Complete }
    public sealed class AdolescenceExitSequence : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] FlyAgent fly;
        [SerializeField] WheelchairLocomotion chair;
        [SerializeField] Transform room;
        [SerializeField] GameObject wings, smoke, cigarette;
        NavMeshAgent agent;
        FlyContactReceiver receiver;
        FlyHesitationFeedback hesitation;
        Collider[] colliders;
        bool[] colliderStates, providerStates;
        LocomotionProvider[] providers;
        bool chairEnabled, flyEnabled, receiverEnabled, hesitationEnabled;
        Vector3 start;
        float elapsed;
        public bool IsPlaying { get; private set; }
        public AdolescenceExitPhase Phase { get; private set; }
        public bool SmokeVisible => smoke.activeSelf;
        public bool WingsVisible => wings.activeSelf;
        public event Action Completed;
        public void Configure(GameBalanceConfig balance,FlyAgent actor,WheelchairLocomotion rig,Transform container,GameObject wingVisual,GameObject smokeVisual,GameObject cigaretteVisual)
        { config=balance; fly=actor; chair=rig; room=container; wings=wingVisual; smoke=smokeVisual; cigarette=cigaretteVisual; }
        public bool Play()
        {
            if(IsPlaying || Phase!=AdolescenceExitPhase.None || !GameState.Current.TryBeginAdolescenceExitEvent(Time.time)) return false;
            agent=fly.GetComponent<NavMeshAgent>(); receiver=fly.GetComponent<FlyContactReceiver>(); hesitation=fly.GetComponent<FlyHesitationFeedback>();
            start=fly.transform.position; elapsed=0; IsPlaying=true; Phase=AdolescenceExitPhase.Flying;
            flyEnabled=fly.enabled; receiverEnabled=receiver.enabled; hesitationEnabled=hesitation.enabled;
            fly.enabled=false; receiver.enabled=false; hesitation.enabled=false; agent.enabled=false; fly.Learning?.Reset();
            colliders=fly.GetComponentsInChildren<Collider>(); colliderStates=new bool[colliders.Length];
            for(int i=0;i<colliders.Length;i++) { colliderStates[i]=colliders[i].enabled; colliders[i].enabled=false; }
            providers=chair.GetComponents<LocomotionProvider>(); providerStates=new bool[providers.Length]; chairEnabled=chair.enabled;
            for(int i=0;i<providers.Length;i++) { providerStates[i]=providers[i].enabled; providers[i].enabled=false; }
            chair.enabled=false; wings.SetActive(true); smoke.SetActive(false); cigarette.SetActive(false);
            return true;
        }
        void Update()
        {
            if(!IsPlaying) return;
            var previousPhase=Phase;
            elapsed+=Time.deltaTime;
            float t=Mathf.Clamp01(elapsed/Mathf.Clamp(config.adolescenceExitSceneSeconds,10,15));
            var middle=room.TransformPoint(new Vector3(12,3.3f,11));
            var window=room.TransformPoint(new Vector3(20,1.6f,20.4f));
            var exit=room.TransformPoint(new Vector3(23,.3f,12.1f));
            if(t<1f/3)
            {
                Phase=AdolescenceExitPhase.Flying;
                fly.transform.position=t<1f/6 ? Vector3.Lerp(start,middle,t*6) : Vector3.Lerp(middle,window,(t-1f/6)*6);
                foreach(Transform wing in wings.transform) wing.localRotation=Quaternion.Euler(0,0,Mathf.Sin(elapsed*18)*25);
            }
            else if(t<5f/6)
            {
                Phase=AdolescenceExitPhase.AtWindow; fly.transform.position=window;
                wings.SetActive(false); smoke.SetActive(true); cigarette.SetActive(true);
                for(int i=0;i<smoke.transform.childCount;i++)
                {
                    float rise=Mathf.Repeat(elapsed*.45f+i*.3f,1);
                    var puff=smoke.transform.GetChild(i); puff.localPosition=new Vector3(.1f+rise*.15f,.55f+rise*.8f,.25f);
                    puff.localScale=Vector3.one*(.08f+rise*.18f);
                }
            }
            else
            {
                Phase=AdolescenceExitPhase.Extinguishing; smoke.SetActive(false); cigarette.SetActive(false);
                fly.transform.position=Vector3.Lerp(window,exit,(t-5f/6)*6);
            }
            if(Phase!=previousPhase)
                InfantRoomView.SpeakFor(fly,Phase==AdolescenceExitPhase.AtWindow ? "콜록… 목 아파." : "됐어. 이제 갈래.");
            if(t<1) return;
            agent.enabled=true;
            if(!agent.Warp(exit)) Debug.LogError("Adolescence exit landing has no NavMesh.",this);
            Restore(); Phase=AdolescenceExitPhase.Complete;
            GameState.Current.LogEvent(Time.time,AdolescenceRoomController.RoomId,"flight_smoke_completed","extinguished");
            Completed?.Invoke();
        }
        void Restore()
        {
            IsPlaying=false; wings.SetActive(false); smoke.SetActive(false); cigarette.SetActive(false);
            if(chair!=null) chair.enabled=chairEnabled;
            if(providers!=null) for(int i=0;i<providers.Length;i++) if(providers[i]!=null) providers[i].enabled=providerStates[i];
            if(fly!=null) fly.enabled=flyEnabled;
            if(receiver!=null) receiver.enabled=receiverEnabled;
            if(hesitation!=null) hesitation.enabled=hesitationEnabled;
            if(colliders!=null) for(int i=0;i<colliders.Length;i++) if(colliders[i]!=null) colliders[i].enabled=colliderStates[i];
        }
        void OnDisable() { if(IsPlaying) { if(agent!=null) agent.enabled=true; Restore(); } }
    }
}
