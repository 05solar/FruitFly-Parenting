using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Ending;
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
using Object = UnityEngine.Object;

namespace FruitFlyParenting.Tests.PlayMode
{
    public sealed class SimulationNavigationFuzzTests
    {
        InputDevice left;
        GameBalanceConfig config;
        float oldScale, oldCapture, oldFixed;
        readonly List<string> evidence = new List<string>();
        readonly string checkpoint = "Logs/step16-fuzz-checkpoint.json";
        void Inject(Scene scene, LoadSceneMode mode)
        {
            var state = new GameState(config, 160016); state.CaptureFinalYoungAdultSnapshot(0); GameState.SetCurrent(state);
            foreach (var room in Object.FindObjectsByType<InfantRoomController>()) room.CheckpointPath = checkpoint;
            foreach (var room in Object.FindObjectsByType<ChildhoodRoomController>()) room.CheckpointPath = checkpoint;
            foreach (var room in Object.FindObjectsByType<AdolescenceRoomController>()) room.CheckpointPath = checkpoint;
            foreach (var room in Object.FindObjectsByType<YoungAdultRoomController>()) room.CheckpointPath = checkpoint;
            foreach (var room in Object.FindObjectsByType<MiddleAgeRoomController>()) room.CheckpointPath = checkpoint;
            if (scene.name == "06_MiddleAge") Object.FindAnyObjectByType<WheelchairLocomotion>().transform.position = new Vector3(2, 0, 11);
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            oldScale = Time.timeScale; oldCapture = Time.captureDeltaTime; oldFixed = Time.fixedDeltaTime;
            config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            InputSystem.RegisterLayout(@"{""name"":""SimulationFuzzController"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Stick"",""format"":""VEC2"",""usages"":[""Primary2DAxis""]}]}");
            left = InputSystem.AddDevice("SimulationFuzzController"); InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            SceneManager.sceneLoaded += Inject;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= Inject;
            if (left != null) InputSystem.RemoveDevice(left);
            Time.timeScale = oldScale; Time.captureDeltaTime = oldCapture; Time.fixedDeltaTime = oldFixed;
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null; GameState.SetCurrent(null); Object.Destroy(config);
            if (File.Exists(checkpoint)) File.Delete(checkpoint);
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/step16-navigation-fuzz.csv", evidence);
        }
        [UnityTest] public IEnumerator FiveMainRooms_RunFiveMinutesEach_WithRandomContactsAndWheelchairInput()
        {
            evidence.Add("scene,simulatedSeconds,frames,contacts,visits,chairMetres,chairDegrees,actor");
            string[] scenes = { "01_Infant", "03_Childhood", "04_Adolescence", "05_YoungAdult", "06_MiddleAge" };
            foreach (string scene in scenes)
            {
                yield return SceneManager.LoadSceneAsync(scene); yield return null; yield return null;
                Time.timeScale = 1; Time.captureDeltaTime = .05f; Time.fixedDeltaTime = .02f;
                var fly = Object.FindAnyObjectByType<FlyAgent>();
                var nav = fly != null ? fly.GetComponent<NavMeshAgent>() : null;
                var contact = fly != null ? fly.GetComponent<FlyContactReceiver>() : null;
                var chair = Object.FindAnyObjectByType<WheelchairLocomotion>();
                var room = Object.FindAnyObjectByType<RoomController>();
                var random = new System.Random(160016 + Array.IndexOf(scenes, scene));
                float start = Time.time, wallStart = Time.realtimeSinceStartup, nextContact = start + 95, nextSteer = start, still = 0;
                Vector3 previous = fly != null ? fly.transform.position : chair.transform.position;
                Vector3 chairTarget = new Vector3(12, 0, 9);
                Vector3 previousChair = chair.transform.position;
                float previousAngle = chair.transform.eulerAngles.y, chairMetres = 0, chairDegrees = 0;
                int frames = 0, contacts = 0;
                if (room != null)
                {
                    // Includes the infant north tablet slot: every interaction ring must be reachable on the actual baked mesh.
                    foreach (var slot in room.ItemSlots)
                    {
                        Assert.IsTrue(slot.TryGetNavigationPoint(nav.nextPosition, nav.agentTypeID, out var destination), scene + ":" + slot.ItemId);
                        var path = new NavMeshPath();
                        Assert.IsTrue(NavMesh.CalculatePath(nav.nextPosition, destination, nav.areaMask, path));
                        Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, scene + ":" + slot.ItemId);
                    }
                }
                while (Time.time - start < 300)
                {
                    Assert.Less(Time.realtimeSinceStartup - wallStart, 180, "Fuzz wall-clock watchdog");
                    if (scene == "06_MiddleAge" && Time.time - start < 60) chairTarget = new Vector3(12, 0, 9);
                    else if (Time.time >= nextSteer)
                    { chairTarget = new Vector3(1 + (float)random.NextDouble() * 22, 0, 1 + (float)random.NextDouble() * 20); nextSteer = Time.time + 8; }
                    var to = chairTarget - chair.transform.position;
                    float turn = Mathf.DeltaAngle(chair.transform.eulerAngles.y, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                    InputSystem.QueueDeltaStateEvent((StickControl)left["thumbstick"], new Vector2(Mathf.Clamp(turn / 25, -1, 1), Mathf.Abs(turn) < 35 ? 1 : 0));
                    if (fly != null && Time.time >= nextContact && (Time.time - start) % 60 < 30)
                    {
                        var direction = new Vector3((float)random.NextDouble() * 2 - 1, 0, (float)random.NextDouble() * 2 - 1).normalized;
                        contact.ReceiveContact(random.Next(2) == 0 ? PushSource.Wheelchair : PushSource.ElectricStick, direction, Time.time);
                        contacts++; nextContact = Time.time + 1.1f + (float)random.NextDouble() * 6;
                    }
                    yield return null; frames++;
                    chairMetres += Vector3.Distance(previousChair, chair.transform.position); previousChair = chair.transform.position;
                    chairDegrees += Mathf.Abs(Mathf.DeltaAngle(previousAngle, chair.transform.eulerAngles.y)); previousAngle = chair.transform.eulerAngles.y;
                    var actor = fly != null ? fly.transform : chair.transform;
                    var local = room != null ? room.transform.InverseTransformPoint(actor.position) : actor.position;
                    Assert.That(local.x, Is.InRange(0f, 24f), scene + " x");
                    Assert.That(local.z, Is.InRange(0f, 22f), scene + " z");
                    if (nav != null)
                    {
                        Assert.IsTrue(nav.isOnNavMesh, scene); Assert.IsFalse(nav.isOnOffMeshLink, scene);
                        if (fly.IsProtectingActivity) { still = 0; previous = actor.position; }
                        else if (Vector3.Distance(previous, actor.position) < .03f) still += Time.deltaTime;
                        else { still = 0; previous = actor.position; }
                        Assert.Less(still, 60, scene + " stalled");
                        foreach (var hit in Physics.OverlapSphere(actor.position, .12f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (hit.transform.IsChildOf(actor) || hit.transform.IsChildOf(chair.transform)) continue;
                            Assert.IsFalse(hit is BoxCollider && hit.bounds.size.y > .4f, scene + " inside collider " + hit.name);
                        }
                    }
                    Assert.That(chair.transform.position.x, Is.InRange(0f, 24f), scene + " chair x");
                    Assert.That(chair.transform.position.z, Is.InRange(0f, 22f), scene + " chair z");
                    foreach (var hit in Physics.OverlapSphere(chair.transform.position + Vector3.up * .65f, .2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform.IsChildOf(chair.transform) || (fly != null && hit.transform.IsChildOf(fly.transform))) continue;
                        Assert.IsFalse(hit is BoxCollider && hit.bounds.size.y > .4f, scene + " chair inside collider " + hit.name);
                    }
                }
                Assert.Greater(chairMetres, 1, scene + " wheelchair did not travel");
                Assert.Greater(chairDegrees, 10, scene + " wheelchair did not turn");
                evidence.Add(scene + "," + (Time.time - start).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + frames + "," + contacts + "," +
                    GameState.Current.Relationship.AutonomousVisitCount + "," + chairMetres.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + chairDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                    (fly != null ? "FlyAgent+wheelchair" : "wheelchair approach + seated rotation (no ending NavMesh actor)"));
                InputSystem.QueueDeltaStateEvent((StickControl)left["thumbstick"], Vector2.zero);
            }
        }
    }
}
