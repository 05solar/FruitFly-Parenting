using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Tool;

namespace FruitFlyParenting.Room
{
    public sealed class YoungAdultItemEffects
    {
        readonly GameBalanceConfig config;
        readonly GameState state;
        readonly ItemEffectApplier effects;
        public YoungAdultItemEffects(GameBalanceConfig balance,GameState game)
        { config=balance; state=game; effects=new ItemEffectApplier(config,state,YoungAdultRoomController.RoomId); }
        public ItemVisitResult Visit(ItemDefinition item,bool first,bool effectsEnabled,float now,ItemVisitContext context=default)
        {
            using var impact=state.TrackImpact(now,YoungAdultRoomController.RoomId,item.itemId,item.displayName);
            var result=effects.Visit(item,first,effectsEnabled,now,context); string line=result.Narrative;
            // Recovery may happen on a later revisit, including when the letter/workshop was experienced before the rupture.
            if(effectsEnabled && item.itemId=="youngadult_workshop" && state.RestartYoungAdultWork(now))
                line="새 일 구했어. 여기서 다시 시작해 보려고.";
            if(item.itemId=="youngadult_letter")
            {
                bool autonomous=!context.IsForced && state.Relationship.CountRecentInterventions(now,config.autonomousVisitContactWindowSeconds)==0;
                bool reconnected=effectsEnabled && autonomous && state.ReconnectYoungAdult(now);
                line=LetterLine(state.Relationship.Trust,config);
                if(reconnected) line="오랜만이야. 연락 안 해서 미안해.";
                if(state.HasGamblingBankruptcy) line="잃은 돈이 너무 많아. 혼자 해결은 못 하겠어.";
                state.RecordYoungAdultLetter(line,now);
            }
            if(item.itemId=="youngadult_park" && state.HasGamblingBankruptcy) line="일단 숨 좀 돌리자. 내일 상담부터 받아야지.";
            return new ItemVisitResult(result.FirstVisit,result.EffectsApplied,line);
        }
        public static string LetterLine(float trust,GameBalanceConfig config) => trust<config.youngAdultResistanceTrustThreshold ?
            "아직은 혼자 있고 싶어. 편지로 얘기할게." : trust<config.youngAdultHighTrustThreshold ?
            "요즘 어떻게 지내? 시간 되면 한번 보자." : "내일 내가 할게. 끝나고 같이 밥 먹자.";
    }
}
