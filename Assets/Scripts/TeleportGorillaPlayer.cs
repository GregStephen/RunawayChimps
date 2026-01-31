using System.Collections;
using UnityEngine;

public class TeleportGorillaPlayer : MonoBehaviour
{
    [Header("References")]
    public Transform GorillaPlayer;
    public GameObject[] ObjectsToDisable;
    public Transform TeleportLocation;

    [Header("Settings")]
    public float WaitTime = 1f;

    [Header("Optional Effects")]
    public GameObject TeleportOverlay;   // Optional
    public AudioSource TeleportSound;    // Optional

    private LayerMask defaultLayers;

    private void Start()
    {
        defaultLayers = GorillaLocomotion.Player.Instance.locomotionEnabledLayers;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("MainCamera"))
        {
            Debug.Log("[Teleport] Triggered by MainCamera");

            // Optional overlay
            if (TeleportOverlay != null)
                TeleportOverlay.SetActive(true);

            // Optional sound
            if (TeleportSound != null)
                TeleportSound.Play();

            foreach (GameObject obj in ObjectsToDisable)
            {
                if (obj != null)
                    obj.SetActive(false);
            }

            StartCoroutine(TeleportAfterDelay());
        }
    }

    private IEnumerator TeleportAfterDelay()
    {
        yield return new WaitForSeconds(WaitTime);

        Rigidbody playerRigidbody = GorillaPlayer.GetComponent<Rigidbody>();

        // Disable collisions
        GorillaLocomotion.Player.Instance.locomotionEnabledLayers = default;
        GorillaLocomotion.Player.Instance.headCollider.enabled = false;
        GorillaLocomotion.Player.Instance.bodyCollider.enabled = false;

        // Make kinematic for teleport
        playerRigidbody.isKinematic = true;

        // Teleport
        if (TeleportLocation != null)
        {
            GorillaPlayer.position = TeleportLocation.position;
            Debug.Log($"[Teleport] Player teleported to {TeleportLocation.position}");
        }
        else
        {
            Debug.LogWarning("[Teleport] No TeleportLocation assigned!");
        }

        playerRigidbody.velocity = Vector3.zero;

        yield return new WaitForSeconds(WaitTime);

        // Re-enable collisions
        GorillaLocomotion.Player.Instance.locomotionEnabledLayers = defaultLayers;
        GorillaLocomotion.Player.Instance.headCollider.enabled = true;
        GorillaLocomotion.Player.Instance.bodyCollider.enabled = true;

        playerRigidbody.isKinematic = false;

        foreach (GameObject obj in ObjectsToDisable)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        // Optional overlay off
        if (TeleportOverlay != null)
            TeleportOverlay.SetActive(false);
    }
}
