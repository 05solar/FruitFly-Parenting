using System;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Core
{
    [Serializable] public sealed class ExperienceImpact
    {
        public string label;
        public float stress,rebellion,trust;
        public float Magnitude=>Math.Abs(stress)+Math.Abs(rebellion)+Math.Abs(trust);
        public ExperienceImpact Copy()=>(ExperienceImpact)MemberwiseClone();
    }
    // A logical visit/contact is one experience, even if it changes several stats or calls nested effects.
    internal sealed class ImpactScope : IDisposable
    {
        readonly GameState state;
        readonly float now,s,r,t;
        readonly string room,reason,label;
        readonly bool owner;
        bool disposed;
        public ImpactScope(GameState state,float now,string room,string reason,string label)
        {
            this.state=state; this.now=now; this.room=room; this.reason=reason; this.label=label;
            owner=state.ImpactDepth++==0; s=state.Stats.GetValue(StatType.Stress); r=state.Stats.GetValue(StatType.Rebellion); t=state.Relationship.Trust;
        }
        public void Dispose()
        {
            if(disposed) return; disposed=true; state.ImpactDepth--;
            if(!owner) return;
            var impact=new ExperienceImpact { label=label,stress=state.Stats.GetValue(StatType.Stress)-s,
                rebellion=state.Stats.GetValue(StatType.Rebellion)-r,trust=state.Relationship.Trust-t };
            if(impact.Magnitude>0) state.RecordImpact(now,room,reason,impact);
        }
    }
}
