using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class RespawnToOriginalSpawn : MonoBehaviour
{
    [Header("Out of bounds")]
    public float killY = -30f;
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
            Debug.Log($"[Respawn] KillY set to: {killY}");
        }
    }

    void Update()
    {

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

        if (grab && grab.isSelected)
        {
            Debug.Log($"[Respawn] {name} was selected. Forcing release.");
            grab.enabled = false;
        }

        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        transform.position = spawnPos + Vector3.up * respawnUpOffset;
        transform.rotation = spawnRot;
        transform.localScale = spawnScale;

        if (grab)
            StartCoroutine(ReenableGrabNextFrame());
    }

    System.Collections.IEnumerator ReenableGrabNextFrame()
    {
        yield return null;

        grab.enabled = true;

        if (verboseLogging)
            Debug.Log($"[Respawn] {name} interactable re-enabled.");
    }
}
