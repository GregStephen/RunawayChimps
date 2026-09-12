using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RigSpawnSnapper : MonoBehaviour
{
    [Header("Scene + Spawn")]
    public string HubSceneName = "Hub_Base";
    public string HubSpawnObjectName = "HubSpawn";

    [Header("Timing")]
    public int FramesToWait = 2;
    [Min(1)] public int FixedSettleSteps = 3;

    [Header("XR tracking-origin stability")]
    [Min(2)] public int TrackingOffsetStableFrames = 6;
    [Min(10)] public int TrackingOffsetMaxWaitFrames = 120;
    [Min(0.0001f)] public float TrackingOffsetEpsilon = 0.001f;

    [Header("Grounding")]
    public LayerMask GroundMask = ~0;
    public float GroundSkin = 0.06f;
    public float RaycastUp = 2.0f;
    public float RaycastDown = 5.0f;

    [Header("References (optional, auto-found if blank)")]
    public XROrigin xrOrigin;
    public Rigidbody gorillaPlayerRigidbody;
    public CapsuleCollider gorillaBodyCapsule;

    private bool _snapping;

    private void Awake()
    {
        if (xrOrigin == null)
            xrOrigin = GetComponent<XROrigin>();

        // The Bootstrap rig persists across sector travel. This guard is intentionally
        // created at runtime so it protects startup, travel, capture respawns and later
        // XR tracking-origin/recenter corrections without adding a scene-only reference.
        if (GetComponent<RigFloorPenetrationGuard>() == null)
            gameObject.AddComponent<RigFloorPenetrationGuard>();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The travel coordinator selects the hallway or computer return marker.
        if (RunawayChimps.Travel.SectorTravelService.I != null &&
            RunawayChimps.Travel.SectorTravelService.I.IsBusy) return;
        if (scene.name != HubSceneName)
            return;

        RetrySnap();
    }

    public void RetrySnap()
    {
        if (!SceneManager.GetSceneByName(HubSceneName).isLoaded) return;
        if (_snapping) return;

        StartCoroutine(CoSnapAndGround());
    }

    private IEnumerator CoSnapAndGround()
    {
        _snapping = true;

        // Let the scene initialize enough for the persistent tracked rig to exist.
        for (int i = 0; i < FramesToWait; i++)
            yield return null;

        var hubScene = SceneManager.GetSceneByName(HubSceneName);
        var spawnGo = FindInScene(hubScene, HubSpawnObjectName);
        if (spawnGo == null)
        {
            Debug.LogError($"[RigSpawnSnapper] Could not find '{HubSpawnObjectName}' in loaded scene '{HubSceneName}'.");
            AppState.I?.Fail("Hub spawn is missing.");
            _snapping = false;
            yield break;
        }

        if (xrOrigin == null || xrOrigin.Camera == null)
        {
            Debug.LogError("[RigSpawnSnapper] Missing XROrigin or XROrigin.Camera.");
            AppState.I?.Fail("The tracked rig camera is missing.");
            _snapping = false;
            yield break;
        }

        GorillaLocomotion.Player locomotionPlayer = GorillaLocomotion.Player.Instance;
        if (xrOrigin.CameraFloorOffsetObject != null)
        {
            if (gorillaPlayerRigidbody == null)
                gorillaPlayerRigidbody = xrOrigin.CameraFloorOffsetObject.GetComponent<Rigidbody>();

            if (gorillaBodyCapsule == null && locomotionPlayer != null)
                gorillaBodyCapsule = locomotionPlayer.bodyCollider;

            if (gorillaBodyCapsule == null)
                gorillaBodyCapsule = xrOrigin.CameraFloorOffsetObject.GetComponentInChildren<CapsuleCollider>();
        }

        if (gorillaBodyCapsule == null)
        {
            Debug.LogError("[RigSpawnSnapper] Missing GorillaPlayer body capsule.");
            AppState.I?.Fail("The player body collider is missing.");
            _snapping = false;
            yield break;
        }

        // GorillaPlayer is currently also the XROrigin CameraFloorOffsetObject. Freeze the
        // entire compound collider hierarchy before waiting for that XR-managed offset to
        // settle; otherwise hand/head/body physics and XROrigin can move the same hierarchy
        // at the same time and make a seemingly-correct ground snap invalid a frame later.
        bool playerWasEnabled = locomotionPlayer != null && locomotionPlayer.enabled;
        if (locomotionPlayer != null)
            locomotionPlayer.enabled = false;

        Collider[] rigColliders = xrOrigin.GetComponentsInChildren<Collider>(true);
        bool[] colliderStates = new bool[rigColliders.Length];
        for (int i = 0; i < rigColliders.Length; i++)
        {
            Collider collider = rigColliders[i];
            if (collider == null) continue;
            colliderStates[i] = collider.enabled;
            collider.enabled = false;
        }

        bool hadRb = gorillaPlayerRigidbody != null;
        bool prevKinematic = false;
        if (hadRb)
        {
            prevKinematic = gorillaPlayerRigidbody.isKinematic;
            if (!prevKinematic)
            {
                gorillaPlayerRigidbody.velocity = Vector3.zero;
                gorillaPlayerRigidbody.angularVelocity = Vector3.zero;
            }
            gorillaPlayerRigidbody.isKinematic = true;
        }

        yield return WaitForTrackingOffsetStability();
        yield return new WaitForFixedUpdate();

        Transform cam = xrOrigin.Camera.transform;
        float targetYaw = spawnGo.transform.rotation.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(cam.eulerAngles.y, targetYaw);
        xrOrigin.transform.RotateAround(cam.position, Vector3.up, deltaYaw);

        Vector3 cameraShift = spawnGo.transform.position - cam.position;
        cameraShift.y = 0f;
        xrOrigin.transform.position += cameraShift;

        bool grounded = false;
        int settleSteps = Mathf.Max(1, FixedSettleSteps);
        int stableFixedSteps = 0;
        int settleAttempts = 0;
        int maxSettleAttempts = settleSteps + 12;
        Transform trackingOffset = xrOrigin.CameraFloorOffsetObject != null
            ? xrOrigin.CameraFloorOffsetObject.transform
            : null;
        Vector3 lastTrackingOffset = trackingOffset != null ? trackingOffset.localPosition : Vector3.zero;
        float trackingEpsilonSqr = TrackingOffsetEpsilon * TrackingOffsetEpsilon;

        while (stableFixedSteps < settleSteps && settleAttempts < maxSettleAttempts)
        {
            Physics.SyncTransforms();
            grounded = GroundCorrect(spawnGo.transform.position, hubScene, locomotionPlayer);
            if (!grounded)
                break;

            locomotionPlayer?.ResetAfterTeleport();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            settleAttempts++;

            if (trackingOffset != null &&
                (trackingOffset.localPosition - lastTrackingOffset).sqrMagnitude > trackingEpsilonSqr)
            {
                lastTrackingOffset = trackingOffset.localPosition;
                stableFixedSteps = 0;
            }
            else
            {
                stableFixedSteps++;
            }
        }

        // Give XROrigin one final rendered update while the compound rig is still frozen.
        // If it applies a late floor/device tracking-origin offset, ground against the new
        // physical body pose before any collider or Rigidbody response is restored.
        if (grounded)
        {
            yield return null;
            Physics.SyncTransforms();
            grounded = GroundCorrect(spawnGo.transform.position, hubScene, locomotionPlayer);
            locomotionPlayer?.ResetAfterTeleport();
            Physics.SyncTransforms();
        }

        SphereCollider headCollider = locomotionPlayer != null ? locomotionPlayer.headCollider : null;
        bool clear = grounded && HasSafeClearance(hubScene, headCollider, locomotionPlayer, out string blocker);

        // Restore the entire compound collider set while the Rigidbody is still kinematic,
        // then release physics. The post-release RigFloorPenetrationGuard catches any late
        // XR/physics displacement without continuously changing normal locomotion height.
        for (int i = 0; i < rigColliders.Length; i++)
        {
            if (rigColliders[i] != null)
                rigColliders[i].enabled = colliderStates[i];
        }
        Physics.SyncTransforms();

        if (hadRb)
        {
            gorillaPlayerRigidbody.isKinematic = prevKinematic;
            if (!prevKinematic)
            {
                gorillaPlayerRigidbody.velocity = Vector3.zero;
                gorillaPlayerRigidbody.angularVelocity = Vector3.zero;
                gorillaPlayerRigidbody.WakeUp();
            }
        }
        if (locomotionPlayer != null)
            locomotionPlayer.enabled = playerWasEnabled;
        Physics.SyncTransforms();

        if (!grounded)
        {
            AppState.I?.Fail("No safe floor was found beneath the Hub spawn.");
            _snapping = false;
            yield break;
        }

        if (!clear)
        {
            Debug.LogError($"[RigSpawnSnapper] Hub spawn clearance is blocked by '{blocker}'.", this);
            AppState.I?.Fail("The Hub spawn area is obstructed.");
            _snapping = false;
            yield break;
        }

        AppState.I?.MarkRigSnapped();
        AppState.I?.TryMarkReady();

        Debug.Log(
            $"[RigSpawnSnapper] Snapped + ground-corrected after {settleAttempts} fixed attempt(s) / " +
            $"{stableFixedSteps} stable step(s); XR floor offset was stabilized before release.");
        _snapping = false;
    }

    private IEnumerator WaitForTrackingOffsetStability()
    {
        if (xrOrigin == null || xrOrigin.CameraFloorOffsetObject == null)
            yield break;

        Transform offset = xrOrigin.CameraFloorOffsetObject.transform;
        Vector3 last = offset.localPosition;
        int stableFrames = 0;
        int waitedFrames = 0;
        int requiredStable = Mathf.Max(2, TrackingOffsetStableFrames);
        int maxWait = Mathf.Max(requiredStable, TrackingOffsetMaxWaitFrames);
        float epsilonSqr = TrackingOffsetEpsilon * TrackingOffsetEpsilon;

        while (stableFrames < requiredStable && waitedFrames < maxWait)
        {
            yield return null;
            waitedFrames++;

            Vector3 current = offset.localPosition;
            if ((current - last).sqrMagnitude <= epsilonSqr)
            {
                stableFrames++;
            }
            else
            {
                last = current;
                stableFrames = 0;
            }
        }

        if (stableFrames < requiredStable)
        {
            Debug.LogWarning(
                $"[RigSpawnSnapper] XR floor-offset transform did not remain stable for {requiredStable} frames; " +
                "continuing with guarded grounding and post-release recovery.",
                this);
        }
    }

    private bool GroundCorrect(Vector3 referencePos, Scene hubScene, GorillaLocomotion.Player locomotionPlayer)
    {
        if (gorillaBodyCapsule == null) return false;

        GetCapsule(gorillaBodyCapsule, out Vector3 a, out Vector3 b, out float radius);
        Vector3 bodyCenter = (a + b) * 0.5f;
        float bodyTop = Mathf.Max(a.y, b.y) + radius;
        float rayStartY = Mathf.Max(referencePos.y + RaycastUp, bodyTop + 0.25f);
        Vector3 rayStart = new Vector3(bodyCenter.x, rayStartY, bodyCenter.z);
        float rayDistance = rayStartY - (referencePos.y - RaycastDown);

        int mask = GetGroundMask(locomotionPlayer);
        RaycastHit floor = default;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(rayStart, Vector3.down, rayDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(xrOrigin.transform) || hit.collider.gameObject.scene != hubScene ||
                hit.normal.y < 0.65f || hit.point.y > referencePos.y + 0.2f) continue;
            if (!found || hit.distance < floor.distance) { floor = hit; found = true; }
        }
        if (!found) return false;

        float bottom = Mathf.Min(a.y, b.y) - radius;
        xrOrigin.transform.position += Vector3.up * (floor.point.y + Mathf.Max(0.03f, GroundSkin) - bottom);
        Physics.SyncTransforms();
        return true;
    }

    private bool HasSafeClearance(
        Scene scene,
        SphereCollider headCollider,
        GorillaLocomotion.Player locomotionPlayer,
        out string blocker)
    {
        blocker = null;
        int mask = GetGroundMask(locomotionPlayer);

        GetCapsule(gorillaBodyCapsule, out var a, out var b, out var radius);
        foreach (var collider in Physics.OverlapCapsule(a, b, radius, mask, QueryTriggerInteraction.Ignore))
        {
            if (!IsExternalSceneCollider(collider, scene)) continue;
            blocker = collider.name;
            return false;
        }

        if (headCollider == null)
            return true;

        Vector3 scale = headCollider.transform.lossyScale;
        float headRadius = headCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 center = headCollider.transform.TransformPoint(headCollider.center);
        foreach (var collider in Physics.OverlapSphere(center, headRadius, mask, QueryTriggerInteraction.Ignore))
        {
            if (!IsExternalSceneCollider(collider, scene)) continue;
            blocker = collider.name;
            return false;
        }
        return true;
    }

    private int GetGroundMask(GorillaLocomotion.Player locomotionPlayer)
    {
        int mask = GroundMask.value;
        if (locomotionPlayer == null)
            return mask;

        int locomotionMask = locomotionPlayer.locomotionEnabledLayers.value;
        int intersection = mask & locomotionMask;
        return intersection != 0 ? intersection : mask;
    }

    private bool IsExternalSceneCollider(Collider collider, Scene scene)
    {
        return collider != null &&
            !collider.transform.IsChildOf(xrOrigin.transform) &&
            collider.gameObject.scene == scene;
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

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        if (!scene.isLoaded) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == objectName) return root;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.gameObject;
        }
        return null;
    }
}
