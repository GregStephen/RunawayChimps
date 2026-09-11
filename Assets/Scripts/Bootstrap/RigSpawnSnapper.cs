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

    [Header("Grounding")]
    public LayerMask GroundMask = ~0;
    public float GroundSkin = 0.03f;
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

        // Let Hub scene init
        for (int i = 0; i < FramesToWait; i++)
            yield return null;

        // Wait for physics tick (important for capsules/ground)
        yield return new WaitForFixedUpdate();

        var spawnGo = GameObject.Find(HubSpawnObjectName);
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

        // Freeze RB during move
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

        // Spawn markers represent a floor-relative destination, not an eye-height target.
        // Keep the tracked camera's current vertical offset, align only X/Z + yaw, then
        // ground the locomotion capsule. This matches SectorTravelService.PlaceRig and
        // prevents startup from forcing the headset/camera down to a floor marker.
        float targetYaw = spawnGo.transform.rotation.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(cam.eulerAngles.y, targetYaw);
        xrOrigin.transform.RotateAround(cam.position, Vector3.up, deltaYaw);

        Vector3 cameraShift = spawnGo.transform.position - cam.position;
        cameraShift.y = 0f;
        xrOrigin.transform.position += cameraShift;

        // Ground-correct so the body capsule bottom sits just above the actual Hub floor.
        Physics.SyncTransforms();
        bool grounded = GroundCorrect(spawnGo.transform.position);
        locomotionPlayer?.ResetAfterTeleport();
        Physics.SyncTransforms();

        // Restore RB
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

        if (!grounded)
        {
            AppState.I?.Fail("No safe floor was found beneath the Hub spawn.");
            _snapping = false;
            yield break;
        }
        AppState.I?.MarkRigSnapped();
        AppState.I?.TryMarkReady();

        Debug.Log("[RigSpawnSnapper] Snapped + ground-corrected.");
        _snapping = false;
    }

    private bool GroundCorrect(Vector3 referencePos)
    {
        if (gorillaBodyCapsule == null) return false;
        Vector3 rayStart = referencePos + Vector3.up * RaycastUp;
        RaycastHit floor = default;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(rayStart, Vector3.down, RaycastUp + RaycastDown, GroundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(xrOrigin.transform) || hit.collider.gameObject.scene.name != HubSceneName ||
                hit.normal.y < 0.65f || hit.point.y > referencePos.y + 0.2f) continue;
            if (!found || hit.distance < floor.distance) { floor = hit; found = true; }
        }
        if (!found) return false;
        // Bounds include the real capsule center, radius and transform scale.
        xrOrigin.transform.position += Vector3.up * (floor.point.y + GroundSkin - gorillaBodyCapsule.bounds.min.y);
        return true;
    }
}
