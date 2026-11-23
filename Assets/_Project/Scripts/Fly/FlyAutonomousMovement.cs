using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Fly
{
    public class FlyAutonomousMovement
    {
        readonly GameBalanceConfig config;
        readonly ItemChoiceModel choices;
        public ItemChoiceDecision LastDecision { get; private set; }
        public FlyAutonomousMovement(GameBalanceConfig config, IRandomSource random, GameState state = null)
        {
            this.config = config;
            choices = new ItemChoiceModel(config, state ?? new GameState(config, 0), random);
        }
        public bool IsFreeRoamPhase(float timeSinceRoomEnter) => timeSinceRoomEnter < config.freeRoamDurationSeconds;
        public bool ShouldForceRepick(float dwellSeconds) => dwellSeconds >= config.maxItemDwellSeconds;
        public string PickNextDestination(IReadOnlyList<ItemChoiceCandidate> candidates, string currentId, float elapsed, LifeStage stage)
        {
            LastDecision = choices.ChooseNextItem(candidates, currentId, stage, elapsed);
            return LastDecision.SelectedId;
        }
        // Definition-free room shells keep the same API and weighted exit tickets.
        public string PickNextDestination(IReadOnlyList<string> candidateIds, string currentId, float timeSinceRoomEnter)
        {
            if (candidateIds == null || candidateIds.Count == 0) throw new ArgumentException("No candidates.");
            var candidates = candidateIds.GroupBy(id => id)
                .Select(group => new ItemChoiceCandidate(group.Key, tickets: group.Count())).ToList();
            return PickNextDestination(candidates, currentId, timeSinceRoomEnter, LifeStage.Infant);
        }
    }
}
