using UnityEngine;

namespace RunawayChimps.ThreatFeedback
{
    /// <summary>
    /// Per-monster local presentation tuning. This is intentionally presentation-only;
    /// it never changes AI detection, capture, or Photon authority.
    /// </summary>
    [System.Serializable]
    public sealed class ThreatProfile
    {
        [Min(0.1f)] public float beginDistance = 10f;
        [Min(0f)] public float maximumDistance = 1.25f;
        [Range(0f, 1f)] public float pursuitBaseline = 0.12f;
        [Range(0f, 1f)] public float maximumThreat = 1f;
        public AnimationCurve response = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public float Evaluate(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f)
                return 0f;

            float maxDistance = Mathf.Max(0f, maximumDistance);
            float startDistance = Mathf.Max(maxDistance + 0.01f, beginDistance);
            float baseline = Mathf.Clamp01(pursuitBaseline);
            float maxThreat = Mathf.Clamp01(maximumThreat);

            if (distance >= startDistance)
                return baseline * maxThreat;

            float closeness = Mathf.Clamp01((startDistance - distance) / (startDistance - maxDistance));
            float curved = response != null ? Mathf.Clamp01(response.Evaluate(closeness)) : closeness;
            return Mathf.Clamp01(Mathf.Lerp(baseline, 1f, curved) * maxThreat);
        }
    }
}
