using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Simulation;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FruitFlyParenting.Tests.EditMode
{
    public sealed class SimulationRunnerTests
    {
        [Test] public void CompleteRunsDoNotLoadScenesOrReplaceGlobalStateAndReplayEntireLog()
        {
            var current = GameState.Current; int sceneCount = SceneManager.sceneCount;
            var runner = new SimulationRunner(SimulationRunnerMenu.LoadConfig(), SimulationRunnerMenu.LoadRooms(), SimulationRunnerMenu.LoadEndings());
            foreach (var policy in new IPlayerPolicy[] { new PassivePolicy(), new RandomInterventionPolicy(), new AggressiveCoercionPolicy() })
            {
                var first = runner.Run(160016, policy); var second = runner.Run(160016, policy);
                Assert.That(SimulationRunnerMenu.Fingerprint(second), Is.EqualTo(SimulationRunnerMenu.Fingerprint(first)));
                Assert.That(first.violations, Is.Empty);
                Assert.That(first.state.eventLog.Count(e => e.eventType == "room_enter"), Is.EqualTo(4));
                Assert.That(first.state.finalYoungAdultSnapshot.IsCaptured, Is.True);
                if(policy is PassivePolicy)
                    foreach(var room in SimulationRunnerMenu.LoadRooms())
                        Assert.That(first.state.eventLog.Count(e=>e.roomId==room.Id && e.eventType=="item_first" && e.experience.autonomous),Is.GreaterThanOrEqualTo(2));
                foreach (var impact in first.decisiveExperiences)
                    Assert.That(first.state.eventLog.Any(e => e.impact != null && e.impact.label == impact.label &&
                        e.impact.stress == impact.stress && e.impact.rebellion == impact.rebellion && e.impact.trust == impact.trust), Is.True);
            }
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount)); Assert.That(GameState.Current, Is.SameAs(current));
        }
        [TestCase("childhood_family")]
        [TestCase("adolescence_parents")]
        [TestCase("youngadult_letter")]
        public void ParentTrustRequiresFirstAutonomousVisitAndCannotBeFarmed(string id)
        {
            var config = SimulationRunnerMenu.LoadConfig();
            var item = SimulationRunnerMenu.LoadRooms().SelectMany(r => r.Items).Single(i => i.Definition.itemId == id).Definition;
            foreach (string mode in new[] { "autonomous", "coerced", "complying", "recent", "late" })
            {
                var state = new GameState(config, 16); var effects = new ItemEffectApplier(config, state, "audit");
                if (mode == "recent") state.Relationship.RecordIntervention(99);
                var context = new ItemVisitContext(mode == "complying", mode == "coerced" ? CoercionKind.Wheelchair : CoercionKind.None);
                effects.Visit(item, true, mode != "late", 100, context);
                Assert.That(state.Relationship.Trust, Is.EqualTo(config.trustDefaultValue + (mode == "autonomous" ? config.autonomousParentTrustGain : 0)), mode);
                effects.Visit(item, true, true, 200);
                Assert.That(state.EventLog.Count(e => e.eventType == "autonomous_parent_trust"), Is.EqualTo(mode == "autonomous" ? 1 : 0), mode);
            }
        }
        [Test] public void ScenarioBoundariesCoverThousandSamples()
        {
            Assert.That(SimulationScenarioAudit.Run(SimulationRunnerMenu.LoadConfig(), SimulationRunnerMenu.LoadRooms(), SimulationRunnerMenu.LoadEndings()), Has.Length.EqualTo(3));
        }
        [Test] public void ParentTrustLogReportsActualClampedGain()
        {
            var config = SimulationRunnerMenu.LoadConfig(); var state = new GameState(config, 16);
            state.Relationship.AdjustTrust(48, "boundary fixture");
            var item = SimulationRunnerMenu.LoadRooms().SelectMany(r => r.Items).Single(i => i.Definition.itemId == "childhood_family").Definition;
            new ItemEffectApplier(config, state, "audit").Visit(item, true, true, 100);
            Assert.That(state.Relationship.Trust, Is.EqualTo(100));
            Assert.That(state.EventLog.Single(e => e.eventType == "autonomous_parent_trust").payload, Does.EndWith("gain=2"));
        }
    }
}
