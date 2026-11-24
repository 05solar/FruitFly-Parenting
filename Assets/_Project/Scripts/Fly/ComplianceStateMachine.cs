using System.Collections.Generic;
using FruitFlyParenting.Core;
using FruitFlyParenting.Data;
using FruitFlyParenting.Tool;
using UnityEngine;

namespace FruitFlyParenting.Fly
{
    public enum ComplianceState { Free, AvoidingTool, Complying }
    public enum ComplianceContactResult { Ignored, Accumulated, Entered, Continued }

    // No Unity lifecycle, NavMesh or stat mutation: callers supply time, R and resolved direction.
    public sealed class ComplianceStateMachine
    {
        readonly GameBalanceConfig config;
        readonly FlyMoodState mood;
        readonly Queue<(float time, float score)> contacts = new Queue<(float, float)>();
        float lastAccepted = float.NegativeInfinity, until;
        Vector3 referenceDirection;
        public ComplianceState State { get; private set; }
        public float Score { get; private set; }
        public Vector3 Direction { get; private set; }
        public float ComplyUntil => until;
        public float SuspendedUntil { get; private set; } = float.NegativeInfinity;
        public bool IsSuspended(float now)=>now<SuspendedUntil;
        public void Suspend(float now,float seconds)
        {
            CompleteVisit(); SuspendedUntil=Mathf.Max(SuspendedUntil,now+seconds);
            State=ComplianceState.AvoidingTool;
        }
        public ComplianceStateMachine(GameBalanceConfig config, FlyMoodState mood)
        { this.config = config; this.mood = mood; }

        public void Tick(float now)
        {
            if(IsSuspended(now)) { State=ComplianceState.AvoidingTool; return; }
            if (State == ComplianceState.Complying)
            {
                if (now >= until) CompleteVisit();
                else { mood.ForceEndAvoidance(); return; }
            }
            if (now - lastAccepted >= config.complianceResetIdleSeconds) ClearScore();
            while (contacts.Count > 0 && now - contacts.Peek().time > config.complianceAccumulationWindowSeconds)
                Score -= contacts.Dequeue().score;
            if (contacts.Count == 0) { Score = 0f; referenceDirection = Vector3.zero; }
            State = mood.IsAvoiding(now) ? ComplianceState.AvoidingTool : ComplianceState.Free;
        }

        public ComplianceContactResult RecordContact(PushSource source, Vector3 direction, float rebellion, float now)
        {
            Tick(now);
            if(IsSuspended(now)) return ComplianceContactResult.Ignored;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (direction.sqrMagnitude == 0f || now - lastAccepted < config.complianceMinimumContactIntervalSeconds)
                return ComplianceContactResult.Ignored;
            if (referenceDirection.sqrMagnitude > 0f &&
                Vector3.Angle(referenceDirection, direction) >= config.complianceOppositeAngleDegrees)
            {
                if (State == ComplianceState.Complying) CompleteVisit();
                else ClearScore(); // A direction change alone must not cancel existing avoidance.
            }
            lastAccepted = now;
            if (State == ComplianceState.Complying)
            {
                if (Vector3.Angle(Direction, direction) <= config.complianceSameDirectionToleranceDegrees)
                { until = now + config.complianceDurationSeconds; Direction = direction; }
                mood.ForceEndAvoidance();
                return ComplianceContactResult.Continued;
            }
            if (referenceDirection.sqrMagnitude == 0f) referenceDirection = direction;
            float score = source == PushSource.ElectricStick ? config.complianceElectricStickPushScore : config.complianceWheelchairPushScore;
            contacts.Enqueue((now, score)); Score += score;
            float threshold = rebellion < config.complianceRebellionBoundary ? config.complianceThresholdLowRebellion : config.complianceThresholdHighRebellion;
            if (Score < threshold) return ComplianceContactResult.Accumulated;
            State = ComplianceState.Complying;
            Direction = direction;
            until = now + config.complianceDurationSeconds;
            mood.ForceEndAvoidance();
            return ComplianceContactResult.Entered;
        }
        void ClearScore() { contacts.Clear(); Score = 0f; referenceDirection = Vector3.zero; }
        public void CompleteVisit()
        {
            // Finishing a visit clears the score, not the global minimum contact interval.
            ClearScore();
            State = ComplianceState.Free; mood.ForceEndAvoidance();
        }
        public void EnterRoom() { CompleteVisit(); lastAccepted = float.NegativeInfinity; SuspendedUntil=float.NegativeInfinity; }
    }
}
