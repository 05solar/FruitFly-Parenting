using UnityEngine;

namespace FruitFlyParenting.Room
{
    // 에디터에서만 보이는 좌표 검증용 Gizmo(스펙 2.3). 05/09/10/11번에서 물품 배치 확인용으로 재사용한다.
    public class RoomCoordinateGizmos : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            foreach (var (_, pos) in RoomCoordinates.ItemSlots)
            {
                Vector3 world = transform.TransformPoint(new Vector3(pos.x, 0.5f, pos.y));
                Gizmos.DrawWireSphere(world, 0.3f);
            }

            Gizmos.color = Color.cyan;
            Vector3 startWorld = transform.TransformPoint(
                new Vector3(RoomCoordinates.WheelchairStart.x, 0.5f, RoomCoordinates.WheelchairStart.y));
            Gizmos.DrawWireSphere(startWorld, 0.35f);

            Gizmos.color = Color.green;
            Vector3 entranceWorld = transform.TransformPoint(
                new Vector3(RoomCoordinates.WestEntrance.x, 0.5f, RoomCoordinates.WestEntrance.y));
            Vector3 exitWorld = transform.TransformPoint(
                new Vector3(RoomCoordinates.EastExit.x, 0.5f, RoomCoordinates.EastExit.y));
            Gizmos.DrawWireSphere(entranceWorld, 0.3f);
            Gizmos.DrawWireSphere(exitWorld, 0.3f);
        }
    }
}
