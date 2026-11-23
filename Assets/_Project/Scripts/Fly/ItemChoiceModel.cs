using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Fly
{
    public sealed class ItemChoiceCandidate
    {
        public string Id { get; }
        public string[] Tags { get; }
        public float BaseAttraction { get; }
        public bool Reachable { get; }
        public int Tickets { get; }
        public ItemChoiceCandidate(string id, string[] tags = null, float attraction = 0, bool reachable = true, int tickets = 1)
        {
            if (string.IsNullOrEmpty(id) || float.IsNaN(attraction) || float.IsInfinity(attraction) || tickets < 1)
                throw new ArgumentException("Invalid choice candidate.");
            Id = id; Tags = TagDefinitions.ValidateAndDistinct(tags); BaseAttraction = attraction; Reachable = reachable; Tickets = tickets;
        }
    }
    public sealed class ItemChoiceScore
    {
        public string Id;
        public double BaseAttraction, Preference, Avoidance, Need, Noise, SoftmaxProbability, Probability;
        public int Tickets;
        public double Score => BaseAttraction + Preference - Avoidance + Need + Noise;
    }
    public sealed class ItemChoiceDecision
    {
        public string SelectedId;
        public float ExplorationFloor;
        public bool UniformPhase;
        public IReadOnlyList<ItemChoiceScore> Candidates;
    }

    public sealed class ItemChoiceModel
    {
        readonly GameBalanceConfig config;
        readonly GameState state;
        readonly IRandomSource random;
        public ItemChoiceModel(GameBalanceConfig config, GameState state, IRandomSource random)
        { this.config = config; this.state = state; this.random = random; }
        public float ExplorationFloor(LifeStage stage)
        {
            float stageFloor = stage switch
            {
                LifeStage.Infant => config.infantExplorationFloor,
                LifeStage.Childhood => config.childhoodExplorationFloor,
                LifeStage.Adolescence => config.adolescenceExplorationFloor,
                LifeStage.YoungAdult => config.youngAdultExplorationFloor,
                _ => config.randomExplorationFloor
            };
            return Math.Clamp(Math.Max(config.randomExplorationFloor, stageFloor), 0f, 1f);
        }
        double Draw() => random.NextInt(int.MaxValue) / (double)int.MaxValue;
        public ItemChoiceDecision Evaluate(IReadOnlyList<ItemChoiceCandidate> candidates, string currentId,
            LifeStage stage, float elapsed, bool addNoise = false)
        {
            var available = candidates?.Where(c => c.Reachable).ToList() ?? new List<ItemChoiceCandidate>();
            var filtered = available.Where(c => c.Id != currentId).ToList();
            if (filtered.Count > 0) available = filtered; // Only-one-candidate exception from 03.
            if (available.Count == 0) throw new ArgumentException("No reachable candidates.");
            if (available.Select(c => c.Id).Distinct().Count() != available.Count) throw new ArgumentException("Duplicate candidate IDs.");
            bool uniform = stage == LifeStage.Infant && elapsed < config.freeRoamDurationSeconds;
            float floor = uniform ? 1f : ExplorationFloor(stage);
            var scores = new List<ItemChoiceScore>();
            foreach (var candidate in available)
            {
                double preference = 0, avoidance = 0, need = 0;
                foreach (var tag in candidate.Tags)
                {
                    preference += state.Preferences.GetPreference(tag); avoidance += state.Preferences.GetAvoidance(tag);
                    if (tag == TagDefinitions.Rest)
                        need += Math.Clamp((state.Stats.GetValue(StatType.Stress) - config.statDefaultValue) /
                            Math.Max(1f, config.statMax - config.statDefaultValue), 0f, 1f) * config.restStressNeedWeight;
                    if (tag == TagDefinitions.Exercise)
                        need += Math.Clamp((config.statDefaultValue - state.Stats.GetValue(StatType.Health)) /
                            Math.Max(1f, config.statDefaultValue - config.statMin), 0f, 1f) * config.exerciseHealthNeedWeight;
                }
                int count = Math.Max(1, candidate.Tags.Length);
                scores.Add(new ItemChoiceScore { Id = candidate.Id, BaseAttraction = candidate.BaseAttraction,
                    Preference = preference / count, Avoidance = avoidance / count, Need = need,
                    Noise = !uniform && addNoise ? (Draw() * 2 - 1) * config.choiceNoiseAmplitude : 0,
                    Tickets = candidate.Tickets });
            }
            double max = scores.Max(s => s.Score);
            double denominator = scores.Sum(s => Math.Exp(s.Score - max) * s.Tickets);
            double tickets = scores.Sum(s => (double)s.Tickets);
            foreach (var score in scores)
            {
                score.SoftmaxProbability = Math.Exp(score.Score - max) * score.Tickets / denominator;
                score.Probability = (1 - floor) * score.SoftmaxProbability + floor * score.Tickets / tickets;
            }
            return new ItemChoiceDecision { Candidates = scores.AsReadOnly(), ExplorationFloor = floor, UniformPhase = uniform };
        }
        public ItemChoiceDecision ChooseNextItem(IReadOnlyList<ItemChoiceCandidate> candidates, string currentId, LifeStage stage, float elapsed)
        {
            var decision = Evaluate(candidates, currentId, stage, elapsed, true);
            double roll = Draw(), cumulative = 0;
            decision.SelectedId = decision.Candidates[decision.Candidates.Count - 1].Id;
            foreach (var candidate in decision.Candidates)
            {
                cumulative += candidate.Probability;
                if (roll < cumulative) { decision.SelectedId = candidate.Id; break; }
            }
            return decision;
        }
    }
}
