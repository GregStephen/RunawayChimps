using System;
using UnityEngine;

namespace RunawayChimps.Level2
{
    public enum Level2NoiseKind { Search, FuseImpact, Charging }

    public readonly struct Level2NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly Level2NoiseKind Kind;
        public readonly GameObject Source;

        public Level2NoiseEvent(Vector3 position, float radius, Level2NoiseKind kind, GameObject source)
        {
            Position = position;
            Radius = radius;
            Kind = kind;
            Source = source;
        }
    }

    public static class Level2NoiseBus
    {
        public static event Action<Level2NoiseEvent> Emitted;

        public static void Emit(Vector3 position, float radius, Level2NoiseKind kind, GameObject source = null)
        {
            Emitted?.Invoke(new Level2NoiseEvent(position, Mathf.Max(0f, radius), kind, source));
        }
    }
}
