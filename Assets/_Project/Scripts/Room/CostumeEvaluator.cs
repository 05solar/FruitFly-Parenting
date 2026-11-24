namespace FruitFlyParenting.Room
{
    public static class CostumeEvaluator
    {
        // Presentation only: no statistics, random draws, candidate filtering or ending flags.
        public static bool ShouldWearHiddenCostume(float infantEndS, float childhoodEndS, float bothThreshold=70, float averageThreshold=75)
            => (infantEndS>=bothThreshold && childhoodEndS>=bothThreshold) || (infantEndS+childhoodEndS)*.5f>=averageThreshold;
    }
}
