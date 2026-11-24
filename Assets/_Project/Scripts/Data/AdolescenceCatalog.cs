using UnityEngine;

namespace FruitFlyParenting.Data
{
    public static class AdolescenceCatalog
    {
        static RoomItemSpec Item(string id,string name,float x,float z,string[] tags,float[] stats,string line,string revisit,float trust=0,float rebellion=0,bool delinquency=false)
            => new RoomItemSpec(id,name,new Vector2(x,z),tags,stats,line,revisit,trust,rebellion,"adolescence_",delinquency);
        // Four outer slots retain step 09. The unspecified north corner is (3,20):
        // 2.5m footprints do not overlap; 1m furniture leaves >=1.5m to the outer wall.
        public static readonly RoomItemSpec[] Entries = {
            Item("exam","입시 책상",5,17,new[]{"성과","공부"},new[]{12f,12,-3,0,-2},"밤새 공부하고 작은 합격증을 받아요.","합격증을 바라보다 잠깐 눈을 감아요."),
            Item("club","동아리 작업대",12,18,new[]{"창작","관계"},new[]{2f,6,0,6,4},"친구와 프로젝트를 완성해요.","같이 만든 것을 다시 고쳐 봐요."),
            Item("stadium","경기장",19,17,new[]{"운동","성과"},new[]{7f,1,11,5,0},"경기가 끝나고 친구와 악수해요.","이기든 지든 다음 경기를 준비해요."),
            Item("counsel","상담실",5,5,new[]{"휴식","관계"},new[]{-10f,1,1,0,5},"친구에게 마음을 말할 용기가 생겨요.","이번에는 보호자에게도 말해 볼래요."),
            Item("stars","친구와 옥상 별보기",12,4,new[]{"관계","휴식"},new[]{-6f,2,0,8,6},"친구 하늘이와 같은 별을 찾아요.","하늘이에게 다음 약속을 건네요."),
            Item("nightgame","무단 밤샘 놀이",19,5,new[]{"놀이","소비"},new[]{-4f,-2,-7,5,1},"밤에는 웃었지만 다음 날 자꾸 졸아요.","즐거운 밤과 피곤한 아침을 함께 떠올려요."),
            Item("job","아르바이트 계산대",21,14,new[]{"성과","관계"},new[]{5f,3,4,3,1},"처음 스스로 번 돈을 세어 봐요.","내가 한 일을 천천히 이야기해요."),
            Item("parents","부모와 대화 의자",3,14,new[]{"관계"},new[]{-5f,0,0,0,9},"이번에는 내 말을 끝까지 들어 주세요.","우리 이야기를 조금 더 이어 가요."),
            Item("camp","전교 1등 합숙반 ★",8,20,new[]{"성과","공부"},new[]{24f,25,-15,-5,-9},"빛나는 성적표 옆에 놓친 연락이 쌓여요.","친구의 연락을 뒤늦게 읽어요.",-5),
            Item("training","전국 선발 훈련소 ★",16,20,new[]{"운동","성과"},new[]{20f,0,23,8,-8},"이름을 알렸지만 다친 몸은 쉬어야 해요.","오늘은 몸의 이야기도 들어요.",-4),
            Item("performance","교칙에 맞서는 공개 공연 ★",8,2,new[]{"반항","창작"},new[]{13f,3,-5,20,-10},"내 노래를 부르고 학교와 보호자의 반발을 마주해요.","내 노래를 다른 방식으로도 전하고 싶어요.",-7,18),
            Item("dayoff","학교를 빠지고 떠난 하루 ★",16,2,new[]{"반항","휴식"},new[]{-22f,-12,3,5,-12},"숨을 돌렸지만 놓친 수업과 가족의 걱정이 남아요.","다음에는 떠나기 전에 말하고 싶어요.",-9,20),
            Item("theft","친구 무리의 절도 제안 ★",3,20,new[]{"반항","관계"},new[]{-10f,-7,-2,15,-18},"훔친 일이 드러나 가족의 신뢰를 잃어요.","일어난 일을 없애지는 못해도 다음 선택은 남아 있어요.",-8,20,true)
        };
    }
}
