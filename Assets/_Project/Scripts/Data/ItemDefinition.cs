using System;
using FruitFlyParenting.Core;
using UnityEngine;

namespace FruitFlyParenting.Data
{
    [Serializable]
    public struct ItemStatDelta
    {
        public StatType stat;
        public float amount;
        public ItemStatDelta(StatType stat, float amount) { this.stat = stat; this.amount = amount; }
    }

    [CreateAssetMenu(fileName = "Item", menuName = "FruitFlyParenting/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        public string[] tags = Array.Empty<string>();
        public ItemStatDelta[] statDeltas = Array.Empty<ItemStatDelta>();
        public bool isExtreme;
        [TextArea] public string[] narrativeLines = Array.Empty<string>();
        [TextArea] public string childReflectionLine;
    }
}
