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
            new RoomItemSpec("contest","경시대회 책상",new Vector2(5,17),new[]{"성과","공부"},new[]{10f,12,-2,2,0},"나 상 받았어! 이제 놀아도 돼?","다음에도 잘해야 되는데…"),
            new RoomItemSpec("craft","만들기 책상",new Vector2(12,18),new[]{"창작","공부"},new[]{1f,7,1,2,3},"어? 안 날아가네. 다시 접어야지.","이번엔 날개를 크게 해 볼까?"),
            new RoomItemSpec("soccer","축구 골대",new Vector2(19,17),new[]{"운동","관계"},new[]{1f,0,10,5,2},"여기! 내가 패스할게!","이번엔 내가 골 넣을 거야!"),
            new RoomItemSpec("library","도서관 쿠션",new Vector2(5,5),new[]{"공부","휴식"},new[]{-3f,6,0,0,1},"이 책 재밌겠다!","여기 봐! 이 장면 진짜 웃겨."),
            new RoomItemSpec("birthday","친구의 생일상",new Vector2(12,4),new[]{"관계"},new[]{-2f,1,0,9,5},"생일 축하해! 선물 열어 봐!","내 생일에도 꼭 와야 돼!"),
            new RoomItemSpec("arcade","오락기",new Vector2(19,5),new[]{"놀이","소비"},new[]{-5f,1,-2,3,1},"와, 깼다! 너도 해 볼래?","이 판은 내가 잘해!"),
            new RoomItemSpec("walk","동네 산책길",new Vector2(21,14),new[]{"운동","휴식"},new[]{-6f,1,7,0,2},"저 구름 강아지 같지 않아?","이쪽 길로 가 보고 싶어!"),
            new RoomItemSpec("family","가족 식탁",new Vector2(3,14),new[]{"관계","휴식"},new[]{-5f,0,2,0,8},"오늘 학교에서 무슨 일 있었냐면!","다 먹으면 같이 놀아 줄 거지?"),
            new RoomItemSpec("gifted","전국 영재 선발 시험장 ★",new Vector2(8,20),new[]{"성과","공부"},new[]{20f,23,-9,7,-7},"나 뽑혔대! 근데 친구들은 어디 갔지?","친구들이랑 놀 시간은 없어?",-4),
            new RoomItemSpec("star","어린이 스타 촬영장 ★",new Vector2(16,20),new[]{"성과","관계"},new[]{17f,2,-13,22,-8},"사진 또 찍어? 나 졸린데…","오늘은 집에 일찍 가면 안 돼?",-4),
            new RoomItemSpec("hideout","숙제 찢기와 비밀 아지트 ★",new Vector2(8,2),new[]{"반항","놀이"},new[]{-16f,-11,4,8,-7},"숙제 안 해! 여기서 놀 거야!","여기는 우리만 아는 비밀이야.",-5,15),
            new RoomItemSpec("nightgame","몰래 밤새는 게임방 ★",new Vector2(16,2),new[]{"반항","소비"},new[]{-18f,-9,-17,11,-5},"한 판만 더! 아직 안 졸려!","아, 약속 있었는데… 늦었다!",-4,12)
        };
    }
}
