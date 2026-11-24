namespace FruitFlyParenting.Ending
{
    public static class EndingCatalog
    {
        public static readonly string[] Titles={"빈 지갑의 귀가","마법소녀의 약속","조폭이 된 자녀","육아 대성공","육아 성공","빚쟁이 육아","비어 있는 생일 의자","집 밖의 큰 이름","캥거루족","반대편의 팀","작은 식탁의 불빛","자기만의 도시","길어진 쉼표","다시 열린 문"};
        public static readonly string[] Lines={"잃은 것은 남아 있어요. 다음 하루부터 다시 시작할래요.","내가 좋아해서 고른 길이야.","자랑하러 왔는데, 예전의 내가 생각났어요.","이 열쇠를 함께 쓰고 싶어요.","다음 여행은 우리 둘이 함께 가요.","내가 진 빚을 함께 이야기할 수 있을까요?","아직 하지 못한 말이 남아 있어요.","멀리서도 내 소식을 듣고 있나요?","여기서 다음 일을 찾아볼게요.","오늘은 승부보다 우리 이야기가 좋아요.","다음 저녁에도 함께 먹어요.","내가 만든 것과 이 도시의 이야기를 보낼게요.","잠시만 곁에 있어 줄래요?","늦었지만, 내 이야기를 들려주고 싶어요."};
        public static readonly string[] Scenes={"생활비와 집을 잃은 아이가 문 앞에 섰다.","별빛 옷의 아이가 밤하늘로 손을 내민다.","조직의 동료들과 온 아이는 옛 사진 앞에서 돌아선다.","아이가 새 집의 열쇠를 건넨다.","평범한 일상 사이로 여행 표 두 장이 놓였다.","빚을 적은 서류와 함께 문을 두드린다.","생일의 촛불이 작아진다. 전화는 울리지 않는다.","뉴스에는 아이의 이름이, 방에는 빈 의자가 남았다.","같이 지낼 짐을 내려놓고 구직 달력을 편다.","함께 경기를 보며 대화를 나눈다.","정기적으로 함께하는 작은 저녁 식탁.","먼 도시에서 작품과 편지가 도착한다.","일을 멈춘 아이와 조용히 회복을 기다린다.","몇 년 뒤 문이 열리고 새로운 이야기가 시작된다."};
        public static readonly string[] Calendars={"다시 시작하는 하루","별을 보러 가는 밤","사진 속 오래전 날","함께 살 첫날","둘이 떠나는 날","빚을 이야기할 날","비어 있는 생일","뉴스가 나온 날","다음 일을 찾는 날","같이 보는 경기","다음 저녁 약속","작품이 도착한 날","쉬어 가는 날","몇 년 뒤, 오늘"};
        public static void Configure(EndingDefinition d,int priority)
        {
            int i=priority-1; d.priority=priority; d.condition=(EndingCondition)priority; d.endingId="ending_"+priority.ToString("00");
            d.title=Titles[i]; d.childLine=Lines[i]; d.sceneLine=Scenes[i]; d.calendarText=Calendars[i];
            d.letterbox=(priority==7 || priority==3) ? LetterboxState.Empty : priority==12 ? LetterboxState.Overflowing : LetterboxState.Letter;
            d.suitcase=(priority==1 || priority==9) ? SuitcaseState.Worn : (priority==2 || priority==5 || priority==12) ? SuitcaseState.Packed : SuitcaseState.Absent;
            d.handle=priority==4 ? HandleState.Key : (priority==3 || priority==7 || priority==8 || priority==12) ? HandleState.Closed : HandleState.Open;
            d.childPresent=priority!=7 && priority!=8 && priority!=12;
            d.lightColor=priority==2 ? new UnityEngine.Color(.65f,.65f,1) : (priority==7 || priority==8) ? new UnityEngine.Color(.65f,.75f,.85f) : new UnityEngine.Color(1,.82f,.58f);
        }
    }
}
