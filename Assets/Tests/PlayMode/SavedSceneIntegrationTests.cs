using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using NUnit.Framework;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FruitFlyParenting.Tests.PlayMode
{
    // 저장된 씬을 그대로 로드한다. 테스트 중 재베이크하면 저장 누락을 숨길 수 있다.
    public class SavedSceneIntegrationTests
    {
        RoomController room;
        FlyAgent fly;
        NavMeshAgent agent;
        float originalTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTimeScale = Time.timeScale;
            yield return SceneManager.LoadSceneAsync("03_RoomShellTest");
            room = Object.FindAnyObjectByType<RoomController>();
            fly = Object.FindAnyObjectByType<FlyAgent>();
            agent = fly.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(room.GetComponent<NavMeshSurface>().navMeshData);
            Assert.IsTrue(agent.isOnNavMesh);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = originalTimeScale;
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            GameState.SetCurrent(null);
        }

        [UnityTest]
        public IEnumerator SavedScene_FlyWandersFiveMinutes_WithoutCollisionsOrStalling()
        {
            Time.timeScale = 10f;
            float start = Time.time;
            float deadline = Time.realtimeSinceStartup + 90f;
            float stillTimer = 0f;
            Vector3 last = fly.transform.position;
            var visited = new HashSet<string>();
            foreach (var slot in room.ItemSlots) slot.OnFirstVisit += () => visited.Add(slot.ItemId);
            var capsule = fly.GetComponent<CapsuleCollider>();
            var obstacles = room.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToArray();
            while (Time.time - start < 300f)
            {
                yield return null;
                Assert.Less(Time.realtimeSinceStartup, deadline, "Five-minute simulation timed out.");
                Assert.IsTrue(agent.isOnNavMesh);
                var pos = fly.transform.position;
                Assert.That(pos.x, Is.InRange(0f, 24f));
                Assert.That(pos.z, Is.InRange(0f, 22f));
                Assert.Less(pos.y, 0.8f, "Fly climbed onto furniture.");
                if (!agent.pathPending) Assert.AreEqual(NavMeshPathStatus.PathComplete, agent.pathStatus);
                foreach (var obstacle in obstacles)
                    Assert.IsFalse(Physics.ComputePenetration(capsule, pos, fly.transform.rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth) && depth > 0.02f,
                        $"Fly penetrated {obstacle.name}.");
                if (Vector3.Distance(pos, last) < 0.01f) stillTimer += Time.deltaTime;
                else { stillTimer = 0f; last = pos; }
                Assert.Less(stillTimer, 40f, "Fly stalled longer than the 30-second item dwell.");
            }
            Assert.GreaterOrEqual(visited.Count, 2, "Movement must result in actual item visits.");
            Debug.Log($"Saved scene: simulated 300 seconds; {visited.Count} distinct item visits; no penetration or stalling.");
        }

        [Test]
        public void SavedNavMesh_AllFloorTrianglesAndSlotsAreReachable()
        {
            var triangles = NavMesh.CalculateTriangulation();
            Assert.IsNotEmpty(triangles.indices);
            var path = new NavMeshPath();
            for (int i = 0; i < triangles.indices.Length; i += 3)
            {
                var center = (triangles.vertices[triangles.indices[i]] + triangles.vertices[triangles.indices[i + 1]] +
                    triangles.vertices[triangles.indices[i + 2]]) / 3f;
                Assert.Less(center.y, 0.2f, "Furniture tops must not form separate navigation islands.");
                Assert.IsTrue(NavMesh.CalculatePath(agent.nextPosition, center, NavMesh.AllAreas, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"Unreachable navigation triangle at {center}");
            }
            foreach (var slot in room.ItemSlots)
            {
                Assert.IsTrue(slot.TryGetNavigationPoint(agent.nextPosition, agent.agentTypeID, out var point), slot.ItemId);
                Assert.Less(Vector3.Distance(point, slot.transform.position), slot.InteractionRadius);
                Debug.Log($"{slot.ItemId}: center={slot.transform.position}, reachable approach={point}");
            }
            Debug.Log($"NavMesh: {triangles.indices.Length / 3} triangles, one connected floor region, all 9 slots reachable.");
        }

        [Test]
        public void WheelchairRoutes_HavePhysicalClearanceAndOpenExitView()
        {
            Physics.SyncTransforms();
            Vector3 start = new Vector3(12f, 0f, 9f);
            var routes = new List<(Vector3 from, Vector3 to)>
            {
                (new Vector3(0f, 0f, 11f), start), (start, new Vector3(24f, 0f, 11f)),
            };
            foreach (var slot in room.ItemSlots)
            {
                var point = slot.transform.position;
                point.y = 0f;
                point.z += point.z > 11f ? -1.6f : 1.6f;
                routes.Add((start, point));
                AssertClear(point, point, 1f); // 2m turning diameter
            }
            foreach (var route in routes)
            {
                AssertClear(route.from, route.to, 0.4f); // 0.8m wheelchair proxy
                AssertClear(route.from, route.to, 0.75f); // 1.5m clear passage
            }
            AssertClear(start, start, 1.5f); // central 3x3m area
            var view = new Vector3(24f, 1.2f, 11f) - (start + Vector3.up * 1.2f);
            Assert.IsFalse(Physics.RaycastAll(start + Vector3.up * 1.2f, view.normalized, view.magnitude,
                ~0, QueryTriggerInteraction.Ignore).Any(hit => hit.transform.IsChildOf(room.transform)));
        }

        void AssertClear(Vector3 from, Vector3 to, float radius)
        {
            Vector3 bottom = from + Vector3.up * (radius + 0.02f);
            Vector3 top = from + Vector3.up * Mathf.Max(radius + 0.02f, 2f - radius);
            Assert.IsFalse(Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore)
                .Any(c => c.transform.IsChildOf(room.transform)), $"No {radius * 2}m clearance at {from}");
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return;
            Assert.IsFalse(Physics.CapsuleCastAll(bottom, top, radius, delta.normalized, delta.magnitude, ~0,
                QueryTriggerInteraction.Ignore).Any(hit => hit.transform.IsChildOf(room.transform)),
                $"Blocked {radius * 2}m route: {from} -> {to}");
        }

        [UnityTest]
        public IEnumerator ItemTrigger_FirstVisitRevisitAndRoomReset_WorkThroughPhysics()
        {
            fly.enabled = false;
            agent.isStopped = true;
            agent.ResetPath();
            var slot = room.ItemSlots[0];
            int first = 0, revisits = 0;
            slot.OnFirstVisit += () => first++;
            slot.OnRevisit += () => revisits++;
            Assert.IsTrue(slot.TryGetNavigationPoint(agent.nextPosition, agent.agentTypeID, out var point));
            Assert.IsTrue(agent.Warp(point));
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, first);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(1, first);
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(1, first);
            Assert.AreEqual(0, revisits);
            Assert.IsTrue(agent.Warp(new Vector3(12f, 0f, 9f)));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(agent.Warp(point));
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(1, revisits);
            Assert.IsTrue(agent.Warp(new Vector3(12f, 0f, 9f)));
            yield return new WaitForFixedUpdate();
            room.ResetVisits();
            Assert.IsTrue(agent.Warp(point));
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(2, first);
        }

        [UnityTest]
        public IEnumerator RoomCamera_ProducesRenderedGeometry()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Requires a graphics device; run without -nographics.");
            var camera = Object.FindAnyObjectByType<XROrigin>().Camera;
            var target = new RenderTexture(640, 360, 24);
            var pixels = new Texture2D(640, 360, TextureFormat.RGB24, false);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < 5; i++) yield return null;
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                pixels.Apply();
                var colors = pixels.GetPixels();
                Assert.Greater(colors.Max(c => c.grayscale) - colors.Min(c => c.grayscale), 0.05f,
                    "Frame is blank or has no visible geometry.");
                Assert.Less(colors.Count(c => c.r > 0.9f && c.b > 0.9f && c.g < 0.1f), colors.Length / 100,
                    "Missing shader (magenta) pixels detected.");
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/room-render.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator BootstrapScene_HasWorkingXRRigAndGameState()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameStateBootstrap>()) Object.Destroy(bootstrap.gameObject);
            yield return null;
            yield return SceneManager.LoadSceneAsync("00_Bootstrap");
            yield return null;
            Assert.IsNotNull(GameState.Current);
            Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<Camera>().Length);
            Assert.IsTrue(Camera.main.isActiveAndEnabled);
        }
    }
}
