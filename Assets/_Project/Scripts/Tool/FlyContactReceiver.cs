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
        PushResolver push;
        public ToolUsageTracker Usage { get; private set; }
        public ComplianceStateMachine Compliance { get; private set; }
        public CoercionDetector Coercion { get; private set; }
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
            push = new PushResolver(config);
            Usage = new ToolUsageTracker(config, GameState.Current, Time.time);
            Compliance = new ComplianceStateMachine(config, Usage.Mood);
            Coercion = new CoercionDetector(config);
            Usage.EnterRoom(gameObject.scene.name, Time.time);
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
            if (!ContactsEnabled) return; // 05 관찰 시간에는 집계/수치/연출/밀림 모두 비활성화.
            if (!EnsureInitialized()) return;
            Usage.RecordContact(source, currentTime);
            var result = push.Resolve(source, contactDirection, transform.position);
            Coercion.Record(currentTime, source, transform.position, result.Direction);
            EventBus.RaiseToolUsed(source, result.Direction, fly.CurrentDestinationId);
            var state = GameState.Current;
            var response = Compliance.RecordContact(source, result.Direction, state.Stats.GetValue(StatType.Rebellion), currentTime);
            if (response == ComplianceContactResult.Entered || response == ComplianceContactResult.Continued)
            {
                bool entry = response == ComplianceContactResult.Entered;
                string reason = entry ? "compliance_enter" : "compliance_contact";
                state.Stats.ApplyDelta(StatType.Stress, entry ? config.complianceThresholdStressDelta : config.compliancePerContactStressDelta, reason);
                state.Stats.ApplyDelta(StatType.Rebellion, entry ? config.complianceThresholdRebellionDelta : config.compliancePerContactRebellionDelta, reason);
                state.LogEvent(currentTime, state.CurrentRoomId, reason, source.ToString());
                fly.GuideCompliance(Compliance.Direction);
            }
            if (Compliance.State == ComplianceState.Complying) return;
            if (fly.IsComplying) fly.ReleaseCompliance();
            // 회피는 이동 저항이다. 접촉 집계/전기 +2/감전 표시는 계속 발생한다.
            if (Usage.Mood.IsAvoiding(currentTime)) fly.ResumeAutonomy();
            else fly.ApplyPush(result);
        }

        public void ForceEndAvoidance() => Usage?.Mood.ForceEndAvoidance();

        public ItemVisitContext PrepareItemVisit(ItemSlot slot, bool first, float now)
        {
            if (!EnsureInitialized()) return default;
            Compliance.Tick(now);
            var coercion = first && CoercionDetector.IsTrainingItem(slot.Definition, config)
                ? Coercion.Evaluate(now, slot.transform.position) : CoercionKind.None;
            var context = new ItemVisitContext(Compliance.State == ComplianceState.Complying, coercion);
            if (context.IsForced)
            {
                var state = GameState.Current;
                state.LogEvent(now, state.CurrentRoomId, "forced_visit", slot.ItemId + ";first=" + first + ";complying=" + context.WasComplying);
            }
            return context;
        }
        public void CompleteItemVisit(ItemVisitContext context)
        {
            if (!context.WasComplying) return;
            Compliance.CompleteVisit();
            fly.ReleaseCompliance();
        }
    }
}
