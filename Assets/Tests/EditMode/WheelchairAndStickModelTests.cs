using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Tool;
using FruitFlyParenting.Wheelchair;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class WheelchairAndStickModelTests
    {
        GameBalanceConfig config;
        GameState state;
        ToolUsageTracker usage;
        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAllSubscribers();
            config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            state = new GameState(config, 42);
            usage = new ToolUsageTracker(config, state, 0f);
        }
        [TearDown]
        public void TearDown() { EventBus.ClearAllSubscribers(); Object.DestroyImmediate(config); }

        [Test]
        public void Bumper_RequiresOverlapAndActualMovement()
        {
            var bumper = new WheelchairBumperPushResolver(config);
            Assert.IsFalse(bumper.TryContact(true, 0f, 0f));
            Assert.IsFalse(bumper.TryContact(false, 1f, 0f));
            Assert.IsFalse(bumper.TryContact(true, 0.049f, 0f));
            Assert.IsTrue(bumper.TryContact(true, 0.05f, 0f));
        }
        [Test]
        public void Bumper_CooldownIsOneSecond_InvalidContactsDoNotConsumeIt()
        {
            var bumper = new WheelchairBumperPushResolver(config);
            Assert.IsTrue(bumper.TryContact(true, 1f, 0f));
            Assert.IsFalse(bumper.TryContact(true, 1f, 0.999f));
            Assert.IsFalse(bumper.TryContact(true, 0f, 1f));
            Assert.IsTrue(bumper.TryContact(true, 1f, 1f));
        }
        [Test]
        public void PushDirection_IsAwayFromSource_AndElectricIsStronger()
        {
            var resolver = new PushResolver(config);
            var weak = resolver.Resolve(PushSource.Wheelchair, Vector3.back, Vector3.zero);
            var strong = resolver.Resolve(PushSource.ElectricStick, new Vector3(0f, 1f, -2f), Vector3.zero);
            Assert.AreEqual(Vector3.forward, weak.Direction);
            Assert.AreEqual(Vector3.forward, strong.Direction);
            Assert.AreEqual(1.5f, weak.Force);
            Assert.AreEqual(3f, strong.Force);
        }
        [Test]
        public void ElectricShock_IsPublishedForEveryElectricContactOnly()
        {
            int count = 0;
            Vector3 at = new Vector3(4f, 0.3f, 6f);
            EventBus.ElectricShockVisual += position => { count++; Assert.AreEqual(at, position); };
            var resolver = new PushResolver(config);
            resolver.Resolve(PushSource.Wheelchair, Vector3.back, at);
            Assert.AreEqual(0, count);
            resolver.Resolve(PushSource.ElectricStick, Vector3.back, at);
            resolver.Resolve(PushSource.ElectricStick, Vector3.back, at);
            Assert.AreEqual(2, count);
        }
        [Test]
        public void MixedContacts_ShareWindowAndTrustPenalty_ElectricAddsTwo()
        {
            usage.RecordContact(PushSource.Wheelchair, 0f);
            usage.RecordContact(PushSource.Wheelchair, 1f);
            usage.RecordContact(PushSource.ElectricStick, 2f);
            Assert.AreEqual(3, usage.RecentContactCount);
            Assert.AreEqual(0.65f, usage.LastBaseStressDelta, 0.0001f);
            Assert.AreEqual(53.45f, state.Stats.GetValue(StatType.Stress), 0.0001f);
            Assert.AreEqual(48f, state.Relationship.Trust);
            Assert.AreEqual(3, state.Relationship.TotalInterventionCount);
        }
        [Test]
        public void ElectricStress_HasSeparateReasonTag_WithoutTraining()
        {
            float immediate = 0f;
            state.Stats.OnStatChanged += (type, oldValue, newValue, reason) =>
            { if (reason == "electric_stick_immediate") immediate += newValue - oldValue; };
            usage.RecordContact(PushSource.ElectricStick, 0f);
            usage.RecordContact(PushSource.ElectricStick, 0.1f);
            Assert.AreEqual(4f, immediate);
            Assert.AreEqual(54.8f, state.Stats.GetValue(StatType.Stress), 0.0001f);
            Assert.AreEqual(0f, state.Stats.GetValue(StatType.Rebellion));
        }
        [Test]
        public void Window_IncludesExactlySixtySeconds_ButDropsOlderRecords()
        {
            usage.RecordContact(PushSource.Wheelchair, 0f);
            usage.RecordContact(PushSource.Wheelchair, 60f);
            Assert.AreEqual(2, usage.RecentContactCount);
            usage.RecordContact(PushSource.Wheelchair, 60.01f);
            Assert.AreEqual(2, usage.RecentContactCount);
            Assert.AreEqual(3, state.Relationship.TotalInterventionCount);
        }
        [Test]
        public void StressFormula_CapsBaseAtThree_AndFinalStatAtHundred()
        {
            for (int i = 0; i < 100; i++) usage.RecordContact(PushSource.ElectricStick, i * 0.1f);
            Assert.AreEqual(3f, usage.LastBaseStressDelta);
            Assert.AreEqual(100f, state.Stats.GetValue(StatType.Stress));
            Assert.AreEqual(0f, state.Relationship.Trust);
        }
        [TestCase(4.999f, 49f)]
        [TestCase(5f, 50f)]
        public void RapidRepeat_HasStrictFiveSecondBoundary(float secondTime, float expectedTrust)
        {
            usage.RecordContact(PushSource.ElectricStick, 0f);
            usage.RecordContact(PushSource.Wheelchair, secondTime);
            Assert.AreEqual(expectedTrust, state.Relationship.Trust);
        }
        [Test]
        public void WatchingAlone_RewardsEverySixtySeconds_CappedPerRoom()
        {
            usage.Tick(59.99f); Assert.AreEqual(50f, state.Relationship.Trust);
            usage.Tick(60f); Assert.AreEqual(51f, state.Relationship.Trust);
            usage.Tick(120f); Assert.AreEqual(52f, state.Relationship.Trust);
            usage.Tick(600f); Assert.AreEqual(54f, state.Relationship.Trust);
            usage.EnterRoom("next", 600f);
            usage.Tick(660f); Assert.AreEqual(55f, state.Relationship.Trust);
        }
        [Test]
        public void Contact_RestartsIdleRewardClock()
        {
            usage.Tick(59f);
            usage.RecordContact(PushSource.Wheelchair, 59f);
            usage.Tick(118.99f); Assert.AreEqual(50f, state.Relationship.Trust);
            usage.Tick(119f); Assert.AreEqual(51f, state.Relationship.Trust);
        }
        [Test]
        public void FourthMixedContact_StartsTwelveSecondAvoidance_WithoutExtension()
        {
            for (int i = 0; i < 4; i++) usage.RecordContact(i % 2 == 0 ? PushSource.Wheelchair : PushSource.ElectricStick, i);
            Assert.IsTrue(usage.Mood.IsAvoiding(3f));
            usage.RecordContact(PushSource.ElectricStick, 4f);
            Assert.AreEqual(5, state.Relationship.TotalInterventionCount);
            Assert.IsTrue(usage.Mood.IsAvoiding(14.999f));
            Assert.IsFalse(usage.Mood.IsAvoiding(15f));
        }
        [Test]
        public void ForceEndAvoidance_EndsImmediately_AndClearsStreak()
        {
            for (int i = 0; i < 4; i++) usage.RecordContact(PushSource.ElectricStick, i);
            usage.Mood.ForceEndAvoidance();
            Assert.IsFalse(usage.Mood.IsAvoiding(3.1f));
            usage.RecordContact(PushSource.Wheelchair, 3.2f);
            Assert.IsFalse(usage.Mood.IsAvoiding(3.2f));
        }
        [Test]
        public void ConsecutiveContacts_ResetAfterFiveSecondPause()
        {
            var mood = new FlyMoodState(config);
            mood.RecordContact(0f); mood.RecordContact(1f); mood.RecordContact(2f); mood.RecordContact(7f);
            Assert.IsFalse(mood.IsAvoiding(7f));
        }
        [Test]
        public void ElectricEpisode_DeduplicatesColliders_ButAllowsImmediateReentry()
        {
            var episodes = new ContactEpisodeTracker<int, int>();
            Assert.IsTrue(episodes.Enter(1, 10));
            Assert.IsFalse(episodes.Enter(1, 10));
            Assert.IsFalse(episodes.Enter(1, 11));
            episodes.Exit(1, 10);
            Assert.IsFalse(episodes.Enter(1, 10)); // 다른 충돌체는 아직 접촉 중
            episodes.Exit(1, 10); episodes.Exit(1, 11);
            Assert.IsTrue(episodes.Enter(1, 10)); // 시간 쿨다운 없음
        }
        [Test]
        public void Wheelchair_AcceleratesBrakesAndTurnsWithinConfiguredLimits()
        {
            var motion = new WheelchairMotionModel(config);
            motion.Step(Vector2.one, 0.1f);
            Assert.AreEqual(0.06f, motion.Speed, 0.0001f);
            Assert.AreEqual(6f, motion.TurnSpeed, 0.0001f);
            motion.Step(Vector2.one, 10f);
            Assert.AreEqual(0.8f, motion.Speed, 0.0001f);
            Assert.AreEqual(30f, motion.TurnSpeed, 0.0001f);
            motion.Step(Vector2.zero, 0.1f);
            Assert.AreEqual(0f, motion.Speed, "Releasing input must brake in the same frame.");
            Assert.AreEqual(0f, motion.TurnSpeed);
            motion.Step(Vector2.zero, 1f);
            Assert.AreEqual(0f, motion.Speed);
            Assert.AreEqual(0f, motion.TurnSpeed);
        }
        [Test]
        public void Wheelchair_DeadzoneIgnoresStickDrift_ReverseIsSmooth()
        {
            var motion = new WheelchairMotionModel(config);
            motion.Step(Vector2.one * 0.1f, 1f);
            Assert.AreEqual(0f, motion.Speed);
            motion.Step(Vector2.up, 2f);
            motion.Step(Vector2.down, 0.1f);
            Assert.AreEqual(0.68f, motion.Speed, 0.0001f);
            motion.Step(Vector2.down, 2f);
            Assert.AreEqual(-0.8f, motion.Speed, 0.0001f);
        }
    }
}
