using System;
using System.IO;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class SaveSystemTests
    {
        string tempFilePath;

        static GameBalanceConfig NewConfig()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            config.statDefaultValue = 50f;
            config.statMin = 0f;
            config.statMax = 100f;
            config.rebellionInitialValue = 0f;
            config.trustDefaultValue = 50f;
            config.trustMin = 0f;
            config.trustMax = 100f;
            return config;
        }

        [SetUp]
        public void SetUp()
        {
            tempFilePath = Path.Combine(Path.GetTempPath(), "ffp_savesystem_test_" + Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }

        [Test]
        public void SaveThenLoad_RoundTripsAllTrackedValues()
        {
            var config = NewConfig();
            var original = new GameState(config, 42);
            original.CurrentRoomId = "infant_room";
            original.Stats.ApplyDelta(StatType.Intelligence, 15f, "test");
            original.Stats.ApplyDelta(StatType.Stress, -5f, "test");
            original.Stats.ApplyDelta(StatType.Rebellion, 7f, "test");
            original.Relationship.AdjustTrust(20f, "test");
            original.Relationship.RecordIntervention(0f);
            original.Relationship.RecordIntervention(30f);
            original.Relationship.RecordAutonomousVisit();
            original.Relationship.RecordAutonomousVisit();
            original.Relationship.RecordItemRevisit("item_a");
            original.Relationship.RecordItemRevisit("item_a");
            original.LogEvent(1.5f, "infant_room", "item_visited", "item_a");
            original.LogEvent(2.5f, "infant_room", "tool_used", "wheelchair");

            var saveSystem = new SaveSystem(config);
            saveSystem.Save(original, tempFilePath);

            Assert.IsTrue(File.Exists(tempFilePath));

            GameState loaded = saveSystem.Load(tempFilePath);

            Assert.AreEqual(original.RunSeed, loaded.RunSeed);
            Assert.AreEqual(original.CurrentRoomId, loaded.CurrentRoomId);

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                Assert.AreEqual(original.Stats.GetValue(type), loaded.Stats.GetValue(type), $"스탯 {type} 불일치");
            }

            Assert.AreEqual(original.Relationship.Trust, loaded.Relationship.Trust);
            Assert.AreEqual(original.Relationship.TotalInterventionCount, loaded.Relationship.TotalInterventionCount);
            Assert.AreEqual(original.Relationship.AutonomousVisitCount, loaded.Relationship.AutonomousVisitCount);
            Assert.AreEqual(original.Relationship.GetItemRevisitCount("item_a"), loaded.Relationship.GetItemRevisitCount("item_a"));

            Assert.AreEqual(original.EventLog.Count, loaded.EventLog.Count);
            Assert.AreEqual(original.EventLog[0].eventType, loaded.EventLog[0].eventType);
            Assert.AreEqual(original.EventLog[1].payload, loaded.EventLog[1].payload);
        }

        [Test]
        public void SaveFileExists_ReflectsActualFileState()
        {
            var config = NewConfig();
            var saveSystem = new SaveSystem(config);

            Assert.IsFalse(saveSystem.SaveFileExists(tempFilePath));

            saveSystem.Save(new GameState(config, 1), tempFilePath);

            Assert.IsTrue(saveSystem.SaveFileExists(tempFilePath));
        }
    }
}
