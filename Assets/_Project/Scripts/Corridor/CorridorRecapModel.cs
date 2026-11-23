using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;

namespace FruitFlyParenting.Corridor
{
    public sealed class RecapMemory
    {
        public ItemExperience Experience { get; internal set; }
        public bool WasPushedAway { get; internal set; }
    }
    public sealed class CorridorRecap
    {
        public RecapMemory[] Memories { get; internal set; }
        public int InterventionCount { get; internal set; }
        public bool HadInterruptedActivity { get; internal set; }
    }

    public static class CorridorRecapModel
    {
        public static CorridorRecap Build(IReadOnlyList<EventLogEntry> log, string roomId, int count)
        {
            int start = 0;
            for (int i = 0; i < log.Count; i++)
                if (log[i].roomId == roomId && log[i].eventType == "checkpoint" && log[i].payload == "room_enter") start = i;
            var events = log.Skip(start).Where(e => e.roomId == roomId).ToArray();
            var pushed = new HashSet<string>(events.Where(e => e.eventType == "item_pushed_away" || e.eventType == "item_avoided")
                .Select(e => e.payload));
            // 실제 0~100 clamp 후 변화량 순서. 동점이면 방문 순서이며 난수를 소비하지 않는다.
            var memories = events.Where(e => e.eventType == "item_first")
                .Select(e => e.experience == null || string.IsNullOrEmpty(e.experience.itemId) ? LegacyExperience(e.payload) : e.experience)
                .Where(e => e != null && !string.IsNullOrEmpty(e.itemId))
                .GroupBy(e => e.itemId).Select(group => group.First())
                .OrderByDescending(e => e.actualChangeMagnitude)
                .Take(Math.Max(0, Math.Min(3, count)))
                .Select(e => new RecapMemory { Experience = e, WasPushedAway = pushed.Contains(e.itemId) }).ToArray();
            return new CorridorRecap { Memories = memories, InterventionCount = events.Count(e => e.eventType == "contact"),
                HadInterruptedActivity = events.Any(e => e.eventType == "item_pushed_away") };
        }
        static ItemExperience LegacyExperience(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;
            string id = payload.Split(';')[0];
            const string marker = ";narrative=";
            int at = payload.IndexOf(marker, StringComparison.Ordinal);
            // 예전 저장에 없는 이름·수치·아이 대사를 만들어 내지 않는다.
            return new ItemExperience { itemId = id, displayName = id, narrative = at < 0 ? "" : payload.Substring(at + marker.Length) };
        }
    }
}
