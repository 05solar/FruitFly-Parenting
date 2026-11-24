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
                line="동네 작은 일터에서 새 일을 시작해요. 떠난 뒤에도 다시 고를 수 있어요.";
            if(item.itemId=="youngadult_letter")
            {
                bool autonomous=!context.IsForced && state.Relationship.CountRecentInterventions(now,config.autonomousVisitContactWindowSeconds)==0;
                bool reconnected=effectsEnabled && autonomous && state.ReconnectYoungAdult(now);
                line=LetterLine(state.Relationship.Trust,config);
                if(reconnected) line="다시 연락을 시작해요. "+line;
                if(state.HasGamblingBankruptcy) line+=" 잃은 생활은 남아 있지만 다음 이야기는 이어 가고 싶어요.";
                state.RecordYoungAdultLetter(line,now);
            }
            if(item.itemId=="youngadult_park" && state.HasGamblingBankruptcy) line="손실은 사라지지 않지만 잠시 쉬며 다음 하루를 생각해요.";
            return new ItemVisitResult(result.FirstVisit,result.EffectsApplied,line);
        }
        public static string LetterLine(float trust,GameBalanceConfig config) => trust<config.youngAdultResistanceTrustThreshold ?
            "“아직 거리가 필요해요. 내 이야기를 여기 적어 둘게요.”" : trust<config.youngAdultHighTrustThreshold ?
            "“서로 모르는 이야기가 많네요. 조금씩 다시 말해 볼까요.”" : "“내일은 내가 해 볼게요. 돌아오면 함께 이야기해요.”";
    }
}
