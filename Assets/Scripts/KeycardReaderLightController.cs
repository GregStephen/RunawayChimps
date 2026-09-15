using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Matches physical color/symbol keycards to a reader, provides immediate LED feedback,
/// and optionally submits a matching gameplay KeyCard to an explicitly authored objective.
/// Persistent accepted-card progress is displayed separately by KeycardLockProgressIndicator.
/// </summary>
[DisallowMultipleComponent]
public sealed class KeycardReaderLightController : MonoBehaviour
{
    [Header("Reader identity")]
    [Tooltip("Prefer an explicit credential on reader variants. Auto remains a fallback for legacy/name-authored readers.")]
    [SerializeField] private KeycardCredential expectedCredential = KeycardCredential.Auto;
    [SerializeField] private Transform readerRoot;

    [Header("Status LED")]
    [SerializeField] private Renderer statusLedRenderer;
    [SerializeField] private Material standbyMaterial;
    [SerializeField] private Material acceptedMaterial;
    [Tooltip("How long the reader stays green after an accepted card is consumed.")]
    [SerializeField, Min(0f)] private float acceptedFlashSeconds = 0.45f;

    [Header("Objective submission")]
    [Tooltip("When enabled, a matching gameplay KeyCard may be submitted to the authored KeyBox objective.")]
    [SerializeField] private bool submitMatchingCardsToKeyBox = true;
    [Tooltip("Assign the intended objective explicitly. If left empty, exactly one KeyBox deliberately nested under this reader root may be used. Scene-wide KeyBox discovery is never performed.")]
    [SerializeField] private KeyBox keyBox;

    private readonly HashSet<Collider> acceptedColliders = new HashSet<Collider>();
    private readonly Dictionary<Collider, KeyCard> matchingGameplayColliders = new Dictionary<Collider, KeyCard>();
    private readonly HashSet<KeyCard> pendingGameplayCards = new HashSet<KeyCard>();
    private readonly List<Collider> staleColliderScratch = new List<Collider>();
    private readonly List<KeyCard> pendingCardScratch = new List<KeyCard>();

    private KeycardCredential resolvedCredential;
    private bool showingAccepted;
    private bool warnedAboutConfiguration;
    private bool warnedMissingGameplayCard;
    private bool warnedMissingProgressSource;
    private bool warnedAmbiguousProgressSource;
    private float acceptedVisualUntil = -1f;

    private void Awake()
    {
        ResolveReferences();
        ResolveAuthoredKeyBox();

        resolvedCredential = expectedCredential == KeycardCredential.Auto
            ? ParseCredential(readerRoot != null ? readerRoot.name : string.Empty)
            : expectedCredential;

        if (standbyMaterial == null && statusLedRenderer != null)
            standbyMaterial = statusLedRenderer.sharedMaterial;

        if (resolvedCredential == KeycardCredential.Auto || statusLedRenderer == null ||
            standbyMaterial == null || acceptedMaterial == null)
        {
            WarnAboutConfiguration();
        }

        SetAcceptedVisual(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // KeyCards use a deliberately larger trigger-only volume for easy XR pickup.
        // Scanner acceptance must come from the card's tight solid physical collider so the
        // grab affordance cannot make a card count while it is still several centimetres away.
        if (other == null || other.isTrigger)
            return;

        if (!TryResolveMatchingCard(other, out KeyCard gameplayCard))
            return;

        acceptedColliders.Add(other);
        if (gameplayCard != null)
            matchingGameplayColliders[other] = gameplayCard;
        SetAcceptedVisual(true);

        if (!submitMatchingCardsToKeyBox)
            return;

        if (gameplayCard == null)
        {
            WarnMissingGameplayCard();
            return;
        }

        pendingGameplayCards.Add(gameplayCard);
        TrySubmitPendingCard(gameplayCard);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null || other.isTrigger)
            return;

        acceptedColliders.Remove(other);

        if (matchingGameplayColliders.TryGetValue(other, out KeyCard gameplayCard))
        {
            matchingGameplayColliders.Remove(other);
            if (!HasMatchingOverlap(gameplayCard))
                pendingGameplayCards.Remove(gameplayCard);
        }

        RefreshAcceptedVisual();
    }

    private void LateUpdate()
    {
        RetryPendingSubmissions();

        if (showingAccepted)
            RefreshAcceptedVisual();
    }

    private void OnDisable()
    {
        acceptedColliders.Clear();
        matchingGameplayColliders.Clear();
        pendingGameplayCards.Clear();
        acceptedVisualUntil = -1f;
        SetAcceptedVisual(false);
    }

    /// <summary>
    /// Allows scene/bootstrap code to bind a specific objective without enabling any
    /// global discovery behavior. A serialized Inspector reference remains preferred.
    /// </summary>
    public void BindObjective(KeyBox source)
    {
        keyBox = source;
        warnedMissingProgressSource = false;
        warnedAmbiguousProgressSource = false;
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

    private void ResolveAuthoredKeyBox()
    {
        if (keyBox != null || readerRoot == null)
            return;

        // The existing Level 1 objective was deliberately reparented beneath its
        // reader when the authored reader replaced the old KeyBox housing. Treat that
        // hierarchy as an explicit binding, but never search the rest of the scene.
        KeyBox[] nestedBoxes = readerRoot.GetComponentsInChildren<KeyBox>(true);
        if (nestedBoxes.Length == 1)
        {
            keyBox = nestedBoxes[0];
            return;
        }

        if (nestedBoxes.Length > 1)
            WarnAmbiguousProgressSource();
    }

    private bool TryResolveMatchingCard(Collider other, out KeyCard gameplayCard)
    {
        gameplayCard = null;
        if (other == null || resolvedCredential == KeycardCredential.Auto)
            return false;

        gameplayCard = other.GetComponentInParent<KeyCard>();

        // Explicit gameplay-card identity is authoritative. It prevents scene/object
        // renames or unrelated child visuals from silently changing a card's credential.
        KeycardCredential presented = gameplayCard != null
            ? gameplayCard.Credential
            : KeycardCredential.Auto;

        if (presented == KeycardCredential.Auto)
        {
            // Legacy/name-authored compatibility: prefer the collider/ancestor names.
            Transform candidate = other.transform;
            while (candidate != null)
            {
                presented = ParseCredential(candidate.name);
                if (presented != KeycardCredential.Auto)
                    break;

                if (gameplayCard != null && candidate == gameplayCard.transform)
                    break;

                candidate = candidate.parent;
            }
        }

        // A legacy reusable gameplay wrapper may contain the colored FBX as a child.
        // This path is intentionally fallback-only; explicit KeyCard.Credential wins.
        if (presented == KeycardCredential.Auto && gameplayCard != null)
            presented = FindCredentialInHierarchy(gameplayCard.transform);

        return presented != KeycardCredential.Auto && presented == resolvedCredential;
    }

    private void RetryPendingSubmissions()
    {
        if (!submitMatchingCardsToKeyBox || pendingGameplayCards.Count == 0)
            return;

        pendingCardScratch.Clear();
        foreach (KeyCard card in pendingGameplayCards)
            pendingCardScratch.Add(card);

        foreach (KeyCard card in pendingCardScratch)
            TrySubmitPendingCard(card);
    }

    private void TrySubmitPendingCard(KeyCard gameplayCard)
    {
        if (gameplayCard == null || gameplayCard.IsInserted || !HasMatchingOverlap(gameplayCard))
        {
            pendingGameplayCards.Remove(gameplayCard);
            return;
        }

        if (keyBox == null)
            ResolveAuthoredKeyBox();

        if (keyBox == null)
        {
            WarnMissingProgressSource();
            return;
        }

        if (!gameplayCard.TryInsertInto(keyBox))
            return;

        pendingGameplayCards.Remove(gameplayCard);

        // The accepted card is destroyed by its normal lifecycle, so hold the
        // reader green briefly even after its collider disappears.
        acceptedVisualUntil = Mathf.Max(
            acceptedVisualUntil,
            Time.unscaledTime + Mathf.Max(0f, acceptedFlashSeconds));
        SetAcceptedVisual(true);
    }

    private bool HasMatchingOverlap(KeyCard gameplayCard)
    {
        if (gameplayCard == null)
            return false;

        foreach (KeyValuePair<Collider, KeyCard> pair in matchingGameplayColliders)
        {
            Collider collider = pair.Key;
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && pair.Value == gameplayCard)
                return true;
        }

        return false;
    }

    private static KeycardCredential FindCredentialInHierarchy(Transform root)
    {
        if (root == null)
            return KeycardCredential.Auto;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            KeycardCredential credential = ParseCredential(candidate.name);
            if (credential != KeycardCredential.Auto)
                return credential;
        }

        return KeycardCredential.Auto;
    }

    private static KeycardCredential ParseCredential(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return KeycardCredential.Auto;

        string normalized = Normalize(objectName);
        if (normalized.Contains("ambertriangle"))
            return KeycardCredential.AmberTriangle;
        if (normalized.Contains("cyanthreebars") || normalized.Contains("cyan3bars"))
            return KeycardCredential.CyanThreeBars;
        if (normalized.Contains("redcircle"))
            return KeycardCredential.RedCircle;
        if (normalized.Contains("violetdiamond"))
            return KeycardCredential.VioletDiamond;

        return KeycardCredential.Auto;
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
        staleColliderScratch.Clear();
        foreach (Collider collider in acceptedColliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                staleColliderScratch.Add(collider);
        }

        foreach (Collider collider in staleColliderScratch)
        {
            acceptedColliders.Remove(collider);
            matchingGameplayColliders.Remove(collider);
        }

        staleColliderScratch.Clear();
        foreach (KeyValuePair<Collider, KeyCard> pair in matchingGameplayColliders)
        {
            Collider collider = pair.Key;
            if (collider == null || pair.Value == null || !acceptedColliders.Contains(collider))
                staleColliderScratch.Add(collider);
        }

        foreach (Collider collider in staleColliderScratch)
            matchingGameplayColliders.Remove(collider);

        pendingGameplayCards.RemoveWhere(card =>
            card == null || card.IsInserted || !HasMatchingOverlap(card));
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
            $"[KeycardReader] '{name}' recognized a matching gameplay card but has no authored KeyBox objective. " +
            "Assign the intended KeyBox explicitly, or nest exactly one KeyBox beneath this reader root.",
            this);
    }

    private void WarnAmbiguousProgressSource()
    {
        if (warnedAmbiguousProgressSource || !Application.isPlaying)
            return;

        warnedAmbiguousProgressSource = true;
        Debug.LogWarning(
            $"[KeycardReader] '{name}' contains more than one nested KeyBox, so objective submission is disabled until one is assigned explicitly.",
            this);
    }
}
