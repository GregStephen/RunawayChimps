using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Provides local visual feedback for the color/symbol keycard readers.
/// A reader stays amber until the matching physical keycard enters its scan trigger,
/// then uses the authored accepted-green material while that card remains present.
/// This component deliberately does not open doors, consume cards, or change progression.
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

    private readonly HashSet<Collider> acceptedColliders = new HashSet<Collider>();
    private CredentialType resolvedCredential;
    private bool showingAccepted;
    private bool warnedAboutConfiguration;

    private void Awake()
    {
        ResolveReferences();
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
        if (!IsMatchingCard(other))
            return;

        acceptedColliders.Add(other);
        SetAcceptedVisual(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!acceptedColliders.Remove(other))
            return;

        RemoveInvalidAcceptedColliders();
        SetAcceptedVisual(acceptedColliders.Count > 0);
    }

    private void LateUpdate()
    {
        if (!showingAccepted)
            return;

        RemoveInvalidAcceptedColliders();
        if (acceptedColliders.Count == 0)
            SetAcceptedVisual(false);
    }

    private void OnDisable()
    {
        acceptedColliders.Clear();
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

    private bool IsMatchingCard(Collider other)
    {
        if (other == null || resolvedCredential == CredentialType.Auto)
            return false;

        Transform candidate = other.transform;
        while (candidate != null)
        {
            CredentialType presented = ParseCredential(candidate.name);
            if (presented != CredentialType.Auto)
                return presented == resolvedCredential;

            candidate = candidate.parent;
        }

        return false;
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
            "Matching-card light feedback will remain inactive until the reader is configured.",
            this);
    }
}
