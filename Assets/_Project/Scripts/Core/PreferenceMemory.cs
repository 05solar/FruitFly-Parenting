using System;
using System.Collections.Generic;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Core
{
    [Serializable]
    public struct TagMemoryEntry
    {
        public string tag;
        public float preference, avoidance;
    }

    // Run-scoped memory. No item IDs, transforms, scene lifecycles or random draws.
    public sealed class PreferenceMemory
    {
        readonly GameBalanceConfig config;
        readonly Dictionary<string, float> preferences = new Dictionary<string, float>();
        readonly Dictionary<string, float> avoidance = new Dictionary<string, float>();
        public PreferenceMemory(GameBalanceConfig config)
        {
            this.config = config;
            foreach (var tag in TagDefinitions.All) { preferences.Add(tag, 0); avoidance.Add(tag, 0); }
        }
        public float GetPreference(string tag) => preferences[tag];
        public float GetAvoidance(string tag) => avoidance[tag];
        public void LearnPreference(IEnumerable<string> tags, float delta)
        {
            RequireFinite(delta);
            foreach (var tag in TagDefinitions.ValidateAndDistinct(tags))
                preferences[tag] = Math.Clamp(preferences[tag] + delta, -config.preferenceMemoryLimit, config.preferenceMemoryLimit);
        }
        public void LearnAvoidance(IEnumerable<string> tags, float delta)
        {
            RequireFinite(delta);
            foreach (var tag in TagDefinitions.ValidateAndDistinct(tags))
                avoidance[tag] = Math.Clamp(avoidance[tag] + delta, 0f, config.avoidanceMemoryLimit);
        }
        public List<TagMemoryEntry> Snapshot()
        {
            var entries = new List<TagMemoryEntry>();
            foreach (var tag in TagDefinitions.All)
                entries.Add(new TagMemoryEntry { tag = tag, preference = preferences[tag], avoidance = avoidance[tag] });
            return entries;
        }
        public void Restore(IEnumerable<TagMemoryEntry> entries)
        {
            foreach (var tag in TagDefinitions.All) { preferences[tag] = 0; avoidance[tag] = 0; }
            if (entries == null) return; // Pre-08 saves have no memory.
            foreach (var entry in entries)
            {
                if (!TagDefinitions.IsValid(entry.tag)) continue;
                RequireFinite(entry.preference); RequireFinite(entry.avoidance);
                preferences[entry.tag] = Math.Clamp(entry.preference, -config.preferenceMemoryLimit, config.preferenceMemoryLimit);
                avoidance[entry.tag] = Math.Clamp(entry.avoidance, 0f, config.avoidanceMemoryLimit);
            }
        }
        static void RequireFinite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Memory must be finite."); }
    }
}
