using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.VR;
using RunawayChimps.Zones;

/// <summary>
/// Global proximity service (put in Bootstrap).
/// Reactors register/unregister themselves as they are loaded/unloaded with additive scenes.
/// This manager:
///  - Tracks the local player's transform via PhotonVRManager (no scene scans)
///  - Optionally filters checks by zone to avoid cross-zone interactions
/// </summary>
public class ProximityManager : MonoBehaviour
{
    public static ProximityManager Instance { get; private set; }

    [Header("Tuning")]
    [Tooltip("Seconds between proximity checks. Lower = more responsive, higher = cheaper.")]
    public float checkInterval = 0.1f;
    [Tooltip("Enable verbose logging from Proximity system (gated at runtime).")]
    public bool verboseLogging = false;
    [Tooltip("Seconds between attempts to fallback-scan for a local Player GameObject when PhotonVR is not available.")]
    public float fallbackScanInterval = 2f;
    [Header("Vent Audio Distance")]
    public bool useVentGraphForDistance = true;

    [Tooltip("If true, only use vent path distance when both player and reactor are 'in vents'.")]
    public bool requireBothInVents = true;

    [Tooltip("If path distance is Infinity (different vent networks), treat as this far away.")]
    public float infinityDistance = 9999f;
    private readonly HashSet<ProximityReactor> reactors = new HashSet<ProximityReactor>();
    private readonly List<ProximityReactor> snapshot = new List<ProximityReactor>();
    private Transform localPlayer;

    private float timer;
    private float lastFallbackScanTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe to PhotonVRManager readiness so we can attach to its LocalHeadBound event
        PhotonVRManager.ManagerReady += OnPhotonVRManagerReady;
        // If manager already exists, handle it now
        if (PhotonVRManager.Manager != null)
            OnPhotonVRManagerReady();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        PhotonVRManager.ManagerReady -= OnPhotonVRManagerReady;
        if (PhotonVRManager.Manager != null)
        {
            PhotonVRManager.Manager.LocalHeadBound -= BindLocalPlayer;
        }
    }

    private void OnPhotonVRManagerReady()
    {
        // Subscribe to LocalHeadBound to receive the head transform when available
        PhotonVRManager.Manager.LocalHeadBound += BindLocalPlayer;

        // If manager already has a head assigned, bind immediately
        if (PhotonVRManager.Manager.Head != null)
            BindLocalPlayer(PhotonVRManager.Manager.Head);
    }

    private void Start()
    {
        TryBindLocalPlayer();
    }

    /// <summary>
    /// Event-driven binding: call this when the local player/head is available to avoid fallback scans.
    /// </summary>
    public void BindLocalPlayer(Transform head)
    {
        if (head == null) return;
        localPlayer = head;
        if (verboseLogging)
            Debug.Log($"[ProximityManager] Bound local player head: {head.name}");
    }

    private void Update()
    {
        if (!TryBindLocalPlayer())
            return;

        timer += Time.deltaTime;
        if (timer < checkInterval) return;
        timer = 0f;

        // Cache local zone once per tick (cheap, avoids repeated lookups).
        ZoneId localZone = ZoneId.None;
        var zoneSvc = ZoneStateService.Instance;
        if (zoneSvc != null)
            localZone = zoneSvc.LocalZone;

        // Iterate over a snapshot to avoid collection modification during callbacks
        snapshot.Clear();
        snapshot.AddRange(reactors);
        foreach (var reactor in snapshot)
        {
            if (reactor == null || !reactor.isActiveAndEnabled)
                continue;

            // A skipped reactor still needs an exit and a zero proximity value,
            // otherwise its material/audio callbacks retain the previous zone's state.
            if (reactor.ZoneId != ZoneId.None &&
                reactor.ZoneId != localZone)
            {
                reactor.ClearProximity();
                continue;
            }

            float distance = ComputeDistance(localPlayer, reactor);
            reactor.UpdateProximity(distance, localPlayer);
        }
        snapshot.Clear();
    }
    private float ComputeDistance(Transform player, ProximityReactor reactor)
    {
        // Fallback
        float euclid = Vector3.Distance(player.position, reactor.transform.position);

        if (!useVentGraphForDistance)
            return euclid;

        // Need VentGraph
        if (VentGraph.Instance == null)
            return euclid;

        // Optional vent-state gating (recommended)
        if (requireBothInVents)
        {
            // PlayerVentState.LocalPlayerInVent is in your project already
            if (!PlayerVentState.LocalPlayerInVent)
                return euclid;

            // Use reactor-provided signal when available
            if (!reactor.IsInVents)
                return euclid;
        }

        float path = VentGraph.Instance.GetPathDistance(reactor.transform.position, player.position);

        if (float.IsInfinity(path) || path > infinityDistance)
            return infinityDistance;

        // IMPORTANT: Path distance should be used instead of euclid, not min().
        return path;
    }
    /// <summary>
    /// Preferred: binds to the local XR rig head from PhotonVRManager (stable & fast).
    /// Falls back to scanning only if needed.
    /// </summary>
    private bool TryBindLocalPlayer()
    {
        // Best: PhotonVRManager rig head
        if (PhotonVRManager.Manager != null && PhotonVRManager.Manager.Head != null)
        {
            localPlayer = PhotonVRManager.Manager.Head;
            return true;
        }

        // Fallback: find local PhotonView (keep as last resort)
        if (localPlayer == null)
        {
            // Throttle expensive fallback scans
            if (Time.time - lastFallbackScanTime < fallbackScanInterval)
                return false;
            lastFallbackScanTime = Time.time;

            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var player in players)
            {
                PhotonView view = player.GetComponentInParent<PhotonView>();
                if (view != null && view.IsMine)
                {
                    localPlayer = player.transform;
                    if (verboseLogging)
                        Debug.Log($"[ProximityManager] Local player found via fallback: {localPlayer.name}");
                    return true;
                }
            }

            // Don't spam every frame; only log occasionally if you want.
            // Debug.LogWarning("[ProximityManager] No local player found yet — waiting for PhotonVR to spawn player.");
            return false;
        }

        return true;
    }

    public void Register(ProximityReactor reactor)
    {
        if (reactor == null) return;
        if (!reactors.Contains(reactor))
            reactors.Add(reactor);
    }

    public void Unregister(ProximityReactor reactor)
    {
        if (reactor == null) return;
        reactors.Remove(reactor);
    }

}
