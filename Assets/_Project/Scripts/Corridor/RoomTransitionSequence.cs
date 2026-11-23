using System;

namespace FruitFlyParenting.Corridor
{
    public enum LifeStage { Prologue, Infant, Childhood, Adolescence, YoungAdult, MiddleAge }
    public enum TransitionPhase { Idle, FadingToCorridor, RevealingCorridor, Corridor, FadingToRoom, RevealingRoom, Complete }
    public enum CorridorSection { Recap, Narration, Silhouette }

    // 단일 씬의 인접한 생애 단계만 허용한다. 호출자가 실제 시간 경과량을 전달한다.
    public sealed class RoomTransitionSequence
    {
        readonly float fadeSeconds;
        float elapsed;
        public TransitionPhase Phase { get; private set; }
        public float LastFadeOutSeconds { get; private set; }
        public float FadeAlpha => Phase == TransitionPhase.FadingToCorridor || Phase == TransitionPhase.FadingToRoom
            ? Math.Min(1f, elapsed / fadeSeconds)
            : Phase == TransitionPhase.RevealingCorridor || Phase == TransitionPhase.RevealingRoom
                ? 1f - Math.Min(1f, elapsed / fadeSeconds) : 0f;
        public bool MovementAllowed => Phase == TransitionPhase.Idle || Phase == TransitionPhase.Corridor || Phase == TransitionPhase.Complete;
        public static bool Allows(LifeStage from, LifeStage to) =>
            from >= LifeStage.Prologue && from < LifeStage.MiddleAge && (int)to == (int)from + 1;

        public RoomTransitionSequence(float fadeSeconds) { this.fadeSeconds = Math.Max(.5f, fadeSeconds); }
        public bool Begin(LifeStage from, LifeStage to)
        {
            if (Phase != TransitionPhase.Idle || !Allows(from, to)) return false;
            SetPhase(TransitionPhase.FadingToCorridor); return true;
        }
        public bool BeginNextRoom(bool ready)
        {
            if (Phase != TransitionPhase.Corridor || !ready) return false;
            SetPhase(TransitionPhase.FadingToRoom); return true;
        }
        public void Advance(float realSeconds)
        {
            if (MovementAllowed) return;
            elapsed += Math.Max(0, realSeconds);
            if (elapsed < fadeSeconds) return;
            if (Phase == TransitionPhase.FadingToCorridor || Phase == TransitionPhase.FadingToRoom)
                LastFadeOutSeconds = elapsed;
            // 한 번에 한 단계만 넘긴다. 긴 프레임에도 컨테이너 교체는 완전한 암전에서만 일어난다.
            switch (Phase)
            {
                case TransitionPhase.FadingToCorridor: SetPhase(TransitionPhase.RevealingCorridor); break;
                case TransitionPhase.RevealingCorridor: SetPhase(TransitionPhase.Corridor); break;
                case TransitionPhase.FadingToRoom: SetPhase(TransitionPhase.RevealingRoom); break;
                case TransitionPhase.RevealingRoom: SetPhase(TransitionPhase.Complete); break;
            }
        }
        void SetPhase(TransitionPhase phase) { Phase = phase; elapsed = 0; }
    }
}
