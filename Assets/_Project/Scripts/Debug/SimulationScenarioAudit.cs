using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using UnityEngine;

namespace FruitFlyParenting.Simulation
{
    public static class SimulationScenarioAudit
    {
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Scenario audit: " + message); }
        static bool Equal(float a, float b) => Math.Abs(a - b) < .001f;
        public static string[] Run(GameBalanceConfig config, SimulationRoom[] rooms, EndingDefinition[] endings)
        {
            var random = new System.Random(160016);
            var checks = new List<string>();
            for (int seed = 0; seed < 1000; seed++)
            {
                var state = new GameState(config, seed);
                var usage = new ToolUsageTracker(config, state, 0);
                float now = 0, previous = 0;
                for (int n = 1; n <= 20; n++)
                {
                    now += 1 + (float)random.NextDouble(); usage.RecordContact(PushSource.Wheelchair, now);
                    float expected = Math.Min(3, .4f + .25f * Math.Max(0, n - 2));
                    Require(Equal(usage.LastBaseStressDelta, expected) && usage.LastBaseStressDelta >= previous, "4.3 repeated stress / seed " + seed);
                    previous = usage.LastBaseStressDelta;
                }
                foreach (var source in new[] { PushSource.Wheelchair, PushSource.ElectricStick })
                {
                    state = new GameState(config, seed); state.Stats.ApplyDelta(StatType.Rebellion, 100, "boundary fixture");
                    var model = new ContactModel(config, state, 0); model.EnterRoom("audit", 0);
                    int threshold = source == PushSource.Wheelchair ? 8 : 4;
                    ContactOutcome outcome = default;
                    for (int n = 0; n < threshold; n++) outcome = model.Receive(source, Vector3.left, Vector3.zero, LifeStage.Childhood, "training", n * 1.1f);
                    Require(outcome.Motion == ContactMotion.Comply && model.Compliance.State == ComplianceState.Complying, "9.8 R100 compliance");
                    Require(state.EventLog.Count(e => e.eventType == "compliance_enter") == 1, "one entry cost");
                    model.Compliance.EnterRoom();
                    model.Compliance.RecordContact(source, Vector3.right, 100, 20);
                    model.Compliance.RecordContact(source, Vector3.left, 100, 22);
                    Require(Equal(model.Compliance.Score, source == PushSource.Wheelchair ? 1 : 2), "opposite direction reset");
                    model.Compliance.Tick(32); Require(model.Compliance.Score == 0, "10-second idle reset");
                }
                state = new GameState(config, seed);
                var avoid = new ContactModel(config, state, 0);
                for (int n = 0; n < 4; n++) avoid.Receive(PushSource.Wheelchair, Vector3.left, Vector3.zero, LifeStage.Infant, "item", n * 1.1f);
                var ignoredPush = avoid.Receive(PushSource.Wheelchair, Vector3.right, Vector3.zero, LifeStage.Infant, "item", 4.4f);
                Require(ignoredPush.Motion == ContactMotion.Avoid, "9.4 avoidance blocks displacement while below compliance threshold");
                // Avoidance blocks movement, not costs. This is the 4.6 override, not a blanket input discard.
                Require(state.Relationship.TotalInterventionCount == 5, "avoidance still records contacts");
                float infantS = (float)random.NextDouble() * 100, childhoodS = (float)random.NextDouble() * 100;
                Require(CostumeEvaluator.ShouldWearHiddenCostume(infantS, childhoodS) ==
                    ((infantS >= 70 && childhoodS >= 70) || infantS + childhoodS >= 150), "9.4 costume thresholds");
                foreach (float rebellion in new[] { 59f, 60f })
                {
                    state = new GameState(config, seed); state.Stats.ApplyDelta(StatType.Rebellion, rebellion, "boundary fixture"); state.CaptureAdolescenceExit(480);
                    Require(state.IsFlightSmokeEventTriggered == (rebellion == 60), "9.7 flight 59/60");
                }
                // Remove each gang prerequisite independently; high R alone is never sufficient.
                for (int omitted = 0; omitted < 6; omitted++)
                {
                    state = new GameState(config, seed);
                    state.Stats.ApplyDelta(StatType.Rebellion, omitted == 1 ? 74 : 75 + random.Next(26), "boundary fixture");
                    state.Stats.ApplyDelta(StatType.Popularity, (omitted == 2 ? 59 : 60 + random.Next(41)) - 50, "boundary fixture");
                    state.Relationship.AdjustTrust((omitted == 3 ? 46 : random.Next(46)) - 50, "boundary fixture");
                    if (omitted != 4) state.RecordDelinquency("adolescence_theft", 0);
                    if (omitted != 5) state.ApplyYoungAdultFlags(seed % 2 == 0 ? YoungAdultFlags.ContactBlocked : YoungAdultFlags.Resigned, "fixture", 0);
                    state.CaptureFinalYoungAdultSnapshot(1);
                    Require(EndingEvaluator.Matches(EndingCondition.Gang, new EndingEvaluationContext(state.FinalYoungAdultSnapshot)) == (omitted == 0), "9.8 gang conjunction");
                    state = GameState.FromSaveData(config, state.ToSaveData());
                    state.ApplyYoungAdultFlags(YoungAdultFlags.GamblingBankruptcy, "youngadult_gambling", 2);
                    var bankruptcy = new YoungAdultSnapshot(state, 3);
                    Require(EndingEvaluator.Evaluate(bankruptcy, endings).condition == EndingCondition.Bankruptcy, "9.5 bankruptcy precedence");
                }
            }
            checks.Add("1000 seeds: escalating stress, avoidance displacement, R100 weak/strong compliance, direction/idle reset, costume fuzz, R59/60 exit, 6000 gang conjunction and bankruptcy priority samples");
            var trainingItems = rooms.SelectMany(r => r.Items).Select(i => i.Definition).Where(i => CoercionDetector.IsTrainingItem(i, config)).Take(5).ToArray();
            foreach (var source in new[] { PushSource.Wheelchair, PushSource.ElectricStick })
            {
                var state = new GameState(config, 16); var effects = new ItemEffectApplier(config, state, "audit");
                var model = new ContactModel(config, state, 0);
                var item = trainingItems[0]; var target = new Vector3(3, 0, 0);
                model.Receive(source, Vector3.left, Vector3.zero, LifeStage.Childhood, item.itemId, 100);
                var context = model.PrepareVisit(item, target, true, 102);
                float s = state.Stats.GetValue(StatType.Stress), r = state.Stats.GetValue(StatType.Rebellion);
                effects.Visit(item, true, true, 102, context);
                float baseS = item.statDeltas.Where(d => d.stat == StatType.Stress).Sum(d => d.amount);
                Require(Equal(state.Stats.GetValue(StatType.Stress) - s, baseS + (source == PushSource.Wheelchair ? 1 : 3)), "9.7 forced S");
                Require(Equal(state.Stats.GetValue(StatType.Rebellion) - r, source == PushSource.Wheelchair ? 2 : 5), "9.7 forced R");
                s = state.Stats.GetValue(StatType.Stress); r = state.Stats.GetValue(StatType.Rebellion);
                effects.Visit(item, true, true, 103, context);
                Require(Equal(s, state.Stats.GetValue(StatType.Stress)) && Equal(r, state.Stats.GetValue(StatType.Rebellion)), "forced visit duplicate");
                for (int i = 1; i < trainingItems.Length; i++) effects.Visit(trainingItems[i], true, true, 110 + i);
                Require(state.EventLog.Count(e => e.eventType == "repeated_training") == 3, "third distinct training and cap three");
            }
            checks.Add("Weak/strong forced training exact increments; repeat visits cannot farm; third distinct training onward capped at three");
            var tablet = rooms[0].Items.Single(i => i.Definition.itemId == "infant_tablet").Definition;
            {
                var state = new GameState(config, 16); var effects = new ItemEffectApplier(config, state, rooms[0].Id);
                effects.Visit(tablet, true, true, 1); var values = state.ToSaveData().statValues;
                effects.Visit(tablet, true, true, 2);
                Require(values.SequenceEqual(state.ToSaveData().statValues), "tablet once");
                Require(state.Preferences.GetPreference(TagDefinitions.Consumption) != 0 && state.Preferences.GetPreference(TagDefinitions.Play) != 0, "tablet tag learning");
                var decision = new ItemChoiceModel(config, state, new SystemRandomSource(state.Random)).Evaluate(rooms[1].Items.Select(i => new ItemChoiceCandidate(i.Definition.itemId, i.Definition.tags)).ToArray(), null, LifeStage.Childhood, 0);
                Require(decision.Candidates.Single(i => i.Id == "childhood_arcade").Preference != 0, "tablet memory reaches childhood choice");
            }
            checks.Add("Tablet first-visit effects and transfer to childhood consumption/play choice");
            return checks.ToArray();
        }
    }
}
