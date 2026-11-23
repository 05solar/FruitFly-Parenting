using System.Collections;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
using FruitFlyParenting.Wheelchair;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FruitFlyParenting.Tests.PlayMode
{
    public class WheelchairAndStickIntegrationTests
    {
        InputDevice left, right;
        WheelchairLocomotion chair;
        FlyAgent fly;
        FlyContactReceiver receiver;
        NavMeshAgent agent;
        float oldScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldScale = Time.timeScale;
            Time.timeScale = 1f;
            InputSystem.RegisterLayout(@"{
                ""name"":""Step04Controller"", ""extend"":""XRController"",
                ""controls"":[
                    {""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]},
                    {""name"":""trigger"",""layout"":""Axis"",""format"":""FLT""}
                ]}");
            left = InputSystem.AddDevice("Step04Controller");
            right = InputSystem.AddDevice("Step04Controller");
            InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            yield return SceneManager.LoadSceneAsync("03_RoomShellTest");
            chair = Object.FindAnyObjectByType<WheelchairLocomotion>();
            fly = Object.FindAnyObjectByType<FlyAgent>();
            receiver = fly.GetComponent<FlyContactReceiver>();
            agent = fly.GetComponent<NavMeshAgent>();
            yield return null;
            Assert.IsNotNull(chair);
            Assert.IsNotNull(receiver.Usage);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (left != null) InputSystem.RemoveDevice(left);
            if (right != null) InputSystem.RemoveDevice(right);
            Time.timeScale = oldScale;
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        static void Stick(InputDevice device, Vector2 value) => InputSystem.QueueDeltaStateEvent((StickControl)device["thumbstick"], value);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        void PlaceChair(Vector3 position, Vector3 facing)
        {
            var body = chair.GetComponent<CharacterController>();
            body.enabled = false;
            chair.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            body.enabled = true;
            Physics.SyncTransforms();
        }
        void HoldFly(Vector3 position)
        {
            Assert.IsTrue(agent.Warp(position));
            agent.SetDestination(position);
            agent.isStopped = true;
        }

        [UnityTest]
        public IEnumerator OnlyLeftStick_DrivesAndTurns_WithBraking()
        {
            fly.gameObject.SetActive(false);
            Vector3 initial = chair.transform.position;
            Quaternion rotation = chair.transform.rotation;
            Stick(right, Vector2.one);
            InputSystem.QueueDeltaStateEvent((AxisControl)right["trigger"], 1f);
            yield return new WaitForSeconds(0.4f);
            Assert.Less(Vector3.Distance(Flat(initial), Flat(chair.transform.position)), 0.01f);
            Assert.Less(Quaternion.Angle(rotation, chair.transform.rotation), 0.1f);
            Stick(left, Vector2.up);
            yield return new WaitForSeconds(1f);
            Assert.Greater(Vector3.Dot(chair.transform.position - initial, rotation * Vector3.forward), 0.15f);
            Stick(left, Vector2.zero);
            yield return new WaitForSeconds(1f);
            Vector3 stopped = chair.transform.position;
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Vector3.Distance(Flat(stopped), Flat(chair.transform.position)), 0.01f);
            Stick(left, Vector2.right);
            yield return new WaitForSeconds(1f);
            Assert.That(Quaternion.Angle(rotation, chair.transform.rotation), Is.InRange(10f, 35f));
            Assert.Less(Vector3.Distance(Flat(stopped), Flat(chair.transform.position)), 0.01f);
            Assert.AreEqual(0, GameState.Current.Relationship.TotalInterventionCount);
        }

        [UnityTest]
        public IEnumerator StationaryBumperDoesNothing_MovingBumperPushes()
        {
            PlaceChair(new Vector3(12f, 0f, 9f), Vector3.forward);
            HoldFly(new Vector3(12f, 0.3f, 9.75f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, GameState.Current.Relationship.TotalInterventionCount);
            Vector3 before = fly.transform.position;
            Stick(left, Vector2.up);
            float deadline = Time.time + 2f;
            while (GameState.Current.Relationship.TotalInterventionCount == 0 && Time.time < deadline) yield return null;
            Assert.AreEqual(1, GameState.Current.Relationship.TotalInterventionCount);
            Assert.AreEqual(50.4f, GameState.Current.Stats.GetValue(StatType.Stress), 0.001f);
            Assert.AreEqual(0, fly.GetComponent<FlyShockFeedback>().ShockCount);
            Stick(left, Vector2.zero);
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(fly.transform.position.z - before.z, 0.6f);
        }

        [UnityTest]
        public IEnumerator Wheelchair_CollidesWithWall_ActualSpeedFallsToZero()
        {
            fly.gameObject.SetActive(false);
            PlaceChair(new Vector3(1.2f, 0f, 7f), Vector3.left);
            Stick(left, Vector2.up);
            yield return new WaitForSeconds(3f);
            Assert.That(chair.transform.position.x, Is.InRange(0.38f, 0.5f));
            Assert.Less(chair.ActualVelocity.magnitude, 0.03f);
            Assert.AreEqual(0, GameState.Current.Relationship.TotalInterventionCount);
        }

        [UnityTest]
        public IEnumerator ElectricTip_UsesPhysicalContact_NoButton_NoTimeCooldown_NoColliderDuplicates()
        {
            fly.enabled = false;
            agent.isStopped = true;
            var extra = new GameObject("Second fly collider");
            extra.transform.SetParent(fly.transform, false);
            extra.AddComponent<SphereCollider>().radius = 0.25f;
            var tool = Object.FindAnyObjectByType<LoveStickTool>();
            var hand = tool.transform.parent.parent;
            Vector3 offset = tool.transform.position - hand.position;
            Vector3 contact = fly.transform.position + Vector3.right * 0.2f;
            hand.position = contact - offset;
            Physics.SyncTransforms();
            float first = Time.time;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.08f);
            Assert.AreEqual(1, GameState.Current.Relationship.TotalInterventionCount);
            Assert.IsTrue(fly.GetComponent<FlyShockFeedback>().IsVisible);
            hand.position += Vector3.up * 2f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            hand.position -= Vector3.up * 2f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Less(Time.time - first, 1f);
            Assert.AreEqual(2, GameState.Current.Relationship.TotalInterventionCount);
            Assert.AreEqual(54.8f, GameState.Current.Stats.GetValue(StatType.Stress), 0.001f);
            Assert.AreEqual(2, fly.GetComponent<FlyShockFeedback>().ShockCount);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(fly.GetComponent<FlyShockFeedback>().IsVisible);
        }

        [UnityTest]
        public IEnumerator PushDistances_AreWeakOnePointFiveAndStrongThreeMeters()
        {
            var start = new Vector3(12f, 0.3f, 7f);
            foreach (var source in new[] { PushSource.Wheelchair, PushSource.ElectricStick })
            {
                HoldFly(start);
                yield return null;
                Vector3 before = fly.transform.position;
                receiver.ReceiveContact(source, Vector3.left, Time.time);
                float deadline = Time.time + 1f;
                while (fly.IsBeingPushed && Time.time < deadline) yield return null;
                Assert.IsFalse(fly.IsBeingPushed);
                Assert.AreEqual(source == PushSource.Wheelchair ? 1.5f : 3f,
                    Vector3.Distance(Flat(before), Flat(fly.transform.position)), 0.08f);
            }
        }

        [UnityTest]
        public IEnumerator StrongPush_ClipsAtWall_WithoutLeavingNavMesh()
        {
            HoldFly(new Vector3(23.4f, 0.3f, 7f));
            yield return null;
            receiver.ReceiveContact(PushSource.ElectricStick, Vector3.left, Time.time);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(agent.isOnNavMesh);
            Assert.LessOrEqual(fly.transform.position.x, 23.8f);
            var capsule = fly.GetComponent<CapsuleCollider>();
            var room = Object.FindAnyObjectByType<RoomController>();
            foreach (var collider in room.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger))
                Assert.IsFalse(Physics.ComputePenetration(capsule, fly.transform.position, fly.transform.rotation,
                    collider, collider.transform.position, collider.transform.rotation, out _, out float depth) && depth > 0.02f,
                    "Push penetrated " + collider.name);
        }

        [UnityTest]
        public IEnumerator PushedAwayFromItem_CanAutonomouslyReturnToSameItem()
        {
            Time.timeScale = 4f;
            string original = fly.CurrentDestinationId;
            var slot = Object.FindAnyObjectByType<RoomController>().ItemSlots.First(s => s.ItemId == original);
            float deadline = Time.time + 25f;
            while ((agent.pathPending || agent.remainingDistance > 0.1f) && Time.time < deadline) yield return null;
            Assert.Less(Vector3.Distance(fly.transform.position, slot.transform.position), slot.InteractionRadius);
            Vector3 away = Flat(fly.transform.position - slot.transform.position).normalized;
            receiver.ReceiveContact(PushSource.Wheelchair, -away, Time.time);
            yield return new WaitForSeconds(0.32f);
            Assert.Greater(Vector3.Distance(fly.transform.position, slot.transform.position), slot.InteractionRadius);
            Assert.AreEqual(original, fly.CurrentDestinationId);
            deadline = Time.time + 15f;
            while ((agent.pathPending || agent.remainingDistance > 0.1f) && Time.time < deadline) yield return null;
            Assert.AreEqual(original, fly.CurrentDestinationId);
            Assert.Less(Vector3.Distance(fly.transform.position, slot.transform.position), slot.InteractionRadius);
        }

        [UnityTest]
        public IEnumerator FourthContact_ResumesOldGoal_AndComplianceHookRestoresPushing()
        {
            string goal = fly.CurrentDestinationId;
            for (int i = 0; i < 4; i++) receiver.ReceiveContact(i % 2 == 0 ? PushSource.Wheelchair : PushSource.ElectricStick, Vector3.left, Time.time);
            Assert.IsTrue(receiver.Usage.Mood.IsAvoiding(Time.time));
            Assert.IsFalse(fly.IsBeingPushed);
            Assert.AreEqual(goal, fly.CurrentDestinationId);
            receiver.ReceiveContact(PushSource.ElectricStick, Vector3.left, Time.time);
            Assert.AreEqual(5, GameState.Current.Relationship.TotalInterventionCount);
            Assert.IsFalse(fly.IsBeingPushed);
            receiver.ForceEndAvoidance();
            receiver.ReceiveContact(PushSource.Wheelchair, Vector3.left, Time.time);
            Assert.IsTrue(fly.IsBeingPushed);
            yield return null;
        }
    }
}
