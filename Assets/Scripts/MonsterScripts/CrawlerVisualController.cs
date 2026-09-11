using UnityEngine;

/// <summary>
/// Keeps the proven Crawler gameplay root (navigation, capture, Photon sync, audio)
/// while replacing MiniGamesKidFirstRig's visible rig with the authored Zombie Crawl model.
/// Navigation owns world movement; the Animator only supplies visual crawl motion.
/// </summary>
[DisallowMultipleComponent]
public sealed class CrawlerVisualController : MonoBehaviour
{
    [Header("Visual replacement")]
    [SerializeField] private string zombieObjectName = "Zombie Crawl";
    [SerializeField] private Vector3 visualLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 visualLocalEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool preserveAuthoredScale = true;
    [SerializeField] private Vector3 fallbackLocalScale = new Vector3(0.1705642f, 0.1705642f, 0.1705642f);

    [Header("Animation matching")]
    [SerializeField, Min(0.01f)] private float referenceMetersPerSecond = 5f;
    [SerializeField, Min(0f)] private float stationaryThreshold = 0.04f;
    [SerializeField, Min(0f)] private float minimumMovingPlayback = 0.45f;
    [SerializeField, Min(0f)] private float maximumPlayback = 1.6f;
    [SerializeField, Min(0f)] private float chasePlaybackBoost = 1.08f;
    [SerializeField, Min(0f)] private float playbackSmoothing = 10f;
    [SerializeField, Min(0.1f)] private float teleportDistance = 2f;

    private MonsterNavigation navigation;
    private Transform zombieVisual;
    private Animator zombieAnimator;
    private Vector3 previousPosition;
    private float smoothedPlayback;
    private bool initialized;

    public Animator VisualAnimator => zombieAnimator;
    public Transform VisualRoot => zombieVisual;

    private void Awake()
    {
        navigation = GetComponent<MonsterNavigation>();
        TryInitialize();
        previousPosition = transform.position;
    }

    private void OnEnable()
    {
        if (!initialized) TryInitialize();
        previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (!initialized && !TryInitialize()) return;
        if (zombieAnimator == null) return;

        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        delta.y = 0f;

        float distance = delta.magnitude;
        float actualSpeed = Time.deltaTime > 0f ? distance / Time.deltaTime : 0f;

        // Scene travel, controller handoff, respawn and network correction must not
        // make the crawl animation flash at extreme speed for a single frame.
        if (distance >= teleportDistance)
            actualSpeed = 0f;

        float targetPlayback = 0f;
        if (actualSpeed >= stationaryThreshold)
        {
            targetPlayback = Mathf.Clamp(
                actualSpeed / referenceMetersPerSecond,
                minimumMovingPlayback,
                maximumPlayback);

            if (navigation != null && navigation.IsChasing)
                targetPlayback = Mathf.Min(maximumPlayback, targetPlayback * chasePlaybackBoost);
        }

        float t = playbackSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-playbackSmoothing * Time.deltaTime);
        smoothedPlayback = Mathf.Lerp(smoothedPlayback, targetPlayback, t);

        if (targetPlayback == 0f && smoothedPlayback < 0.02f)
            smoothedPlayback = 0f;

        zombieAnimator.speed = smoothedPlayback;
    }

    private bool TryInitialize()
    {
        if (initialized) return true;

        GameObject zombieObject = GameObject.Find(zombieObjectName);
        if (zombieObject == null)
        {
            Debug.LogWarning($"{name}: '{zombieObjectName}' was not found; keeping the existing Crawler visual until it is available.", this);
            return false;
        }

        zombieVisual = zombieObject.transform;
        Vector3 authoredScale = zombieVisual.localScale;

        // The gameplay root rotates and moves. The visual child owns the mesh's
        // forward-axis correction so navigation itself remains model-agnostic.
        zombieVisual.SetParent(transform, false);
        zombieVisual.localPosition = visualLocalPosition;
        zombieVisual.localRotation = Quaternion.Euler(visualLocalEuler);
        zombieVisual.localScale = preserveAuthoredScale ? authoredScale : fallbackLocalScale;
        zombieObject.SetActive(true);

        zombieAnimator = zombieObject.GetComponent<Animator>();
        if (zombieAnimator == null)
        {
            Debug.LogError($"{name}: '{zombieObjectName}' has no Animator.", this);
            return false;
        }

        zombieAnimator.applyRootMotion = false;
        zombieAnimator.speed = 0f;

        DisableLegacyVisuals();

        if (navigation != null)
            navigation.modelForwardOffset = Vector3.zero;

        var gate = GetComponent<MonsterActivationGate>();
        if (gate != null)
            gate.SetAnimator(zombieAnimator);

        initialized = true;
        Debug.Log($"{name}: Zombie Crawl visual is now driven by the existing Crawler gameplay root.", this);
        return true;
    }

    private void DisableLegacyVisuals()
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(zombieVisual)) continue;
            renderer.enabled = false;
        }

        foreach (var animator in GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || animator == zombieAnimator) continue;
            animator.enabled = false;
        }
    }
}
