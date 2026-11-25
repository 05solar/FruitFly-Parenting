using System;
using UnityEngine;

namespace FruitFlyParenting.Data
{
    [Flags] public enum YoungAdultFlags { None=0, HighRiskContract=1, ContactBlocked=2, Resigned=4, GamblingBankruptcy=8 }
    public static class YoungAdultCatalog
    {
        static RoomItemSpec Item(string id,string name,float x,float z,string[] tags,float[] stats,string line,string revisit,float trust=0,float rebellion=0,YoungAdultFlags flags=YoungAdultFlags.None)
            => new RoomItemSpec(id,name,new Vector2(x,z),tags,stats,line,revisit,trust,rebellion,"youngadult_",false,flags);
        // Normal slots stay within 1m of the canonical layout; opposing offsets make the room feel rearranged.
        // Outer slots and north corner retain the tested 09/10 coordinates and 1.5m wall clearance.
        public static readonly RoomItemSpec[] Entries = {
            Item("interview","대기업 면접실",5.5f,16.5f,new[]{"성과","관계"},new[]{11f,3,-2,8,0},"떨리네. 준비한 만큼만 말하자.","붙으면 여기서 계속 일하게 되려나."),
            Item("studio","연구·창작 스튜디오",11.5f,18,new[]{"공부","창작"},new[]{5f,10,0,2,2},"드디어 됐다. 생각한 대로 나왔어.","여기만 조금 더 손보자."),
            Item("stadium","프로 경기장",19,16.5f,new[]{"운동","성과"},new[]{9f,0,10,6,0},"오늘은 꼭 내 실력 보여 줄 거야.","다음 경기, 같이 맞춰 보자."),
            Item("workshop","동네 작은 일터",4.5f,5.5f,new[]{"관계","성과"},new[]{2f,2,3,4,5},"어서 오세요. 늘 드시던 걸로요?","오늘도 잘 부탁해요."),
            Item("dinner","친구·연인의 저녁 식탁",12.5f,4.5f,new[]{"관계"},new[]{-5f,0,1,6,10},"밥부터 먹자. 오늘 어땠어?","다음 주에도 여기서 만날까?"),
            Item("investment","투자·과소비 계약서",19.5f,5.5f,new[]{"소비","성과"},new[]{12f,1,-3,3,-3},"수익은 좋아 보이는데… 감당되겠지?","이자까지 내면 얼마나 남지?",flags:YoungAdultFlags.HighRiskContract),
            Item("park","쉬어 가는 공원",21,14,new[]{"휴식","운동"},new[]{-10f,1,6,0,3},"휴… 잠깐 앉았다 가자.","이렇게 쉬니까 좀 살 것 같네."),
            Item("letter","부모에게 쓰는 편지",3,14,new[]{"관계","창작"},new[]{-4f,1,0,0,9},"전화로는 못 하겠어서 편지 써요.","밥은 잘 챙겨 먹고 있어요."),
            Item("startup","모든 것을 건 창업 계약 ★",8,20,new[]{"성과","소비"},new[]{25f,14,-12,20,-15},"계약은 했는데… 빚을 다 갚을 수 있을까.","이번 달 비용부터 다시 계산하자.",-6,0,YoungAdultFlags.HighRiskContract),
            Item("selection","세계 선수 선발전 ★",16,20,new[]{"운동","성과"},new[]{23f,0,25,15,-10},"내 이름 있어! 이걸 진짜 해냈네.","다들 잘 지내려나. 연락 좀 해야겠다.",-5),
            Item("independence","부모 연락 차단과 독립 선언 ★",8,2,new[]{"반항","관계"},new[]{-20f,2,3,7,-22},"당분간 연락하지 마. 혼자 있고 싶어.","연락할 준비는 아직 안 됐어.",-18,20,YoungAdultFlags.ContactBlocked),
            Item("quit","충동적인 여행과 퇴사 ★",16,2,new[]{"반항","휴식"},new[]{-25f,-5,9,6,-13},"그만뒀어. 일단 며칠 쉬려고.","이제 일은 어떻게 구하지?",-8,14,YoungAdultFlags.Resigned),
            Item("gambling","반짝이는 도박장 ★",3,20,new[]{"소비","반항"},new[]{25f,-12,-18,-22,-20},"생활비도 집도… 다 잃었어.","다시 하진 않을 거야. 도움부터 구하자.",-12,0,YoungAdultFlags.GamblingBankruptcy)
        };
    }
}
