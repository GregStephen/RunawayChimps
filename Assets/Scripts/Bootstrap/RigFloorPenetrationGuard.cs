using RunawayChimps.Travel;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Last-line recovery for the persistent Gorilla locomotion body when XR tracking-origin
/// or physics settling moves the body capsule slightly below a valid walkable floor after
/// startup, sector travel, respawn, or headset recentering. It never pulls a player down
/// and only corrects shallow penetration into a floor directly beneath the actual body.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class RigFloorPenetrationGuard : MonoBehaviour
{
    [Header("Floor recovery")]
    [SerializeField, Min(0.001f)] private float allowedPenetration = 0.015f;
    [SerializeField, Min(0.01f)] private float recoverySkin = 0.04f;
    [SerializeField, Min(0.1f)] private float maxRecoveryDepth = 0.75f;
    [SerializeField, Min(0.1f)] private float probeAboveBody = 0.8f;
    [SerializeField, Min(0.1f)] private float probeBelowBody = 1.5f;
    [SerializeField, Min(0.05f)] private float recoveryCooldown = 0.2f;

    private readonly RaycastHit[] floorHits = new RaycastHit[16];
    private XROrigin origin;
    private GorillaLocomotion.Player player;
    private Rigidbody body;
    private float nextRecoveryTime;
    private int recoveryCount;
    private bool warnedDeepPenetration;
    private bool warnedHitBufferFull;
    private string startupRecoverySceneName = "Hub_Base";

    private void Awake()
    {
        TryBind();
    }

    /// <summary>
    /// Tells the persistent guard which loaded world scene owns cold-start floor geometry
    /// while Loading is still the active scene. Normal sector travel remains protected by
    /// SectorTravelService and uses the destination scene after travel completes.
    /// </summary>
    public void ConfigureStartupRecoveryScene(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
            startupRecoverySceneName = sceneName;
    }

    private void FixedUpdate()
    {
        if (Time.unscaledTime < nextRecoveryTime || !TryBind())
            return;

        // The travel/startup placement paths deliberately own the rig while they are busy.
        // Do not fight their kinematic placement. Once RigSpawnSnapper has completed its
        // post-release proof, however, keep guarding the body even if the Loading scene is
        // still visible and overall Photon/avatar startup readiness has not finished yet.
        if (SectorTravelService.I != null && SectorTravelService.I.IsBusy)
            return;
        if (AppState.I != null && !AppState.I.RigSnapped)
            return;
        if (!player.enabled || !player.bodyCollider.enabled || body.isKinematic)
            return;

        Scene scene = ResolveRecoveryScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (EnsureAboveSupportFloor(scene, out bool recovered) && recovered)
            nextRecoveryTime = Time.unscaledTime + recoveryCooldown;
    }

    /// <summary>
    /// Verifies that the real Gorilla body capsule is not below a same-scene walkable
    /// support floor. Shallow penetration is lifted immediately; this method never moves
    /// the player downward. It is public so RigSpawnSnapper can prove the released rig for
    /// several physics steps before startup is marked snapped.
    /// </summary>
    public bool EnsureAboveSupportFloor(Scene scene, out bool recovered)
    {
        recovered = false;
        if (!TryBind() || !scene.IsValid() || !scene.isLoaded || !player.bodyCollider.enabled)
            return false;

        Physics.SyncTransforms();
        if (!TryFindSupportFloor(scene, out RaycastHit floor, out float bodyBottom))
            return false;

        float penetration = floor.point.y - bodyBottom;
        if (penetration <= allowedPenetration)
            return true;

        if (penetration > maxRecoveryDepth)
        {
            WarnDeepPenetration(scene, penetration);
            return false;
        }

        RecoverFromFloor(scene, penetration);
        recovered = true;

        // Verify the correction against the actual capsule/floor pose rather than assuming
        // the transform move succeeded. A failed verification keeps startup from declaring
        // the rig safe and exposing a player already intersecting the floor.
        Physics.SyncTransforms();
        if (!TryFindSupportFloor(scene, out floor, out bodyBottom))
            return false;

        return floor.point.y - bodyBottom <= allowedPenetration;
    }

    private Scene ResolveRecoveryScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded && active.name != "Loading")
            return active;

        // Cold startup intentionally keeps Loading active while Hub is already loaded.
        // The old guard skipped this entire interval, leaving a gap after physics was
        // released. Use the configured Hub scene for support-floor checks during that gap.
        if (!string.IsNullOrWhiteSpace(startupRecoverySceneName))
        {
            Scene startupScene = SceneManager.GetSceneByName(startupRecoverySceneName);
            if (startupScene.IsValid() && startupScene.isLoaded)
                return startupScene;
        }

        return default;
    }

    private bool TryBind()
    {
        if (player == null)
            player = GorillaLocomotion.Player.Instance;
        if (origin == null && player != null)
            origin = player.GetComponentInParent<XROrigin>();
        if (body == null && player != null)
            body = player.GetComponent<Rigidbody>();

        return player != null && origin != null && origin.Camera != null && body != null && player.bodyCollider != null;
    }

    private bool TryFindSupportFloor(Scene scene, out RaycastHit floor, out float bodyBottom)
    {
        floor = default;
        bodyBottom = 0f;

        GetCapsule(player.bodyCollider, out Vector3 a, out Vector3 b, out float radius);
        Vector3 center = (a + b) * 0.5f;
        bodyBottom = Mathf.Min(a.y, b.y) - radius;
        float bodyTop = Mathf.Max(a.y, b.y) + radius;

        Vector3 rayStart = new Vector3(center.x, bodyTop + probeAboveBody, center.z);
        float rayDistance = probeAboveBody + (bodyTop - bodyBottom) + probeBelowBody + maxRecoveryDepth;
        int hitCount = Physics.RaycastNonAlloc(
            rayStart,
            Vector3.down,
            floorHits,
            rayDistance,
            player.locomotionEnabledLayers,
            QueryTriggerInteraction.Ignore);

        if (hitCount == floorHits.Length && !warnedHitBufferFull)
        {
            warnedHitBufferFull = true;
            Debug.LogWarning(
                "[RigFloorPenetrationGuard] Floor probe hit buffer filled; inspect unusually dense collision around the player if recovery becomes unreliable.",
                this);
        }

        bool found = false;
        float nearestVerticalDistance = float.MaxValue;
        float highestRecoverableFloor = Mathf.Min(bodyBottom + maxRecoveryDepth, center.y);
        float lowestSupportFloor = bodyBottom - probeBelowBody;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = floorHits[i];
            if (hit.collider == null || hit.collider.gameObject.scene != scene ||
                hit.collider.transform.IsChildOf(origin.transform) || hit.normal.y < 0.65f ||
                hit.point.y > highestRecoverableFloor || hit.point.y < lowestSupportFloor)
                continue;

            float verticalDistance = Mathf.Abs(hit.point.y - bodyBottom);
            if (found && verticalDistance >= nearestVerticalDistance)
                continue;

            floor = hit;
            nearestVerticalDistance = verticalDistance;
            found = true;
        }

        return found;
    }

    private void WarnDeepPenetration(Scene scene, float penetration)
    {
        if (warnedDeepPenetration)
            return;

        warnedDeepPenetration = true;
        Debug.LogError(
            $"[RigFloorPenetrationGuard] Body is {penetration:0.000} m below a floor in '{scene.name}', " +
            "which exceeds the automatic recovery limit. Inspect the spawn/floor geometry.",
            this);
    }

    private void RecoverFromFloor(Scene scene, float penetration)
    {
        bool playerWasEnabled = player.enabled;
        bool wasKinematic = body.isKinematic;

        if (playerWasEnabled)
            player.enabled = false;

        if (!wasKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;

        origin.transform.position += Vector3.up * (penetration + recoverySkin);
        Physics.SyncTransforms();
        player.ResetAfterTeleport();
        Physics.SyncTransforms();

        body.isKinematic = wasKinematic;
        if (!wasKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        if (playerWasEnabled)
            player.enabled = true;

        recoveryCount++;

        if (recoveryCount <= 5)
        {
            Debug.LogWarning(
                $"[RigFloorPenetrationGuard] Recovered the Gorilla body from {penetration:0.000} m of floor penetration in '{scene.name}'.",
                this);
        }
    }

    private static void GetCapsule(CapsuleCollider capsule, out Vector3 a, out Vector3 b, out float radius)
    {
        Vector3 scale = capsule.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        int axis = capsule.direction;
        Vector3 direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        radius = capsule.radius * Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
        float half = Mathf.Max(0f, capsule.height * scale[axis] * 0.5f - radius);
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 offset = capsule.transform.TransformDirection(direction).normalized * half;
        a = center + offset;
        b = center - offset;
    }
}
