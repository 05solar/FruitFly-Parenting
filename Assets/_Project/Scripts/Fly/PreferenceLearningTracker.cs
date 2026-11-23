using System;
using System.Collections.Generic;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Fly
{
    [Serializable]
    public sealed class LearningExperience
    {
        public string originalItemId, outcomeItemId, outcome;
        public string[] tags;
        public Vector3 approachDirection;
        public int interventionCount;
        public float stress, utility, preferenceDelta, avoidanceDelta;
    }

    // Contacts propose an episode; only confirmed movement outcomes teach avoidance.
    public sealed class PreferenceLearningTracker
    {
        readonly GameBalanceConfig config;
        readonly GameState state;
        readonly List<(float time, float weight)> contacts = new List<(float, float)>();
        string roomId, originalId;
        string[] tags;
        Vector3 approachDirection;
        float stress;
        public PreferenceLearningTracker(GameBalanceConfig config, GameState state)
        { this.config = config; this.state = state; }
        public void Reset() { contacts.Clear(); originalId = null; }
        public void RecordContact(string room, ItemDefinition intendedItem, Vector3 direction, PushSource source, float now, float currentStress)
        {
            if (intendedItem == null) { Reset(); return; }
            if (originalId != intendedItem.itemId || roomId != room) Reset();
            originalId = intendedItem.itemId; roomId = room;
            tags = TagDefinitions.ValidateAndDistinct(intendedItem.tags);
            approachDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            stress = currentStress;
            Prune(now);
            contacts.Add((now, source == PushSource.ElectricStick ? config.avoidanceElectricLearningGain : config.avoidanceWheelchairLearningGain));
        }
        void Prune(float now) => contacts.RemoveAll(c => now - c.time > config.learningInterventionWindowSeconds);
        public bool ConfirmArrival(string room, string arrivedId, float now)
        {
            if (originalId == null) return false;
            if (arrivedId == originalId || room != roomId) { Reset(); return false; }
            return Learn(now, arrivedId, "redirected_arrival");
        }
        public bool ConfirmPushedAway(string room, string departedId, float now)
        {
            if (room != roomId || originalId != departedId) return false;
            return Learn(now, string.Empty, "pushed_away");
        }
        bool Learn(float now, string arrivedId, string outcome)
        {
            Prune(now);
            float gain = 0f;
            foreach (var contact in contacts) if (contact.time <= now) gain += contact.weight;
            if (gain <= 0f || tags.Length == 0) { Reset(); return false; }
            gain = Math.Min(config.avoidanceEpisodeGainLimit, gain);
            state.Preferences.LearnAvoidance(tags, gain);
            var experience = new LearningExperience
            {
                originalItemId = originalId, outcomeItemId = arrivedId, tags = (string[])tags.Clone(),
                approachDirection = approachDirection, interventionCount = contacts.Count,
                stress = stress, outcome = outcome, avoidanceDelta = gain
            };
            state.LogLearning(now, roomId, "learning_avoidance", experience);
            // The existing contact tracker owns trust costs; learning never charges them twice.
            Reset();
            return true;
        }

        public static void LearnVisit(GameBalanceConfig config, GameState state, string room, ItemDefinition item,
            IReadOnlyDictionary<StatType, float> actualChanges, float now)
        {
            var validTags = TagDefinitions.ValidateAndDistinct(item.tags);
            double utility = 0;
            foreach (var change in actualChanges)
            {
                if (change.Key == StatType.Rebellion) continue;
                utility += change.Value * (change.Key == StatType.Stress ? -config.preferenceStressWeight : config.preferenceGrowthWeight);
            }
            float delta = Math.Clamp((float)utility * config.preferenceLearningRate,
                -config.preferenceVisitGainLimit, config.preferenceVisitGainLimit);
            if (validTags.Length == 0 || delta == 0f) return;
            state.Preferences.LearnPreference(validTags, delta);
            state.LogLearning(now, room, "learning_preference", new LearningExperience
            {
                originalItemId = item.itemId, outcomeItemId = item.itemId, tags = validTags,
                stress = state.Stats.GetValue(StatType.Stress), utility = (float)utility, preferenceDelta = delta,
                outcome = delta > 0 ? "enjoyment" : "fatigue"
            });
        }
    }
}
