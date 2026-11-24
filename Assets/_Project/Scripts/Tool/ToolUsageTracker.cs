using System;
using System.Globalization;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;

namespace FruitFlyParenting.Tool
{
    // 두 접촉 소스가 공유한다. currentTime은 호출자가 전달하므로 가짜 시간으로 검증 가능하다.
    public sealed class ToolUsageTracker
    {
        readonly GameBalanceConfig config;
        readonly GameState state;
        float rewardCursor;
        float rewardedThisRoom;
        float rebellionReliefThisRoom;
        public FlyMoodState Mood { get; }
        public int RecentContactCount { get; private set; }
        public float LastBaseStressDelta { get; private set; }

        public ToolUsageTracker(GameBalanceConfig config, GameState state, float currentTime)
        {
            this.config = config;
            this.state = state;
            Mood = new FlyMoodState(config);
            rewardCursor = currentTime;
        }

        public void EnterRoom(string roomId, float currentTime)
        {
            state.CurrentRoomId = roomId;
            rewardCursor = currentTime;
            rewardedThisRoom = 0f;
            rebellionReliefThisRoom = 0f;
            Mood.ForceEndAvoidance();
        }

        public void Tick(float currentTime)
        {
            if (config.neglectRewardIntervalSeconds <= 0f) return;
            int intervals = (int)Math.Floor((currentTime - rewardCursor) / config.neglectRewardIntervalSeconds);
            if (intervals <= 0) return;
            using var impact=state.TrackImpact(currentTime,state.CurrentRoomId,"watching_only","곁에서 기다린 시간");
            rewardCursor += intervals * config.neglectRewardIntervalSeconds;
            float gain = Math.Min(intervals * config.neglectRewardTrustGain,
                Math.Max(0f, config.neglectRewardTrustCapPerRoom - rewardedThisRoom));
            float relief = Math.Min(intervals * Math.Max(0f, -config.watchingOnlyRebellionDelta),
                Math.Max(0f, config.watchingOnlyRebellionCapPerRoom - rebellionReliefThisRoom));
            if (relief > 0f)
            {
                state.Stats.ApplyDelta(StatType.Rebellion, -relief, "watching_only");
                rebellionReliefThisRoom += relief;
                state.LogEvent(currentTime, state.CurrentRoomId, "rebellion_recovery", "watching_only:" + (-relief).ToString(CultureInfo.InvariantCulture));
            }
            if (gain <= 0f) return;
            state.Relationship.AdjustTrust(gain, "left_alone");
            rewardedThisRoom += gain;
            state.LogEvent(currentTime, state.CurrentRoomId, "trust", "left_alone:" + gain.ToString(CultureInfo.InvariantCulture));
        }

        public void RecordContact(PushSource source, float currentTime)
        {
            Tick(currentTime); // 정확히 60초를 기다린 뒤 접촉한 경우 이미 얻은 보상을 보존한다.
            using var impact=state.TrackImpact(currentTime,state.CurrentRoomId,"contact",source==PushSource.ElectricStick ? "전기 봉 접촉" : "휠체어 접촉");
            var relationship = state.Relationship;
            float? previous = relationship.LastInterventionTime;
            if (previous.HasValue && currentTime - previous.Value < config.rapidRepeatThresholdSeconds)
            {
                relationship.AdjustTrust(config.rapidRepeatTrustPenalty, "rapid_repeat");
                state.LogEvent(currentTime, state.CurrentRoomId, "trust", "rapid_repeat:" + config.rapidRepeatTrustPenalty.ToString(CultureInfo.InvariantCulture));
            }
            rewardCursor = currentTime;
            relationship.PruneInterventionsOlderThan(currentTime, config.interventionWindowSeconds);
            relationship.RecordIntervention(currentTime); // 이번 접촉을 포함한 n
            RecentContactCount = relationship.CountRecentInterventions(currentTime, config.interventionWindowSeconds);
            LastBaseStressDelta = Math.Min(config.interventionStressDeltaCap, config.baseInterventionStressDelta +
                config.interventionWindowWeightCoefficient * Math.Max(0, RecentContactCount - config.interventionUnweightedCount));
            state.Stats.ApplyDelta(StatType.Stress, LastBaseStressDelta, "contact_base");
            // 07 강제 훈련 S+3과 별개인 접촉 자체의 +2. 07에서는 이 항을 다시 적용하지 않는다.
            if (source == PushSource.ElectricStick)
                state.Stats.ApplyDelta(StatType.Stress, config.electricStickImmediateStressDelta, "electric_stick_immediate");
            Mood.RecordContact(currentTime);
            state.LogEvent(currentTime, state.CurrentRoomId, "contact", string.Format(CultureInfo.InvariantCulture,
                "{0};n={1};baseStress={2};electricStress={3};avoiding={4}", source, RecentContactCount,
                LastBaseStressDelta, source == PushSource.ElectricStick ? config.electricStickImmediateStressDelta : 0f, Mood.IsAvoiding(currentTime)));
        }
    }
}
