using System.Collections.Generic;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using UnityEngine;

namespace FruitFlyParenting.Tool
{
    public enum CoercionKind { None, Wheelchair, ElectricStick }

    public readonly struct ItemVisitContext
    {
        public readonly bool WasComplying;
        public readonly CoercionKind Coercion;
        public bool IsForced => WasComplying || Coercion != CoercionKind.None;
        public ItemVisitContext(bool complying, CoercionKind coercion)
        { WasComplying = complying; Coercion = coercion; }
    }

    // Resolved push directions, not the opposite (fly-to-tool) contact normal.
    public sealed class CoercionDetector
    {
        readonly GameBalanceConfig config;
        readonly List<Contact> contacts = new List<Contact>();
        readonly struct Contact
        {
            public readonly float Time;
            public readonly PushSource Source;
            public readonly Vector3 Position, Direction;
            public Contact(float time, PushSource source, Vector3 position, Vector3 direction)
            { Time = time; Source = source; Position = position; Direction = direction; }
        }
        public CoercionDetector(GameBalanceConfig config) { this.config = config; }
        public void Reset() => contacts.Clear();
        public void Record(float now, PushSource source, Vector3 position, Vector3 direction)
        {
            contacts.RemoveAll(c => now - c.Time > config.coercionDetectionWindowSeconds);
            contacts.Add(new Contact(now, source, position, Vector3.ProjectOnPlane(direction, Vector3.up).normalized));
        }
        public CoercionKind Evaluate(float now, Vector3 itemPosition)
        {
            contacts.RemoveAll(c => now - c.Time > config.coercionDetectionWindowSeconds);
            var result = CoercionKind.None;
            foreach (var c in contacts)
            {
                var toward = Vector3.ProjectOnPlane(itemPosition - c.Position, Vector3.up);
                if (c.Time > now || c.Direction.sqrMagnitude == 0f || toward.sqrMagnitude == 0f ||
                    Vector3.Angle(c.Direction, toward) > config.coercionDirectionToleranceDegrees) continue;
                if (c.Source == PushSource.ElectricStick) return CoercionKind.ElectricStick;
                result = CoercionKind.Wheelchair;
            }
            return result;
        }
        public static bool IsTrainingItem(ItemDefinition item, GameBalanceConfig config)
        {
            if (item == null) return false;
            float intelligence = 0f, health = 0f;
            foreach (var delta in item.statDeltas)
            {
                if (delta.stat == StatType.Intelligence) intelligence += delta.amount;
                if (delta.stat == StatType.Health) health += delta.amount;
            }
            return intelligence >= config.trainingMinimumBenefit || health >= config.trainingMinimumBenefit;
        }
    }
}
