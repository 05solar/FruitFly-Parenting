using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using UnityEngine;

namespace FruitFlyParenting.Tool
{
    [RequireComponent(typeof(FlyAgent))]
    public sealed class FlyContactReceiver : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        FlyAgent fly;
        ContactModel model;
        public ToolUsageTracker Usage => model?.Usage;
        public ComplianceStateMachine Compliance => model?.Compliance;
        public CoercionDetector Coercion => model?.Coercion;
        public bool ContactsEnabled { get; set; } = true;
        public void Configure(GameBalanceConfig balance) => config = balance;
        void OnEnable() => EventBus.RoomEntered += EnterRoom;
        void OnDisable() => EventBus.RoomEntered -= EnterRoom;
        void Start() => EnsureInitialized();
        bool EnsureInitialized()
        {
            if (Usage != null) return true;
            if (config == null || GameState.Current == null) return false;
            fly = GetComponent<FlyAgent>();
            model = new ContactModel(config, GameState.Current, Time.time);
            model.EnterRoom(gameObject.scene.name, Time.time);
            return true;
        }
        void Update()
        {
            if (!EnsureInitialized()) return;
            Usage.Tick(Time.time);
            Compliance.Tick(Time.time);
            if (fly.IsComplying && Compliance.State != ComplianceState.Complying) fly.ReleaseCompliance();
        }
        void EnterRoom(string roomId)
        {
            if (!EnsureInitialized()) return;
            Usage.EnterRoom(roomId, Time.time);
            Compliance.EnterRoom(); Coercion.Reset();
            if (fly.IsComplying) fly.ReleaseCompliance();
        }

        public void ReceiveContact(PushSource source, Vector3 contactDirection, float currentTime)
        {
            if (!ContactsEnabled) return; // Scene transitions and scripted sequences temporarily suspend contact.
            if (!EnsureInitialized()) return;
            var outcome = model.Receive(source, contactDirection, transform.position, fly.Stage, fly.CurrentDestinationId, currentTime);
            if (source == PushSource.ElectricStick) EventBus.RaiseElectricShockVisual(transform.position);
            EventBus.RaiseToolUsed(source, outcome.Push.Direction, fly.CurrentDestinationId);
            if (outcome.Motion == ContactMotion.PreserveActivity)
            { fly.PreserveCurrentActivityUntil(Compliance.SuspendedUntil); return; }
            if (outcome.Motion == ContactMotion.Comply)
            { fly.RecordLearningContact(source, currentTime); fly.GuideCompliance(Compliance.Direction); return; }
            if (fly.IsComplying) fly.ReleaseCompliance();
            if (outcome.Motion == ContactMotion.Avoid) fly.ResumeAutonomy();
            else { fly.RecordLearningContact(source, currentTime); fly.ApplyPush(outcome.Push); }
        }

        public void ForceEndAvoidance() => Usage?.Mood.ForceEndAvoidance();

        public ItemVisitContext PrepareItemVisit(ItemSlot slot, bool first, float now)
        {
            if (!EnsureInitialized()) return default;
            return model.PrepareVisit(slot.Definition, slot.transform.position, first, now);
        }

        public void CompleteItemVisit(ItemVisitContext context)
        {
            if (!context.WasComplying) return;
            Compliance.CompleteVisit();
            fly.ReleaseCompliance();
        }
    }
}
