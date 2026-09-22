using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "RunawayChimps/Audio/Surface Audio Profile")]
public class SurfaceAudioProfile : ScriptableObject
{
    [Tooltip("Short, single impacts. Null and duplicate entries are ignored; use distinct recordings for variation.")]
    public AudioClip[] clips;

    [Range(0f, 1f)] public float volume = 0.8f;
    [Range(0.5f, 2f)] public float pitchMin = 0.95f;
    [Range(0.5f, 2f)] public float pitchMax = 1.05f;

    [Min(0f), Tooltip("Minimum seconds between impacts from one emitter. Left and right hands remain independent.")]
    public float minInterval = 0.06f;
    [Min(0f), Tooltip("Minimum approach speed INTO the surface, in metres per second. Not collision impulse.")]
    public float minImpact = 0.25f;
    [Min(0f), Tooltip("Approach speed in metres per second that reaches this profile's full volume.")]
    public float fullImpactSpeed = 2.5f;
    [Range(0f, 1f), Tooltip("Fraction of full volume used at the minimum accepted impact speed.")]
    public float minVolumeFraction = 0.25f;

    [Header("Optional Mixer Routing")]
    public AudioMixerGroup outputGroup;

    public float EffectiveMinImpact => Mathf.Clamp(FiniteOr(minImpact, 0.25f), 0f, 100f);
    public float EffectiveMinInterval => Mathf.Clamp(FiniteOr(minInterval, 0.06f), 0f, 10f);

    public bool HasPlayableClips
    {
        get
        {
            if (clips == null) return false;
            for (int index = 0; index < clips.Length; index++)
                if (clips[index] != null) return true;
            return false;
        }
    }

    // Selection state belongs to an emitter, never to this shared asset. Reservoir
    // sampling needs no temporary list and gives each distinct recording equal weight.
    public bool TryChooseClip(AudioClip previous, out AudioClip clip)
    {
        clip = null;
        if (clips == null) return false;

        AudioClip onlyPrevious = null;
        int choices = 0;
        for (int index = 0; index < clips.Length; index++)
        {
            AudioClip candidate = clips[index];
            if (candidate == null) continue;
            if (candidate == previous)
            {
                onlyPrevious = candidate;
                continue;
            }

            bool duplicate = false;
            for (int earlier = 0; earlier < index; earlier++)
            {
                if (clips[earlier] != candidate) continue;
                duplicate = true;
                break;
            }
            if (duplicate) continue;
            choices++;
            if (Random.Range(0, choices) == 0) clip = candidate;
        }

        if (clip == null) clip = onlyPrevious;
        return clip != null;
    }

    public float EvaluateVolume(float intoSpeed)
    {
        if (!IsFinite(intoSpeed) || intoSpeed <= 0f || intoSpeed < EffectiveMinImpact) return 0f;
        float peakSpeed = Mathf.Clamp(
            FiniteOr(fullImpactSpeed, 2.5f), EffectiveMinImpact + 0.01f, 101f);
        float strength = Mathf.InverseLerp(EffectiveMinImpact, peakSpeed, intoSpeed);
        float floor = Mathf.Clamp01(FiniteOr(minVolumeFraction, 0.25f));
        return Mathf.Clamp01(FiniteOr(volume, 0.8f)) * Mathf.Lerp(floor, 1f, strength * strength);
    }

    public float ChoosePitch()
    {
        float low = Mathf.Clamp(FiniteOr(pitchMin, 0.95f), 0.5f, 2f);
        float high = Mathf.Clamp(FiniteOr(pitchMax, 1.05f), 0.5f, 2f);
        return Random.Range(Mathf.Min(low, high), Mathf.Max(low, high));
    }

    private void OnValidate()
    {
        volume = Mathf.Clamp01(FiniteOr(volume, 0.8f));
        pitchMin = Mathf.Clamp(FiniteOr(pitchMin, 0.95f), 0.5f, 2f);
        pitchMax = Mathf.Clamp(FiniteOr(pitchMax, 1.05f), pitchMin, 2f);
        minInterval = EffectiveMinInterval;
        minImpact = EffectiveMinImpact;
        fullImpactSpeed = Mathf.Clamp(FiniteOr(fullImpactSpeed, 2.5f), minImpact + 0.01f, 101f);
        minVolumeFraction = Mathf.Clamp01(FiniteOr(minVolumeFraction, 0.25f));
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float FiniteOr(float value, float fallback) => IsFinite(value) ? value : fallback;
}
