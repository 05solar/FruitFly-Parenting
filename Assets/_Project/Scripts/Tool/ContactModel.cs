using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using UnityEngine;

namespace FruitFlyParenting.Tool
{
    public enum ContactMotion { Push, Avoid, Comply, PreserveActivity }
    public readonly struct ContactOutcome
    {
        public readonly PushResult Push;
        public readonly ContactMotion Motion;
        public ContactOutcome(PushResult push, ContactMotion motion) { Push = push; Motion = motion; }
    }

    // Shared by the live adapter and headless runner; no scene, clock or global event dependencies.
    public sealed class ContactModel
    {
        readonly GameBalanceConfig config;
        readonly GameState state;
        readonly PushResolver resolver;
        public ToolUsageTracker Usage { get; }
        public ComplianceStateMachine Compliance { get; }
        public CoercionDetector Coercion { get; }
        public ContactModel(GameBalanceConfig config, GameState state, float now)
        {
            this.config = config; this.state = state; resolver = new PushResolver(config);
            Usage = new ToolUsageTracker(config, state, now);
            Compliance = new ComplianceStateMachine(config, Usage.Mood); Coercion = new CoercionDetector(config);
        }
        public void EnterRoom(string room, float now)
        { Usage.EnterRoom(room, now); Compliance.EnterRoom(); Coercion.Reset(); }
        public void Tick(float now) { Usage.Tick(now); Compliance.Tick(now); }
        public ContactOutcome Receive(PushSource source, Vector3 normal, Vector3 position, LifeStage stage, string destination, float now)
        {
            float trustBefore = state.Relationship.Trust;
            Usage.Tick(now);
            using var impact = state.TrackImpact(now, state.CurrentRoomId, "contact", source == PushSource.ElectricStick ? "전기 봉 접촉" : "휠체어 접촉");
            Usage.RecordContact(source, now);
            var push = resolver.Resolve(source, normal, position, false);
            if (stage == LifeStage.YoungAdult && (trustBefore < config.youngAdultResistanceTrustThreshold || Compliance.IsSuspended(now)))
            {
                if (trustBefore < config.youngAdultResistanceTrustThreshold) Compliance.Suspend(now, config.youngAdultActivityProtectionSeconds);
                Coercion.Reset();
                state.LogEvent(now, state.CurrentRoomId, "youngadult_tool_resisted", destination);
                return new ContactOutcome(push, ContactMotion.PreserveActivity);
            }
            Coercion.Record(now, source, position, push.Direction);
            var response = Compliance.RecordContact(source, push.Direction, state.Stats.GetValue(StatType.Rebellion), now);
            if (response == ComplianceContactResult.Entered || response == ComplianceContactResult.Continued)
            {
                bool entry = response == ComplianceContactResult.Entered;
                string reason = entry ? "compliance_enter" : "compliance_contact";
                state.Stats.ApplyDelta(StatType.Stress, entry ? config.complianceThresholdStressDelta : config.compliancePerContactStressDelta, reason);
                state.Stats.ApplyDelta(StatType.Rebellion, entry ? config.complianceThresholdRebellionDelta : config.compliancePerContactRebellionDelta, reason);
                state.LogEvent(now, state.CurrentRoomId, reason, source.ToString());
            }
            return new ContactOutcome(push, Compliance.State == ComplianceState.Complying ? ContactMotion.Comply :
                Usage.Mood.IsAvoiding(now) ? ContactMotion.Avoid : ContactMotion.Push);
        }
        public ItemVisitContext PrepareVisit(ItemDefinition item, Vector3 position, bool first, float now)
        {
            Compliance.Tick(now);
            var coercion = first && CoercionDetector.IsTrainingItem(item, config) ? Coercion.Evaluate(now, position) : CoercionKind.None;
            var context = new ItemVisitContext(Compliance.State == ComplianceState.Complying, coercion);
            if (context.IsForced) state.LogEvent(now, state.CurrentRoomId, "forced_visit", item.itemId + ";first=" + first + ";complying=" + context.WasComplying);
            return context;
        }
    }
}
