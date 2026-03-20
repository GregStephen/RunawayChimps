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
    [Header("Vent Audio Distance")]
    public bool useVentGraphForDistance = true;

    [Tooltip("If true, only use vent path distance when both player and reactor are 'in vents'.")]
    public bool requireBothInVents = true;

    [Tooltip("If path distance is Infinity (different vent networks), treat as this far away.")]
    public float infinityDistance = 9999f;
    private readonly List<ProximityReactor> reactors = new List<ProximityReactor>();
    private Transform localPlayer;

    private float timer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        TryBindLocalPlayer();
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

        foreach (var reactor in reactors)
        {
            if (reactor == null)
                continue;

            if (localZone != ZoneId.None &&
                reactor.ZoneId != ZoneId.None &&
                reactor.ZoneId != localZone)
            {
                continue;
            }

            float distance = ComputeDistance(localPlayer, reactor.transform);
            reactor.UpdateProximity(distance, localPlayer);
        }

    }
    private float ComputeDistance(Transform player, Transform reactor)
    {
        // Fallback
        float euclid = Vector3.Distance(player.position, reactor.position);

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

            // For the monster/reactor side, simplest is a bool flag on ProximityReactor or monster root.
            // If you don't have that yet, assume reactor is in vents when its ZoneId is a vent zone.
            // (Best fix: add a bool IsInVents to reactor/monster)
            bool reactorInVents = true; // replace with your actual signal if you have one
            if (!reactorInVents)
                return euclid;
        }

        float path = VentGraph.Instance.GetPathDistance(reactor.position, player.position);

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
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var player in players)
            {
                PhotonView view = player.GetComponentInParent<PhotonView>();
                if (view != null && view.IsMine)
                {
                    localPlayer = player.transform;
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

    /// <summary>
    /// Zone filtering without hard dependency:
    /// If your ProximityReactor implements a public ZoneId field/property named "ZoneId" or "Zone",
    /// we’ll use it. Otherwise, no zone filtering occurs.
    /// 
    /// Recommended (in ProximityReactor):
    /// public ZoneId ZoneId = ZoneId.Level1;
    /// </summary>
    private static bool TryGetReactorZone(ProximityReactor reactor, out ZoneId zone)
    {
        // Default
        zone = ZoneId.None;

        // Try common member names without forcing you to change Reactor immediately.
        var t = reactor.GetType();

        var field = t.GetField("ZoneId");
        if (field != null && field.FieldType == typeof(ZoneId))
        {
            zone = (ZoneId)field.GetValue(reactor);
            return true;
        }

        field = t.GetField("Zone");
        if (field != null && field.FieldType == typeof(ZoneId))
        {
            zone = (ZoneId)field.GetValue(reactor);
            return true;
        }

        var prop = t.GetProperty("ZoneId");
        if (prop != null && prop.PropertyType == typeof(ZoneId))
        {
            zone = (ZoneId)prop.GetValue(reactor);
            return true;
        }

        prop = t.GetProperty("Zone");
        if (prop != null && prop.PropertyType == typeof(ZoneId))
        {
            zone = (ZoneId)prop.GetValue(reactor);
            return true;
        }

        return false;
    }
}
