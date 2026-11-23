using FruitFlyParenting.Room;
using NUnit.Framework;

namespace FruitFlyParenting.Tests.EditMode
{
    public class ItemDwellTrackerTests
    {
        [Test]
        public void Tick_ReturnsTrueExactlyOnce_WhenThresholdCrossed()
        {
            var tracker = new ItemDwellTracker(2f);
            tracker.OnEnter();

            Assert.IsFalse(tracker.Tick(1f), "1초 누적, 아직 2초 미만이므로 false");
            Assert.IsTrue(tracker.Tick(1.5f), "누적 2.5초, 이번에 임계를 넘겼으므로 true");
            Assert.IsFalse(tracker.Tick(1f), "이미 이번 체류에서 트리거됐으므로 재호출되면 안 됨");
        }

        [Test]
        public void OnExit_BeforeThreshold_ResetsTimer()
        {
            var tracker = new ItemDwellTracker(2f);
            tracker.OnEnter();
            tracker.Tick(1.9f);
            tracker.OnExit();
            tracker.OnEnter();

            Assert.IsFalse(tracker.Tick(1.9f), "리셋 후에는 처음부터 다시 누적해야 한다");
            Assert.IsTrue(tracker.Tick(0.2f));
        }

        [Test]
        public void ReEntering_AfterCompletedVisit_CanTriggerAgain()
        {
            var tracker = new ItemDwellTracker(2f);
            tracker.OnEnter();
            Assert.IsTrue(tracker.Tick(2f));

            tracker.OnExit();
            tracker.OnEnter();

            Assert.IsTrue(tracker.Tick(2f),
                "재방문 시에도 Tick은 다시 true를 낼 수 있어야 한다(첫방문/재방문 구분은 RoomController 책임)");
        }

        [Test]
        public void Tick_WhileNotOccupied_AlwaysFalse()
        {
            var tracker = new ItemDwellTracker(2f);
            Assert.IsFalse(tracker.Tick(5f));
        }
    }
}
