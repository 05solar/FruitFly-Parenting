using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using UnityEngine;

namespace FruitFlyParenting.Core
{
    [Serializable]
    public sealed class YoungAdultSnapshot
    {
        // Unity inline serialization materializes a missing/null class as an empty object.
        // Keep a discriminator so pre-exit and older checkpoints cannot invent a final result.
        [SerializeField] bool captured;
        public bool IsCaptured=>captured;
        [SerializeField] float stress,intelligence,health,popularity,love,rebellion,trust,capturedAt;
        [SerializeField] bool gamblingBankruptcy,highRiskContract,contactBlocked,resigned,delinquencyRecord,achievementVisited;
        [SerializeField] bool adolescenceFlightSmoke;
        [SerializeField] float adolescenceExitRebellion;
        [SerializeField] int interventions,autonomousVisits;
        [SerializeField] string lastLetter;
        [SerializeField] string[] visitedItemIds;
        public float Stress=>stress; public float Intelligence=>intelligence; public float Health=>health;
        public float Popularity=>popularity; public float Love=>love; public float Rebellion=>rebellion; public float Trust=>trust;
        public float CapturedAt=>capturedAt;
        public bool GamblingBankruptcy=>gamblingBankruptcy; public bool HighRiskContract=>highRiskContract;
        public bool ContactBlocked=>contactBlocked; public bool Resigned=>resigned; public bool DelinquencyRecord=>delinquencyRecord;
        public bool AchievementVisited=>achievementVisited;
        public bool AdolescenceFlightSmoke=>adolescenceFlightSmoke; public float AdolescenceExitRebellion=>adolescenceExitRebellion;
        public int Interventions=>interventions; public int AutonomousVisits=>autonomousVisits; public string LastLetter=>lastLetter;
        public IReadOnlyList<string> VisitedItemIds=>Array.AsReadOnly(visitedItemIds??Array.Empty<string>());
        public YoungAdultSnapshot(GameState state,float now)
        {
            captured=true;
            stress=state.Stats.GetValue(StatType.Stress); intelligence=state.Stats.GetValue(StatType.Intelligence);
            health=state.Stats.GetValue(StatType.Health); popularity=state.Stats.GetValue(StatType.Popularity);
            love=state.Stats.GetValue(StatType.Love); rebellion=state.Stats.GetValue(StatType.Rebellion); trust=state.Relationship.Trust;
            capturedAt=now; gamblingBankruptcy=state.HasGamblingBankruptcy; highRiskContract=state.HasHighRiskContract;
            contactBlocked=state.HasContactBlocked; resigned=state.HasResigned; delinquencyRecord=state.HasDelinquencyRecord;
            lastLetter=state.LastYoungAdultLetter; interventions=state.Relationship.TotalInterventionCount; autonomousVisits=state.Relationship.AutonomousVisitCount;
            adolescenceFlightSmoke=state.IsFlightSmokeEventTriggered; adolescenceExitRebellion=state.AdolescenceExitRebellion;
            var visits=state.EventLog.Where(e=>e.roomId==YoungAdultRoomController.RoomId && e.eventType=="item_first" && e.experience!=null).Select(e=>e.experience).ToArray();
            visitedItemIds=visits.Select(e=>e.itemId).Distinct().ToArray();
            achievementVisited=visits.Any(e=>e.tags!=null && e.tags.Contains(TagDefinitions.Achievement));
        }
        public YoungAdultSnapshot Copy()
        { var copy=(YoungAdultSnapshot)MemberwiseClone(); copy.visitedItemIds=visitedItemIds==null ? Array.Empty<string>() : (string[])visitedItemIds.Clone(); return copy; }
        public string ToLogString()=>FormattableString.Invariant($"S={stress};I={intelligence};H={health};P={popularity};L={love};R={rebellion};T={trust};Gambling={gamblingBankruptcy};Contract={highRiskContract};Blocked={contactBlocked};Resigned={resigned};Delinquency={delinquencyRecord}");
    }
}
