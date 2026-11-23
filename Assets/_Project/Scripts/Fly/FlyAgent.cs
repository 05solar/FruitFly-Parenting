using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using UnityEngine;
using UnityEngine.AI;

namespace FruitFlyParenting.Fly
{
    // NavMeshAgent 래퍼. 의사결정(어디로 갈지, 언제 강제로 다시 고를지)은
    // FlyAutonomousMovement(순수 클래스)에 맡기고, 이 클래스는 실제 좌표 질의/SetDestination만 수행한다.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    public class FlyAgent : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] RoomController room;

        NavMeshAgent agent;
        FlyAutonomousMovement movement;

        float roomEnterTime;
        float arrivalTime;
        string currentDestinationId;
        bool hasArrivedAtCurrentDestination;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 에디터/런타임 빌더가 공통으로 쓰는 설정 경로(SerializedObject를 쓰지 않음 → PlayMode에서도 호출 가능).
        public void ConfigureForBuilder(GameBalanceConfig gameBalanceConfig, RoomController roomController)
        {
            config = gameBalanceConfig;
            room = roomController;
        }

        void Start()
        {
            IRandomSource randomSource = GameState.Current != null
                ? new SystemRandomSource(GameState.Current.Random)
                : new SystemRandomSource(new System.Random());

            movement = new FlyAutonomousMovement(config, randomSource);
            roomEnterTime = Time.time;
            PickAndMoveToNext();
        }

        void Update()
        {
            if (agent == null || movement == null || room == null) return;
            if (agent.pathPending) return;

            if (!hasArrivedAtCurrentDestination && agent.remainingDistance <= agent.stoppingDistance + 0.01f)
            {
                hasArrivedAtCurrentDestination = true;
                arrivalTime = Time.time;
            }

            if (hasArrivedAtCurrentDestination)
            {
                float dwell = Time.time - arrivalTime;
                if (movement.ShouldForceRepick(dwell))
                {
                    PickAndMoveToNext();
                }
            }
        }

        void PickAndMoveToNext()
        {
            var candidateIds = room.GetAllItemIds();
            if (candidateIds.Count == 0) return;

            float timeSinceRoomEnter = Time.time - roomEnterTime;
            string nextId = movement.PickNextDestination(candidateIds, currentDestinationId, timeSinceRoomEnter);

            ItemSlot targetSlot = FindSlot(nextId);
            if (targetSlot == null) return;

            currentDestinationId = nextId;
            hasArrivedAtCurrentDestination = false;
            agent.SetDestination(targetSlot.transform.position);
        }

        ItemSlot FindSlot(string id)
        {
            foreach (var slot in room.ItemSlots)
            {
                if (slot.ItemId == id) return slot;
            }
            return null;
        }
    }
}
