using System;
using System.Collections.Generic;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Tool;
using UnityEngine;

namespace FruitFlyParenting.Room
{
    [DefaultExecutionOrder(-40)]
    public sealed class YoungAdultRoomController : MonoBehaviour
    {
        public const string RoomId="05_YoungAdult";
        [SerializeField] GameBalanceConfig config;
        [SerializeField] RoomController room;
        [SerializeField] FlyAgent fly;
        [SerializeField] RoomDestination entrance;
        [SerializeField] GameObject exitDoor;
        [SerializeField] RoomTransitionController incoming,outgoing;
        [SerializeField] InfantRoomView view;
        [SerializeField] GamblingMontage gambling;
        [SerializeField] bool startImmediately;
        readonly List<Action> unsubscribe=new List<Action>();
        YoungAdultItemEffects effects;
        SaveSystem saves;
        FlyContactReceiver contact;
        bool leaving,wasOpen,exitOnlyDirected,tomorrowSpoken,exitDirected;
        ItemSlot pendingGambling;
        ItemVisitContext pendingContext;
        public RoomTimeline Timeline { get; private set; }
        public RoomController Room=>room;
        public FlyAgent Fly=>fly;
        public InfantRoomView View=>view;
        public GamblingMontage Gambling=>gambling;
        public RoomTransitionController Transition=>outgoing;
        public bool HasEntered=>Timeline!=null;
        public string CheckpointPath { get; set; }
        public void Configure(GameBalanceConfig balance,RoomController container,FlyAgent actor,RoomDestination entry,
            GameObject door,InfantRoomView display,GamblingMontage montage,RoomTransitionController exit)
        { config=balance; room=container; fly=actor; entrance=entry; exitDoor=door; view=display; gambling=montage; outgoing=exit; }
        public void ConfigureIncoming(RoomTransitionController value)=>incoming=value;
        public void ConfigureStandalone()=>startImmediately=true;
        void OnEnable() { if(entrance!=null) entrance.Entering+=Begin; }
        void OnDisable() { if(entrance!=null) entrance.Entering-=Begin; EventBus.ToolUsed-=UsedTool; }
        void Start() { if(startImmediately) { entrance.Prepare(); entrance.Enter(); } }
        void Begin()
        {
            if(HasEntered) return;
            CheckpointPath=CheckpointPath??incoming?.CheckpointPath; Timeline=new RoomTimeline(config);
            effects=new YoungAdultItemEffects(config,GameState.Current); saves=new SaveSystem(config);
            room.ConfigureLifeStage(LifeStage.YoungAdult); room.RefreshItemSlots(); room.ResetVisits(); GameState.Current.CurrentRoomId=RoomId;
            contact=fly.GetComponent<FlyContactReceiver>(); contact.ContactsEnabled=false;
            foreach(var slot in room.ItemSlots)
            {
                Action first=()=>Visit(slot,true),revisit=()=>Visit(slot,false);
                slot.OnFirstVisit+=first; slot.OnRevisit+=revisit;
                unsubscribe.Add(()=> { if(slot!=null) { slot.OnFirstVisit-=first; slot.OnRevisit-=revisit; } });
            }
            if(incoming!=null) { incoming.Sound.enabled=false; outgoing.Sound.TakeOverPrevious(incoming.Sound.Next); }
            outgoing.CheckpointPath=CheckpointPath; outgoing.TransitionStarted+=Leaving; outgoing.CorridorEntered+=EnteredCorridor;
            outgoing.gameObject.SetActive(true); view.Initialize(Camera.main,room,"자기 방");
            view.ShowNarrative("가구도 하루의 순서도, 이제 내가 골라 두었어요.");
            gambling.Completed+=GamblingCompleted; EventBus.ToolUsed+=UsedTool; fly.gameObject.SetActive(true);
            if(startImmediately) { EventBus.RaiseRoomEntered(RoomId); Save("room_enter"); }
            Refresh();
        }
        void Update() { if(!HasEntered || leaving || GameState.Current==null) return; Timeline.Advance(Time.deltaTime); Refresh(); }
        void Refresh()
        {
            bool ready=Timeline.DoorOpen && !gambling.IsPlaying;
            exitDoor.SetActive(!Timeline.DoorOpen || gambling.IsPlaying); outgoing.SourceExitOpen=ready; contact.ContactsEnabled=Timeline.ToolsEnabled && !gambling.IsPlaying;
            room.ConfigureExit(ready,Timeline.ExitOnly,room.transform.TransformPoint(new Vector3(23,0,12.1f)),config.roomExitChoiceWeight);
            if(Timeline.DoorOpen && !wasOpen) { wasOpen=true; GameState.Current.LogEvent(Time.time,RoomId,"door_open","elapsed="+Timeline.Elapsed); }
            if(ready && (!exitDirected || (Timeline.ExitOnly && !exitOnlyDirected)))
            { fly.HeadToExit(); exitDirected=true; exitOnlyDirected=Timeline.ExitOnly; }
            if(!tomorrowSpoken && Timeline.Elapsed>=Mathf.Max(config.roomEarliestExitSeconds,config.roomDoorOpenSeconds)*config.youngAdultTomorrowFraction && !gambling.IsPlaying)
            { tomorrowSpoken=true; Say("youngadult_tomorrow","“내일은 내가 해 볼게. 지켜봐 줄래?”"); }
            view.Refresh(Timeline,GameState.Current,room.VisitedItemIds.Count,gambling.IsPlaying ? "흘러간 시간과 남은 선택을 잠시 지켜봐 주세요." : null);
        }
        void Visit(ItemSlot slot,bool first)
        {
            if(leaving || gambling.IsPlaying) return;
            if(first && Timeline.EffectsEnabled && slot.ItemId=="youngadult_gambling" && gambling.Play())
            {
                pendingGambling=slot; pendingContext=slot.LastVisitContext; exitDoor.SetActive(true); outgoing.SourceExitOpen=false; contact.ContactsEnabled=false;
                GameState.Current.LogEvent(Time.time,RoomId,"gambling_montage_started","first_visit"); Save("gambling_started"); return;
            }
            Apply(slot,first,Timeline.EffectsEnabled,slot.LastVisitContext);
        }
        void GamblingCompleted()
        {
            // Eligibility is fixed when the first visit starts: crossing 10 minutes during the montage cannot erase the loss.
            if(pendingGambling==null) return;
            GameState.Current.LogEvent(Time.time,RoomId,"gambling_montage_completed","loss_applied_once");
            Apply(pendingGambling,true,true,pendingContext); pendingGambling=null; Refresh();
        }
        void Apply(ItemSlot slot,bool first,bool effectsEnabled,ItemVisitContext context)
        {
            var result=effects.Visit(slot.Definition,first,effectsEnabled,Time.time,context);
            view.ShowNarrative(result.Narrative); EventBus.RaiseItemVisited(slot.ItemId,result.FirstVisit);
            Save(result.FirstVisit ? "item_first:"+slot.ItemId : "item_revisit:"+slot.ItemId);
        }
        void UsedTool(PushSource source,Vector3 direction,string target)
        {
            if(!HasEntered || leaving) return;
            if(fly.IsProtectingActivity) Say("youngadult_boundary","“지금 하던 일은 내가 마칠게.”");
            else if(GameState.Current.Relationship.Trust>=config.youngAdultHighTrustThreshold) Say("youngadult_advice","“말해 줘서 고마워. 다음 길은 내가 골라 볼게.”");
        }
        void Say(string type,string line) { view.ShowNarrative(line); GameState.Current.LogEvent(Time.time,RoomId,type,line); }
        void Leaving() { leaving=true; contact.ContactsEnabled=false; }
        void EnteredCorridor()=>view.HideHud();
        void Save(string checkpoint) { GameState.Current.LogEvent(Time.time,RoomId,"checkpoint",checkpoint); saves.SaveCheckpoint(GameState.Current,checkpoint,CheckpointPath); }
        void OnDestroy()
        {
            foreach(var remove in unsubscribe) remove();
            if(gambling!=null) gambling.Completed-=GamblingCompleted;
            if(outgoing!=null) { outgoing.TransitionStarted-=Leaving; outgoing.CorridorEntered-=EnteredCorridor; }
        }
    }
}
