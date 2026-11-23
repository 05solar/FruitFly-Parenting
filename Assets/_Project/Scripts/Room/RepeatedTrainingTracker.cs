using System.Collections.Generic;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Room
{
    public sealed class RepeatedTrainingTracker
    {
        readonly GameBalanceConfig config;
        readonly HashSet<string> visited = new HashSet<string>();
        public int UniqueTrainingVisits => visited.Count;
        public int Applications { get; private set; }
        public RepeatedTrainingTracker(GameBalanceConfig config) { this.config = config; }
        public bool RecordFirstVisit(string itemId)
        {
            if (!visited.Add(itemId) || visited.Count < config.repeatedTrainingStartVisitCount ||
                Applications >= config.repeatedTrainingMaxApplications) return false;
            Applications++;
            return true;
        }
        public void Reset() { visited.Clear(); Applications = 0; }
    }
}
