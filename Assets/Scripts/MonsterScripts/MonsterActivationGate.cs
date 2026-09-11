using UnityEngine;
using RunawayChimps.Zones;

public class MonsterActivationGate : MonoBehaviour
{
    public ZoneId activeZone = ZoneId.Level1_Vents;

    [Header("What to gate (recommended)")]
    public Animator animator;
    public MonoBehaviour[] aiScripts;
    public AudioSource[] audioSources;

    [Tooltip("If you REALLY want to toggle the whole root, assign it here. Otherwise leave null.")]
    public GameObject monsterRootOptional;

    private void Awake()
    {
        // Auto-wire common refs if you forget
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (audioSources == null || audioSources.Length == 0) audioSources = GetComponentsInChildren<AudioSource>(true);

        Apply(ZoneStateService.Instance != null ? ZoneStateService.Instance.LocalZone : ZoneId.None);
    }

    private void OnEnable()
    {
        if (ZoneStateService.Instance != null)
        {
            ZoneStateService.Instance.OnLocalZoneChanged += Apply;
            // If you add OnLocalZoneApplied later, you can hook it too.
            // ZoneStateService.Instance.OnLocalZoneApplied += Apply;
        }
        Apply(ZoneStateService.Instance != null ? ZoneStateService.Instance.LocalZone : ZoneId.None);
    }

    private void OnDisable()
    {
        if (ZoneStateService.Instance != null)
        {
            ZoneStateService.Instance.OnLocalZoneChanged -= Apply;
            // ZoneStateService.Instance.OnLocalZoneApplied -= Apply;
        }
    }

    public void SetAnimator(Animator replacement)
    {
        animator = replacement;
        Apply(ZoneStateService.Instance != null ? ZoneStateService.Instance.LocalZone : ZoneId.None);
    }

    private void Apply(ZoneId z)
    {
        bool active = (z == activeZone);
        Debug.Log($"[MonsterActivationGate] localZone={z} activeZone={activeZone} active={active}", this);

        // Preferred: gate components
        if (animator != null) animator.enabled = active;

        if (aiScripts != null)
            foreach (var s in aiScripts)
            {
                if (s == null) continue;
                bool sharedMonster = GetComponent<RunawayChimps.Travel.SectorMonsterSync>() != null;
                if (sharedMonster && (s is MonsterNavigation || s is RunawayChimps.Travel.SectorMonsterSync)) continue;
                s.enabled = active;
            }

        if (audioSources != null)
        {
            foreach (var a in audioSources)
            {
                if (a == null) continue;

                if (!active)
                {
                    if (a.isPlaying) a.Stop();
                    a.enabled = false;   // prevents Play() calls doing anything while gated
                }
                else
                {
                    a.enabled = true;
                    a.mute = false;      // ensure we recover from any previous mute
                }
            }
        }


        // Optional: also toggle root (use only if monster is tiny)
        if (monsterRootOptional != null)
            monsterRootOptional.SetActive(active);
    }
}
