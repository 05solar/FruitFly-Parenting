using System;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Room
{
    // 게임 시간만 사용한다. VR 페이드의 실제 시간과 분리한다.
    public sealed class RoomTimeline
    {
        readonly GameBalanceConfig config;
        public float Elapsed { get; private set; }
        public RoomTimeline(GameBalanceConfig config) { this.config = config; }
        public void Advance(float seconds) { Elapsed += Math.Max(0f, seconds); }
        public float ToolCooldownRemaining => Math.Max(0f, config.toolCooldownOnRoomEnterSeconds - Elapsed);
        public bool ToolsEnabled => ToolCooldownRemaining <= 0f;
        public bool DoorOpen => Elapsed >= Math.Max(config.roomEarliestExitSeconds, config.roomDoorOpenSeconds);
        public bool EffectsEnabled => Elapsed < config.roomEffectsStopSeconds;
        public bool ExitOnly => !EffectsEnabled;
        public float DayProgress => Math.Min(1f, Elapsed / Math.Max(1f, config.roomDoorOpenSeconds));
    }
}
