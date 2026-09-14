using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Matches physical color/symbol keycards to a reader, provides immediate LED feedback,
/// and optionally submits a matching gameplay KeyCard to the same-scene objective KeyBox.
/// Persistent accepted-card progress is displayed separately by KeycardLockProgressIndicator.
/// </summary>
[DisallowMultipleComponent]
public sealed class KeycardReaderLightController : MonoBehaviour
{
    public enum CredentialType
    {
        Auto = 0,
        AmberTriangle = 1,
        CyanThreeBars = 2,
        RedCircle = 3,
        VioletDiamond = 4,
    }

    [Header("Reader identity")]
    [Tooltip("Auto infers the credential from the reader root name, e.g. Reader_Amber_Triangle.")]
    [SerializeField] private CredentialType expectedCredential = CredentialType.Auto;
    [SerializeField] private Transform readerRoot;

    [Header("Status LED")]
    [SerializeField] private Renderer statusLedRenderer;
    [SerializeField] private Material standbyMaterial;
    [SerializeField] private Material acceptedMaterial;
    [Tooltip("How long the reader stays green after an accepted card is consumed.")]
    [SerializeField, Min(0f)] private float acceptedFlashSeconds = 0.45f;

    [Header("Objective submission")]
    [Tooltip("When enabled, a matching gameplay KeyCard is submitted through KeyBox.TryAddKey.")]
    [SerializeField] private bool submitMatchingCardsToKeyBox = true;
    [SerializeField] private KeyBox keyBox;
    [Tooltip("If no KeyBox is assigned, bind the unique Level 1 completion KeyBox in this scene, or the unique same-scene KeyBox as a fallback.")]
    [SerializeField] private bool autoBindUniqueKeyBoxInScene = true;

    private readonly HashSet<Collider> acceptedColliders = new HashSet<Collider>();
    private CredentialType resolvedCredential;
    private bool showingAccepted;
    private bool warnedAboutConfiguration;
    private bool warnedMissingGameplayCard;
    private bool warnedMissingProgressSource;
    private float acceptedVisualUntil = -1f;

    private void Awake()
    {
        ResolveReferences();
        ResolveKeyBox();

        resolvedCredential = expectedCredential == CredentialType.Auto
            ? ParseCredential(readerRoot != null ? readerRoot.name : string.Empty)
            : expectedCredential;

        if (standbyMaterial == null && statusLedRenderer != null)
            standbyMaterial = statusLedRenderer.sharedMaterial;

        if (resolvedCredential == CredentialType.Auto || statusLedRenderer == null ||
            standbyMaterial == null || acceptedMaterial == null)
        {
            WarnAboutConfiguration();
        }

        SetAcceptedVisual(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolveMatchingCard(other, out KeyCard gameplayCard))
            return;

        acceptedColliders.Add(other);
        SetAcceptedVisual(true);

        if (!submitMatchingCardsToKeyBox)
            return;

        if (gameplayCard == null)
        {
            WarnMissingGameplayCard();
            return;
        }

        if (keyBox == null)
            ResolveKeyBox();

        if (keyBox == null)
        {
            WarnMissingProgressSource();
            return;
        }

        if (gameplayCard.TryInsertInto(keyBox))
        {
            // The accepted card is destroyed by its normal lifecycle, so hold the
            // reader green briefly even after its collider disappears.
            acceptedVisualUntil = Mathf.Max(
                acceptedVisualUntil,
                Time.unscaledTime + Mathf.Max(0f, acceptedFlashSeconds));
            SetAcceptedVisual(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!acceptedColliders.Remove(other))
            return;

        RefreshAcceptedVisual();
    }

    private void LateUpdate()
    {
        if (!showingAccepted)
            return;

        RefreshAcceptedVisual();
    }

    private void OnDisable()
    {
        acceptedColliders.Clear();
        acceptedVisualUntil = -1f;
        SetAcceptedVisual(false);
    }

    private void ResolveReferences()
    {
        if (readerRoot == null)
            readerRoot = transform.parent;

        if (statusLedRenderer != null || readerRoot == null)
            return;

        Transform led = readerRoot.Find("Status_LED");
        if (led != null)
            statusLedRenderer = led.GetComponent<Renderer>();
    }

    private void ResolveKeyBox()
    {
        if (keyBox != null)
            return;

        KeyBox parentBox = GetComponentInParent<KeyBox>();
        if (parentBox != null)
        {
            keyBox = parentBox;
            return;
        }

        if (!autoBindUniqueKeyBoxInScene || !gameObject.scene.IsValid())
            return;

        KeyBox[] boxes = FindObjectsOfType<KeyBox>(true);
        KeyBox uniqueCompletionBox = null;
        KeyBox uniqueSameSceneBox = null;
        int completionMatches = 0;
        int sameSceneMatches = 0;

        foreach (KeyBox box in boxes)
        {
            if (box == null || box.gameObject.scene != gameObject.scene)
                continue;

            uniqueSameSceneBox = box;
            sameSceneMatches++;

            if (!box.travelToLevelTwoOnComplete)
                continue;

            uniqueCompletionBox = box;
            completionMatches++;
        }

        if (completionMatches == 1)
            keyBox = uniqueCompletionBox;
        else if (completionMatches == 0 && sameSceneMatches == 1)
            keyBox = uniqueSameSceneBox;
    }

    private bool TryResolveMatchingCard(Collider other, out KeyCard gameplayCard)
    {
        gameplayCard = null;
        if (other == null || resolvedCredential == CredentialType.Auto)
            return false;

        gameplayCard = other.GetComponentInParent<KeyCard>();
        CredentialType presented = CredentialType.Auto;

        // Prefer the collider/ancestor names. This works when the imported colored
        // card itself owns the collider or when the gameplay wrapper carries the identity.
        Transform candidate = other.transform;
        while (candidate != null)
        {
            presented = ParseCredential(candidate.name);
            if (presented != CredentialType.Auto)
                break;

            if (gameplayCard != null && candidate == gameplayCard.transform)
                break;

            candidate = candidate.parent;
        }

        // A reusable gameplay KeyCard wrapper may instead contain the colored FBX as
        // a child, so fall back to its visual hierarchy for the credential identity.
        if (presented == CredentialType.Auto && gameplayCard != null)
            presented = FindCredentialInHierarchy(gameplayCard.transform);

        return presented != CredentialType.Auto && presented == resolvedCredential;
    }

    private static CredentialType FindCredentialInHierarchy(Transform root)
    {
        if (root == null)
            return CredentialType.Auto;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            CredentialType credential = ParseCredential(candidate.name);
            if (credential != CredentialType.Auto)
                return credential;
        }

        return CredentialType.Auto;
    }

    private static CredentialType ParseCredential(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return CredentialType.Auto;

        string normalized = Normalize(objectName);
        if (normalized.Contains("ambertriangle"))
            return CredentialType.AmberTriangle;
        if (normalized.Contains("cyanthreebars") || normalized.Contains("cyan3bars"))
            return CredentialType.CyanThreeBars;
        if (normalized.Contains("redcircle"))
            return CredentialType.RedCircle;
        if (normalized.Contains("violetdiamond"))
            return CredentialType.VioletDiamond;

        return CredentialType.Auto;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }

    private void RefreshAcceptedVisual()
    {
        RemoveInvalidAcceptedColliders();
        bool latchedAcceptance = Time.unscaledTime < acceptedVisualUntil;
        SetAcceptedVisual(acceptedColliders.Count > 0 || latchedAcceptance);
    }

    private void RemoveInvalidAcceptedColliders()
    {
        acceptedColliders.RemoveWhere(collider =>
            collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
    }

    private void SetAcceptedVisual(bool accepted)
    {
        showingAccepted = accepted;
        if (statusLedRenderer == null)
            return;

        Material target = accepted ? acceptedMaterial : standbyMaterial;
        if (target != null && statusLedRenderer.sharedMaterial != target)
            statusLedRenderer.sharedMaterial = target;
    }

    private void WarnAboutConfiguration()
    {
        if (warnedAboutConfiguration)
            return;

        warnedAboutConfiguration = true;
        Debug.LogWarning(
            $"[KeycardReader] '{name}' is missing its reader identity or LED material references. " +
            "Matching-card feedback will remain inactive until the reader is configured.",
            this);
    }

    private void WarnMissingGameplayCard()
    {
        if (warnedMissingGameplayCard || !Application.isPlaying)
            return;

        warnedMissingGameplayCard = true;
        Debug.LogWarning(
            $"[KeycardReader] '{name}' recognized a matching color/symbol object, but it has no KeyCard component. " +
            "Use the imported keycard art inside a gameplay KeyCard wrapper so the scan can count toward the lock.",
            this);
    }

    private void WarnMissingProgressSource()
    {
        if (warnedMissingProgressSource || !Application.isPlaying)
            return;

        warnedMissingProgressSource = true;
        Debug.LogWarning(
            $"[KeycardReader] '{name}' recognized a matching gameplay card but could not resolve a KeyBox. " +
            "Assign the intended KeyBox explicitly if this scene contains more than one completion lock.",
            this);
    }
}
