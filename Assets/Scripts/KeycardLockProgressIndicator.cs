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
    [Tooltip("Used for prefab/scene preview until a KeyBox is bound.")]
    [SerializeField, Min(1)] private int previewRequiredKeys = 2;
    [SerializeField] private bool autoBindUniqueKeyBoxInScene = true;

    [Header("Indicator lamps")]
    [SerializeField] private Renderer[] progressLights;
    [SerializeField] private Material standbyMaterial;
    [SerializeField] private Material acceptedMaterial;

    private bool subscribed;
    private bool warnedMissingSource;
    private bool warnedCapacity;

    private void OnEnable()
    {
        ResolveKeyBox();
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        // Retry once after every scene object has completed Awake/OnEnable.
        if (keyBox == null)
        {
            ResolveKeyBox();
            Subscribe();
            Refresh();
        }

        if (Application.isPlaying && keyBox == null && !warnedMissingSource)
        {
            warnedMissingSource = true;
            Debug.LogWarning(
                $"[KeycardLockProgress] '{name}' could not find a unique KeyBox in its scene. " +
                "Assign the intended KeyBox in the Inspector so the lock panel can display live progress.",
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
        Refresh();
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
        int required = keyBox != null ? keyBox.RequiredKeys : Mathf.Max(1, previewRequiredKeys);
        int accepted = keyBox != null ? keyBox.CurrentKeys : 0;
        ApplyProgress(accepted, required);
    }

    private void HandleProgressChanged(int accepted, int required)
    {
        ApplyProgress(accepted, required);
    }

    private void ApplyProgress(int accepted, int required)
    {
        if (progressLights == null)
            return;

        required = Mathf.Max(1, required);
        accepted = Mathf.Clamp(accepted, 0, required);

        if (required > progressLights.Length && !warnedCapacity)
        {
            warnedCapacity = true;
            Debug.LogWarning(
                $"[KeycardLockProgress] '{name}' needs {required} lamps but only {progressLights.Length} are configured. " +
                "Add more lamp renderers to represent every required card.",
                this);
        }

        for (int index = 0; index < progressLights.Length; index++)
        {
            Renderer lamp = progressLights[index];
            if (lamp == null)
                continue;

            bool used = index < required;
            if (lamp.gameObject.activeSelf != used)
                lamp.gameObject.SetActive(used);

            if (!used)
                continue;

            Material target = index < accepted ? acceptedMaterial : standbyMaterial;
            if (target != null && lamp.sharedMaterial != target)
                lamp.sharedMaterial = target;
        }
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
        KeyBox candidate = null;
        int matches = 0;

        foreach (KeyBox box in boxes)
        {
            if (box == null || box.gameObject.scene != gameObject.scene)
                continue;

            candidate = box;
            matches++;
            if (matches > 1)
                break;
        }

        if (matches == 1)
            keyBox = candidate;
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
