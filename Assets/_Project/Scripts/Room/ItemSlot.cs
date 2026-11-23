using System;
using FruitFlyParenting.Fly;
using FruitFlyParenting.Data;
using FruitFlyParenting.Tool;
using UnityEngine;
using UnityEngine.AI;

namespace FruitFlyParenting.Room
{
    // 물품 상호작용 지점. 05 방 컨트롤러가 방문 이벤트를 효과/서사/저장에 연결한다.
    [RequireComponent(typeof(SphereCollider))]
    public class ItemSlot : MonoBehaviour
    {
        [SerializeField] string itemId;
        [SerializeField] ItemDefinition definition;
        [SerializeField] float interactionRadius = 1.2f;
        [SerializeField] float footprint = 2.5f; // 경고 영역 한 변 길이(정사각형), Gizmo 전용
        [SerializeField] float minDwellSeconds = 2f;

        public event Action OnFirstVisit;
        public event Action OnRevisit;

        public string ItemId => itemId;
        public ItemDefinition Definition => definition;
        public void AssignDefinition(ItemDefinition item) { definition = item; itemId = item.itemId; }
        public float InteractionRadius => interactionRadius;
        public ItemVisitContext LastVisitContext { get; private set; }
        public bool HasConfirmedCurrentStay => dwellTracker != null && dwellTracker.HasConfirmedVisit;
        FlyAgent occupant;

        // 슬롯 중심에는 가구가 있으므로, 상호작용 반경 안의 바닥 중 도달 가능한 점을 고른다.
        public bool TryGetNavigationPoint(Vector3 from, int agentTypeId, out Vector3 destination)
        {
            destination = default;
            var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
            var path = new NavMeshPath();
            float closest = float.PositiveInfinity;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector3 candidate = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (interactionRadius * 0.8f);
                candidate.y = 0f;
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.2f, filter) ||
                    !NavMesh.CalculatePath(from, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
                float distance = (from - hit.position).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                destination = hit.position;
            }
            return !float.IsPositiveInfinity(closest);
        }

        RoomController room;
        ItemDwellTracker dwellTracker;

        void Awake()
        {
            room = GetComponentInParent<RoomController>();
            ApplyColliderSettings();
            if (dwellTracker == null)
            {
                dwellTracker = new ItemDwellTracker(minDwellSeconds);
            }
        }

        // 에디터/런타임 빌더(RoomShellBuilder, Setup03Tools)가 공통으로 쓰는 설정 경로.
        // SerializedObject를 쓰지 않으므로 런타임(PlayMode)에서도 그대로 호출할 수 있다.
        public void ConfigureForBuilder(string id, float radius, float footprintSize, float dwellSeconds)
        {
            itemId = id;
            interactionRadius = radius;
            footprint = footprintSize;
            minDwellSeconds = dwellSeconds;
            dwellTracker = new ItemDwellTracker(minDwellSeconds);
            ApplyColliderSettings();
        }

        void ApplyColliderSettings()
        {
            var sphere = GetComponent<SphereCollider>();
            if (sphere == null) return;
            sphere.isTrigger = true;
            sphere.radius = interactionRadius;
        }

        void Update()
        {
            if (dwellTracker != null && dwellTracker.Tick(Time.deltaTime))
            {
                bool isFirstVisit = room != null && room.TryMarkFirstVisit(itemId);
                occupant?.ConfirmLearningArrival(this, Time.time);
                var receiver = occupant != null ? occupant.GetComponent<FlyContactReceiver>() : null;
                LastVisitContext = receiver != null ? receiver.PrepareItemVisit(this, isFirstVisit, Time.time) : default;
                if (isFirstVisit)
                {
                    OnFirstVisit?.Invoke();
                }
                else
                {
                    OnRevisit?.Invoke();
                }
                receiver?.CompleteItemVisit(LastVisitContext);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            var fly = other.GetComponentInParent<FlyAgent>();
            if (fly == null) return;
            occupant = fly;
            dwellTracker?.OnEnter();
        }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<FlyAgent>() == null) return;
            dwellTracker?.OnExit();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(footprint, 0.1f, footprint));
            Gizmos.color = new Color(0f, 0.6f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
