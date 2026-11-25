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
            Item("exam","입시 책상",5,17,new[]{"성과","공부"},new[]{12f,12,-3,0,-2},"붙었다… 아, 이제 좀 자자.","붙긴 했는데 왜 이렇게 피곤하지."),
            Item("club","동아리 작업대",12,18,new[]{"창작","관계"},new[]{2f,6,0,6,4},"야, 우리 이거 진짜 완성했네.","여기만 고치면 더 괜찮겠다."),
            Item("stadium","경기장",19,17,new[]{"운동","성과"},new[]{7f,1,11,5,0},"수고했어. 아까 패스 좋더라.","다음 경기엔 실수 안 해야지."),
            Item("counsel","상담실",5,5,new[]{"휴식","관계"},new[]{-10f,1,1,0,5},"요즘 좀 힘들어요. 어디서부터 말하지…","집에서도 한번 얘기해 볼게요."),
            Item("stars","친구와 옥상 별보기",12,4,new[]{"관계","휴식"},new[]{-6f,2,0,8,6},"하늘아, 저 별 보여? 엄청 밝다.","다음에도 여기서 보자."),
            Item("nightgame","무단 밤샘 놀이",19,5,new[]{"놀이","소비"},new[]{-4f,-2,-7,5,1},"벌써 아침이야? 아, 망했다.","재밌긴 했는데… 너무 졸려."),
            Item("job","아르바이트 계산대",21,14,new[]{"성과","관계"},new[]{5f,3,4,3,1},"이게 내가 번 돈이네.","오늘 손님 진짜 많았어."),
            Item("parents","부모와 대화 의자",3,14,new[]{"관계"},new[]{-5f,0,0,0,9},"잠깐만. 내 말부터 끝까지 들어 줘.","아까 하던 얘기, 좀 더 해도 돼?"),
            Item("camp","전교 1등 합숙반 ★",8,20,new[]{"성과","공부"},new[]{24f,25,-15,-5,-9},"1등이다… 답장할 틈도 없었네.","연락 못 해서 미안. 요즘 정신없었어.",-5),
            Item("training","전국 선발 훈련소 ★",16,20,new[]{"운동","성과"},new[]{20f,0,23,8,-8},"선발됐어! 근데 발목이 좀 아파.","오늘은 쉬어야겠어. 아직 아프네.",-4),
            Item("performance","교칙에 맞서는 공개 공연 ★",8,2,new[]{"반항","창작"},new[]{13f,3,-5,20,-10},"혼나도 할래. 이건 내 노래니까.","다음엔 어디서 부르면 좋을까?",-7,18),
            Item("dayoff","학교를 빠지고 떠난 하루 ★",16,2,new[]{"반항","휴식"},new[]{-22f,-12,3,5,-12},"오늘은 학교 못 가겠어. 좀 쉬고 싶어.","걱정했지? 말 안 하고 나가서 미안.",-9,20),
            Item("theft","친구 무리의 절도 제안 ★",3,20,new[]{"반항","관계"},new[]{-10f,-7,-2,15,-18},"들켰어… 나 왜 그랬지.","내가 가져갔어. 돌려줄게. 미안해.",-8,20,true)
        };
    }
}
