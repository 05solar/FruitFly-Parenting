using System.Collections.Generic;
using UnityEngine;

namespace FruitFlyParenting.Room
{
    // "단일 영속 씬 + 방 컨테이너 활성화" 구조의 최소 구현(tech-stack.md 3장).
    // 06번에서 복도/방 전환 로직이 이 컨트롤러를 확장한다.
    public class RoomController : MonoBehaviour
    {
        public const string ExitDestinationId = "__exit";
        public bool ExitAvailable { get; private set; }
        public bool ExitOnly { get; private set; }
        public Vector3 ExitPoint { get; private set; }
        int exitChoiceWeight;
        public void ConfigureExit(bool available, bool onlyExit, Vector3 point, int weight)
        { ExitAvailable = available; ExitOnly = onlyExit; ExitPoint = point; exitChoiceWeight = Mathf.Max(1, weight); }

        public List<string> GetDestinationIds()
        {
            var ids = ExitOnly && ExitAvailable ? new List<string>() : GetAllItemIds();
            if (ExitAvailable)
                for (int i = 0; i < exitChoiceWeight; i++) ids.Add(ExitDestinationId);
            return ids;
        }
        readonly HashSet<string> visitedItemIds = new HashSet<string>();

        public IReadOnlyList<ItemSlot> ItemSlots { get; private set; } = System.Array.Empty<ItemSlot>();

        void Awake()
        {
            RefreshItemSlots();
        }

        public void RefreshItemSlots()
        {
            ItemSlots = GetComponentsInChildren<ItemSlot>(true);
        }

        // ItemSlot이 최초 방문인지 판정하고, 최초 방문이면 기록한다. 방 단위로 리셋된다(ResetVisits).
        public bool TryMarkFirstVisit(string itemId) => visitedItemIds.Add(itemId);

        public void ResetVisits() => visitedItemIds.Clear();

        public IReadOnlyCollection<string> VisitedItemIds => visitedItemIds;

        public List<string> GetAllItemIds()
        {
            var ids = new List<string>(ItemSlots.Count);
            foreach (var slot in ItemSlots)
            {
                ids.Add(slot.ItemId);
            }
            return ids;
        }
    }
}
