using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;
using System.Collections;

public class MonsterTouchRespawnPhotonVR : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Tag on the top Gorilla Rig root object")]
    public string playerTag = "Player";
    [Tooltip("Transform to respawn the player at")]
    public Transform respawnPoint;

    [Header("Fade Settings")]
    public float fadeDuration = 1f;
    public Color fadeColor = Color.black;

    private Canvas fadeCanvas;
    private Image fadeImage;
    private bool isFading = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isFading) return;

        // Get the root of the collider (Gorilla Rig)
        Transform rigRoot = other.transform.root;

        if (!rigRoot.CompareTag(playerTag))
        {
            Debug.Log("[MonsterTouch] Collider is not part of player tag");
            return;
        }

        // Look for PhotonView anywhere under the Gorilla Rig
        PhotonView view = rigRoot.GetComponentInChildren<PhotonView>();
        if (view == null)
        {
            Debug.LogWarning("[MonsterTouch] ❌ No PhotonView found in player prefab hierarchy!");
            return;
        }

        // Only local player's rig should react
        if (!view.IsMine)
        {
            Debug.Log("[MonsterTouch] PhotonView is not local player — skipping.");
            return;
        }

        // Find XRRig in the rig to handle camera/fade
        XRRig xrRig = rigRoot.GetComponentInChildren<XRRig>();
        if (xrRig == null)
        {
            Debug.LogWarning("[MonsterTouch] ❌ No XRRig found in player hierarchy!");
            return;
        }

        Debug.Log("[MonsterTouch] ✅ Local player hit! Triggering fade + respawn.");
        SetupFadeCanvas(xrRig);
        StartCoroutine(FadeAndRespawn(xrRig));
    }

    private void SetupFadeCanvas(XRRig xrRig)
    {
        if (fadeCanvas != null) return;

        var cam = xrRig.cameraGameObject;
        GameObject canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(cam.transform, false);

        fadeCanvas = canvasGO.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        fadeImage = new GameObject("FadeImage").AddComponent<Image>();
        fadeImage.transform.SetParent(fadeCanvas.transform, false);
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

        RectTransform rect = fadeImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private IEnumerator FadeAndRespawn(XRRig xrRig)
    {
        isFading = true;

        // Fade out
        yield return StartCoroutine(Fade(0f, 1f));

        // Move Gorilla Rig (preserves camera offset)
        Vector3 cameraOffset = xrRig.cameraGameObject.transform.position - xrRig.transform.position;
        xrRig.transform.position = respawnPoint.position - cameraOffset + Vector3.up * 0.1f;
        xrRig.transform.rotation = respawnPoint.rotation;

        yield return new WaitForSeconds(0.25f);

        // Fade in
        yield return StartCoroutine(Fade(1f, 0f));

        isFading = false;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, endAlpha);
    }
}
