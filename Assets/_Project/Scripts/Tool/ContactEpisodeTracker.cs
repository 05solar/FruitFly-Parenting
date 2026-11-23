using System.Collections.Generic;

namespace FruitFlyParenting.Tool
{
    // 하나의 아이가 여러 충돌체를 가져도 하나의 접촉으로 센다. 시간 쿨다운은 없다.
    public sealed class ContactEpisodeTracker<TTarget, TCollider>
    {
        readonly Dictionary<TTarget, HashSet<TCollider>> contacts = new Dictionary<TTarget, HashSet<TCollider>>();
        public bool Enter(TTarget targetId, TCollider colliderId)
        {
            if (!contacts.TryGetValue(targetId, out var colliders))
                contacts[targetId] = colliders = new HashSet<TCollider>();
            bool wasEmpty = colliders.Count == 0;
            return colliders.Add(colliderId) && wasEmpty;
        }
        public void Exit(TTarget targetId, TCollider colliderId)
        {
            if (!contacts.TryGetValue(targetId, out var colliders)) return;
            colliders.Remove(colliderId);
            if (colliders.Count == 0) contacts.Remove(targetId);
        }
        public void Clear() => contacts.Clear();
    }
}
