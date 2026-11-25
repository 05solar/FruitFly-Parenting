using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Tool;
using UnityEngine;

namespace FruitFlyParenting.Room
{
    [DefaultExecutionOrder(-40)]
    public sealed class AdolescenceRoomController : MonoBehaviour
    {
        public const string RoomId="04_Adolescence";
        [SerializeField] GameBalanceConfig config;
        [SerializeField] RoomController room;
        [SerializeField] FlyAgent fly;
        [SerializeField] RoomDestination entrance;
        [SerializeField] GameObject exitDoor;
        [SerializeField] RoomTransitionController incoming,outgoing;
        [SerializeField] InfantRoomView view;
        [SerializeField] AdolescenceCostumeView costume;
        [SerializeField] AdolescenceExitSequence exitSequence;
        [SerializeField] bool startImmediately;
        readonly List<Action> unsubscribe=new List<Action>();
        ItemEffectApplier effects;
        SaveSystem saves;
        FlyContactReceiver contact;
        bool leaving,wasOpen,exitDirected;
        int logCursor;
        public RoomTimeline Timeline { get; private set; }
        public AdolescenceNarrative Narrative { get; private set; }
        public RoomController Room => room;
        public FlyAgent Fly => fly;
        public InfantRoomView View => view;
        public AdolescenceCostumeView Costume => costume;
        public AdolescenceExitSequence ExitSequence => exitSequence;
        public RoomTransitionController Transition => outgoing;
        public bool HasEntered => Timeline!=null;
        public string CheckpointPath { get; set; }
        public void Configure(GameBalanceConfig balance,RoomController container,FlyAgent actor,RoomDestination entry,
            GameObject door,InfantRoomView display,AdolescenceCostumeView clothes,AdolescenceExitSequence sequence,RoomTransitionController exit)
        { config=balance; room=container; fly=actor; entrance=entry; exitDoor=door; view=display; costume=clothes; exitSequence=sequence; outgoing=exit; }
        public void ConfigureIncoming(RoomTransitionController value) => incoming=value;
        public void ConfigureStandalone() => startImmediately=true;
        void OnEnable() { if(entrance!=null) entrance.Entering+=Begin; }
        void OnDisable() { if(entrance!=null) entrance.Entering-=Begin; EventBus.ToolUsed-=UsedTool; }
        void Start() { if(startImmediately) { entrance.Prepare(); entrance.Enter(); } }
        void Begin()
        {
            if(HasEntered) return;
            CheckpointPath=CheckpointPath??incoming?.CheckpointPath;
            Timeline=new RoomTimeline(config); Narrative=new AdolescenceNarrative(config);
            effects=new ItemEffectApplier(config,GameState.Current,RoomId); saves=new SaveSystem(config);
            room.ConfigureLifeStage(LifeStage.Adolescence); room.RefreshItemSlots(); room.ResetVisits();
            GameState.Current.CurrentRoomId=RoomId; logCursor=GameState.Current.EventLog.Count;
            GameState.Current.CaptureAdolescenceCostume(Time.time); costume.Apply(GameState.Current.AdolescenceHiddenCostume);
            contact=fly.GetComponent<FlyContactReceiver>(); contact.ContactsEnabled=false;
            foreach(var slot in room.ItemSlots)
            {
                Action first=()=>Visit(slot,true),revisit=()=>Visit(slot,false);
                slot.OnFirstVisit+=first; slot.OnRevisit+=revisit;
                if(slot.ItemId=="adolescence_parents") slot.AllowEntry=AllowConversation;
                unsubscribe.Add(()=> { if(slot!=null) { slot.OnFirstVisit-=first; slot.OnRevisit-=revisit; slot.AllowEntry=null; } });
            }
            if(incoming!=null) { incoming.Sound.enabled=false; outgoing.Sound.TakeOverPrevious(incoming.Sound.Next); }
            outgoing.CheckpointPath=CheckpointPath; outgoing.TransitionStarted+=Leaving; outgoing.CorridorEntered+=EnteredCorridor;
            outgoing.gameObject.SetActive(true); view.Initialize(Camera.main,room,"갈림길");
            view.ShowDialogue("오늘은 내가 하고 싶은 거 할래.");
            fly.AutonomousApproachChosen+=Chose; EventBus.ToolUsed+=UsedTool; exitSequence.Completed+=CompletedExitScene;
            fly.gameObject.SetActive(true);
            if(startImmediately) { EventBus.RaiseRoomEntered(RoomId); Save("room_enter"); }
            Refresh();
        }
        void Update()
        {
            if(!HasEntered || leaving || GameState.Current==null) return;
            Timeline.Advance(Time.deltaTime); Refresh();
        }
        void Refresh()
        {
            var state=GameState.Current;
            if(!exitSequence.IsPlaying) Say("waiting_choice",Narrative.Advance(Timeline.Elapsed));
            if(Timeline.DoorOpen && !wasOpen)
            {
                wasOpen=true; state.CaptureAdolescenceExit(Time.time);
                state.LogEvent(Time.time,RoomId,"door_open","elapsed="+Timeline.Elapsed);
                if(exitSequence.Play()) Say("flight_smoke","잠깐 바람 좀 쐬고 올게.");
                Save("adolescence_exit_snapshot");
            }
            bool ready=Timeline.DoorOpen && !exitSequence.IsPlaying;
            exitDoor.SetActive(!Timeline.DoorOpen); outgoing.SourceExitOpen=ready;
            contact.ContactsEnabled=!exitSequence.IsPlaying;
            room.ConfigureExit(ready,Timeline.ExitOnly,room.transform.TransformPoint(new Vector3(23,0,12.1f)),config.roomExitChoiceWeight);
            room.RestAttractionBonus=Narrative.InWaitingWindow(Timeline.Elapsed) ? config.adolescenceLateRestAttraction : 0;
            if(ready && (!exitDirected || (Timeline.ExitOnly && !exitOnlyDirected)))
            {
                fly.HeadToExit(); exitDirected=true; exitOnlyDirected=Timeline.ExitOnly;
            }
            while(logCursor<state.EventLog.Count)
            {
                var entry=state.EventLog[logCursor++];
                if(entry.roomId==RoomId && entry.eventType=="learning_avoidance" && entry.learning!=null)
                    Say("blocked_preference",Narrative.Block(entry.learning.tags,state.Stats.GetValue(StatType.Rebellion)));
            }
            view.Refresh(Timeline,state,room.VisitedItemIds.Count,
                exitSequence.IsPlaying ? "장면 진행 중" : null);
        }
        bool exitOnlyDirected;
        bool AllowConversation()
        {
            if(!Narrative.RefuseConversation(GameState.Current.Relationship.Trust)) return true;
            fly.RefuseApproach(config.adolescenceRefusalSeconds);
            Say("conversation_refused","지금은 얘기하기 싫어. 나중에 하자.");
            return false;
        }
        void Chose(ItemSlot slot) { if(!leaving && slot!=null) Say("voiced_preference",Narrative.Approach(slot.Definition)); }
        void UsedTool(PushSource source,Vector3 direction,string itemId) { if(HasEntered && !leaving) Narrative.ToolUsed(Timeline.Elapsed); }
        void Say(string type,string line)
        { if(string.IsNullOrEmpty(line)) return; view.ShowDialogue(line); GameState.Current.LogEvent(Time.time,RoomId,type,line); }
        void Visit(ItemSlot slot,bool first)
        {
            if(leaving || exitSequence.IsPlaying) return;
            var result=effects.Visit(slot.Definition,first,Timeline.EffectsEnabled,Time.time,slot.LastVisitContext);
            string line=result.Narrative;
            if(slot.ItemId=="adolescence_club" && room.VisitedItemIds.Contains("adolescence_performance")) line="그때 부른 노래 있잖아. 같이 고쳐 볼래?";
            if((slot.ItemId=="adolescence_counsel" || slot.ItemId=="adolescence_parents") && room.VisitedItemIds.Contains("adolescence_dayoff")) line="그날 왜 학교 안 갔냐면… 들어 줄래?";
            view.ShowDialogue(line); EventBus.RaiseItemVisited(slot.ItemId,result.FirstVisit);
            if(result.FirstVisit) Save("item_first:"+slot.ItemId);
        }
        void CompletedExitScene() { Save("adolescence_exit_event_end"); Refresh(); }
        void Leaving() { leaving=true; contact.ContactsEnabled=false; }
        void EnteredCorridor() => view.HideHud();
        void Save(string checkpoint) { GameState.Current.LogEvent(Time.time,RoomId,"checkpoint",checkpoint); saves.SaveCheckpoint(GameState.Current,checkpoint,CheckpointPath); }
        void OnDestroy()
        {
            foreach(var remove in unsubscribe) remove();
            if(fly!=null) fly.AutonomousApproachChosen-=Chose;
            if(exitSequence!=null) exitSequence.Completed-=CompletedExitScene;
            if(outgoing!=null) { outgoing.TransitionStarted-=Leaving; outgoing.CorridorEntered-=EnteredCorridor; }
        }
    }
}
