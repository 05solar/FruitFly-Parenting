using System.Collections.Generic;
using FruitFlyParenting.Room;
using NUnit.Framework;
using UnityEngine;

namespace FruitFlyParenting.Tests.EditMode
{
    // 스펙 2.3 좌표 표를 코드에 고정해, Gizmo/실제 배치가 표에서 어긋나면 이 테스트가 바로 잡아낸다.
    public class RoomCoordinatesTests
    {
        [Test]
        public void ItemSlotCoordinates_MatchSpecTable()
        {
            var expected = new Dictionary<string, Vector2>
            {
                { "slot_nw", new Vector2(5f, 17f) },
                { "slot_nc", new Vector2(12f, 18f) },
                { "slot_ne", new Vector2(19f, 17f) },
                { "slot_sw", new Vector2(5f, 5f) },
                { "slot_sc", new Vector2(12f, 4f) },
                { "slot_se", new Vector2(19f, 5f) },
                // 스펙 2.3 "배치 원칙": 서측/동측은 입구-출구 중앙선(z=11)에서 3m 이상 벗어나야 하므로
                // z=14로 조정된 값을 검증한다(RoomCoordinates.cs 주석 참고).
                { "slot_w", new Vector2(3f, 14f) },
                { "slot_e", new Vector2(21f, 14f) },
                { "slot_aux_n", new Vector2(8f, 20f) },
            };

            Assert.AreEqual(expected.Count, RoomCoordinates.ItemSlots.Length);

            foreach (var (id, pos) in RoomCoordinates.ItemSlots)
            {
                Assert.IsTrue(expected.ContainsKey(id), $"알 수 없는 슬롯 id: {id}");
                Assert.AreEqual(expected[id], pos, $"슬롯 {id} 좌표 불일치");
            }
        }

        [Test]
        public void WheelchairStartAndDoors_MatchSpec()
        {
            Assert.AreEqual(new Vector2(0f, 11f), RoomCoordinates.WestEntrance);
            Assert.AreEqual(new Vector2(24f, 11f), RoomCoordinates.EastExit);
            Assert.AreEqual(new Vector2(12f, 9f), RoomCoordinates.WheelchairStart);
        }

        [Test]
        public void RoomDimensions_MatchSpec()
        {
            Assert.AreEqual(24f, RoomCoordinates.RoomWidthX);
            Assert.AreEqual(22f, RoomCoordinates.RoomDepthZ);
            Assert.AreEqual(6f, RoomCoordinates.RoomHeight);
            Assert.AreEqual(3f, RoomCoordinates.DoorWidth);
        }
    }
}
