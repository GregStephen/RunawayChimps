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
        if (scene.name != HubSceneName)
            return;

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
            _snapping = false;
            yield break;
        }

        if (xrOrigin == null || xrOrigin.Camera == null)
        {
            Debug.LogError("[RigSpawnSnapper] Missing XROrigin or XROrigin.Camera.");
            _snapping = false;
            yield break;
        }

        // Auto-find GorillaPlayer RB + capsule via floor offset object
        if (xrOrigin.CameraFloorOffsetObject != null)
        {
            if (gorillaPlayerRigidbody == null)
                gorillaPlayerRigidbody = xrOrigin.CameraFloorOffsetObject.GetComponent<Rigidbody>();

            if (gorillaBodyCapsule == null)
                gorillaBodyCapsule = xrOrigin.CameraFloorOffsetObject.GetComponentInChildren<CapsuleCollider>();
        }

        // Freeze RB during move
        bool hadRb = gorillaPlayerRigidbody != null;
        bool prevKinematic = false;
        if (hadRb)
        {
            prevKinematic = gorillaPlayerRigidbody.isKinematic;
            gorillaPlayerRigidbody.isKinematic = true;
            gorillaPlayerRigidbody.velocity = Vector3.zero;
            gorillaPlayerRigidbody.angularVelocity = Vector3.zero;
        }

        // 1) Place the CAMERA at spawn (XR-correct)
        xrOrigin.MoveCameraToWorldLocation(spawnGo.transform.position);

        // 2) Apply yaw around camera position
        var cam = xrOrigin.Camera.transform;
        float targetYaw = spawnGo.transform.rotation.eulerAngles.y;
        float deltaYaw = targetYaw - cam.eulerAngles.y;
        transform.RotateAround(cam.position, Vector3.up, deltaYaw);

        // 3) Ground-correct so the capsule bottom sits above floor
        GroundCorrect(cam.position);

        // Restore RB
        if (hadRb)
        {
            gorillaPlayerRigidbody.isKinematic = prevKinematic;
            gorillaPlayerRigidbody.velocity = Vector3.zero;
            gorillaPlayerRigidbody.angularVelocity = Vector3.zero;
            gorillaPlayerRigidbody.WakeUp();
        }

        AppState.I?.MarkRigSnapped();
        AppState.I?.TryMarkReady();

        Debug.Log("[RigSpawnSnapper] Snapped + ground-corrected.");
        _snapping = false;
    }

    private void GroundCorrect(Vector3 referencePos)
    {
        float capsuleBottomOffset = 0.5f; // fallback
        if (gorillaBodyCapsule != null)
        {
            capsuleBottomOffset = Mathf.Max(0.01f, (gorillaBodyCapsule.height * 0.5f) - gorillaBodyCapsule.radius);
        }

        Vector3 rayStart = referencePos + Vector3.up * RaycastUp;
        if (Physics.Raycast(rayStart, Vector3.down, out var hit, RaycastUp + RaycastDown, GroundMask, QueryTriggerInteraction.Ignore))
        {
            float desiredBottomY = hit.point.y + GroundSkin;
            float currentBottomY = referencePos.y - capsuleBottomOffset;
            float deltaY = desiredBottomY - currentBottomY;

            transform.position += new Vector3(0f, deltaY, 0f);
        }
        else
        {
            Debug.LogWarning("[RigSpawnSnapper] GroundCorrect raycast hit nothing. Check GroundMask/colliders.");
        }
    }
}
