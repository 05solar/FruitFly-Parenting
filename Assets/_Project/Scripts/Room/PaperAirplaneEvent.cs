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
            PaperAirplaneOutcome.ApologizeAndRepair => "친구의 종이비행기가 찢어졌어요.\n“미안해. 우리 같이 고쳐 보자.”",
            PaperAirplaneOutcome.PostponeApology => "찢어진 종이비행기 앞에서 고개를 돌려요.\n“지금은 혼자 있고 싶어. 사과는 조금 뒤에…”",
            PaperAirplaneOutcome.TalkTomorrow => "종이비행기를 조심히 내려놓아요.\n“내일 친구와 다시 이야기해 볼래.”",
            _ => "친구가 접어 준 종이비행기"
        };
        public static string CorridorLine(PaperAirplaneOutcome outcome, bool interrupted) =>
            (outcome switch {
                PaperAirplaneOutcome.ApologizeAndRepair => "접힌 자리에 테이프가 남았어요. 함께 고친 약속은 기억할래요.",
                PaperAirplaneOutcome.PostponeApology => "찢어진 날개를 아직 붙이지 못했어요. 미뤄 둔 말을 기억해요.",
                PaperAirplaneOutcome.TalkTomorrow => "종이비행기를 내일의 대화 곁에 두었어요.",
                _ => "종이비행기에 아직 적지 못한 이야기가 있어요."
            }) + (interrupted ? "\n“그때 하던 놀이를 멈춰야 했어요. 다음에는 끝까지 해 보고 싶어요.”" : "\n“내가 끝까지 해 볼 시간을 주었어요.”");
    }
}
