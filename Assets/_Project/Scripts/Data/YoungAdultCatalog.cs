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
            Item("interview","대기업 면접실",5.5f,16.5f,new[]{"성과","관계"},new[]{11f,3,-2,8,0},"큰 기회 앞에서 긴 숨을 골라요.","내가 하고 싶은 일을 다시 생각해요."),
            Item("studio","연구·창작 스튜디오",11.5f,18,new[]{"공부","창작"},new[]{5f,10,0,2,2},"오래 품은 아이디어가 모양을 얻어요.","어제의 아이디어를 조금 더 다듬어요."),
            Item("stadium","프로 경기장",19,16.5f,new[]{"운동","성과"},new[]{9f,0,10,6,0},"팀에서 내 자리를 얻으려 애써요.","동료와 다음 경기를 준비해요."),
            Item("workshop","동네 작은 일터",4.5f,5.5f,new[]{"관계","성과"},new[]{2f,2,3,4,5},"가까운 곳에서 일과 사람을 돌봐요.","익숙한 사람들과 하루를 시작해요."),
            Item("dinner","친구·연인의 저녁 식탁",12.5f,4.5f,new[]{"관계"},new[]{-5f,0,1,6,10},"누군가와 함께 미래를 이야기해요.","다음 저녁의 약속을 나눠요."),
            Item("investment","투자·과소비 계약서",19.5f,5.5f,new[]{"소비","성과"},new[]{12f,1,-3,3,-3},"큰 수익의 가능성과 빚의 위험에 서명해요.","계약에 남은 책임을 다시 읽어요.",flags:YoungAdultFlags.HighRiskContract),
            Item("park","쉬어 가는 공원",21,14,new[]{"휴식","운동"},new[]{-10f,1,6,0,3},"아무것도 증명하지 않고 잠시 쉬어요.","다시 숨을 고르고 내 속도를 찾아요."),
            Item("letter","부모에게 쓰는 편지",3,14,new[]{"관계","창작"},new[]{-4f,1,0,0,9},"내 말로 편지를 쓰기 시작해요.","다음 이야기를 편지에 덧붙여요."),
            Item("startup","모든 것을 건 창업 계약 ★",8,20,new[]{"성과","소비"},new[]{25f,14,-12,20,-15},"투자 발표가 끝나고 빚의 무게를 느껴요.","새 일의 가능성과 책임을 함께 바라봐요.",-6,0,YoungAdultFlags.HighRiskContract),
            Item("selection","세계 선수 선발전 ★",16,20,new[]{"운동","성과"},new[]{23f,0,25,15,-10},"후보 명단에 이름을 올리고 멀어진 사람들을 떠올려요.","경기장 밖의 시간도 돌아봐요.",-5),
            Item("independence","부모 연락 차단과 독립 선언 ★",8,2,new[]{"반항","관계"},new[]{-20f,2,3,7,-22},"연락을 끊고 내 생활을 시작해요.","연락할지 말지는 다시 내가 생각해 볼래요.",-18,20,YoungAdultFlags.ContactBlocked),
            Item("quit","충동적인 여행과 퇴사 ★",16,2,new[]{"반항","휴식"},new[]{-25f,-5,9,6,-13},"일터를 떠나 새로운 도시를 걸어요.","다음에 어떤 일을 할지 생각해요.",-8,14,YoungAdultFlags.Resigned),
            Item("gambling","반짝이는 도박장 ★",3,20,new[]{"소비","반항"},new[]{25f,-12,-18,-22,-20},"작은 당첨 뒤 시간이 흘러 생활비와 집을 잃었어요.","그때의 손실은 남아 있어요. 다음 선택을 생각해요.",-12,0,YoungAdultFlags.GamblingBankruptcy)
        };
    }
}
