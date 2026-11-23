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
    public sealed class ChildhoodRoomController : MonoBehaviour
    {
        public const string RoomId = "03_Childhood";
        [SerializeField] GameBalanceConfig config;
        [SerializeField] RoomController room;
        [SerializeField] FlyAgent fly;
        [SerializeField] RoomDestination entrance;
        [SerializeField] GameObject exitDoor;
        [SerializeField] RoomTransitionController incoming, outgoing;
        [SerializeField] InfantRoomView view;
        [SerializeField] PaperAirplaneView airplaneView;
        [SerializeField] bool startImmediately;
        readonly List<Action> unsubscribe = new List<Action>();
        ItemEffectApplier effects;
        SaveSystem saves;
        FlyContactReceiver contact;
        bool wasOpen, wasExitOnly, leaving;
        public RoomTimeline Timeline { get; private set; }
        public PaperAirplaneEvent Airplane { get; private set; }
        public RoomController Room => room;
        public FlyAgent Fly => fly;
        public InfantRoomView View => view;
        public PaperAirplaneView AirplaneView => airplaneView;
        public RoomTransitionController Transition => outgoing;
        public bool HasEntered => Timeline != null;
        public string CheckpointPath { get; set; }
        public void Configure(GameBalanceConfig balance, RoomController container, FlyAgent actor, RoomDestination entry,
            GameObject door, InfantRoomView display, PaperAirplaneView paper, RoomTransitionController exit)
        { config = balance; room = container; fly = actor; entrance = entry; exitDoor = door; view = display; airplaneView = paper; outgoing = exit; }
        public void ConfigureIncoming(RoomTransitionController transition) => incoming = transition;
        public void ConfigureStandalone() => startImmediately = true;
        void OnEnable() { if (entrance != null) entrance.Entering += Begin; }
        void OnDisable() { if (entrance != null) entrance.Entering -= Begin; }
        void Start() { if (startImmediately) { entrance.Prepare(); entrance.Enter(); } }
        void Begin()
        {
            if (HasEntered) return;
            CheckpointPath = CheckpointPath ?? incoming?.CheckpointPath;
            Timeline = new RoomTimeline(config); Airplane = new PaperAirplaneEvent(config);
            effects = new ItemEffectApplier(config,GameState.Current,RoomId); saves = new SaveSystem(config);
            room.ConfigureLifeStage(LifeStage.Childhood); room.RefreshItemSlots(); room.ResetVisits();
            GameState.Current.CurrentRoomId = RoomId;
            contact = fly.GetComponent<FlyContactReceiver>(); contact.ContactsEnabled = false;
            foreach (var slot in room.ItemSlots)
            {
                Action first = () => Visit(slot,true), revisit = () => Visit(slot,false);
                slot.OnFirstVisit += first; slot.OnRevisit += revisit;
                unsubscribe.Add(() => { if (slot != null) { slot.OnFirstVisit -= first; slot.OnRevisit -= revisit; } });
            }
            if (incoming != null)
            {
                incoming.Sound.enabled = false;
                outgoing.Sound.TakeOverPrevious(incoming.Sound.Next);
            }
            outgoing.CheckpointPath = CheckpointPath;
            outgoing.TransitionStarted += Leaving;
            outgoing.CorridorEntered += EnteredCorridor;
            outgoing.gameObject.SetActive(true);
            view.Initialize(Camera.main,room,"놀이터와 첫 약속");
            view.ShowNarrative("교실과 놀이터 사이에서, 오늘의 놀이를 골라요.");
            fly.gameObject.SetActive(true);
            if (startImmediately) { EventBus.RaiseRoomEntered(RoomId); Save("room_enter"); }
            Refresh();
        }
        void Update()
        {
            if (!HasEntered || leaving || GameState.Current == null) return;
            Timeline.Advance(Time.deltaTime); Refresh();
        }
        void Refresh()
        {
            contact.ContactsEnabled = Timeline.ToolsEnabled;
            exitDoor.SetActive(!Timeline.DoorOpen); outgoing.SourceExitOpen = Timeline.DoorOpen;
            room.ConfigureExit(Timeline.DoorOpen,Timeline.ExitOnly,room.transform.TransformPoint(new Vector3(23,0,12.1f)),config.roomExitChoiceWeight);
            if ((Timeline.DoorOpen && !wasOpen) || (Timeline.ExitOnly && !wasExitOnly)) fly.HeadToExit();
            if (Timeline.DoorOpen && !wasOpen) GameState.Current.LogEvent(Time.time,RoomId,"door_open","elapsed="+Timeline.Elapsed);
            wasOpen = Timeline.DoorOpen; wasExitOnly = Timeline.ExitOnly;
            if (Airplane.TryTrigger(Timeline.Elapsed,room.VisitedItemIds,GameState.Current.Relationship.Trust))
            {
                GameState.Current.RecordPaperAirplane(Airplane.Outcome,Time.time);
                airplaneView.Play(Airplane.Outcome);
                view.ShowNarrative(PaperAirplaneEvent.SceneLine(Airplane.Outcome));
                Save("paper_airplane");
            }
            view.Refresh(Timeline,GameState.Current,room.VisitedItemIds.Count);
        }
        void Visit(ItemSlot slot, bool first)
        {
            if (leaving) return;
            var result = effects.Visit(slot.Definition,first,Timeline.EffectsEnabled,Time.time,slot.LastVisitContext);
            view.ShowNarrative(result.Narrative); EventBus.RaiseItemVisited(slot.ItemId,result.FirstVisit);
            if (result.FirstVisit) Save("item_first:"+slot.ItemId);
        }
        void Leaving() { leaving = true; contact.ContactsEnabled = false; }
        void EnteredCorridor() => view.HideHud();
        void Save(string checkpoint)
        {
            GameState.Current.LogEvent(Time.time,RoomId,"checkpoint",checkpoint);
            saves.SaveCheckpoint(GameState.Current,checkpoint,CheckpointPath);
        }
        void OnDestroy()
        {
            foreach (var remove in unsubscribe) remove();
            if (outgoing != null) { outgoing.TransitionStarted -= Leaving; outgoing.CorridorEntered -= EnteredCorridor; }
        }
    }
}
