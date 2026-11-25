using System;
using System.Linq;
using FruitFlyParenting.Comfort;
using FruitFlyParenting.Core;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using FruitFlyParenting.Wheelchair;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace FruitFlyParenting.Tests.EditMode
{
    public sealed class ComfortAuditTests
    {
        [Test] public void NeutralAxesStopImmediatelyButReverseAndAccelerationRemainSmooth()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            try
            {
                var motion = new WheelchairMotionModel(config);
                motion.Step(Vector2.one, 5, 15); Assert.That(motion.TurnSpeed, Is.EqualTo(15));
                motion.Step(new Vector2(.05f, 1), .01f, 15); Assert.That(motion.TurnSpeed, Is.Zero); Assert.That(motion.Speed, Is.GreaterThan(0));
                motion.Step(new Vector2(1, .05f), .01f, 15); Assert.That(motion.Speed, Is.Zero);
                motion.Step(Vector2.zero, 0); Assert.That(motion.TurnSpeed, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void PreferencesSurviveNewInstancesAndRunResetWithBoundsAndVersionValidation()
        {
            string key = "step14-test-" + Guid.NewGuid();
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            try
            {
                var value = new ComfortPreferences { turnSpeed = 100, heightOffset = -8, vignette = false }; value.Save(key);
                var loaded = ComfortPreferences.Load(key);
                Assert.That(loaded.turnSpeed, Is.EqualTo(45)); Assert.That(loaded.heightOffset, Is.EqualTo(-.35f)); Assert.That(loaded.vignette, Is.False);
                var first = new GameState(config, 1); var next = new GameState(config, 2);
                Assert.That(ComfortPreferences.Load(key).turnSpeed, Is.EqualTo(45));
                PlayerPrefs.SetString(key, "{\"version\":99}"); Assert.That(ComfortPreferences.Load(key).turnSpeed, Is.EqualTo(30));
                PlayerPrefs.SetString(key, "invalid"); Assert.That(ComfortPreferences.Load(key).vignette, Is.True);
            }
            finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void CalibrationIsRelativeAndBoundedAndStatDeltasUseShapeAndSign()
        {
            Assert.That(ComfortPreferences.CalibratedOffset(.1f, 1.4f), Is.EqualTo(-.1f).Within(.0001f));
            Assert.That(ComfortPreferences.CalibratedOffset(0, 2), Is.EqualTo(-.35f));
            StringAssert.Contains("▲", ComfortController.FormatDelta(StatType.Stress, 2));
            StringAssert.Contains("+2.0", ComfortController.FormatDelta(StatType.Stress, 2));
            StringAssert.Contains("▼", ComfortController.FormatDelta(StatType.Health, -3));
            StringAssert.Contains("-3.0", ComfortController.FormatDelta(StatType.Health, -3));
        }
        [Test] public void EveryMainSceneHasSettingsAndFeedbackAndOnlyContinuousProviders()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
                {
                    var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                    var chairs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WheelchairLocomotion>(true)).ToArray();
                    Assert.That(chairs.Length, Is.EqualTo(1), entry.path);
                    Assert.That(chairs[0].GetComponents<ComfortController>().Length, Is.EqualTo(1), entry.path);
                    foreach (var transform in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, entry.path + ":" + transform.name);
                    foreach (var p in chairs[0].GetComponents<LocomotionProvider>())
                        Assert.That(p is ContinuousMoveProvider || p is ContinuousTurnProvider, Is.True, p.GetType().Name);
                    foreach (var slot in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ItemSlot>(true)))
                        Assert.That(slot.GetComponents<ItemApproachFeedback>().Length, Is.EqualTo(1), entry.path + ":" + slot.ItemId);
                }
            }
            finally
            {
                if (previous.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
        [Test] public void AllRoutesHaveDistinctAdjacentStageAudioAndSafeFade()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>("Assets/_Project/ScriptableObjects/GameBalanceConfig.asset");
            Assert.That(config.roomFadeSeconds, Is.GreaterThanOrEqualTo(.5f));
            foreach (string guid in AssetDatabase.FindAssets("t:RoomTransitionDefinition"))
            {
                var route = AssetDatabase.LoadAssetAtPath<RoomTransitionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(route.previousRoomSound, Is.Not.Null); Assert.That(route.nextRoomSound, Is.Not.Null);
                if (route.fromStage != LifeStage.Prologue) Assert.That(route.previousRoomSound, Is.Not.SameAs(route.nextRoomSound));
            }
        }
    }
}
