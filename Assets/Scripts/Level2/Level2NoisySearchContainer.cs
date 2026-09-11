using UnityEngine;

namespace RunawayChimps.Level2
{
    public sealed class Level2NoisySearchContainer : MonoBehaviour
    {
        public float noiseRadius = 10f;
        public float cooldown = .35f;
        float nextNoise;

        // Call from the eventual drawer/cabinet animation when it starts moving/opening.
        public void NotifyOpened()
        {
            if (Time.time < nextNoise) return;
            nextNoise = Time.time + cooldown;
            Level2NoiseBus.Emit(transform.position, noiseRadius, Level2NoiseKind.Search, gameObject);
        }
    }
}
