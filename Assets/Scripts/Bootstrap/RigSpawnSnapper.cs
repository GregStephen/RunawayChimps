using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using RunawayChimps.Multiplayer;

public class RigSpawnSnapper : MonoBehaviour
{
    [Header("Scene + Spawn")]
    public string HubSceneName = "Hub_Base";
    public string HubSpawnObjectName = "HubSpawn";

    [Header("Timing")]
    public int FramesToWait = 2;
    [Min(1)] public int FixedSettleSteps = 3;
    [Min(1)] public int PostReleaseStableFixedSteps = 3;
    [Min(1)] public int PostReleaseMaxAttempts = 12;
    [Min(1f)] public float SpawnSlotWaitSeconds = 12f;

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
    private bool _retryRequested;
    private int _snapGeneration;
    private IEnumerator _snapRoutine;
    private Coroutine _snapCoroutine;
    private RigFloorPenetrationGuard _floorGuard;
    private HubSpawnSlotAllocator _spawnSlots;

    private void Awake()
    {
        if (xrOrigin == null)
            xrOrigin = GetComponent<XROrigin>();
        _spawnSlots = GetComponent<HubSpawnSlotAllocator>();
        if (_spawnSlots == null) _spawnSlots = gameObject.AddComponent<HubSpawnSlotAllocator>();

        // The Bootstrap rig persists across sector travel. This guard is intentionally
        // created at runtime so it protects startup, travel, capture respawns and later
        // XR tracking-origin/recenter corrections without adding a scene-only reference.
        _floorGuard = GetComponent<RigFloorPenetrationGuard>();
        if (_floorGuard == null)
            _floorGuard = gameObject.AddComponent<RigFloorPenetrationGuard>();
        _floorGuard.ConfigureStartupRecoveryScene(HubSceneName);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDisable()
    {
        _snapGeneration++;
        _retryRequested = false;
        IEnumerator routine = _snapRoutine;
        if (_snapCoroutine != null)
            StopCoroutine(_snapCoroutine);
        // Unity can stop a coroutine when its GameObject is disabled. Explicit disposal
        // guarantees that this attempt restores any compound rig state it still owns.
        (routine as System.IDisposable)?.Dispose();
        _snapRoutine = null;
        _snapCoroutine = null;
        _snapping = false;
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
        Scene hub = SceneManager.GetSceneByName(HubSceneName);
        if (!isActiveAndEnabled || !hub.IsValid() || !hub.isLoaded || IsTravelBusy()) return;

        // A new room/retry must invalidate an in-flight attempt as well as completed
        // placement. The queued request runs after the old attempt restores its rig.
        _snapGeneration++;
        _retryRequested = true;
        AppState.I?.ResetHubPlacementReady();
        if (!_snapping) BeginSnap();
    }

    private void BeginSnap()
    {
        Scene hub = SceneManager.GetSceneByName(HubSceneName);
        if (!_retryRequested || !IsCurrentAttempt(_snapGeneration, hub, null, 0)) return;
        _retryRequested = false;
        _snapping = true;
        IEnumerator routine = CoSnapAndGround(_snapGeneration);
        _snapRoutine = routine;
        Coroutine started = StartCoroutine(routine);
        // StartCoroutine runs up to its first yield immediately; the attempt can have
        // already failed or finished before it returns a handle.
        if (ReferenceEquals(_snapRoutine, routine)) _snapCoroutine = started;
    }

    private static bool IsTravelBusy() => RunawayChimps.Travel.SectorTravelService.I != null &&
        RunawayChimps.Travel.SectorTravelService.I.IsBusy;

    private bool IsCurrentAttempt(int generation, Scene hubScene, Room room, int actorNumber)
    {
        if (!isActiveAndEnabled || generation != _snapGeneration || IsTravelBusy() ||
            !hubScene.IsValid() || !hubScene.isLoaded || SceneManager.GetSceneByName(HubSceneName) != hubScene ||
            (AppState.I != null && !string.IsNullOrEmpty(AppState.I.LastError))) return false;
        return room == null || (PhotonNetwork.InRoom && ReferenceEquals(room, PhotonNetwork.CurrentRoom) &&
            PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber);
    }

    private bool HasLivePhysics(GorillaLocomotion.Player locomotionPlayer) =>
        locomotionPlayer != null && locomotionPlayer.isActiveAndEnabled &&
        gorillaPlayerRigidbody != null && !gorillaPlayerRigidbody.isKinematic &&
        gorillaBodyCapsule != null && gorillaBodyCapsule.enabled;

    private IEnumerator CoSnapAndGround(int generation)
    {
        Scene hubScene = SceneManager.GetSceneByName(HubSceneName);
        Room room = null;
        int actorNumber = 0;
        GorillaLocomotion.Player locomotionPlayer = null;
        Rigidbody frozenBody = null;
        Collider[] rigColliders = null;
        bool[] colliderStates = null;
        bool playerWasEnabled = false;
        bool prevKinematic = false;
        bool frozen = false;

        void RestoreFrozenRig()
        {
            if (!frozen) return;
            frozen = false;
            if (locomotionPlayer != null) locomotionPlayer.ResetAfterTeleport();
            for (int i = 0; i < rigColliders.Length; i++)
                if (rigColliders[i] != null) rigColliders[i].enabled = colliderStates[i];
            Physics.SyncTransforms();
            if (frozenBody != null)
            {
                frozenBody.isKinematic = prevKinematic;
                if (!prevKinematic)
                {
                    frozenBody.velocity = Vector3.zero;
                    frozenBody.angularVelocity = Vector3.zero;
                    frozenBody.WakeUp();
                }
            }
            if (locomotionPlayer != null) locomotionPlayer.enabled = playerWasEnabled;
            Physics.SyncTransforms();
        }

        try
        {
            // Let the scene initialize enough for the persistent tracked rig to exist.
            if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;
            for (int i = 0; i < FramesToWait; i++)
            {
                yield return null;
                if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;
            }

            var spawnGo = FindInScene(hubScene, HubSpawnObjectName);
            if (spawnGo == null)
            {
                Debug.LogError($"[RigSpawnSnapper] Could not find '{HubSpawnObjectName}' in loaded scene '{HubSceneName}'.");
                AppState.I?.Fail("Hub spawn is missing.");
                yield break;
            }

            if (xrOrigin == null || xrOrigin.Camera == null)
            {
                Debug.LogError("[RigSpawnSnapper] Missing XROrigin or XROrigin.Camera.");
                AppState.I?.Fail("The tracked rig camera is missing.");
                yield break;
            }

            locomotionPlayer = GorillaLocomotion.Player.Instance;
            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                if (gorillaPlayerRigidbody == null)
                    gorillaPlayerRigidbody = xrOrigin.CameraFloorOffsetObject.GetComponent<Rigidbody>();

                if (gorillaBodyCapsule == null && locomotionPlayer != null)
                    gorillaBodyCapsule = locomotionPlayer.bodyCollider;

                if (gorillaBodyCapsule == null)
                    gorillaBodyCapsule = xrOrigin.CameraFloorOffsetObject.GetComponentInChildren<CapsuleCollider>();
            }

            if (locomotionPlayer == null || gorillaPlayerRigidbody == null)
            {
                Debug.LogError("[RigSpawnSnapper] Missing Gorilla locomotion or Rigidbody.");
                AppState.I?.Fail("The player locomotion or physics body is missing.");
                yield break;
            }

            if (gorillaBodyCapsule == null)
            {
                Debug.LogError("[RigSpawnSnapper] Missing GorillaPlayer body capsule.");
                AppState.I?.Fail("The player body collider is missing.");
                yield break;
            }

            Vector3 spawnPosition = default;
            Quaternion spawnRotation = Quaternion.identity;
            // Hub can load before authentication/Photon. The global startup timeout owns that wait;
            // the shorter slot timeout starts only after this client is actually in the room.
            while (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null)
            {
                if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;
                yield return null;
            }
            room = PhotonNetwork.CurrentRoom;
            actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            float slotDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, SpawnSlotWaitSeconds);
            while (true)
            {
                if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;
                if (_spawnSlots != null &&
                    _spawnSlots.TryGetLocalSpawnPose(spawnGo.transform, out spawnPosition, out spawnRotation)) break;
                if (Time.realtimeSinceStartup >= slotDeadline)
                {
                    AppState.I?.Fail("Could not reserve a multiplayer Hub spawn slot.");
                    yield break;
                }
                yield return null;
            }

            // GorillaPlayer is currently also the XROrigin CameraFloorOffsetObject. Freeze the
            // entire compound collider hierarchy before waiting for that XR-managed offset to
            // settle; otherwise hand/head/body physics and XROrigin can move the same hierarchy
            // at the same time and make a seemingly-correct ground snap invalid a frame later.
            playerWasEnabled = locomotionPlayer.enabled;
            rigColliders = xrOrigin.GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[rigColliders.Length];
            for (int i = 0; i < rigColliders.Length; i++)
                if (rigColliders[i] != null) colliderStates[i] = rigColliders[i].enabled;

            frozenBody = gorillaPlayerRigidbody;
            prevKinematic = frozenBody.isKinematic;
            frozen = true;
            locomotionPlayer.enabled = false;
            for (int i = 0; i < rigColliders.Length; i++)
                if (rigColliders[i] != null) rigColliders[i].enabled = false;
            if (!prevKinematic)
            {
                frozenBody.velocity = Vector3.zero;
                frozenBody.angularVelocity = Vector3.zero;
            }
            frozenBody.isKinematic = true;

            yield return WaitForTrackingOffsetStability(generation, hubScene, room, actorNumber);
            if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;
            yield return new WaitForFixedUpdate();
            if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;

            Transform cam = xrOrigin.Camera.transform;
            float targetYaw = spawnRotation.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(cam.eulerAngles.y, targetYaw);
            xrOrigin.transform.RotateAround(cam.position, Vector3.up, deltaYaw);

            Vector3 cameraShift = spawnPosition - cam.position;
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
                grounded = GroundCorrect(spawnPosition, hubScene, locomotionPlayer);
                if (!grounded)
                    break;

                locomotionPlayer?.ResetAfterTeleport();
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;
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
                if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;
                Physics.SyncTransforms();
                grounded = GroundCorrect(spawnPosition, hubScene, locomotionPlayer);
                locomotionPlayer?.ResetAfterTeleport();
                Physics.SyncTransforms();
            }

            SphereCollider headCollider = locomotionPlayer != null ? locomotionPlayer.headCollider : null;
            string blocker = null;
            bool clear = grounded &&
                HasSafeClearance(hubScene, headCollider, locomotionPlayer, out blocker);

            // Restore the entire compound collider set while the Rigidbody is still kinematic,
            // then release physics. Do not declare the rig snapped yet: the first live physics
            // steps are exactly where a late XR floor-offset or Gorilla contact correction can
            // invalidate an otherwise-correct frozen placement.
            RestoreFrozenRig();
            if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;

            if (!grounded)
            {
                AppState.I?.Fail("No safe floor was found beneath the Hub spawn.");
                yield break;
            }

            if (!clear)
            {
                Debug.LogError($"[RigSpawnSnapper] Hub spawn clearance is blocked by '{blocker}'.", this);
                AppState.I?.Fail("The Hub spawn area is obstructed.");
                yield break;
            }

            // Prove the *released* rig for consecutive live physics steps before advertising
            // RigSnapped. If a late XR/physics adjustment buries the body, recover it and restart
            // the stability count. This closes the old gap where the guard was disabled until
            // overall startup readiness and while Loading remained the active scene.
            if (!HasLivePhysics(locomotionPlayer))
            {
                AppState.I?.Fail("The player physics could not be released for floor verification.");
                yield break;
            }

            int requiredPostReleaseSteps = Mathf.Max(1, PostReleaseStableFixedSteps);
            int postReleaseAttemptLimit = Mathf.Max(requiredPostReleaseSteps, PostReleaseMaxAttempts);
            int postReleaseStableSteps = 0;
            int postReleaseAttempts = 0;

            while (postReleaseStableSteps < requiredPostReleaseSteps &&
                   postReleaseAttempts < postReleaseAttemptLimit)
            {
                yield return new WaitForFixedUpdate();
                if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;
                if (!HasLivePhysics(locomotionPlayer))
                {
                    AppState.I?.Fail("The player physics was interrupted during floor verification.");
                    yield break;
                }
                Physics.SyncTransforms();
                postReleaseAttempts++;

                if (_floorGuard == null ||
                    !_floorGuard.EnsureAboveSupportFloor(hubScene, out bool recovered))
                {
                    Debug.LogError(
                        "[RigSpawnSnapper] Could not verify a safe Hub support floor after releasing rig physics.",
                        this);
                    AppState.I?.Fail("The player could not settle safely above the Hub floor.");
                    yield break;
                }

                if (recovered)
                {
                    postReleaseStableSteps = 0;
                    continue;
                }

                postReleaseStableSteps++;
            }

            if (postReleaseStableSteps < requiredPostReleaseSteps)
            {
                Debug.LogError(
                    $"[RigSpawnSnapper] Released rig did not remain floor-safe for {requiredPostReleaseSteps} consecutive fixed steps " +
                    $"within {postReleaseAttemptLimit} attempts.",
                    this);
                AppState.I?.Fail("The player could not stabilize above the Hub floor.");
                yield break;
            }

            if (!IsCurrentAttempt(generation, hubScene, room, actorNumber) || !HasLivePhysics(locomotionPlayer))
                yield break;
            AppState.I?.MarkRigSnapped();
            AppState.I?.TryMarkReady();

            Debug.Log(
                $"[RigSpawnSnapper] Snapped + ground-corrected after {settleAttempts} frozen fixed attempt(s) / " +
                $"{stableFixedSteps} stable frozen step(s), then {postReleaseStableSteps} stable live-physics step(s) " +
                $"across {postReleaseAttempts} post-release attempt(s).");
        }
        finally
        {
            RestoreFrozenRig();
            _snapping = false;
            _snapRoutine = null;
            _snapCoroutine = null;
            if (_retryRequested) BeginSnap();
        }
    }

    private IEnumerator WaitForTrackingOffsetStability(int generation, Scene hubScene, Room room, int actorNumber)
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
            if (!IsCurrentAttempt(generation, hubScene, room, actorNumber) || offset == null) yield break;
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
