using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using UnityEngine;

namespace FruitFlyParenting.Simulation
{
    public sealed class SimulationItem
    {
        public ItemDefinition Definition;
        public Vector3 Position;
    }
    public sealed class SimulationRoom
    {
        public string Id;
        public LifeStage Stage;
        public SimulationItem[] Items;
    }
    [Serializable] public sealed class SimulationSettings
    {
        // Spatial approximation only. Gameplay thresholds always come from the shipped balance asset.
        public float stepSeconds = .5f, travelSpeed = 2.5f, interactionRadius = 1.2f, dwellSeconds = 2f;
    }
    public sealed class PolicyObservation
    {
        public GameState State;
        public SimulationRoom Room;
        public SimulationItem Destination;
        public float Elapsed;
        public IReadOnlyCollection<string> Visited;
    }
    public readonly struct PolicyContact
    {
        public readonly PushSource Source;
        public readonly SimulationItem Toward;
        public PolicyContact(PushSource source, SimulationItem toward) { Source = source; Toward = toward; }
    }
    public interface IPlayerPolicy
    {
        string Id { get; }
        void Reset(int seed);
        PolicyContact? Decide(PolicyObservation observation);
    }
    public sealed class PassivePolicy : IPlayerPolicy
    {
        public string Id => "passive";
        public void Reset(int seed) { }
        public PolicyContact? Decide(PolicyObservation observation) => null;
    }
    public class RandomInterventionPolicy : IPlayerPolicy
    {
        protected System.Random random;
        protected float next, interval, electricProbability;
        string room;
        public virtual string Id => "random";
        public virtual void Reset(int seed)
        {
            random = new System.Random(unchecked(seed ^ 0x51F15E)); room = null; next = 0;
            interval = 1f + (float)random.NextDouble() * 79f;
            electricProbability = (float)random.NextDouble();
        }
        protected virtual SimulationItem Target(PolicyObservation o) => o.Room.Items[random.Next(o.Room.Items.Length)];
        public virtual PolicyContact? Decide(PolicyObservation o)
        {
            if (room != o.Room.Id) { room = o.Room.Id; next = o.Elapsed; }
            if (o.Elapsed < next) return null;
            next = o.Elapsed + Math.Max(1f, interval * (.75f + (float)random.NextDouble() * .5f));
            return new PolicyContact(random.NextDouble() < electricProbability ? PushSource.ElectricStick : PushSource.Wheelchair, Target(o));
        }
    }
    public sealed class AggressiveCoercionPolicy : RandomInterventionPolicy
    {
        public override string Id => "aggressive";
        public override void Reset(int seed) { base.Reset(seed); interval = 1.1f; electricProbability = 1; }
        protected override SimulationItem Target(PolicyObservation o) => o.Room.Items.First(i => i.Definition.tags.Contains(TagDefinitions.Study));
    }
    public sealed class TargetedEndingPolicy : RandomInterventionPolicy
    {
        public EndingCondition TargetEnding { get; }
        public bool ObservationOnly => TargetEnding == EndingCondition.MagicPromise || TargetEnding == EndingCondition.GreatSuccess ||
            TargetEnding == EndingCondition.Success || TargetEnding == EndingCondition.Team;
        public override string Id => "target_" + ((int)TargetEnding).ToString("00");
        public TargetedEndingPolicy(EndingCondition target) { TargetEnding = target; }
        public override void Reset(int seed)
        {
            base.Reset(seed);
            // Observe for relationship endings; vary actual contact schedules for pressure/estrangement endings.
            if (ObservationOnly)
                interval = float.PositiveInfinity;
            if (TargetEnding == EndingCondition.SmallTable) { interval = 7f; electricProbability = 0; }
        }
        public override PolicyContact? Decide(PolicyObservation o)
        {
            if (ObservationOnly) return null;
            return base.Decide(o);
        }
        protected override SimulationItem Target(PolicyObservation o)
        {
            if (TargetEnding == EndingCondition.SmallTable)
                // Slow wheelchair nudges interrupt an activity's dwell without the <5s trust penalty.
                return o.Room.Items.First(i => i != o.Destination);
            var preferred = o.Room.Items.Where(i => !o.Visited.Contains(i.Definition.itemId)).Where(i =>
                TargetEnding == EndingCondition.Gang ? i.Definition.recordsDelinquency || i.Definition.tags.Contains(TagDefinitions.Rebellion) :
                TargetEnding == EndingCondition.OwnCity ? i.Definition.tags.Contains(TagDefinitions.Creation) :
                TargetEnding == EndingCondition.Debt ? i.Definition.tags.Contains(TagDefinitions.Achievement) : true).ToArray();
            return preferred.Length == 0 ? base.Target(o) : preferred[random.Next(preferred.Length)];
        }
    }
    [Serializable] public sealed class SimulationResult
    {
        public int seed, ending;
        public string policy;
        public float elapsed;
        public GameStateSaveData state;
        public ExperienceImpact[] decisiveExperiences;
        public string[] violations;
    }
    public sealed class SimulationRunner
    {
        readonly GameBalanceConfig config;
        readonly SimulationRoom[] rooms;
        readonly EndingDefinition[] endings;
        readonly SimulationSettings settings;
        public SimulationRunner(GameBalanceConfig config, SimulationRoom[] rooms, EndingDefinition[] endings, SimulationSettings settings = null)
        { this.config = config; this.rooms = rooms; this.endings = endings; this.settings = settings ?? new SimulationSettings(); }

        public SimulationResult Run(int seed, IPlayerPolicy policy)
        {
            var state = new GameState(config, seed); policy.Reset(seed);
            var violations = new List<string>();
            state.Stats.OnStatChanged += (type, before, after, reason) => {
                if (float.IsNaN(after) || float.IsInfinity(after) || after < 0 || after > 100) violations.Add("stat:" + type + ":" + reason);
            };
            state.Relationship.OnTrustChanged += (before, after, reason) => {
                if (float.IsNaN(after) || float.IsInfinity(after) || after < 0 || after > 100) violations.Add("trust:" + reason);
            };
            float now = config.prologueSeconds;
            state.LogEvent(0, "00_Bootstrap", "prologue", "start");
            foreach (var room in rooms)
            {
                state.CurrentRoomId = room.Id; state.LogEvent(now, room.Id, "room_enter", room.Stage.ToString());
                if (room.Stage == LifeStage.Adolescence) state.CaptureAdolescenceCostume(now);
                var contact = new ContactModel(config, state, now); contact.EnterRoom(room.Id, now);
                var learning = new PreferenceLearningTracker(config, state);
                var effects = new ItemEffectApplier(config, state, room.Id);
                var adultEffects = new YoungAdultItemEffects(config, state);
                var movement = new FlyAutonomousMovement(config, new SystemRandomSource(state.Random), state);
                var airplane = new PaperAirplaneEvent(config);
                var timeline = new RoomTimeline(config);
                var visited = new HashSet<string>();
                var position = new Vector3(12, 0, 11);
                SimulationItem destination = null, guidedItem = null, stayItem = null, pendingGambling = null;
                Vector3 guidancePoint = position;
                bool guiding = false;
                ItemDwellTracker dwell = null;
                float stayed = 0, gamblingAt = -1;
                ItemVisitContext gamblingContext = default;
                var observation = new PolicyObservation { State = state, Room = room, Visited = visited };
                while (!timeline.DoorOpen)
                {
                    float dt = settings.stepSeconds;
                    // Event-driven fast-forward for observational policies, after the dwell has actually fired.
                    if ((policy is PassivePolicy || policy is TargetedEndingPolicy observer && observer.ObservationOnly) &&
                        dwell != null && dwell.HasConfirmedVisit && stayed < config.maxItemDwellSeconds && gamblingAt < 0)
                        dt = Math.Min(config.maxItemDwellSeconds - stayed, Math.Min(10, config.roomDoorOpenSeconds - timeline.Elapsed));
                    now += dt; timeline.Advance(dt); contact.Tick(now);
                    if (guiding && contact.Compliance.State != ComplianceState.Complying)
                    { guiding = false; stayed = 0; dwell = null; stayItem = null; }
                    if (state.Relationship.Trust < 0 || state.Relationship.Trust > 100 || float.IsNaN(state.Relationship.Trust)) violations.Add("trust");
                    if (room.Stage == LifeStage.Childhood && airplane.TryTrigger(timeline.Elapsed, visited, state.Relationship.Trust))
                        state.RecordPaperAirplane(airplane.Outcome, now);
                    if (gamblingAt >= 0)
                    {
                        if (now >= gamblingAt)
                        { adultEffects.Visit(pendingGambling.Definition, true, true, now, gamblingContext); gamblingAt = -1; }
                        else continue;
                    }
                    if (destination == null || (!guiding && !contact.Compliance.IsSuspended(now) && stayed >= config.maxItemDwellSeconds))
                    {
                        string previous = destination?.Definition.itemId;
                        var candidates = room.Items.Select(i => new ItemChoiceCandidate(i.Definition.itemId, i.Definition.tags,
                            i.Definition.baseAttraction + (timeline.Elapsed >= config.roomDoorOpenSeconds - config.adolescenceWaitingWindowSeconds &&
                            room.Stage == LifeStage.Adolescence && i.Definition.tags.Contains(TagDefinitions.Rest) ? config.adolescenceLateRestAttraction : 0))).ToArray();
                        string next = movement.PickNextDestination(candidates, previous, timeline.Elapsed, room.Stage);
                        destination = room.Items.Single(i => i.Definition.itemId == next); stayed = 0; dwell = null; stayItem = null;
                        state.LogEvent(now, room.Id, "choice", next);
                    }
                    observation.Destination = guiding && guidedItem != null ? guidedItem : destination; observation.Elapsed = timeline.Elapsed;
                    var input = policy.Decide(observation);
                    if (input.HasValue)
                    {
                        var command = input.Value;
                        var direction = (command.Toward.Position - position).normalized;
                        var outcome = contact.Receive(command.Source, -direction, position, room.Stage, destination.Definition.itemId, now);
                        if (outcome.Motion == ContactMotion.Push || outcome.Motion == ContactMotion.Comply)
                        {
                            learning.RecordContact(room.Id, destination.Definition, (destination.Position - position).normalized, command.Source, now, state.Stats.GetValue(StatType.Stress));
                            if (outcome.Motion == ContactMotion.Comply)
                            {
                                // Match the live navigation adapter's range, angle and nearest-slot rules without NavMesh queries.
                                guidedItem = room.Items.Where(i => Vector3.Distance(position, i.Position) <= config.complianceNavigationDistance &&
                                    Vector3.Angle(contact.Compliance.Direction, i.Position - position) <= config.complianceSameDirectionToleranceDegrees)
                                    .OrderBy(i => (i.Position - position).sqrMagnitude).FirstOrDefault();
                                guidancePoint = guidedItem != null ? guidedItem.Position : position + contact.Compliance.Direction * config.complianceNavigationDistance;
                                guidancePoint.x = Mathf.Clamp(guidancePoint.x, .5f, 23.5f); guidancePoint.z = Mathf.Clamp(guidancePoint.z, .5f, 21.5f);
                                guiding = true;
                            }
                            else
                            {
                                bool occupied = stayItem != null && Vector3.Distance(position, stayItem.Position) <= settings.interactionRadius;
                                position = Vector3.MoveTowards(position, command.Toward.Position, outcome.Push.Force);
                                stayed = 0; // Live ApplyPush resets arrival, even when the child returns to the same goal.
                                if (occupied && Vector3.Distance(position, stayItem.Position) > settings.interactionRadius)
                                {
                                    learning.ConfirmPushedAway(room.Id, stayItem.Definition.itemId, now);
                                    dwell = null; stayItem = null;
                                }
                            }
                        }
                    }
                    position = Vector3.MoveTowards(position, guiding ? guidancePoint : destination.Position, settings.travelSpeed * dt);
                    var arrived = guiding ? guidedItem : destination;
                    if (arrived == null || Vector3.Distance(position, arrived.Position) > settings.interactionRadius)
                    { stayed = 0; dwell = null; stayItem = null; continue; }
                    if (stayItem != arrived) { stayed = 0; dwell = null; stayItem = arrived; }
                    stayed += dt;
                    if (dwell == null)
                    {
                        learning.ConfirmArrival(room.Id, arrived.Definition.itemId, now);
                        dwell = new ItemDwellTracker(settings.dwellSeconds, arrived.Definition.isExtreme ? config.extremeItemHesitationSeconds : 0);
                        dwell.OnEnter();
                    }
                    if (!dwell.Tick(dt)) continue;
                    bool first = visited.Add(arrived.Definition.itemId);
                    var context = contact.PrepareVisit(arrived.Definition, arrived.Position, first, now);
                    if (room.Stage == LifeStage.YoungAdult && arrived.Definition.itemId == "youngadult_gambling" && first)
                    { gamblingAt = now + config.gamblingWinSeconds + config.gamblingCalendarSeconds + config.gamblingLossSeconds; gamblingContext = context; pendingGambling = arrived; }
                    else if (room.Stage == LifeStage.YoungAdult) adultEffects.Visit(arrived.Definition, first, timeline.EffectsEnabled, now, context);
                    else effects.Visit(arrived.Definition, first, timeline.EffectsEnabled, now, context);
                    if (context.WasComplying) contact.Compliance.CompleteVisit();
                }
                // The live room directs the child to the door at opening; approximate the walk and wheelchair corridor travel.
                if (gamblingAt >= 0) { now = Math.Max(now, gamblingAt); adultEffects.Visit(pendingGambling.Definition, true, true, now, gamblingContext); }
                if (room.Stage == LifeStage.Adolescence) state.CaptureAdolescenceExit(now);
                now += Vector3.Distance(position, new Vector3(23, 0, 12.1f)) / settings.travelSpeed;
                contact.Tick(now);
                state.CaptureRoomExitStress(room.Stage, now);
                state.LogEvent(now, room.Id, "room_exit", timeline.Elapsed.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (room.Stage == LifeStage.YoungAdult) state.CaptureFinalYoungAdultSnapshot(now);
                else now += config.corridorLength / config.wheelchairMaxSpeed + config.roomFadeSeconds * 2;
            }
            var ending = EndingEvaluator.Evaluate(state.FinalYoungAdultSnapshot, endings);
            state.LogEvent(now, "06_MiddleAge", "ending", ending.endingId);
            var duplicates = state.EventLog.Where(e => e.eventType == "item_first").GroupBy(e => e.roomId + ":" + e.experience.itemId).Where(g => g.Count() > 1);
            foreach (var duplicate in duplicates) violations.Add("duplicate:" + duplicate.Key);
            bool gambled = state.EventLog.Any(e => e.eventType == "item_first" && e.experience.itemId == "youngadult_gambling");
            if ((ending.condition == EndingCondition.Bankruptcy) != gambled) violations.Add("gambling precedence/provenance");
            return new SimulationResult { seed = seed, policy = policy.Id, ending = (int)ending.condition, elapsed = now + config.middleAgeSeconds,
                state = state.ToSaveData(), decisiveExperiences = EndingEvaluator.DecisiveExperiences(state.EventLog), violations = violations.ToArray() };
        }
    }
}
