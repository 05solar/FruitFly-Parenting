using System;

namespace FruitFlyParenting.Corridor
{
    public readonly struct CorridorAudioMix
    {
        public readonly float Previous, Next;
        public CorridorAudioMix(float previous, float next) { Previous = previous; Next = next; }
        public static CorridorAudioMix At(float distance, float recapLength, float length)
        {
            float entry = Smooth(Clamp(distance / Math.Max(.01f, recapLength)));
            float crossfade = Smooth(Clamp((distance - recapLength) / Math.Max(.01f, length - recapLength)));
            return new CorridorAudioMix((1f - .4f * entry) * (float)Math.Sqrt(1f - crossfade), (float)Math.Sqrt(crossfade));
        }
        static float Clamp(float value) => Math.Max(0, Math.Min(1, value));
        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
