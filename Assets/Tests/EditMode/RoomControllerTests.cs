using FruitFlyParenting.Room;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    public class RoomControllerTests
    {
        [Test]
        public void TryMarkFirstVisit_TrueOnlyOnce_UntilReset()
        {
            var go = new GameObject("TestRoom");
            var room = go.AddComponent<RoomController>();

            Assert.IsTrue(room.TryMarkFirstVisit("item_a"), "최초 방문은 true여야 한다");
            Assert.IsFalse(room.TryMarkFirstVisit("item_a"), "재방문은 false여야 한다");

            room.ResetVisits();
            Assert.IsTrue(room.TryMarkFirstVisit("item_a"), "리셋 후에는 다시 최초 방문으로 취급되어야 한다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryMarkFirstVisit_TracksEachItemIdIndependently()
        {
            var go = new GameObject("TestRoom");
            var room = go.AddComponent<RoomController>();

            Assert.IsTrue(room.TryMarkFirstVisit("item_a"));
            Assert.IsTrue(room.TryMarkFirstVisit("item_b"));
            Assert.IsFalse(room.TryMarkFirstVisit("item_a"));

            Object.DestroyImmediate(go);
        }
    }
}
