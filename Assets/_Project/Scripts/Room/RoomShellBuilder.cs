using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace FruitFlyParenting.Room
{
    // 런타임에서도 안전하게 호출 가능한 그레이박스 방 생성 로직(스펙 2.2~2.5).
    // 에디터 툴(Setup03Tools)과 PlayMode 테스트가 이 클래스를 공유해서
    // "테스트가 검증하는 지오메트리"와 "실제 저장되는 프리팹의 지오메트리"가 어긋나지 않게 한다.
    public static class RoomShellBuilder
    {
        public const float WallThickness = 0.3f;
        // 북측 보조 슬롯(z=20)과 북벽(z=22) 사이에도 1.5m 통로를 남긴다.
        public const float FurnitureSize = 1f;
        public const float FurnitureHeight = 1.8f;

        public static GameObject BuildAndBake()
        {
            GameObject root = new GameObject("RoomShell");
            root.AddComponent<RoomController>();
#if UNITY_EDITOR
            root.AddComponent<RoomCoordinateGizmos>();
#endif

            CreateBox(
                "Floor", root.transform,
                new Vector3(RoomCoordinates.RoomWidthX / 2f, -0.1f, RoomCoordinates.RoomDepthZ / 2f),
                new Vector3(RoomCoordinates.RoomWidthX, 0.2f, RoomCoordinates.RoomDepthZ));

            BuildWalls(root.transform);
            BuildFurnitureAndSlots(root.transform);

            var navSurface = root.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.Children;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.BuildNavMesh();

            root.GetComponent<RoomController>().RefreshItemSlots();

            return root;
        }

        static void BuildWalls(Transform parent)
        {
            float w = RoomCoordinates.RoomWidthX;
            float d = RoomCoordinates.RoomDepthZ;
            float h = RoomCoordinates.RoomHeight;
            float t = WallThickness;
            float doorHalf = RoomCoordinates.DoorWidth / 2f;
            float doorCenterZ = RoomCoordinates.WestEntrance.y; // == EastExit.y == 11

            // 남/북 벽: 문이 없어 전체 길이 한 조각으로 충분하다.
            CreateBox("Wall_South", parent, new Vector3(w / 2f, h / 2f, -t / 2f), new Vector3(w + t * 2f, h, t));
            CreateBox("Wall_North", parent, new Vector3(w / 2f, h / 2f, d + t / 2f), new Vector3(w + t * 2f, h, t));

            float southSegLen = doorCenterZ - doorHalf;
            float northSegLen = d - (doorCenterZ + doorHalf);

            // 서쪽 벽(입구): 문 폭만큼 간격을 두고 남/북 두 조각으로 나눈다.
            CreateBox("Wall_West_South", parent,
                new Vector3(-t / 2f, h / 2f, southSegLen / 2f), new Vector3(t, h, southSegLen));
            CreateBox("Wall_West_North", parent,
                new Vector3(-t / 2f, h / 2f, doorCenterZ + doorHalf + northSegLen / 2f), new Vector3(t, h, northSegLen));

            // 동쪽 벽(출구): 동일하게 2분할.
            CreateBox("Wall_East_South", parent,
                new Vector3(w + t / 2f, h / 2f, southSegLen / 2f), new Vector3(t, h, southSegLen));
            CreateBox("Wall_East_North", parent,
                new Vector3(w + t / 2f, h / 2f, doorCenterZ + doorHalf + northSegLen / 2f), new Vector3(t, h, northSegLen));
        }

        static void BuildFurnitureAndSlots(Transform parent)
        {
            GameObject slotsRoot = new GameObject("ItemSlots");
            slotsRoot.transform.SetParent(parent, false);

            foreach (var (id, pos) in RoomCoordinates.ItemSlots)
            {
                GameObject slotGo = new GameObject($"ItemSlot_{id}");
                slotGo.transform.SetParent(slotsRoot.transform, false);
                slotGo.transform.localPosition = new Vector3(pos.x, 0.3f, pos.y);

                slotGo.AddComponent<SphereCollider>();
                var itemSlot = slotGo.AddComponent<ItemSlot>();
                itemSlot.ConfigureForBuilder(id, 1.2f, 2.5f, 2f);

                GameObject furniture = GameObject.CreatePrimitive(PrimitiveType.Cube);
                furniture.name = $"Furniture_{id}";
                furniture.transform.SetParent(slotsRoot.transform, false);
                furniture.transform.localPosition = new Vector3(pos.x, FurnitureHeight / 2f, pos.y);
                furniture.transform.localScale = new Vector3(FurnitureSize, FurnitureHeight, FurnitureSize);
                var modifier = furniture.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = NavMesh.GetAreaFromName("Not Walkable");
            }
        }

        static GameObject CreateBox(string name, Transform parent, Vector3 localCenter, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            return go;
        }
    }
}
