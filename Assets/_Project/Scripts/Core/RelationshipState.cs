using System;
using System.Collections.Generic;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Core
{
    [Serializable]
    public struct ItemRevisitEntry
    {
        public string itemId;
        public int count;
    }

    // 부모-아이 신뢰 T와 그 외 비공개 행동 로그(스펙 4.1)를 관리한다.
    // Trust는 AdjustTrust를 통해서만 바뀐다. 나머지 카운터(개입 횟수, 자율 방문 수,
    // 물품별 재방문 수, 최근 개입 타임스탬프)는 0~100 스케일 "수치"가 아니라 로그성 데이터이므로
    // RecordXxx 메서드들로 직접 관리한다 — 이들은 StatSystem.ApplyDelta와 같은 제약 대상이 아니다.
    public class RelationshipState
    {
        public event Action<float, float, string> OnTrustChanged;

        public float Trust { get; private set; }
        public int TotalInterventionCount { get; private set; }
        public int AutonomousVisitCount { get; private set; }
        public float? LastInterventionTime => recentInterventionTimestamps.Count == 0
            ? (float?)null : recentInterventionTimestamps[recentInterventionTimestamps.Count - 1];

        readonly List<float> recentInterventionTimestamps = new List<float>();
        readonly Dictionary<string, int> itemRevisitCounts = new Dictionary<string, int>();

        readonly float min;
        readonly float max;

        public RelationshipState(GameBalanceConfig config)
        {
            min = config.trustMin;
            max = config.trustMax;
            Trust = Clamp(config.trustDefaultValue);
        }

        public void AdjustTrust(float amount, string reasonTag)
        {
            float oldValue = Trust;
            Trust = Clamp(oldValue + amount);
            OnTrustChanged?.Invoke(oldValue, Trust, reasonTag);
        }

        // currentTime은 호출자가 전달한다(실시간 Time.time에 의존하지 않음).
        // 이는 시뮬레이션(16번 프롬프트)에서 가짜 시간을 빠르게 흘려보내기 위한 설계다.
        public void RecordIntervention(float currentTime)
        {
            recentInterventionTimestamps.Add(currentTime);
            TotalInterventionCount++;
        }

        public int CountRecentInterventions(float currentTime, float windowSeconds)
        {
            int count = 0;
            for (int i = 0; i < recentInterventionTimestamps.Count; i++)
            {
                if (currentTime - recentInterventionTimestamps[i] <= windowSeconds)
                {
                    count++;
                }
            }
            return count;
        }

        // 메모리 누수를 막기 위해 호출자(04번 ToolUsageTracker 등)가 주기적으로 오래된 기록을 정리할 수 있다.
        public void PruneInterventionsOlderThan(float currentTime, float windowSeconds)
        {
            recentInterventionTimestamps.RemoveAll(t => currentTime - t > windowSeconds);
        }

        public void RecordAutonomousVisit() => AutonomousVisitCount++;

        public void RecordItemRevisit(string itemId)
        {
            itemRevisitCounts.TryGetValue(itemId, out int count);
            itemRevisitCounts[itemId] = count + 1;
        }

        public int GetItemRevisitCount(string itemId) =>
            itemRevisitCounts.TryGetValue(itemId, out int count) ? count : 0;

        public IReadOnlyDictionary<string, int> ItemRevisitCounts => itemRevisitCounts;

        // 저장/불러오기 전용 복원 경로(SaveSystem에서만 사용). 게임플레이 중에는 RecordXxx를 사용한다.
        public void RestoreCounters(int totalInterventionCount, int autonomousVisitCount, IEnumerable<ItemRevisitEntry> itemRevisits)
        {
            TotalInterventionCount = totalInterventionCount;
            AutonomousVisitCount = autonomousVisitCount;
            itemRevisitCounts.Clear();
            if (itemRevisits == null) return;
            foreach (var entry in itemRevisits)
            {
                itemRevisitCounts[entry.itemId] = entry.count;
            }
        }

        float Clamp(float value) => Math.Min(max, Math.Max(min, value));
    }
}
