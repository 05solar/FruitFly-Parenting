using UnityEngine;

namespace FruitFlyParenting.Room
{
    // 스펙 2.3의 좌표를 한 곳에서만 정의한다. Gizmo, 실제 ItemSlot 배치, 휠체어 클리어런스 검증,
    // EditMode 테스트가 모두 이 상수에서 파생되므로 "Gizmo 좌표와 실제 배치 좌표가 정확히 일치한다"는
    // 요구를 이중 입력 없이 구조적으로 보장한다.
    public static class RoomCoordinates
    {
        public const float RoomWidthX = 24f;
        public const float RoomDepthZ = 22f;
        public const float RoomHeight = 6f;
        public const float DoorWidth = 3f;

        public static readonly Vector2 WestEntrance = new Vector2(0f, 11f);
        public static readonly Vector2 EastExit = new Vector2(24f, 11f);
        public static readonly Vector2 WheelchairStart = new Vector2(12f, 9f);

        public static readonly (string id, Vector2 pos)[] ItemSlots = new (string id, Vector2 pos)[]
        {
            ("slot_nw", new Vector2(5f, 17f)),
            ("slot_nc", new Vector2(12f, 18f)),
            ("slot_ne", new Vector2(19f, 17f)),
            ("slot_sw", new Vector2(5f, 5f)),
            ("slot_sc", new Vector2(12f, 4f)),
            ("slot_se", new Vector2(19f, 5f)),
            // 서측/동측은 스펙 2.3 "배치 원칙"에 따라 입구-출구 중앙선(z=11)에서 3m 이상 벗어나도록
            // z를 14로 조정했다("출입구 중앙선에서 위아래 3m 이상 벗어난 상호작용 영역으로 조정" —
            // 그대로 z=11에 두면 입구→휠체어 시작점 직선 경로가 이 가구를 바로 관통한다. 03번 휠체어
            // 클리어런스 검증에서 실제로 걸려서 발견했다).
            ("slot_w", new Vector2(3f, 14f)),
            ("slot_e", new Vector2(21f, 14f)),
            ("slot_aux_n", new Vector2(8f, 20f)),
        };
    }
}
