using System;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Wheelchair;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace FruitFlyParenting.Corridor
{
    public sealed class RoomTransitionController : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] RoomTransitionDefinition route;
        [SerializeField] GameObject sourceRoom, sourceActor;
        [SerializeField] WheelchairLocomotion chair;
        [SerializeField] CorridorShell corridor;
        [SerializeField] RoomDestination destination;
        [SerializeField] TransitionView view;
        [SerializeField] CorridorAudioController sound;
        RoomTransitionSequence sequence;
        SaveSystem saves;
        LocomotionProvider[] providers;
        bool[] enabledProviders;
        bool chairWasEnabled;
        int lastSection = -1;
        public bool SourceExitOpen { get; set; }
        public string CheckpointPath { get; set; }
        public TransitionPhase Phase => sequence == null ? TransitionPhase.Idle : sequence.Phase;
        public float LastFadeOutSeconds => sequence.LastFadeOutSeconds;
        public CorridorShell Corridor => corridor;
        public RoomDestination Destination => destination;
        public TransitionView View => view;
        public CorridorAudioController Sound => sound;
        public RoomTransitionDefinition Route => route;
        public event Action TransitionStarted, CorridorEntered;

        public void Configure(GameBalanceConfig balance, RoomTransitionDefinition definition, GameObject room, GameObject actor,
            WheelchairLocomotion wheelchair, CorridorShell shell, RoomDestination nextRoom, TransitionView display, CorridorAudioController audio)
        {
            config = balance; route = definition; sourceRoom = room; sourceActor = actor; chair = wheelchair;
            corridor = shell; destination = nextRoom; view = display; sound = audio;
        }
        void Start()
        {
            if (!RoomTransitionSequence.Allows(route.fromStage, route.toStage))
                throw new InvalidOperationException("Only adjacent life-stage transitions are allowed.");
            sequence = new RoomTransitionSequence(config.roomFadeSeconds);
            saves = new SaveSystem(config);
            providers = chair.GetComponents<LocomotionProvider>();
            view.Initialize(Camera.main);
            sound.Initialize(config,route);
        }
        public bool TryEnterCorridor(WheelchairLocomotion enteringChair)
        {
            if (sequence == null || !SourceExitOpen || enteringChair != chair || !sourceRoom.activeInHierarchy ||
                GameState.Current == null || GameState.Current.CurrentRoomId != route.fromRoomId || !corridor.AtSourceBoundary(chair.transform.position)) return false;
            if (!sequence.Begin(route.fromStage,route.toStage)) return false;
            FreezeMovement(); TransitionStarted?.Invoke(); return true;
        }
        public bool TryEnterNextRoom(WheelchairLocomotion enteringChair)
        {
            if (sequence == null || enteringChair != chair || !corridor.AtDestinationBoundary(chair.transform.position)) return false;
            if (!sequence.BeginNextRoom(destination.ReadyForEntry)) return false;
            FreezeMovement(); return true;
        }
        void Update()
        {
            if (sequence == null) return;
            var before = sequence.Phase;
            sequence.Advance(Time.unscaledDeltaTime);
            if (before != sequence.Phase)
            {
                if (sequence.Phase == TransitionPhase.RevealingCorridor) EnterCorridor();
                else if (sequence.Phase == TransitionPhase.RevealingRoom) EnterNextRoom();
                else if (sequence.MovementAllowed) RestoreMovement();
                if (sequence.Phase == TransitionPhase.Complete && destination.OwnsPresentation) view.HideSubtitle();
            }
            view.SetFade(sequence.FadeAlpha);
            if (Phase == TransitionPhase.Corridor || Phase == TransitionPhase.RevealingCorridor)
            {
                corridor.SetNextRoomReady(destination.ReadyForEntry);
                sound.SetCorridorDistance(corridor.Distance(chair.transform.position));
                ShowSection();
            }
        }
        void EnterCorridor()
        {
            GameState.Current.CaptureRoomExitStress(route.fromStage, Time.time);
            // 물리 좌표는 그대로다. 인접한 컨테이너만 완전한 암전에서 교체한다.
            corridor.gameObject.SetActive(true);
            corridor.SetRoute(route);
            corridor.Recap.Show(GameState.Current,route.fromRoomId);
            destination.Prepare();
            sourceRoom.SetActive(false);
            if (sourceActor != null) sourceActor.SetActive(false);
            CorridorEntered?.Invoke();
            EventBus.RaiseRoomExited(route.fromRoomId);
            GameState.Current.CurrentRoomId = route.corridorId;
            Save("corridor_start");
        }
        void EnterNextRoom()
        {
            Save("corridor_end");
            corridor.gameObject.SetActive(false);
            GameState.Current.CurrentRoomId = route.toRoomId;
            destination.Enter();
            EventBus.RaiseRoomEntered(route.toRoomId);
            Save("room_enter");
            sound.EnterNextRoom();
            view.ShowSubtitle(route.nextRoomTitle + "\n" + route.nextRoomLine);
        }
        void ShowSection()
        {
            var section = corridor.SectionAt(chair.transform.position);
            if (lastSection == (int)section) return;
            lastSection = (int)section;
            GameState.Current.LogEvent(Time.time,route.corridorId,"corridor_section",section.ToString());
            string line = section == CorridorSection.Recap ? route.recapTitle + "\n벽에 남은 기억을 천천히 둘러보세요." :
                section == CorridorSection.Narration ? route.narration +
                    (string.IsNullOrEmpty(corridor.Recap.PaperAirplaneLine) ? "" : "\n" + corridor.Recap.PaperAirplaneLine) : route.nextRoomTitle + "\n다음 문으로 함께 가요.";
            view.ShowSubtitle(line);
        }
        void Save(string checkpoint)
        {
            GameState.Current.LogEvent(Time.time,GameState.Current.CurrentRoomId,"checkpoint",checkpoint);
            saves.SaveCheckpoint(GameState.Current,checkpoint,CheckpointPath);
        }
        void FreezeMovement()
        {
            chairWasEnabled = chair.enabled;
            enabledProviders = new bool[providers.Length];
            for (int i=0;i<providers.Length;i++) { enabledProviders[i] = providers[i].enabled; providers[i].enabled = false; }
            chair.enabled = false;
        }
        void RestoreMovement()
        {
            chair.enabled = chairWasEnabled;
            for (int i=0;i<providers.Length;i++) providers[i].enabled = enabledProviders[i];
        }
    }
}
