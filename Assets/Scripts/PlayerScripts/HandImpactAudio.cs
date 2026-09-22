using GorillaLocomotion;
using RunawayChimps.Travel;
using UnityEngine;

/// <summary>Local hand audio driven by the locomotion solver's actual contacts.</summary>
[DisallowMultipleComponent]
public class HandImpactAudio : MonoBehaviour
{
    [Header("Surface response")]
    public SurfaceAudioProfile defaultProfile;
    public LayerMask surfaceLayers = ~0;

    [Header("Hand contact")]
    [Tooltip("Minimum total tracked hand speed in metres per second.")]
    [Min(0f)] public float minSlapSpeed = 0.2f;
    [Tooltip("Minimum speed into the surface. The profile's minImpact also applies.")]
    [Min(0f)] public float minIntoSurfaceSpeed = 0.2f;
    [Tooltip("Minimum seconds between impacts from this hand; profiles can require longer.")]
    [Min(0f)] public float slapLockout = 0.055f;
    [Tooltip("Seconds of actual released contact needed for another strike. Nearby walls do not block release.")]
    [Min(0f)] public float rearmOffSurfaceTime = 0.02f;

    [Header("Rig binding")]
    public Player locomotionPlayer;
    public Transform playerRoot;
    public string handTag = "HandTag";

    [Header("Audio")]
    [Min(0.1f)] public float maxDistance3D = 10f;

    private readonly HandImpactGate gate = new HandImpactGate();
    private SurfaceImpactEmitter emitter;
    private Player boundPlayer;
    private bool isLeft;
    private bool applicationPaused;
    private bool applicationFocused = true;

    private void Awake()
    {
        emitter = GetComponent<SurfaceImpactEmitter>();
        if (emitter == null) emitter = gameObject.AddComponent<SurfaceImpactEmitter>();
        emitter.maxDistance3D = maxDistance3D;
    }

    private void OnEnable()
    {
        ResetContact();
        TryBind();
    }

    private void OnDisable()
    {
        Unbind();
        ResetContact();
    }

    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        ResetContact();
    }

    private void OnApplicationFocus(bool focused)
    {
        applicationFocused = focused;
        ResetContact();
    }

    private void Update()
    {
        if (boundPlayer == null) TryBind();
        // Stop tails even if the disabled locomotion component cannot publish a sample.
        if (IsSuppressed || (boundPlayer != null && !boundPlayer.isActiveAndEnabled))
            ResetContact();
    }

    private bool IsSuppressed => applicationPaused || !applicationFocused ||
        LoadingFlow.IsColdStartupPresentationActive ||
        (SectorTravelService.I != null && SectorTravelService.I.IsBusy);

    private void TryBind()
    {
        if (boundPlayer != null) return;
        Player candidate = locomotionPlayer;
        if (candidate == null && playerRoot != null)
            candidate = playerRoot.GetComponentInChildren<Player>(true);
        if (candidate == null) candidate = GetComponentInParent<Player>();
        if (candidate == null) return;

        // Only the actual local controller/follower may subscribe for that hand.
        // A remote avatar or unrelated child cannot create another copy of its sound.
        bool left = transform == candidate.leftHandTransform || transform == candidate.leftHandFollower;
        bool right = transform == candidate.rightHandTransform || transform == candidate.rightHandFollower;
        if (!left && !right) return;
        boundPlayer = candidate;
        isLeft = left;
        boundPlayer.HandContactUpdated += OnHandContact;
        boundPlayer.HandContactsReset += ResetContact;
    }

    private void Unbind()
    {
        if (boundPlayer != null)
        {
            boundPlayer.HandContactUpdated -= OnHandContact;
            boundPlayer.HandContactsReset -= ResetContact;
        }
        boundPlayer = null;
    }

    private void ResetContact()
    {
        gate.Reset(Time.time);
        if (emitter != null) emitter.StopAll();
    }

    private void OnHandContact(Player.HandContactSample sample)
    {
        if (!isActiveAndEnabled || sample.IsLeft != isLeft) return;
        if (sample.Suppressed || IsSuppressed)
        {
            ResetContact();
            return;
        }

        Collider surface = sample.Hit.collider;
        bool eligible = sample.IsTouching && !IsIgnored(surface);
        SurfaceAudioProfile profile = eligible ? SurfaceAudio.ResolveProfile(surface, defaultProfile) : null;
        Vector3 velocity = sample.Velocity;
        if (eligible && surface.attachedRigidbody != null)
            velocity -= surface.attachedRigidbody.GetPointVelocity(sample.Hit.point);
        float intoSpeed = Vector3.Dot(-velocity, sample.Hit.normal.normalized);
        if (profile == null || velocity.magnitude < minSlapSpeed || intoSpeed < minIntoSurfaceSpeed)
            intoSpeed = 0f;

        // Latch every contact, including quiet or explicitly blocked contacts. Pressing
        // harder later or moving over a collider seam must not become a fresh strike.
        float threshold = profile != null ? Mathf.Max(minIntoSurfaceSpeed, profile.EffectiveMinImpact) : float.MaxValue;
        float interval = profile != null ? Mathf.Max(slapLockout, profile.EffectiveMinInterval) : slapLockout;
        if (!gate.Observe(sample.IsTouching, intoSpeed, Time.time, threshold, interval, rearmOffSurfaceTime))
            return;
        if (profile == null || emitter == null) return;
        emitter.maxDistance3D = maxDistance3D;
        emitter.TryPlay(profile, sample.Hit.point, intoSpeed, Time.time);
    }

    private bool IsIgnored(Collider surface)
    {
        if (surface == null || surface.isTrigger) return true;
        if ((surfaceLayers.value & (1 << surface.gameObject.layer)) == 0) return true;
        if (playerRoot != null && surface.transform.IsChildOf(playerRoot)) return true;
        if (!string.IsNullOrEmpty(handTag) && surface.CompareTag(handTag)) return true;
        return surface.GetComponentInParent<BlockHandSurfaceAudio>() != null;
    }
}
