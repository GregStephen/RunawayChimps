using UnityEngine;

namespace RunawayChimps.Level2
{
    public sealed class Level2NoiseDebug : MonoBehaviour
    {
        public bool logEvents = true;
        public float drawSeconds = 1.5f;
        Level2NoiseEvent? last;

        void OnEnable() => Level2NoiseBus.Emitted += OnNoise;
        void OnDisable() => Level2NoiseBus.Emitted -= OnNoise;

        void OnNoise(Level2NoiseEvent evt)
        {
            last = evt;
            if (logEvents)
                Debug.Log($"[Level2Noise] {evt.Kind} radius={evt.Radius:0.0} at {evt.Position} source={(evt.Source != null ? evt.Source.name : "none")}", evt.Source);
        }

        void OnDrawGizmos()
        {
            if (!last.HasValue || Time.time - last.Value.Time > drawSeconds) return;
            Gizmos.DrawWireSphere(last.Value.Position, last.Value.Radius);
        }
    }
}
