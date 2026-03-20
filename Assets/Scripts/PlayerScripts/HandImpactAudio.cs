using UnityEngine;
using System.Collections.Generic;

public class HandImpactAudio : MonoBehaviour
{
    [Header("Profiles")]
    public SurfaceAudioProfile defaultProfile;

    [Header("Probe (audio only)")]
    public LayerMask surfaceLayers = ~0;
    public float probeRadius = 0.07f;
    public float probeDistance = 0.12f;
    public Vector3 localProbeOffset = Vector3.zero;

    [Header("GTag thresholds")]
    public float minSlapSpeed = 1.6f;          // total hand speed
    public float minIntoSurfaceSpeed = 1.1f;   // component INTO the surface normal

    [Header("Anti-machine-gun")]
    public float slapLockout = 0.12f;          // hard cooldown after any slap
    public float rearmOffSurfaceTime = 0.08f;  // must be off all surfaces this long to re-arm
    public float perColliderCooldown = 0.10f;  // avoid double hits on compound colliders

    [Header("Audio")]
    public float maxDistance3D = 10f;

    [Header("Ignore")]
    public Transform playerRoot;               // GorillaRig root
    public string handTag = "HandTag";

    [Header("Fallback casts")]
    public bool enableFallback = true;

    private AudioSource _src;
    private Vector3 _prevPos;

    private bool _armed = true;
    private float _offSurfaceTimer = 0f;
    private float _lockoutUntil = 0f;

    private readonly Dictionary<int, float> _perColliderNext = new(64);

    private void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
        _src.spatialBlend = 1f;
        _src.rolloffMode = AudioRolloffMode.Logarithmic;
        _src.maxDistance = maxDistance3D;

        _prevPos = transform.position;
    }

    private void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 pos = transform.position;
        Vector3 v = (pos - _prevPos) / dt;   // hand velocity
        float speed = v.magnitude;
        _prevPos = pos;

        Vector3 origin = transform.TransformPoint(localProbeOffset);

        // Track arming based on whether we are near ANY surface.
        bool nearSurface = false;

        // Best candidate across casts
        bool hasCandidate = false;
        RaycastHit bestHit = default;
        float bestInto = 0f;

        // 1) Primary cast: in the impact direction (opposite velocity)
        if (speed > 0.001f)
        {
            Vector3 impactDir = (-v).normalized;

            if (Physics.SphereCast(origin, probeRadius, impactDir, out RaycastHit hit, probeDistance, surfaceLayers, QueryTriggerInteraction.Ignore))
            {
                if (!IsIgnored(hit.collider))
                {
                    nearSurface = true;

                    float into = Vector3.Dot(-v, hit.normal.normalized);
                    if (into > bestInto)
                    {
                        bestInto = into;
                        bestHit = hit;
                        hasCandidate = true;
                    }
                }
            }
        }

        // 2) Fallback: helps in edge cases (slow velocity / odd alignment)
        if (enableFallback)
        {
            // Down (floors), forward/back (walls), right/left (walls)
            Vector3[] dirs =
            {
                Vector3.down,
                transform.forward, -transform.forward,
                transform.right, -transform.right
            };

            for (int i = 0; i < dirs.Length; i++)
            {
                if (Physics.SphereCast(origin, probeRadius, dirs[i], out RaycastHit hit, probeDistance, surfaceLayers, QueryTriggerInteraction.Ignore))
                {
                    if (IsIgnored(hit.collider))
                        continue;

                    nearSurface = true;

                    float into = Vector3.Dot(-v, hit.normal.normalized);
                    if (into > bestInto)
                    {
                        bestInto = into;
                        bestHit = hit;
                        hasCandidate = true;
                    }
                }
            }
        }

        // Rearm logic (GTag-style): only rearm after being truly off surfaces briefly
        if (!nearSurface)
        {
            _offSurfaceTimer += Time.deltaTime;
            if (_offSurfaceTimer >= rearmOffSurfaceTime)
                _armed = true;

            return;
        }
        else
        {
            _offSurfaceTimer = 0f;
        }

        // Gates
        if (!hasCandidate) return;
        if (Time.time < _lockoutUntil) return;
        if (!_armed) return;

        // GTag slap conditions
        if (speed < minSlapSpeed) return;
        if (bestInto < minIntoSurfaceSpeed) return;

        int id = bestHit.collider.GetInstanceID();
        if (_perColliderNext.TryGetValue(id, out float nextT) && Time.time < nextT)
            return;

        PlaySlap(bestHit.collider, bestInto);

        _armed = false;
        _lockoutUntil = Time.time + slapLockout;
        _perColliderNext[id] = Time.time + perColliderCooldown;
    }

    private bool IsIgnored(Collider c)
    {
        if (c == null) return true;

        if (!string.IsNullOrEmpty(handTag) && c.CompareTag(handTag))
            return true;

        if (playerRoot != null && c.transform.IsChildOf(playerRoot))
            return true;

        if (c.GetComponentInParent<BlockHandSurfaceAudio>() != null)
            return true;

        return false;
    }

    private void PlaySlap(Collider surface, float into)
    {
        if (defaultProfile == null || defaultProfile.clips == null || defaultProfile.clips.Length == 0)
            return;

        var sa = surface.GetComponentInParent<SurfaceAudio>();
        var profile = (sa != null && sa.profile != null) ? sa.profile : defaultProfile;
        if (profile == null || profile.clips == null || profile.clips.Length == 0)
            return;

        // Volume curve: use INTO component, punchier like GTag
        float t = Mathf.InverseLerp(minIntoSurfaceSpeed, minIntoSurfaceSpeed * 2.2f, into);
        t = Mathf.Clamp01(t);
        t = t * t;

        float vol = Mathf.Lerp(profile.volume * 0.35f, profile.volume, t);

        var clip = profile.clips[Random.Range(0, profile.clips.Length)];
        _src.pitch = Random.Range(profile.pitchMin, profile.pitchMax);
        _src.outputAudioMixerGroup = profile.outputGroup;
        _src.PlayOneShot(clip, vol);
    }
}
