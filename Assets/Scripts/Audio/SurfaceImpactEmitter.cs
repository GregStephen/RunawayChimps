using UnityEngine;

/// <summary>
/// Local spatial playback shared by hand and future object impacts. Each accepted
/// impact keeps its contact position, pitch and mixer until that voice finishes.
/// Detection, hand rearming and gameplay/network noise decisions belong to callers.
/// </summary>
[DisallowMultipleComponent]
public sealed class SurfaceImpactEmitter : MonoBehaviour
{
    private const int VoiceCount = 4;

    [Min(0.01f)] public float minDistance3D = 0.35f;
    [Min(0.01f)] public float maxDistance3D = 10f;

    private readonly AudioSource[] voices = new AudioSource[VoiceCount];
    private readonly double[] voiceEnds = new double[VoiceCount];
    private readonly ulong[] voiceOrder = new ulong[VoiceCount];
    private GameObject voiceRoot;
    private AudioClip previousClip;
    private float lastImpactTime = float.NegativeInfinity;
    private float previousInterval;
    private ulong sequence;

    private void Awake() => EnsureVoices();
    private void OnDisable() => StopAll();

    private void OnDestroy()
    {
        StopAll();
        if (voiceRoot == null) return;
        if (Application.isPlaying) Destroy(voiceRoot);
        else DestroyImmediate(voiceRoot);
        voiceRoot = null;
    }

    public bool TryPlay(SurfaceAudioProfile profile, Vector3 point, float intoSpeed, float now)
    {
        if (!isActiveAndEnabled || profile == null || !IsFinite(point) ||
            !IsFinite(intoSpeed) || intoSpeed <= 0f || !IsFinite(now) || now < 0f ||
            intoSpeed < profile.EffectiveMinImpact || now < lastImpactTime)
        {
            return false;
        }

        // Both sides of a profile change respect their configured spacing. State is
        // per emitter, so a left-hand impact cannot mute a simultaneous right hand.
        float interval = Mathf.Max(previousInterval, profile.EffectiveMinInterval);
        if (now - lastImpactTime < interval) return false;

        float volume = profile.EvaluateVolume(intoSpeed);
        if (volume <= 0f || !profile.TryChooseClip(previousClip, out AudioClip clip)) return false;
        if (clip.loadState == AudioDataLoadState.Failed) return false;
        if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData()) return false;
        if (clip.loadState != AudioDataLoadState.Loaded) return false;

        EnsureVoices();
        double dspNow = AudioSettings.dspTime;
        int index = FindVoice(dspNow);
        AudioSource source = voices[index];
        source.Stop();
        source.transform.position = point;
        source.pitch = profile.ChoosePitch();
        source.volume = volume;
        source.outputAudioMixerGroup = profile.outputGroup;
        source.minDistance = Mathf.Clamp(FiniteOr(minDistance3D, 0.35f), 0.01f, 100f);
        source.maxDistance = Mathf.Clamp(FiniteOr(maxDistance3D, 10f), source.minDistance, 1000f);
        source.clip = clip;
        source.Play();
        voiceEnds[index] = dspNow + clip.length / source.pitch;
        voiceOrder[index] = ++sequence;
        previousClip = clip;
        lastImpactTime = now;
        previousInterval = profile.EffectiveMinInterval;
        return true;
    }

    public void StopAll()
    {
        for (int index = 0; index < voices.Length; index++)
        {
            if (voices[index] != null)
            {
                voices[index].Stop();
                voices[index].clip = null;
            }
            voiceEnds[index] = 0d;
            voiceOrder[index] = 0;
        }
        lastImpactTime = float.NegativeInfinity;
        previousInterval = 0f;
        previousClip = null;
        sequence = 0;
    }

    private void EnsureVoices()
    {
        if (voiceRoot != null) return;
        voiceRoot = new GameObject(name + " Surface Impact Voices");
        // Keep the pool outside the moving emitter hierarchy, including its scene
        // transitions. Its owning component explicitly stops/destroys it on teardown.
        // This also handles a persistent Gorilla Rig and short-lived object emitters.
        if (Application.isPlaying) DontDestroyOnLoad(voiceRoot);
        for (int index = 0; index < voices.Length; index++)
        {
            var voiceObject = new GameObject("Impact Voice " + (index + 1));
            voiceObject.transform.SetParent(voiceRoot.transform, false);
            AudioSource source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.priority = 100;
            voices[index] = source;
            voiceEnds[index] = 0d;
            voiceOrder[index] = 0;
        }
    }

    private int FindVoice(double dspNow)
    {
        int oldest = 0;
        for (int index = 0; index < voices.Length; index++)
        {
            // Reservation by DSP duration also protects voices issued in the same
            // frame before the audio thread reports isPlaying. Saturation replaces
            // only the oldest voice; no runtime object allocation is needed per hit.
            if (voiceEnds[index] <= dspNow) return index;
            if (voiceOrder[index] < voiceOrder[oldest]) oldest = index;
        }
        return oldest;
    }

    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float FiniteOr(float value, float fallback) => IsFinite(value) ? value : fallback;
}
