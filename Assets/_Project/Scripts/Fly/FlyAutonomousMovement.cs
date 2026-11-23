using System;
using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Fly
{
    // 순수 C# 로직(스펙 4.2): NavMesh/Unity API에 의존하지 않는다.
    // 좌표 계산과 실제 이동 호출은 FlyAgent(MonoBehaviour 어댑터)가 맡고,
    // 이 클래스는 "다음에 어디로 갈지"와 "언제 강제로 다시 고를지"만 결정한다.
    public class FlyAutonomousMovement
    {
        readonly GameBalanceConfig config;
        readonly IRandomSource random;

        public FlyAutonomousMovement(GameBalanceConfig config, IRandomSource random)
        {
            this.config = config;
            this.random = random;
        }

        // 방 진입 후 freeRoamDurationSeconds 이전까지는 선호 없이 100% 무작위다.
        public bool IsFreeRoamPhase(float timeSinceRoomEnter) =>
            timeSinceRoomEnter < config.freeRoamDurationSeconds;

        // 한 목적지에 maxItemDwellSeconds 이상 머물면 강제로 다른 지점을 고른다.
        public bool ShouldForceRepick(float dwellSeconds) =>
            dwellSeconds >= config.maxItemDwellSeconds;

        // candidateIds: 현재 방에서 고를 수 있는 목적지 id 목록(지금 단계에서는 ItemSlot id들).
        // currentId: 방금까지 있던/향하던 목적지(연속 재선택 금지 대상). null/빈 문자열이면 제약 없음.
        //
        // 08번 이전까지는 선호 가중치 시스템이 없으므로 2분 이전/이후 모두 균등 무작위로 고른다.
        // randomExplorationFloor(GameBalanceConfig)는 08번에서 가중치 기반 선택을 섞을 때 쓸 하한 자리다.
        public string PickNextDestination(IReadOnlyList<string> candidateIds, string currentId, float timeSinceRoomEnter)
        {
            if (candidateIds == null || candidateIds.Count == 0)
            {
                throw new ArgumentException("candidateIds가 비어 있습니다.", nameof(candidateIds));
            }

            List<string> filtered = string.IsNullOrEmpty(currentId)
                ? new List<string>(candidateIds)
                : candidateIds.Where(id => id != currentId).ToList();

            // 후보가 현재 위치뿐이라면(= 선택지가 하나뿐) 예외적으로 재선택을 허용한다.
            if (filtered.Count == 0)
            {
                filtered = new List<string>(candidateIds);
            }

            int index = random.NextInt(filtered.Count);
            return filtered[index];
        }
    }
}
