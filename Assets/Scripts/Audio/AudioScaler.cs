using UnityEngine;

public class AudioScaler : MonoBehaviour
{
    public enum AudioMode
    {
        Always,
        WanderOnly,
        ChaseOnly
    }

    [Header("Audio")]
    public AudioSource source;
    public float minVolume = 0f;
    public float maxVolume = 1f;

    [Tooltip("Automatically play/stop based on proximity and mode.")]
    public bool autoPlayStop = true;

    [Range(0f, 1f)]
    [Tooltip("Minimum proximity (0-1) before audio starts playing.")]
    public float playThreshold = 0.05f;

    [Header("Monster State Filter (optional)")]
    public AudioMode mode = AudioMode.Always;
    public MonsterNavigation monsterNavigation;

    [Header("Vent Filter (optional)")]
    [Tooltip("If true, this audio only plays when the LOCAL player is inside a vent.")]
    public bool onlyWhenPlayerInVent = false;

    public bool debugLogs = false;
    private bool lastMute;

    private void LateUpdate()
    {
        if (source == null) return;

        if (source.mute != lastMute)
        {
            Debug.LogWarning($"{name}: AudioSource.mute changed to {source.mute} on frame {Time.frameCount}", this);
            lastMute = source.mute;
        }
    }

    /// <summary>
    /// Called by ProximityReactor via UnityEvent<float>.
    /// Proximity is expected to be 0–1 (0 = far, 1 = very close).
    /// </summary>
    public void SetProximity(float proximity)
    {
        if (source == null)
        {
            Debug.LogWarning($"{name}: AudioScaler has no AudioSource assigned.");
            return;
        }
        source.mute = false;
        if (!source.loop) source.loop = true;
        // 1) Optional: only when local player is in a vent
        if (onlyWhenPlayerInVent && !PlayerVentState.LocalPlayerInVent)
        {
           // if (debugLogs) Debug.Log($"{name}: Player not in vent → stopping.");
            StopIfPlaying();
            return;
        }

        // 2) Optional: filter by monster chase/wander state
        if (monsterNavigation != null)
        {
            bool isChasing = monsterNavigation.IsChasing;

            if (mode == AudioMode.WanderOnly && isChasing)
            {
                if (debugLogs) Debug.Log($"{name}: WanderOnly, but IsChasing=true → stopping.");
                StopIfPlaying();
                return;
            }

            if (mode == AudioMode.ChaseOnly && !isChasing)
            {
                if (debugLogs) Debug.Log($"{name}: ChaseOnly, but IsChasing=false → stopping.");
                StopIfPlaying();
                return;
            }
        }

        // 3) Apply proximity to volume
        proximity = Mathf.Clamp01(proximity);
        float volume = Mathf.Lerp(minVolume, maxVolume, proximity);
        source.volume = volume;

        if (debugLogs)
        {
            Debug.Log($"{name}: mode={mode}, proximity={proximity:F2}, volume={volume:F2}, isPlaying={source.isPlaying}");
        }

        if (!autoPlayStop)
            return;

        if (proximity > playThreshold)
        {
            if (!source.isPlaying)
            {
                if (debugLogs) Debug.Log($"{name}: Starting audio (proximity > threshold).");
                source.mute = false;
                source.Play();
            }
        }
        else
        {
            if (debugLogs) Debug.Log($"{name}: Stopping audio (proximity <= threshold).");
            StopIfPlaying();
        }
    }

    private void StopIfPlaying()
    {
        if (source != null && source.isPlaying)
        {
            //source.Stop();
        }
    }

    [ContextMenu("TEST: Play Now")]
    public void TestPlayNow()
    {
        if (source == null) return;
        source.mute = false;
        source.volume = 1f;
        source.spatialBlend = 0f; // 2D test
        source.Play();
    }

}
