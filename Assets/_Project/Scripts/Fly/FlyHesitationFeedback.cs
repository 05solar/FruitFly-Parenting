using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Fly
{
    public sealed class FlyHesitationFeedback : MonoBehaviour
    {
        [SerializeField] FlyAgent fly;
        [SerializeField] Transform appearance;
        [SerializeField] GameBalanceConfig config;
        bool wasHesitating;
        public void Configure(FlyAgent actor, Transform visual, GameBalanceConfig balance)
        { fly = actor; appearance = visual; config = balance; }
        void Update()
        {
            if (appearance == null || fly == null) return;
            if (fly.IsHesitating && !wasHesitating && !Room.InfantRoomView.SpeakFor(fly,"음…")) Comfort.ComfortController.Announce("망설임");
            wasHesitating = fly.IsHesitating;
            appearance.localRotation = fly.IsHesitating ? Quaternion.Euler(0,0,
                Mathf.Sin(Time.time * Mathf.PI * 2f * config.hesitationSwayFrequency) * config.hesitationSwayDegrees) :
                fly.IsProtectingActivity ? Quaternion.Euler(0,15,0) : Quaternion.identity;
        }
    }
}
