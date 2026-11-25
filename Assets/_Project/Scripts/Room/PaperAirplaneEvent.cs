using System.Collections.Generic;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Room
{
    public enum PaperAirplaneOutcome { None, ApologizeAndRepair, PostponeApology, TalkTomorrow }

    // One snapshot at the scheduled time, with repair taking precedence. No stat changes.
    public sealed class PaperAirplaneEvent
    {
        readonly GameBalanceConfig config;
        public PaperAirplaneOutcome Outcome { get; private set; }
        public PaperAirplaneEvent(GameBalanceConfig config) { this.config = config; }
        public bool TryTrigger(float elapsed, IEnumerable<string> visited, float trust)
        {
            if (Outcome != PaperAirplaneOutcome.None || elapsed < config.roomDoorOpenSeconds * config.paperAirplaneTriggerFraction) return false;
            var ids = new HashSet<string>(visited);
            Outcome = ids.Contains("childhood_craft") || ids.Contains("childhood_birthday") ? PaperAirplaneOutcome.ApologizeAndRepair :
                ids.Contains("childhood_hideout") && trust < config.paperAirplaneLowTrustThreshold ? PaperAirplaneOutcome.PostponeApology : PaperAirplaneOutcome.TalkTomorrow;
            return true;
        }
        public static string SceneLine(PaperAirplaneOutcome outcome) => outcome switch
        {
            PaperAirplaneOutcome.ApologizeAndRepair => "앗, 찢어졌다! 미안해. 같이 고치자.",
            PaperAirplaneOutcome.PostponeApology => "일부러 그런 거 아니야… 지금은 말 안 할래.",
            PaperAirplaneOutcome.TalkTomorrow => "어떡하지… 내일 가서 미안하다고 해야겠다.",
            _ => "이거 친구가 접어 줬어!"
        };
        public static string CorridorLine(PaperAirplaneOutcome outcome, bool interrupted) =>
            (outcome switch {
                PaperAirplaneOutcome.ApologizeAndRepair => "테이프 붙였더니 또 날아가!",
                PaperAirplaneOutcome.PostponeApology => "아직 미안하다고 못 했어…",
                PaperAirplaneOutcome.TalkTomorrow => "내일은 꼭 친구한테 말할 거야.",
                _ => "친구랑 또 비행기 날리고 싶어."
            }) + (interrupted ? "\n아까는 더 놀고 싶었는데." : "\n끝까지 해 봐서 좋았어!");
    }
}
