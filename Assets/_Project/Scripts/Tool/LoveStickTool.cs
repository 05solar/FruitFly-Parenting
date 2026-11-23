using System.Collections.Generic;
using FruitFlyParenting.Core;
using UnityEngine;

namespace FruitFlyParenting.Tool
{
    // 오른손 고정 봉의 끝 트리거에만 부착한다. 버튼·휠체어 입력 경로가 없다.
    public sealed class LoveStickTool : MonoBehaviour
    {
        readonly ContactEpisodeTracker<EntityId, EntityId> episodes = new ContactEpisodeTracker<EntityId, EntityId>();
        readonly Dictionary<Collider, EntityId> active = new Dictionary<Collider, EntityId>();
        readonly List<Collider> stale = new List<Collider>();
        void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<FlyContactReceiver>();
            if (target == null) return;
            var id = target.GetEntityId();
            active[other] = id;
            if (!episodes.Enter(id, other.GetEntityId())) return;
            Vector3 towardTool = transform.position - target.transform.position;
            if (Vector3.ProjectOnPlane(towardTool, Vector3.up).sqrMagnitude < 0.0001f) towardTool = -transform.forward;
            target.ReceiveContact(PushSource.ElectricStick, towardTool, Time.time);
        }
        void OnTriggerExit(Collider other) => RemoveContact(other);
        void RemoveContact(Collider other)
        {
            if (!active.TryGetValue(other, out var targetId)) return;
            episodes.Exit(targetId, other.GetEntityId());
            active.Remove(other);
        }
        void FixedUpdate()
        {
            // 비활성화/파괴는 OnTriggerExit 없이 일어날 수 있다.
            stale.Clear();
            foreach (var contact in active)
                if (contact.Key == null || !contact.Key.enabled || !contact.Key.gameObject.activeInHierarchy) stale.Add(contact.Key);
            foreach (var collider in stale) RemoveContact(collider);
        }
        void OnDisable() { episodes.Clear(); active.Clear(); }
    }
}
