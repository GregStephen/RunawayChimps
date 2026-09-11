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
        public readonly float Time;

        public Level2NoiseEvent(Vector3 position, float radius, Level2NoiseKind kind, GameObject source, float time)
        {
            Position = position;
            Radius = radius;
            Kind = kind;
            Source = source;
            Time = time;
        }
    }

    public static class Level2NoiseBus
    {
        public static event Action<Level2NoiseEvent> Emitted;
        public static Level2NoiseEvent? LastEvent { get; private set; }

        public static void Emit(Vector3 position, float radius, Level2NoiseKind kind, GameObject source = null)
        {
            var evt = new Level2NoiseEvent(position, Mathf.Max(0f, radius), kind, source, Time.time);
            LastEvent = evt;
            Emitted?.Invoke(evt);
        }
    }
}
