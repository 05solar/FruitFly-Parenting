using System;
using System.Collections.Generic;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Core
{
    [Serializable]
    public struct EventLogEntry
    {
        public float timestamp;
        public string roomId; // 전역 이벤트는 빈 문자열을 쓴다.
        public string eventType;
        public string payload;
        public ItemExperience experience;
    }

    // JsonUtility 직렬화 전용 DTO. GameState 자체는 System.Random 등 직렬화 불가능한 멤버를 들고 있어
    // 저장할 때는 이 평탄화된 형태로 변환한다(SaveSystem에서 사용).
    [Serializable]
    public class GameStateSaveData
    {
        public bool adolescenceExitCaptured;
        public float adolescenceExitRebellion;
        public bool flightSmokeEventTriggered;
        public int runSeed;
        public string currentRoomId;
        public StatType[] statTypes;
        public float[] statValues;
        public float trust;
        public int totalInterventionCount;
        public int autonomousVisitCount;
        public List<ItemRevisitEntry> itemRevisits = new List<ItemRevisitEntry>();
        public List<EventLogEntry> eventLog = new List<EventLogEntry>();
    }

    // 회차 전체에 걸친 상태를 들고 있는 순수 C# 클래스.
    // Unity 생명주기가 필요 없으므로 MonoBehaviour를 상속하지 않는다.
    // 씬에서 접근해야 할 때는 GameStateBootstrap(MonoBehaviour 어댑터)을 통해 GameState.Current를 사용한다.
    public class GameState
    {
        public static GameState Current { get; private set; }

        public static void SetCurrent(GameState state) => Current = state;

        readonly GameBalanceConfig config;
        public bool IsHighRebellionDialogue => Stats.GetValue(StatType.Rebellion) >= config.highRebellionDialogueThreshold;
        public bool IsFlightSmokeEventEligible => Stats.GetValue(StatType.Rebellion) >= config.flightSmokeRebellionThreshold;
        public bool IsFlightSmokeEventTriggered { get; private set; }
        public bool AdolescenceExitCaptured { get; private set; }
        public float AdolescenceExitRebellion { get; private set; }
        // Step 10 calls this once when the adolescence door opens. Later R changes cannot alter it.
        public void CaptureAdolescenceExit(float now)
        {
            if (AdolescenceExitCaptured) return;
            AdolescenceExitCaptured = true;
            AdolescenceExitRebellion = Stats.GetValue(StatType.Rebellion);
            IsFlightSmokeEventTriggered = IsFlightSmokeEventEligible;
            LogEvent(now, CurrentRoomId, "adolescence_exit_rebellion",
                AdolescenceExitRebellion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        public StatSystem Stats { get; }
        public RelationshipState Relationship { get; }
        public string CurrentRoomId { get; set; } = string.Empty;
        public int RunSeed { get; }
        public System.Random Random { get; }

        readonly List<EventLogEntry> eventLog = new List<EventLogEntry>();
        public IReadOnlyList<EventLogEntry> EventLog => eventLog;

        public GameState(GameBalanceConfig config, int runSeed)
        {
            this.config = config;
            RunSeed = runSeed;
            Random = new System.Random(runSeed);
            Stats = new StatSystem(config);
            Relationship = new RelationshipState(config);
        }

        public void LogEvent(float timestamp, string roomId, string eventType, string payload, ItemExperience experience = null)
        {
            eventLog.Add(new EventLogEntry
            {
                timestamp = timestamp,
                roomId = roomId ?? string.Empty,
                eventType = eventType,
                payload = payload,
                experience = experience,
            });
        }

        // SaveSystem이 직렬화할 평탄화된 스냅샷을 만든다.
        public GameStateSaveData ToSaveData()
        {
            var statTypes = (StatType[])Enum.GetValues(typeof(StatType));
            var statValues = new float[statTypes.Length];
            for (int i = 0; i < statTypes.Length; i++)
            {
                statValues[i] = Stats.GetValue(statTypes[i]);
            }

            var data = new GameStateSaveData
            {
                adolescenceExitCaptured = AdolescenceExitCaptured,
                adolescenceExitRebellion = AdolescenceExitRebellion,
                flightSmokeEventTriggered = IsFlightSmokeEventTriggered,
                runSeed = RunSeed,
                currentRoomId = CurrentRoomId,
                statTypes = statTypes,
                statValues = statValues,
                trust = Relationship.Trust,
                totalInterventionCount = Relationship.TotalInterventionCount,
                autonomousVisitCount = Relationship.AutonomousVisitCount,
            };

            foreach (var kvp in Relationship.ItemRevisitCounts)
            {
                data.itemRevisits.Add(new ItemRevisitEntry { itemId = kvp.Key, count = kvp.Value });
            }

            data.eventLog.AddRange(eventLog);

            return data;
        }

        // 저장된 스냅샷으로부터 GameState를 복원한다.
        // 복원도 StatSystem.ApplyDelta / RelationshipState.AdjustTrust만을 통해 값을 설정한다 —
        // "모든 수치 변경은 이 두 메서드를 통과한다"는 규칙에 예외를 두지 않기 위함이다.
        public static GameState FromSaveData(GameBalanceConfig config, GameStateSaveData data)
        {
            var state = new GameState(config, data.runSeed)
            {
                CurrentRoomId = data.currentRoomId,
                AdolescenceExitCaptured = data.adolescenceExitCaptured,
                AdolescenceExitRebellion = data.adolescenceExitRebellion,
                IsFlightSmokeEventTriggered = data.flightSmokeEventTriggered,
            };

            for (int i = 0; i < data.statTypes.Length; i++)
            {
                StatType type = data.statTypes[i];
                float target = data.statValues[i];
                float current = state.Stats.GetValue(type);
                state.Stats.ApplyDelta(type, target - current, "save_load_restore");
            }

            float trustDelta = data.trust - state.Relationship.Trust;
            state.Relationship.AdjustTrust(trustDelta, "save_load_restore");
            state.Relationship.RestoreCounters(data.totalInterventionCount, data.autonomousVisitCount, data.itemRevisits);

            state.eventLog.AddRange(data.eventLog);

            return state;
        }
    }
}
