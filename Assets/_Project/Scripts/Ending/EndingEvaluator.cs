using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;

namespace FruitFlyParenting.Ending
{
    public enum EndingCondition { Bankruptcy=1,MagicPromise,Gang,GreatSuccess,Success,Debt,EmptyBirthday,FamousOutside,Kangaroo,Team,SmallTable,OwnCity,LongRest,OpenDoor }
    public sealed class EndingEvaluationContext
    {
        public YoungAdultSnapshot Snapshot { get; }
        public bool Successful=>(Snapshot.Intelligence>=75 || Snapshot.Popularity>=80 || Snapshot.Health>=80) && Snapshot.AchievementVisited;
        public bool Stable=>Snapshot.Stress<70 && Snapshot.Health>=50;
        public bool GoodRelationship=>Snapshot.Trust>=60 && Snapshot.Love>=60;
        public bool Letter=>Snapshot.VisitedItemIds.Contains("youngadult_letter");
        public EndingEvaluationContext(YoungAdultSnapshot snapshot)
        { if(snapshot==null || !snapshot.IsCaptured) throw new ArgumentException("A captured young-adult snapshot is required."); Snapshot=snapshot; }
    }
    public static class EndingEvaluator
    {
        public static bool Matches(EndingCondition condition,EndingEvaluationContext c)
        {
            var s=c.Snapshot;
            return condition switch
            {
                EndingCondition.Bankruptcy=>s.GamblingBankruptcy,
                EndingCondition.MagicPromise=>s.Love==100 && s.Trust>=70 && s.AutonomousRelationshipVisits>=3 && s.VisitedItemIds.Contains("youngadult_dinner"),
                EndingCondition.Gang=>s.Rebellion>=75 && s.Popularity>=60 && s.Trust<=45 && s.DelinquencyRecord && (s.ContactBlocked || s.Resigned),
                EndingCondition.GreatSuccess=>c.Successful && c.Stable && c.GoodRelationship && c.Letter,
                EndingCondition.Success=>c.Stable && c.GoodRelationship && s.RestMealVisits>=2,
                EndingCondition.Debt=>s.HighRiskContract && s.Stress>=70 && s.Trust<60,
                EndingCondition.EmptyBirthday=>s.Trust<25 && !c.Letter && (s.ContactBlocked || s.Interventions>=25),
                EndingCondition.FamousOutside=>c.Successful && s.Trust<40,
                EndingCondition.Kangaroo=>!c.Successful && s.Health<55 && !s.VisitedItemIds.Contains("youngadult_workshop"),
                EndingCondition.Team=>s.Health>=75 && s.StadiumVisited && s.Trust>=45,
                EndingCondition.SmallTable=>!c.Successful && c.GoodRelationship,
                EndingCondition.OwnCity=>s.Intelligence>=65 && s.CreativeVisits>=2 && s.Trust>=40 && s.Trust<=59,
                EndingCondition.LongRest=>s.Stress>=80 && s.Trust>=40,
                EndingCondition.OpenDoor=>true,
                _=>false
            };
        }
        public static EndingDefinition Evaluate(YoungAdultSnapshot snapshot,IEnumerable<EndingDefinition> definitions)
        {
            var c=new EndingEvaluationContext(snapshot); var ordered=definitions.OrderBy(d=>d.priority).ToArray();
            if(ordered.Length!=14 || ordered.Select(d=>d.condition).Distinct().Count()!=14 || ordered.Any(d=>d.priority!=(int)d.condition))
                throw new ArgumentException("The fourteen ending definitions must follow spec priority 1–14.");
            return ordered.First(d=>Matches(d.condition,c));
        }
        public static ExperienceImpact[] DecisiveExperiences(IReadOnlyList<EventLogEntry> log)
            =>log.Where(e=>e.eventType=="experience_impact" && e.impact!=null && e.impact.Magnitude>0)
                .OrderByDescending(e=>e.impact.Magnitude).Take(3).Select(e=>e.impact.Copy()).ToArray();
    }
}
