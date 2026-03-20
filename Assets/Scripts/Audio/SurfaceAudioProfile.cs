using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "RunawayChimps/Audio/Surface Audio Profile")]
public class SurfaceAudioProfile : ScriptableObject
{
    public AudioClip[] clips;

    [Range(0f, 1f)] public float volume = 0.8f;
    [Range(0.5f, 2f)] public float pitchMin = 0.95f;
    [Range(0.5f, 2f)] public float pitchMax = 1.05f;

    public float minInterval = 0.06f;     // per-surface rapid hits limit
    public float minImpact = 0.5f;        // collision impulse/velocity threshold
    [Header("Optional Mixer Routing")]
    public AudioMixerGroup outputGroup;
}