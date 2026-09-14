using UnityEngine;

/// <summary>
/// Drives a separate physical lock-status panel from a KeyBox's accepted-card progress.
/// The first N lamps represent the number of cards required; accepted slots stay green.
/// This intentionally keeps the keycard and reader prefabs visually reusable.
/// </summary>
[DisallowMultipleComponent]
public sealed class KeycardLockProgressIndicator : MonoBehaviour
{
    [Header("Progress source")]
    [SerializeField] private KeyBox keyBox;
    [Tooltip("Used for prefab/scene preview until a KeyBox is explicitly bound.")]
    [SerializeField, Min(1)] private int previewRequiredKeys = 2;

    [Header("Indicator lamps")]
    [SerializeField] private Renderer[] progressLights;
    [SerializeField] private Material standbyMaterial;
    [SerializeField] private Material acceptedMaterial;
    [SerializeField, Min(0.01f)] private float lampSpacing = 0.048f;

    private bool subscribed;
    private bool warnedMissingSource;
    private bool warnedCapacity;

    private void OnEnable()
    {
        ResolveParentKeyBox();
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        // Retry once after authored parent relationships have completed Awake/OnEnable.
        if (keyBox == null)
        {
            ResolveParentKeyBox();
            Subscribe();
            Refresh();
        }

        if (Application.isPlaying && keyBox == null && !warnedMissingSource)
        {
            warnedMissingSource = true;
            Debug.LogWarning(
                $"[KeycardLockProgress] '{name}' has no authored KeyBox source. " +
                "Assign the intended KeyBox in the Inspector, bind it from scene setup code, or parent the panel beneath that KeyBox.",
                this);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        previewRequiredKeys = Mathf.Max(1, previewRequiredKeys);
        lampSpacing = Mathf.Max(0.01f, lampSpacing);
    }
#endif

    public void Bind(KeyBox source)
    {
        if (keyBox == source)
        {
            Refresh();
            return;
        }

        Unsubscribe();
        keyBox = source;
        warnedMissingSource = false;
        Subscribe();
        Refresh();
    }

    [ContextMenu("Refresh lock progress")]
    public void Refresh()
    {
        int requiredKeys = keyBox != null ? keyBox.RequiredKeys : Mathf.Max(1, previewRequiredKeys);
        int acceptedKeys = keyBox != null ? keyBox.CurrentKeys : 0;
        ApplyProgress(acceptedKeys, requiredKeys);
    }

    private void HandleProgressChanged(int acceptedKeys, int requiredKeys)
    {
        ApplyProgress(acceptedKeys, requiredKeys);
    }

    private void ApplyProgress(int acceptedKeys, int requiredKeys)
    {
        if (progressLights == null)
            return;

        requiredKeys = Mathf.Max(1, requiredKeys);
        acceptedKeys = Mathf.Clamp(acceptedKeys, 0, requiredKeys);
        int visibleCount = Mathf.Min(requiredKeys, progressLights.Length);

        if (requiredKeys > progressLights.Length && !warnedCapacity)
        {
            warnedCapacity = true;
            Debug.LogWarning(
                $"[KeycardLockProgress] '{name}' needs {requiredKeys} lamps but only {progressLights.Length} are configured. " +
                "Add more lamp renderers to represent every required card.",
                this);
        }

        float firstLampX = -0.5f * (visibleCount - 1) * lampSpacing;

        for (int index = 0; index < progressLights.Length; index++)
        {
            Renderer lamp = progressLights[index];
            if (lamp == null)
                continue;

            bool used = index < visibleCount;
            if (lamp.gameObject.activeSelf != used)
                lamp.gameObject.SetActive(used);

            if (!used)
                continue;

            Transform lampTransform = lamp.transform;
            Vector3 localPosition = lampTransform.localPosition;
            localPosition.x = firstLampX + index * lampSpacing;
            lampTransform.localPosition = localPosition;

            Material target = index < acceptedKeys ? acceptedMaterial : standbyMaterial;
            if (target != null && lamp.sharedMaterial != target)
                lamp.sharedMaterial = target;
        }
    }

    private void ResolveParentKeyBox()
    {
        if (keyBox != null)
            return;

        // Parent-only lookup is deterministic authoring, unlike the previous scene-wide
        // unique-KeyBox search whose result could change when another lock was added.
        keyBox = GetComponentInParent<KeyBox>();
    }

    private void Subscribe()
    {
        if (subscribed || keyBox == null)
            return;

        keyBox.ProgressChanged += HandleProgressChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (keyBox != null)
            keyBox.ProgressChanged -= HandleProgressChanged;
        subscribed = false;
    }
}
