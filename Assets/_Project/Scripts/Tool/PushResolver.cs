using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Tool
{
    public readonly struct PushResult
    {
        public readonly Vector3 Direction;
        public readonly float Force;
        public PushResult(Vector3 direction, float force) { Direction = direction; Force = force; }
    }

    public sealed class PushResolver
    {
        readonly GameBalanceConfig config;
        public PushResolver(GameBalanceConfig config) { this.config = config; }

        // contactDirection은 아이에서 접촉 소스를 향한다. 휠체어는 -진행방향을 전달한다.
        // Transform/프레임/물리에 의존하지 않는다. Force는 NavMesh 밀림 거리로 해석한다.
        public PushResult Resolve(PushSource source, Vector3 contactDirection, Vector3 targetPosition)
        {
            var direction = -Vector3.ProjectOnPlane(contactDirection, Vector3.up).normalized;
            if (source == PushSource.ElectricStick) EventBus.RaiseElectricShockVisual(targetPosition);
            return new PushResult(direction, source == PushSource.Wheelchair ? config.wheelchairPushForce : config.strongPushForce);
        }
    }
}
