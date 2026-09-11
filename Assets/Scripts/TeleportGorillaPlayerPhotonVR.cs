using System.Collections;
using Photon.VR;
using Photon.VR.Player;
using UnityEngine;
using UnityEngine.UI;

public class TeleportGorillaPlayerPhotonVR : MonoBehaviour
{
    [Header("References")]
    public Transform TeleportLocation;      // Target teleport location
    public float WaitTime = 0.25f;          // Delay during teleport

    [Header("Optional Effects")]
    public GameObject TeleportOverlay;      // Optional UI overlay
    public AudioSource TeleportSound;       // Optional teleport sound
    public Color FadeColor = Color.black;
    public float FadeDuration = 0.5f;

    private Canvas fadeCanvas;
    private Image fadeImage;

    private void OnTriggerEnter(Collider other)
    {
        var travel = RunawayChimps.Travel.SectorTravelService.I;
        if (travel != null)
        {
            TryCaptureThroughTravel(other, travel);
            return;
        }

        // Preserve the old standalone-scene fallback. That path was built around the
        // camera collider and an XRRig hierarchy and is not the supported split-scene
        // capture route.
        if (!other.CompareTag("MainCamera"))
            return;

        var localPlayer = PhotonVRManager.Manager.LocalPlayer;
        if (localPlayer == null)
        {
            Debug.LogWarning("[Teleport] Local player not found yet!");
            return;
        }

        Debug.Log("[Teleport] Triggered by local player.");
        StartCoroutine(TeleportSequence(localPlayer));
    }

    private void OnTriggerStay(Collider other)
    {
        // A player can begin overlapping the Crawler while a zone/travel transition is
        // still finishing. Retry the guarded split-scene path while overlap continues so
        // that contact cannot be permanently missed just because the first Enter frame
        // was ineligible. SectorTravelService.IsBusy prevents duplicate captures.
        var travel = RunawayChimps.Travel.SectorTravelService.I;
        if (travel != null)
            TryCaptureThroughTravel(other, travel);
    }

    private void TryCaptureThroughTravel(Collider other, RunawayChimps.Travel.SectorTravelService travel)
    {
        var localRig = other.GetComponentInParent<LocalRigMarker>();
        if (localRig == null || travel.IsBusy)
            return;

        var zones = RunawayChimps.Zones.ZoneStateService.Instance;
        if (travel.CurrentSector != RunawayChimps.Travel.SectorId.Containment ||
            zones == null || zones.LocalZone != RunawayChimps.Zones.ZoneId.Level1_Vents)
            return;

        var player = GorillaLocomotion.Player.Instance;
        Vector3 capturePosition = player != null ? player.transform.position : other.bounds.center;

        if (travel.RespawnAt(TeleportLocation))
        {
            PlayerInventory.LocalInventory?.DropAllKeyCards(capturePosition);
            if (TeleportSound != null) TeleportSound.Play();
        }
    }

    private IEnumerator TeleportSequence(PhotonVRPlayer localPlayer)
    {
        // Setup fade canvas
        SetupFadeCanvas(localPlayer);

        // Optional overlay
        if (TeleportOverlay != null) TeleportOverlay.SetActive(true);
        if (TeleportSound != null) TeleportSound.Play();

        // Fade out
        Debug.Log("[Teleport] Starting fade out...");
        yield return StartCoroutine(Fade(0f, 1f));

        // Find Gorilla Locomotion player
        var player = GorillaLocomotion.Player.Instance;
        if (player == null)
        {
            Debug.LogError("[Teleport] Gorilla Player instance not found!");
            yield break;
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[Teleport] Local player body has no Rigidbody!");
            yield break;
        }

        Debug.Log("[Teleport] Dropping all keycards at player's position...");
        PlayerInventory.LocalInventory?.DropAllKeyCards(player.transform.position);

        int originalLayers = player.locomotionEnabledLayers;

        player.locomotionEnabledLayers = 0;
        player.headCollider.enabled = false;
        player.bodyCollider.enabled = false;

        rb.isKinematic = true;
        rb.velocity = Vector3.zero;

        Vector3 headOffset = localPlayer.Head.position - player.transform.position;
        player.transform.position = TeleportLocation.position - headOffset;
        player.transform.rotation = TeleportLocation.rotation;

        Debug.Log($"[Teleport] Player teleported to {player.transform.position}");

        yield return new WaitForSeconds(WaitTime);

        player.locomotionEnabledLayers = originalLayers;
        player.headCollider.enabled = true;
        player.bodyCollider.enabled = true;
        rb.isKinematic = false;

        Debug.Log("[Teleport] Starting fade in...");
        yield return StartCoroutine(Fade(1f, 0f));

        if (TeleportOverlay != null) TeleportOverlay.SetActive(false);

        Debug.Log("[Teleport] Teleport sequence completed.");
    }

    private void SetupFadeCanvas(PhotonVRPlayer player)
    {
        if (fadeCanvas != null) return;

        GameObject canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(player.Head, false);

        fadeCanvas = canvasGO.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        fadeImage = new GameObject("FadeImage").AddComponent<Image>();
        fadeImage.transform.SetParent(fadeCanvas.transform, false);
        fadeImage.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, 0f);

        RectTransform rect = fadeImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / FadeDuration);
            fadeImage.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, alpha);
            yield return null;
        }
        fadeImage.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, endAlpha);
    }
}
