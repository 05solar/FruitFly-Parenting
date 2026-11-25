using System;
using System.Collections.Generic;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Tool;
using FruitFlyParenting.Wheelchair;
using UnityEngine;

namespace FruitFlyParenting.Room
{
    public enum InfantRoomPhase { InRoom, Fading, Exited }

    [DefaultExecutionOrder(-50)]
    public sealed class InfantRoomController : MonoBehaviour
    {
        public const string RoomId = "01_Infant";
        [SerializeField] GameBalanceConfig config;
        [SerializeField] RoomController room;
        [SerializeField] FlyAgent fly;
        [SerializeField] WheelchairLocomotion chair;
        [SerializeField] GameObject exitDoor;
        [SerializeField] Light sunlight;
        [SerializeField] Light exitLight;
        [SerializeField] InfantRoomView view;
        [SerializeField] RoomTransitionController transition;
        readonly List<Action> unsubscribe = new List<Action>();
        ItemEffectApplier effects;
        SaveSystem saves;
        FlyContactReceiver contact;
        bool wasOpen, wasExitOnly;
        float previousTimeScale;
        public RoomTimeline Timeline { get; private set; }
        public InfantRoomPhase Phase { get; private set; }
        public InfantRoomView View => view;
        public RoomController Room => room;
        public bool DoorBlocking => exitDoor.activeSelf;
        public float LastFadeRealSeconds => transition.LastFadeOutSeconds;
        public RoomTransitionController Transition => transition;
        public void ConfigureTransition(RoomTransitionController value) => transition = value;
        public string CheckpointPath { get; set; } // 테스트/호스트가 저장 위치를 주입할 수 있다. null이면 기본 경로.

        public void Configure(GameBalanceConfig balance, RoomController container, FlyAgent child,
            WheelchairLocomotion wheelchair, GameObject door, Light sun, Light doorLight, InfantRoomView display)
        { config = balance; room = container; fly = child; chair = wheelchair; exitDoor = door; sunlight = sun; exitLight = doorLight; view = display; }

        void Start()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = Mathf.Clamp(config.infantDebugTimeScale, 0.1f, 20f);
            Timeline = new RoomTimeline(config);
            effects = new ItemEffectApplier(config, GameState.Current, RoomId);
            saves = new SaveSystem(config);
            room.RefreshItemSlots();
            room.ConfigureLifeStage(LifeStage.Infant);
            room.ResetVisits();
            contact = fly.GetComponent<FlyContactReceiver>();
            contact.ContactsEnabled = false;
            foreach (var slot in room.ItemSlots)
            {
                Action first = () => Visit(slot, true);
                Action revisit = () => Visit(slot, false);
                slot.OnFirstVisit += first; slot.OnRevisit += revisit;
                unsubscribe.Add(() => { if (slot != null) { slot.OnFirstVisit -= first; slot.OnRevisit -= revisit; } });
            }
            GameState.Current.CurrentRoomId = RoomId;
            EventBus.RaiseRoomEntered(RoomId);
            view.Initialize(Camera.main, room);
            transition.CheckpointPath = CheckpointPath;
            transition.TransitionStarted += OnTransitionStarted;
            transition.CorridorEntered += OnCorridorEntered;
            Save("room_enter");
            UpdateRoom();
        }

        void Update()
        {
            if (Timeline == null) return;
            if (Phase == InfantRoomPhase.InRoom)
            {
                Timeline.Advance(Time.deltaTime);
                UpdateRoom();
            }
        }

        void UpdateRoom()
        {
            contact.ContactsEnabled = true;
            exitDoor.SetActive(!Timeline.DoorOpen);
            transition.SourceExitOpen = Timeline.DoorOpen;
            exitLight.intensity = Mathf.Lerp(0f, 4f, Timeline.DayProgress);
            sunlight.transform.rotation = Quaternion.Euler(Mathf.Lerp(48f, 18f, Timeline.DayProgress), Mathf.Lerp(-35f, 20f, Timeline.DayProgress), 0);
            sunlight.color = Color.Lerp(new Color(1f, 0.91f, 0.75f), new Color(1f, 0.68f, 0.42f), Timeline.DayProgress);
            room.ConfigureExit(Timeline.DoorOpen, Timeline.ExitOnly, new Vector3(23f, 0f, 12.1f), config.roomExitChoiceWeight);
            if ((Timeline.DoorOpen && !wasOpen) || (Timeline.ExitOnly && !wasExitOnly)) fly.HeadToExit();
            if (Timeline.DoorOpen && !wasOpen) GameState.Current.LogEvent(Time.time, RoomId, "door_open", "elapsed=" + Timeline.Elapsed);
            wasOpen = Timeline.DoorOpen; wasExitOnly = Timeline.ExitOnly;
            view.Refresh(Timeline, GameState.Current, room.VisitedItemIds.Count);
        }

        void Visit(ItemSlot slot, bool first)
        {
            if (Phase != InfantRoomPhase.InRoom) return;
            var result = effects.Visit(slot.Definition, first, Timeline.EffectsEnabled, Time.time, slot.LastVisitContext);
            view.ShowDialogue(result.Narrative);
            EventBus.RaiseItemVisited(slot.ItemId, result.FirstVisit);
            if (result.FirstVisit) Save("item_first:" + slot.ItemId);
        }

        // 트리거는 진입을 알린다. 시선/버튼/시간 경과만으로 전환하지 않는다.
        public bool TryBeginExit(WheelchairLocomotion enteringChair)
        {
            return Phase == InfantRoomPhase.InRoom && transition.TryEnterCorridor(enteringChair);
        }
        void OnTransitionStarted()
        {
            Phase = InfantRoomPhase.Fading;
            contact.ContactsEnabled = false;
        }
        void OnCorridorEntered() { Phase = InfantRoomPhase.Exited; view.HideHud(); }

        void Save(string checkpoint)
        {
            GameState.Current.LogEvent(Time.time, GameState.Current.CurrentRoomId, "checkpoint", checkpoint);
            saves.SaveCheckpoint(GameState.Current, checkpoint, CheckpointPath);
        }

        void OnDestroy()
        {
            if (transition != null)
            {
                transition.TransitionStarted -= OnTransitionStarted;
                transition.CorridorEntered -= OnCorridorEntered;
            }
            foreach (var remove in unsubscribe) remove();
            if (Timeline != null) Time.timeScale = previousTimeScale;
        }
    }
}
