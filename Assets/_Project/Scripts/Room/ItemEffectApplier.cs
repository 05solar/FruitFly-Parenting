using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Tool;
using FruitFlyParenting.Fly;

namespace FruitFlyParenting.Room
{
    public readonly struct ItemVisitResult
    {
        public readonly bool FirstVisit;
        public readonly bool EffectsApplied;
        public readonly string Narrative;
        public ItemVisitResult(bool first, bool effects, string narrative)
        { FirstVisit = first; EffectsApplied = effects; Narrative = narrative; }
    }

    // 방마다 하나씩 생성한다. 슬롯 이벤트가 중복 발행되어도 효과는 방당 한 번뿐이다.
    public sealed class ItemEffectApplier
    {
        readonly GameBalanceConfig config;
        readonly GameState state;
        readonly string roomId;
        readonly RepeatedTrainingTracker training;
        readonly HashSet<string> visited = new HashSet<string>();
        readonly Dictionary<string, int> visits = new Dictionary<string, int>();
        public ItemEffectApplier(GameBalanceConfig config, GameState state, string roomId)
        { this.config = config; this.state = state; this.roomId = roomId; training = new RepeatedTrainingTracker(config); }

        void ApplyTraining(float now, string itemId, string reason, float stress, float rebellion)
        {
            state.Stats.ApplyDelta(StatType.Stress, stress, reason);
            state.Stats.ApplyDelta(StatType.Rebellion, rebellion, reason);
            state.LogEvent(now, roomId, reason, string.Format(CultureInfo.InvariantCulture,
                "{0};S={1};R={2}", itemId, stress, rebellion));
        }

        public ItemVisitResult Visit(ItemDefinition item, bool firstVisitEvent, bool effectsEnabled, float now, ItemVisitContext context = default)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) throw new ArgumentException("Item definition/id is required.");
            using var impact=state.TrackImpact(now,roomId,item.itemId,item.displayName);
            item.ValidateTags();
            bool newId = visited.Add(item.itemId);
            bool first = firstVisitEvent && newId;
            visits.TryGetValue(item.itemId, out int count);
            visits[item.itemId] = count + 1;
            bool apply = first && effectsEnabled;
            float magnitude = 0f;
            var log = new StringBuilder(item.itemId).Append(";tags=").Append(string.Join(",", item.tags));
            log.Append(";effects=").Append(apply);
            if (apply)
            {
                if (item.recordsDelinquency) state.RecordDelinquency(item.itemId, now);
                state.ApplyYoungAdultFlags(item.youngAdultFlags,item.itemId,now);
                if (item.trustDelta != 0f)
                {
                    float beforeTrust = state.Relationship.Trust;
                    state.Relationship.AdjustTrust(item.trustDelta, "item_first:" + item.itemId);
                    log.AppendFormat(CultureInfo.InvariantCulture, ";Trust={0}->{1}", beforeTrust, state.Relationship.Trust);
                }
                // 같은 스탯의 중복 항목도 합산 후 방문 단위 상한을 적용한다.
                var totals = new Dictionary<StatType, float>();
                var actualChanges = new Dictionary<StatType, float>();
                foreach (var delta in item.statDeltas)
                {
                    totals.TryGetValue(delta.stat, out float value);
                    totals[delta.stat] = value + delta.amount;
                }
                foreach (var pair in totals)
                {
                    float before = state.Stats.GetValue(pair.Key);
                    float limit = item.isExtreme ? config.extremeItemStatDeltaLimit : config.normalItemStatDeltaLimit;
                    float amount = Math.Max(-limit, Math.Min(limit, pair.Value));
                    state.Stats.ApplyDelta(pair.Key, amount, "item_first:" + item.itemId);
                    actualChanges[pair.Key] = state.Stats.GetValue(pair.Key) - before;
                    if (pair.Key != StatType.Rebellion) magnitude += Math.Abs(state.Stats.GetValue(pair.Key) - before);
                    log.AppendFormat(CultureInfo.InvariantCulture, ";{0}={1}->{2} ({3:+0.##;-0.##;0})",
                        pair.Key, before, state.Stats.GetValue(pair.Key), state.Stats.GetValue(pair.Key) - before);
                }
                PreferenceLearningTracker.LearnVisit(config, state, roomId, item, actualChanges, now);
            }
            if (apply && CoercionDetector.IsTrainingItem(item, config))
            {
                if (context.Coercion != CoercionKind.None)
                {
                    bool electric = context.Coercion == CoercionKind.ElectricStick;
                    ApplyTraining(now, item.itemId, "coerced_training",
                        electric ? config.coercionElectricStickStressDelta : config.coercionWheelchairStressDelta,
                        electric ? config.coercionElectricStickRebellionDelta : config.coercionWheelchairRebellionDelta);
                }
                if (training.RecordFirstVisit(item.itemId))
                    ApplyTraining(now, item.itemId, "repeated_training", config.repeatedTrainingStressDelta, config.repeatedTrainingRebellionDelta);
            }
            log.Append(";forced=").Append(context.IsForced).Append(";complying=").Append(context.WasComplying);
            bool autonomous=first && !context.IsForced && state.Relationship.CountRecentInterventions(now, config.autonomousVisitContactWindowSeconds) == 0;
            if (autonomous)
                state.Relationship.RecordAutonomousVisit();
            if (!first) state.Relationship.RecordItemRevisit(item.itemId);
            string[] lines = item.narrativeLines;
            int lineIndex = first || lines.Length < 2 ? 0 : 1 + Math.Max(0, count - 1) % (lines.Length - 1);
            string narrative = lines.Length == 0 ? item.displayName : lines[lineIndex];
            log.Append(";narrative=").Append(narrative);
            state.LogEvent(now, roomId, first ? "item_first" : "item_revisit", log.ToString(), new ItemExperience
            {
                itemId = item.itemId, displayName = item.displayName, tags = (string[])item.tags.Clone(),
                narrative = narrative, childLine = item.childReflectionLine, actualChangeMagnitude = magnitude, autonomous=autonomous
            });
            return new ItemVisitResult(first, apply, narrative);
        }
    }
}
