using System.Collections;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace FruitFlyParenting.Tests.PlayMode
{
    // 03-room-shell-and-navmesh.md 완료 조건: "FlyAgent가 5분 이상 돌아다녀도 벽/가구에 끼거나
    // 통과하지 않는다"를 사람이 지켜보는 대신 Time.timeScale을 높여 자동으로 검증한다.
    public class FlyAgentNavigationTests
    {
        GameObject roomGo;
        GameObject flyGo;
        float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;

            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            config.statDefaultValue = 50f;
            config.statMin = 0f;
            config.statMax = 100f;
            config.trustDefaultValue = 50f;
            config.trustMin = 0f;
            config.trustMax = 100f;
            // 스펙 실제 값(2분 자유 로밍, 30초 최대 체류)을 그대로 쓴다 — timeScale로 시간만 압축한다.
            config.freeRoamDurationSeconds = 120f;
            config.randomExplorationFloor = 0.35f;
            config.maxItemDwellSeconds = 30f;

            GameState.SetCurrent(new GameState(config, 999));

            roomGo = RoomShellBuilder.BuildAndBake();

            flyGo = new GameObject("FlyAgent");
            flyGo.transform.position = new Vector3(RoomCoordinates.WheelchairStart.x, 0.3f, RoomCoordinates.WheelchairStart.y);

            var navAgent = flyGo.AddComponent<NavMeshAgent>();
            navAgent.radius = 0.25f;
            navAgent.height = 0.6f;
            navAgent.speed = 2.5f;

            var rb = flyGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var capsule = flyGo.AddComponent<CapsuleCollider>();
            capsule.radius = 0.25f;
            capsule.height = 0.6f;
            capsule.center = new Vector3(0f, 0.3f, 0f);

            var flyAgent = flyGo.AddComponent<FlyAgent>();
            flyAgent.ConfigureForBuilder(config, roomGo.GetComponent<RoomController>());
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            if (flyGo != null) Object.Destroy(flyGo);
            if (roomGo != null) Object.Destroy(roomGo);
            GameState.SetCurrent(null);
        }

        [UnityTest]
        public IEnumerator FlyAgent_WandersForSimulatedFiveMinutes_WithoutLeavingRoomBoundsOrStalling()
        {
            // timeScale 10배 → 실제 300초(5분) 분량의 게임 로직이 약 30초 안에 끝난다.
            Time.timeScale = 10f;

            const float targetSimulatedSeconds = 300f;
            // 무한 대기 방지용 안전장치. nographics 배치 모드는 렌더링이 없어 프레임당 실제 시간이
            // 매우 짧다(실측 ~0.0001초/프레임) — timeScale=10에서 300초를 채우려면 대략 30만 프레임
            // 안팎이 필요하므로 여유를 넉넉히 둔다(그래도 실제 소요 시간은 수십 초 수준).
            const int maxFrames = 1000000;

            float simulatedSeconds = 0f;
            int frame = 0;
            Vector3 lastPosition = flyGo.transform.position;
            float stillTimer = 0f;

            while (simulatedSeconds < targetSimulatedSeconds)
            {
                yield return null;
                frame++;
                simulatedSeconds += Time.deltaTime;

                Vector3 pos = flyGo.transform.position;

                Assert.GreaterOrEqual(pos.x, -0.5f, "초파리가 서쪽 벽 바깥으로 나감(통과 의심)");
                Assert.LessOrEqual(pos.x, RoomCoordinates.RoomWidthX + 0.5f, "초파리가 동쪽 벽 바깥으로 나감(통과 의심)");
                Assert.GreaterOrEqual(pos.z, -0.5f, "초파리가 남쪽 벽 바깥으로 나감(통과 의심)");
                Assert.LessOrEqual(pos.z, RoomCoordinates.RoomDepthZ + 0.5f, "초파리가 북쪽 벽 바깥으로 나감(통과 의심)");

                // 장시간(시뮬레이션 기준 60초 이상) 같은 자리에 멈춰 있으면 "끼임"으로 간주한다.
                if (Vector3.Distance(pos, lastPosition) < 0.01f)
                {
                    stillTimer += Time.deltaTime;
                    Assert.Less(stillTimer, 60f, $"frame {frame}: 60초(시뮬레이션) 이상 움직이지 않음 — 벽/가구에 끼인 것으로 의심됨");
                }
                else
                {
                    stillTimer = 0f;
                    lastPosition = pos;
                }

                if (frame > maxFrames)
                {
                    Assert.Fail($"{maxFrames} 프레임을 넘겼는데도 {targetSimulatedSeconds}초 시뮬레이션이 끝나지 않음");
                }
            }
        }
    }
}
