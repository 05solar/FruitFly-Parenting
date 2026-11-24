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
        public float baseAttraction = 0f;
        public ItemStatDelta[] statDeltas = Array.Empty<ItemStatDelta>();
        public bool isExtreme;
        public float trustDelta;
        public bool recordsDelinquency;
        public YoungAdultFlags youngAdultFlags;
        [TextArea] public string[] narrativeLines = Array.Empty<string>();
        [TextArea] public string childReflectionLine;
        public void ValidateTags() => TagDefinitions.ValidateAndDistinct(tags);
        void OnValidate()
        {
            try { ValidateTags(); }
            catch (ArgumentException exception) { Debug.LogError(itemId + ": " + exception.Message, this); }
        }
    }
}
