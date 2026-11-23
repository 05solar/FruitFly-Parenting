using FruitFlyParenting.Data;
using FruitFlyParenting.Room;
using UnityEngine;
using UnityEngine.AI;

namespace FruitFlyParenting.Fly
{
    // Engine queries are separate from the pure time/direction state machine.
    public static class ComplianceNavigation
    {
        public static bool TryFindDestination(Vector3 from, Vector3 direction, int agentType, int areaMask,
            System.Collections.Generic.IReadOnlyList<ItemSlot> slots, GameBalanceConfig config, out Vector3 destination, out bool usedFallback)
        {
            destination = from; usedFallback = false;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (direction.sqrMagnitude == 0f) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = areaMask };
            var path = new NavMeshPath();
            float nearest = float.PositiveInfinity;
            // Reachable interaction points stop the child within the 2-second dwell region.
            foreach (var slot in slots)
            {
                var toward = Vector3.ProjectOnPlane(slot.transform.position - from, Vector3.up);
                if (toward.magnitude > config.complianceNavigationDistance ||
                    Vector3.Angle(direction, toward) > config.complianceSameDirectionToleranceDegrees ||
                    !slot.TryGetNavigationPoint(from, agentType, out var candidate)) continue;
                float distance = (candidate - from).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; destination = candidate;
            }
            if (!float.IsPositiveInfinity(nearest)) return true;

            var wanted = from + direction * config.complianceNavigationDistance;
            // Clip first: sampling alone could choose the far side of a wall.
            if (NavMesh.Raycast(from, wanted, out var edge, filter))
            { wanted = edge.position; usedFallback = true; }
            if (TryPoint(from, wanted, filter, path, config, out destination)) return true;
            usedFallback = true;
            // Shrink distance and fan sideways; every accepted candidate needs a complete path.
            int distances = Mathf.Max(1, config.complianceNavigationDistanceSteps);
            int angles = Mathf.Max(1, config.complianceNavigationAngleSteps);
            for (int d = distances; d >= 1; d--)
                for (int a = 0; a <= angles; a++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        float angle = sign * config.complianceNavigationFallbackAngleDegrees * a / angles;
                        var heading = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                        var candidate = from + heading * (config.complianceNavigationDistance * d / distances);
                        if (TryPoint(from, candidate, filter, path, config, out destination)) return true;
                    }
            destination = from;
            return false;
        }
        static bool TryPoint(Vector3 from, Vector3 wanted, NavMeshQueryFilter filter, NavMeshPath path,
            GameBalanceConfig config, out Vector3 point)
        {
            point = from;
            if (!NavMesh.SamplePosition(wanted, out var hit, config.complianceNavigationSampleRadius, filter) ||
                (hit.position - from).sqrMagnitude < 0.0001f ||
                !NavMesh.CalculatePath(from, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            point = hit.position;
            return true;
        }
    }
}
