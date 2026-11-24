using System.Collections.Generic;
using System.Linq;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using FruitFlyParenting.Tool;
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
        public PreferenceLearningTracker Learning { get; private set; }
        public ItemChoiceDecision LastChoice => movement?.LastDecision;

        float roomEnterTime;
        float arrivalTime;
        string currentDestinationId;
        bool hasArrivedAtCurrentDestination;
        Vector3 pushTarget;
        float pushEndsAt;
        bool beingPushed;
        bool complying;
        bool pendingExit;
        ItemSlot hesitationSlot;
        bool refusalPending;
        float refusalEndsAt;
        public event System.Action<ItemSlot> AutonomousApproachChosen;
        public bool IsHesitating => ((hesitationSlot != null && hesitationSlot.IsHesitating) || (refusalPending && Time.time<refusalEndsAt)) && !beingPushed;
        public void RefuseApproach(float seconds) { refusalPending=true; refusalEndsAt=Time.time+seconds; }
        public void BeginHesitation(ItemSlot slot) { hesitationSlot = slot; }
        public void EndHesitation(ItemSlot slot)
        {
            if (hesitationSlot != slot) return;
            hesitationSlot = null;
            if (agent != null && agent.isOnNavMesh && !beingPushed) agent.isStopped = false;
        }
        Vector3 complianceDirection;
        readonly List<ItemSlot> pushOriginSlots = new List<ItemSlot>();
        public bool IsBeingPushed => beingPushed;
        public bool IsComplying => complying;
        public Vector3 ComplianceDestination { get; private set; }
        public bool UsedComplianceFallback { get; private set; }
        public string CurrentDestinationId => currentDestinationId;
        float protectActivityUntil;
        public FruitFlyParenting.Corridor.LifeStage Stage=>room.Stage;
        public bool IsProtectingActivity=>Time.time<protectActivityUntil;
        public void PreserveCurrentActivityUntil(float until)
        {
            protectActivityUntil=Mathf.Max(protectActivityUntil,until);
            beingPushed=false; complying=false; pushOriginSlots.Clear(); Learning?.Reset();
            if(agent==null || !agent.isOnNavMesh) return;
            var slot=FindSlot(currentDestinationId); agent.isStopped=false;
            if(slot!=null && slot.TryGetNavigationPoint(agent.nextPosition,agent.agentTypeID,out var point)) agent.SetDestination(point);
            else if(currentDestinationId==RoomController.ExitDestinationId) agent.SetDestination(room.ExitPoint);
        }
        void OnEnable() => EventBus.RoomEntered += OnRoomEntered;
        void OnDisable() => EventBus.RoomEntered -= OnRoomEntered;
        void OnRoomEntered(string id) { roomEnterTime = Time.time; Learning?.Reset(); }

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
            if (config == null || room == null || !agent.isOnNavMesh)
            {
                Debug.LogError("FlyAgent: config, room 또는 베이크된 NavMesh가 없습니다.", this);
                enabled = false;
                return;
            }
            IRandomSource randomSource = GameState.Current != null
                ? new SystemRandomSource(GameState.Current.Random)
                : new SystemRandomSource(new System.Random());

            movement = new FlyAutonomousMovement(config, randomSource, GameState.Current);
            if (GameState.Current != null) Learning = new PreferenceLearningTracker(config, GameState.Current);
            roomEnterTime = Time.time;
            PickAndMoveToNext();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var hud = GetComponent<DebugHud>();
            if (hud == null) hud = gameObject.AddComponent<DebugHud>();
            hud.Configure(this, config);
#endif
        }

        void Update()
        {
            if (agent == null || movement == null || room == null || !agent.isOnNavMesh) return;
            if(IsProtectingActivity) { if(IsHesitating) agent.isStopped=true; return; }
            if(pendingExit && room.ExitAvailable && !complying) HeadToExit();
            if (refusalPending && Time.time>=refusalEndsAt)
            { refusalPending=false; agent.isStopped=false; PickAndMoveToNext(); }
            if (IsHesitating) { agent.isStopped = true; return; }
            if (complying)
            {
                RecordPushDepartures();
                if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
                    GuideCompliance(complianceDirection);
                return;
            }
            if (beingPushed)
            {
                float remaining = pushEndsAt - Time.time;
                var delta = (pushTarget - agent.nextPosition) * Mathf.Clamp01(Time.deltaTime / Mathf.Max(Time.deltaTime, remaining));
                var next = agent.nextPosition + delta;
                var heightOffset = Vector3.up * agent.baseOffset;
                if (NavMesh.Raycast(agent.nextPosition - heightOffset, next - heightOffset, out var edge, agent.areaMask)) next = edge.position + heightOffset;
                next.y = agent.nextPosition.y;
                agent.Move(next - agent.nextPosition);
                RecordPushDepartures();
                if (remaining <= 0f || (pushTarget - agent.nextPosition).sqrMagnitude < 0.0001f) ResumeAutonomy();
                return;
            }
            if (agent.pathPending) return;

            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                PickAndMoveToNext();
                return;
            }

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

        public void PickAndMoveToNext()
        {
            if (movement == null || !agent.isOnNavMesh || complying || beingPushed || IsProtectingActivity) return;
            var candidateIds = room.GetDestinationIds();
            if (candidateIds.Count == 0) return;
            var candidates = new List<ItemChoiceCandidate>();
            var points = new Dictionary<string, Vector3>();
            foreach (var group in candidateIds.GroupBy(id => id))
            {
                string id = group.Key;
                if (id == RoomController.ExitDestinationId)
                {
                    var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                    var path = new NavMeshPath();
                    if (NavMesh.SamplePosition(room.ExitPoint, out var exit, .5f, filter) &&
                        NavMesh.CalculatePath(agent.nextPosition, exit.position, filter, path) && path.status == NavMeshPathStatus.PathComplete)
                        candidates.Add(new ItemChoiceCandidate(id, tickets: group.Count()));
                    continue;
                }
                var slot = FindSlot(id);
                if (slot == null || !slot.TryGetNavigationPoint(agent.nextPosition, agent.agentTypeID, out var point)) continue;
                points.Add(id, point);
                float attraction=slot.Definition != null ? slot.Definition.baseAttraction : 0f;
                if (slot.Definition != null && slot.Definition.tags.Contains(TagDefinitions.Rest)) attraction+=room.RestAttractionBonus;
                candidates.Add(new ItemChoiceCandidate(id, slot.Definition?.tags, attraction));
            }
            if (candidates.Count == 0) { agent.ResetPath(); return; }
            float timeSinceRoomEnter = Time.time - roomEnterTime;
            string nextId = movement.PickNextDestination(candidates, currentDestinationId, timeSinceRoomEnter, room.Stage);

            if (nextId == RoomController.ExitDestinationId)
            {
                HeadToExit();
                return;
            }

            currentDestinationId = nextId;
            hasArrivedAtCurrentDestination = false;
            agent.isStopped = false;
            agent.SetDestination(points[nextId]);
            AutonomousApproachChosen?.Invoke(FindSlot(nextId));
        }

        ItemSlot FindSlot(string id)
        {
            foreach (var slot in room.ItemSlots)
            {
                if (slot.ItemId == id) return slot;
            }
            return null;
        }

        public void HeadToExit()
        {
            if (agent == null || !agent.isOnNavMesh || !room.ExitAvailable) return;
            if(IsProtectingActivity) { pendingExit=true; return; }
            if (complying) { pendingExit = true; return; }
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(room.ExitPoint, out var hit, 0.5f, filter)) return;
            pendingExit = false;
            RecordPushDepartures();
            pushOriginSlots.Clear();
            beingPushed = false;
            agent.isStopped = false;
            currentDestinationId = RoomController.ExitDestinationId;
            hasArrivedAtCurrentDestination = false;
            agent.SetDestination(hit.position);
        }

        public void ApplyPush(PushResult push)
        {
            if (agent == null || !agent.isOnNavMesh) return;
            // Nearest-point sampling alone can jump across walls. Clip the straight segment first.
            Vector3 heightOffset = Vector3.up * agent.baseOffset;
            Vector3 from = agent.nextPosition - heightOffset;
            Vector3 wanted = from + push.Direction * push.Force;
            if (NavMesh.Raycast(from, wanted, out var hit, agent.areaMask)) wanted = hit.position;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(wanted, out var sampled, 0.1f, filter)) return;
            CapturePushOrigins();
            pushTarget = sampled.position + heightOffset;
            pushEndsAt = Time.time + config.pushDurationSeconds;
            beingPushed = true;
            agent.isStopped = true;
            hasArrivedAtCurrentDestination = false;
        }

        public void GuideCompliance(Vector3 direction)
        {
            if (agent == null || !agent.isOnNavMesh) return;
            CapturePushOrigins();
            complying = true; beingPushed = false; complianceDirection = direction;
            var from = agent.nextPosition - Vector3.up * agent.baseOffset;
            if (ComplianceNavigation.TryFindDestination(from, direction, agent.agentTypeID, agent.areaMask,
                room.ItemSlots, config, out var destination, out bool fallback))
            {
                ComplianceDestination = destination; UsedComplianceFallback = fallback;
                agent.isStopped = false;
                agent.SetDestination(destination);
            }
            else { agent.ResetPath(); agent.isStopped = true; }
        }

        public void ReleaseCompliance()
        {
            complying = false;
            ResumeAutonomy();
        }

        public void ResumeAutonomy()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            RecordPushDepartures();
            pushOriginSlots.Clear();
            if (pendingExit || room.ExitOnly || currentDestinationId == RoomController.ExitDestinationId) { HeadToExit(); return; }
            beingPushed = false;
            agent.isStopped = false;
            hasArrivedAtCurrentDestination = false;
            // 이전 물품을 금지하지 않는다. 밀려나도 자유 의지로 돌아갈 수 있다.
            var slot = FindSlot(currentDestinationId);
            if (slot != null && slot.TryGetNavigationPoint(agent.nextPosition, agent.agentTypeID, out var point))
                agent.SetDestination(point);
            else if (movement != null) PickAndMoveToNext();
        }

        void RecordPushDepartures()
        {
            var body = GetComponent<Collider>();
            if (body == null) return;
            for (int i=pushOriginSlots.Count-1;i>=0;i--)
            {
                var slot = pushOriginSlots[i];
                if (slot == null) { pushOriginSlots.RemoveAt(i); continue; }
                if (OverlapsSlot(body,slot,agent.nextPosition)) continue;
                // OnTriggerExit는 다음 물리 틱에 도착할 수 있다. 실제 밀기 위치에서 먼저 기록해
                // 한 프레임에 끝나는 밀기도 놓치지 않으며, 벽에 막힌 밀기는 이탈로 세지 않는다.
                var state = GameState.Current;
                if (state != null) state.LogEvent(Time.time,state.CurrentRoomId,"item_pushed_away",slot.ItemId);
                if (state != null) Learning?.ConfirmPushedAway(state.CurrentRoomId, slot.ItemId, Time.time);
                pushOriginSlots.RemoveAt(i);
            }
        }
        bool OverlapsSlot(Collider body, ItemSlot slot, Vector3 position)
        {
            var region = slot.GetComponent<SphereCollider>();
            return Physics.ComputePenetration(body,position,transform.rotation,region,region.transform.position,region.transform.rotation,out _,out _);
        }

        void CapturePushOrigins()
        {
            RecordPushDepartures();
            pushOriginSlots.Clear();
            var body = GetComponent<Collider>();
            if (body == null) return;
            foreach (var slot in room.ItemSlots)
                if (slot.Definition != null && OverlapsSlot(body, slot, agent.nextPosition)) pushOriginSlots.Add(slot);
        }
        public void RecordLearningContact(PushSource source, float now)
        {
            if (Learning == null || GameState.Current == null) return;
            var body = GetComponent<Collider>();
            var intended = body != null ? room.ItemSlots.FirstOrDefault(slot =>
                slot.Definition != null && slot.HasConfirmedCurrentStay && OverlapsSlot(body, slot, agent.nextPosition)) : null;
            if (intended == null) intended = FindSlot(currentDestinationId);
            var direction = intended != null ? intended.transform.position - transform.position : Vector3.zero;
            Learning.RecordContact(GameState.Current.CurrentRoomId, intended?.Definition, direction, source, now,
                GameState.Current.Stats.GetValue(StatType.Stress));
        }
        public void ConfirmLearningArrival(ItemSlot slot, float now)
        {
            if (GameState.Current != null) Learning?.ConfirmArrival(GameState.Current.CurrentRoomId, slot.ItemId, now);
        }
    }
}
