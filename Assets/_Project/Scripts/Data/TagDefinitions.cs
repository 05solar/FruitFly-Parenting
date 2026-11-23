using System;
using System.Collections.Generic;

namespace FruitFlyParenting.Data
{
    // Keep serialized Korean strings compatible with existing items and saved recaps.
    public static class TagDefinitions
    {
        public const string Play = "놀이", Study = "공부", Rest = "휴식", Exercise = "운동",
            Relationship = "관계", Achievement = "성과", Consumption = "소비", Creation = "창작", Rebellion = "반항";
        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
        { Play, Study, Rest, Exercise, Relationship, Achievement, Consumption, Creation, Rebellion });
        public static bool IsValid(string tag)
        {
            foreach (var value in All) if (value == tag) return true;
            return false;
        }
        public static string[] ValidateAndDistinct(IEnumerable<string> tags)
        {
            var result = new List<string>();
            if (tags == null) return result.ToArray();
            foreach (var tag in tags)
            {
                if (!IsValid(tag)) throw new ArgumentException("Unknown item tag: " + tag);
                if (!result.Contains(tag)) result.Add(tag);
            }
            return result.ToArray();
        }
    }
}
