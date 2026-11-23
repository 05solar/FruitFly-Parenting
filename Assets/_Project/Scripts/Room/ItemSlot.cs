using System;
using FruitFlyParenting.Fly;
using UnityEngine;

namespace FruitFlyParenting.Room
{
    // 물품 상호작용 지점(스펙 2.3/4.2). 아직 S/I/H/P/L 효과는 붙지 않는다 — 04/05번에서 연결한다.
    [RequireComponent(typeof(SphereCollider))]
    public class ItemSlot : MonoBehaviour
    {
        [SerializeField] string itemId;
        [SerializeField] float interactionRadius = 1.2f;
        [SerializeField] float footprint = 2.5f; // 경고 영역 한 변 길이(정사각형), Gizmo 전용
        [SerializeField] float minDwellSeconds = 2f;

        public event Action OnFirstVisit;
        public event Action OnRevisit;

        public string ItemId => itemId;

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
                if (isFirstVisit)
                {
                    OnFirstVisit?.Invoke();
                }
                else
                {
                    OnRevisit?.Invoke();
                }
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FlyAgent>() == null) return;
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
