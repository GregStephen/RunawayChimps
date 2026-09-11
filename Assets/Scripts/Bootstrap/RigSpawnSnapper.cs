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
        // Prevent double-starts if the event fires twice for any reason
        if (_snapping) return;

        StartCoroutine(CoSnapAndGround());
    }

    private IEnumerator CoSnapAndGround()
    {
        _snapping = true;

        // Let Hub scene init and XR tracking deliver an initial pose.
        for (int i = 0; i < FramesToWait; i++)
            yield return null;

        yield return new WaitForFixedUpdate();

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

        // Auto-find the GorillaPlayer rigidbody/body capsule via the floor offset object.
        // Prefer the locomotion body's explicit collider so hand/head capsules cannot be
        // selected accidentally if the hierarchy changes later.
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

        // Keep locomotion and collision response frozen for the whole settling window.
        // XR head pose can still change for a few frames after the Hub becomes visible;
        // repeatedly grounding while frozen prevents that late pose from leaving the
        // capsule partially inside the floor when physics resumes.
        bool playerWasEnabled = locomotionPlayer != null && locomotionPlayer.enabled;
        bool bodyColliderWasEnabled = gorillaBodyCapsule.enabled;
        SphereCollider headCollider = locomotionPlayer != null ? locomotionPlayer.headCollider : null;
        bool headColliderWasEnabled = headCollider != null && headCollider.enabled;
        if (locomotionPlayer != null) locomotionPlayer.enabled = false;
        gorillaBodyCapsule.enabled = false;
        if (headCollider != null) headCollider.enabled = false;

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

        Transform cam = xrOrigin.Camera.transform;
        float targetYaw = spawnGo.transform.rotation.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(cam.eulerAngles.y, targetYaw);
        xrOrigin.transform.RotateAround(cam.position, Vector3.up, deltaYaw);

        Vector3 cameraShift = spawnGo.transform.position - cam.position;
        cameraShift.y = 0f;
        xrOrigin.transform.position += cameraShift;

        bool grounded = false;
        int settleSteps = Mathf.Max(1, FixedSettleSteps);
        for (int step = 0; step < settleSteps; step++)
        {
            Physics.SyncTransforms();
            grounded = GroundCorrect(spawnGo.transform.position, hubScene);
            if (!grounded)
                break;

            locomotionPlayer?.ResetAfterTeleport();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
        }

        // One final correction after the last fixed tick catches a late headset/body
        // transform update before any collider or locomotion behaviour is restored.
        if (grounded)
        {
            Physics.SyncTransforms();
            grounded = GroundCorrect(spawnGo.transform.position, hubScene);
            locomotionPlayer?.ResetAfterTeleport();
            Physics.SyncTransforms();
        }

        bool clear = grounded && HasSafeClearance(hubScene, headCollider, out string blocker);

        gorillaBodyCapsule.enabled = bodyColliderWasEnabled;
        if (headCollider != null) headCollider.enabled = headColliderWasEnabled;
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
        if (locomotionPlayer != null) locomotionPlayer.enabled = playerWasEnabled;
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

        Debug.Log($"[RigSpawnSnapper] Snapped + ground-corrected after {settleSteps} fixed settle step(s).");
        _snapping = false;
    }

    private bool GroundCorrect(Vector3 referencePos, Scene hubScene)
    {
        if (gorillaBodyCapsule == null) return false;
        Vector3 rayStart = referencePos + Vector3.up * RaycastUp;
        RaycastHit floor = default;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(rayStart, Vector3.down, RaycastUp + RaycastDown, GroundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(xrOrigin.transform) || hit.collider.gameObject.scene != hubScene ||
                hit.normal.y < 0.65f || hit.point.y > referencePos.y + 0.2f) continue;
            if (!found || hit.distance < floor.distance) { floor = hit; found = true; }
        }
        if (!found) return false;

        GetCapsule(gorillaBodyCapsule, out var a, out var b, out var radius);
        float bottom = Mathf.Min(a.y, b.y) - radius;
        xrOrigin.transform.position += Vector3.up * (floor.point.y + Mathf.Max(0.03f, GroundSkin) - bottom);
        Physics.SyncTransforms();
        return true;
    }

    private bool HasSafeClearance(Scene scene, SphereCollider headCollider, out string blocker)
    {
        blocker = null;
        GetCapsule(gorillaBodyCapsule, out var a, out var b, out var radius);
        foreach (var collider in Physics.OverlapCapsule(a, b, radius, GroundMask, QueryTriggerInteraction.Ignore))
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
        foreach (var collider in Physics.OverlapSphere(center, headRadius, GroundMask, QueryTriggerInteraction.Ignore))
        {
            if (!IsExternalSceneCollider(collider, scene)) continue;
            blocker = collider.name;
            return false;
        }
        return true;
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
