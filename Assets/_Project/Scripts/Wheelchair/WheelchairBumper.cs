using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Tool;
using UnityEngine;

namespace FruitFlyParenting.Wheelchair
{
    public sealed class WheelchairBumper : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] WheelchairLocomotion locomotion;
        WheelchairBumperPushResolver resolver;
        public void Configure(GameBalanceConfig balance, WheelchairLocomotion movement) { config = balance; locomotion = movement; }
        void OnEnable() { if (config != null) resolver = new WheelchairBumperPushResolver(config); }
        void OnTriggerEnter(Collider other) => Contact(other);
        void OnTriggerStay(Collider other) => Contact(other);
        void Contact(Collider other)
        {
            var target = other.GetComponentInParent<FlyContactReceiver>();
            if (target == null || resolver == null || locomotion == null) return;
            Vector3 velocity = locomotion.ActualVelocity;
            if (resolver.TryContact(true, velocity.magnitude, Time.time))
                target.ReceiveContact(PushSource.Wheelchair, -velocity.normalized, Time.time);
        }
    }
}
