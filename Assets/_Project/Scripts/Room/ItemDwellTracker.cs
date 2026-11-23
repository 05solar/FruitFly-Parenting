namespace FruitFlyParenting.Room
{
    // ItemSlot의 Update() 생명주기에서 분리한 순수 로직(tech-stack.md 4장 원칙).
    // Unity 프레임 루프 없이 EditMode 테스트로 검증 가능하다.
    public class ItemDwellTracker
    {
        readonly float minDwellSeconds;

        bool isOccupied;
        float dwellTimer;
        bool visitTriggeredThisStay;
        public bool HasConfirmedVisit => visitTriggeredThisStay;

        public ItemDwellTracker(float minDwellSeconds)
        {
            this.minDwellSeconds = minDwellSeconds;
        }

        public void OnEnter()
        {
            isOccupied = true;
            dwellTimer = 0f;
            visitTriggeredThisStay = false;
        }

        public void OnExit()
        {
            isOccupied = false;
            dwellTimer = 0f;
            visitTriggeredThisStay = false;
        }

        // deltaTime을 누적하고, 이번 호출에서 막 minDwellSeconds를 넘겼으면 true를 "한 번만" 반환한다.
        public bool Tick(float deltaTime)
        {
            if (!isOccupied || visitTriggeredThisStay)
            {
                return false;
            }

            dwellTimer += deltaTime;
            if (dwellTimer >= minDwellSeconds)
            {
                visitTriggeredThisStay = true;
                return true;
            }

            return false;
        }
    }
}
