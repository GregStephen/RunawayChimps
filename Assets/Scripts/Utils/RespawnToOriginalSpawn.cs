using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class RespawnToOriginalSpawn : MonoBehaviour
{
    [Header("Out of bounds")]
    public float killY = -30f;
    [Min(0f)] public float maxFallBelowSpawn = 3f;
    public float maxDistanceFromSpawn = 0f;
    public float respawnUpOffset = 0.05f;
    public bool useKillYOnly = true;

    [Header("Debug")]
    public bool verboseLogging = true;

    XRGrabInteractable grab;
    Rigidbody rb;

    Vector3 spawnPos;
    Quaternion spawnRot;
    Vector3 spawnScale;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        spawnPos = transform.position;
        spawnRot = transform.rotation;
        spawnScale = transform.localScale;

        if (verboseLogging)
        {
            Debug.Log($"[Respawn] Awake on {name}");
            Debug.Log($"[Respawn] Spawn Position Recorded: {spawnPos}");
            Debug.Log($"[Respawn] KillY set to: {killY}; relative fall recovery: {maxFallBelowSpawn} m");
        }
    }

    void Update()
    {
        // The original absolute KillY is a useful last-resort guard, but it is not enough
        // for additive levels whose authored world height can vary. Recover as soon as an
        // item has fallen meaningfully below its own recorded spawn height so a keycard that
        // tunnels through a floor cannot disappear indefinitely beneath the map.
        if (maxFallBelowSpawn > 0f && transform.position.y < spawnPos.y - maxFallBelowSpawn)
        {
            if (verboseLogging)
                Debug.Log($"[Respawn] {name} fell more than {maxFallBelowSpawn:0.##} m below its spawn. Triggering respawn.");

            RespawnNow();
            return;
        }

        if (transform.position.y < killY)
        {
            if (verboseLogging)
                Debug.Log($"[Respawn] {name} below KillY ({killY}). Triggering respawn.");

            RespawnNow();
            return;
        }

        if (!useKillYOnly && maxDistanceFromSpawn > 0f)
        {
            float dist = Vector3.Distance(transform.position, spawnPos);

            if (verboseLogging)
                Debug.Log($"[Respawn] {name} Distance from spawn: {dist}");

            if (dist > maxDistanceFromSpawn)
            {
                if (verboseLogging)
                    Debug.Log($"[Respawn] {name} exceeded max distance. Triggering respawn.");

                RespawnNow();
            }
        }
    }

    public void RespawnNow()
    {
        if (verboseLogging)
            Debug.Log($"[Respawn] RespawnNow() called on {name}");

        bool reenableGrab = grab != null && grab.enabled && grab.isSelected;
        if (reenableGrab)
        {
            if (verboseLogging)
                Debug.Log($"[Respawn] {name} was selected. Forcing release.");
            grab.enabled = false;
        }

        Vector3 targetPosition = spawnPos + Vector3.up * Mathf.Max(0.02f, respawnUpOffset);

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Move through the Rigidbody as well as the Transform so interpolation cannot
            // render or restore the old below-floor pose for another physics frame.
            rb.position = targetPosition;
            rb.rotation = spawnRot;
            rb.Sleep();
        }

        transform.SetPositionAndRotation(targetPosition, spawnRot);
        transform.localScale = spawnScale;
        Physics.SyncTransforms();

        if (reenableGrab && isActiveAndEnabled)
            StartCoroutine(ReenableGrabNextFrame());
    }

    System.Collections.IEnumerator ReenableGrabNextFrame()
    {
        yield return null;

        if (grab != null)
            grab.enabled = true;

        if (verboseLogging)
            Debug.Log($"[Respawn] {name} interactable re-enabled.");
    }
}
