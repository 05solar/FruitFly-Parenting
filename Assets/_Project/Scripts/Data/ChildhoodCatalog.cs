using UnityEngine;

namespace FruitFlyParenting.Data
{
    // Spec 5.3 values; positions are local to the reused 24 x 22m RoomShell.
    public static class ChildhoodCatalog
    {
        // The spec has no numerical coordinates for north-upper/south-lower slots.
        // (8,20)/(16,20)/(8,2)/(16,2) extend the outer rows; 1m furniture leaves >=1.5m wall clearance.
        // Normal slots retain 2.3, including west/east moved off the door axis to z=14 as in step 03.
        public static readonly RoomItemSpec[] Entries = {
            new RoomItemSpec("contest","경시대회 책상",new Vector2(5,17),new[]{"성과","공부"},new[]{10f,12,-2,2,0},"상장을 꼭 쥐지만 눈을 비벼요.","상장을 다시 보고 숨을 골라요."),
            new RoomItemSpec("craft","만들기 책상",new Vector2(12,18),new[]{"창작","공부"},new[]{1f,7,1,2,3},"실패한 종이비행기를 다시 접어요.","이번에는 다른 날개를 접어 봐요."),
            new RoomItemSpec("soccer","축구 골대",new Vector2(19,17),new[]{"운동","관계"},new[]{1f,0,10,5,2},"친구를 보고 공을 패스해요.","친구에게 다시 공을 굴려요."),
            new RoomItemSpec("library","도서관 쿠션",new Vector2(5,5),new[]{"공부","휴식"},new[]{-3f,6,0,0,1},"내가 고른 책을 천천히 읽어요.","좋아하는 페이지를 다시 펴요."),
            new RoomItemSpec("birthday","친구의 생일상",new Vector2(12,4),new[]{"관계"},new[]{-2f,1,0,9,5},"친구의 이름을 부르며 축하해요.","다음 생일에도 같이 놀기로 해요."),
            new RoomItemSpec("arcade","오락기",new Vector2(19,5),new[]{"놀이","소비"},new[]{-5f,1,-2,3,1},"게임을 마치고 웃으며 쉬어요.","익숙한 음악에 발을 까딱여요."),
            new RoomItemSpec("walk","동네 산책길",new Vector2(21,14),new[]{"운동","휴식"},new[]{-6f,1,7,0,2},"목적 없이 주변을 둘러봐요.","아까 못 본 구름을 찾아요."),
            new RoomItemSpec("family","가족 식탁",new Vector2(3,14),new[]{"관계","휴식"},new[]{-5f,0,2,0,8},"보호자와 첫 약속을 나눠요.","우리 약속을 다시 이야기해요."),
            new RoomItemSpec("gifted","전국 영재 선발 시험장 ★",new Vector2(8,20),new[]{"성과","공부"},new[]{20f,23,-9,7,-7},"큰 상을 받았지만 친구의 축하를 놓쳐요.","상을 바라보다 친구 쪽으로 고개를 돌려요.",-4),
            new RoomItemSpec("star","어린이 스타 촬영장 ★",new Vector2(16,20),new[]{"성과","관계"},new[]{17f,2,-13,22,-8},"팬들에게 손을 흔들고 집에서는 쓰러져 잠들어요.","밝은 조명 앞에서 잠시 눈을 감아요.",-4),
            new RoomItemSpec("hideout","숙제 찢기와 비밀 아지트 ★",new Vector2(8,2),new[]{"반항","놀이"},new[]{-16f,-11,4,8,-7},"숙제를 찢고 내가 정한 놀이 규칙에 웃어요.","우리만 아는 놀이를 다시 떠올려요.",-5,15),
            new RoomItemSpec("nightgame","몰래 밤새는 게임방 ★",new Vector2(16,2),new[]{"반항","소비"},new[]{-18f,-9,-17,11,-5},"친구들과 밤새 웃지만 다음 날 약속을 놓쳐요.","즐거웠던 밤과 놓친 약속을 함께 떠올려요.",-4,12)
        };
    }
}
