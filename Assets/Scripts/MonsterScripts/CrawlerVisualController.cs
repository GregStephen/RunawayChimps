using UnityEngine;

/// <summary>
/// Keeps the proven Crawler gameplay root (navigation, capture, Photon sync, audio)
/// while replacing MiniGamesKidFirstRig's visible rig with the authored Zombie Crawl model.
/// Navigation owns world movement; the Animator supplies limb motion while
/// CrawlerBodyPathFollower keeps the long torso aligned to the traveled vent path.
/// </summary>
[DisallowMultipleComponent]
public sealed class CrawlerVisualController : MonoBehaviour
{
    [Header("Visual replacement")]
    [SerializeField] private string zombieObjectName = "Zombie Crawl";
    [SerializeField] private Vector3 visualLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 visualLocalEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool preserveAuthoredScale = true;
    [Tooltip("Used as the desired world scale when authored scale preservation is disabled.")]
    [SerializeField] private Vector3 fallbackLocalScale = new Vector3(0.1705642f, 0.1705642f, 0.1705642f);
    [SerializeField, Min(0.1f)] private float visualScaleMultiplier = 1f;

    [Header("Crawler gameplay tuning")]
    [SerializeField, Min(1f)] private float detectionRange = 12f;
    [SerializeField, Min(0.1f)] private float wanderSpeed = 2.25f;
    [SerializeField, Min(0.1f)] private float chaseSpeed = 4.5f;
    [SerializeField, Range(0.05f, 0.5f)] private float detectionInterval = 0.1f;

    [Header("Animation matching")]
    [SerializeField] private string crawlStateName = "Base Layer.mixamo_com";
    [SerializeField, Min(0.01f)] private float referenceMetersPerSecond = 2.25f;
    [SerializeField, Min(0f)] private float stationaryThreshold = 0.04f;
    [SerializeField, Min(0f)] private float minimumMovingPlayback = 0.55f;
    [SerializeField, Min(0f)] private float maximumPlayback = 2.3f;
    [SerializeField, Min(0f)] private float chasePlaybackBoost = 1.08f;
    [SerializeField, Min(0f)] private float playbackSmoothing = 12f;
    [SerializeField, Min(0.1f)] private float teleportDistance = 2f;

    [Header("Animation verification")]
    [SerializeField, Min(0.25f)] private float animationProbeWindow = 0.75f;
    [SerializeField, Min(0.01f)] private float minimumProbeRotationDegrees = 0.08f;

    private MonsterNavigation navigation;
    private Transform visualAnchor;
    private Transform zombieVisual;
    private Animator zombieAnimator;
    private CrawlerBodyPathFollower bodyPathFollower;
    private Vector3 previousPosition;
    private float smoothedPlayback;
    private bool initialized;

    private int crawlStateHash;
    private int crawlShortStateHash;
    private Transform[] animationProbeBones;
    private Quaternion[] previousProbeRotations;
    private float probeElapsed;
    private float probeRotationDegrees;
    private float previousNormalizedTime;
    private bool probeSawStateAdvance;
    private bool attemptedAnimationRecovery;
    private bool warnedAnimationBinding;

    public Animator VisualAnimator => zombieAnimator;
    public Transform VisualRoot => zombieVisual;

    private void Awake()
    {
        navigation = GetComponent<MonsterNavigation>();
        ApplyNavigationTuning();
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
        {
            actualSpeed = 0f;
            if (bodyPathFollower != null)
                bodyPathFollower.ResetTrail();
        }

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

        if (targetPlayback > 0f && zombieAnimator.enabled)
        {
            EnsureCrawlState();

            AnimatorStateInfo state = zombieAnimator.GetCurrentAnimatorStateInfo(0);
            if (!state.loop && state.normalizedTime >= 0.98f)
                zombieAnimator.Play(state.fullPathHash, 0, Mathf.Repeat(state.normalizedTime, 1f));
        }

        zombieAnimator.speed = smoothedPlayback;
        VerifyAnimationMotion(targetPlayback > 0f);
    }

    private bool TryInitialize()
    {
        if (initialized) return true;

        GameObject zombieObject = FindInOwnScene(zombieObjectName);
        if (zombieObject == null)
        {
            Debug.LogWarning($"{name}: '{zombieObjectName}' was not found in {gameObject.scene.name}; keeping the existing Crawler visual until it is available.", this);
            return false;
        }

        zombieVisual = zombieObject.transform;

        // The authored Zombie exists as a scene root at its intended world size. The
        // gameplay root is itself scaled down, so copying the Zombie's old LOCAL scale
        // beneath that root shrinks it a second time. Capture the intended world size
        // before reparenting and compensate for the gameplay root's lossy scale.
        Vector3 desiredWorldScale = preserveAuthoredScale ? zombieVisual.lossyScale : fallbackLocalScale;
        desiredWorldScale *= visualScaleMultiplier;

        GameObject anchorObject = new GameObject("CrawlerVisualAnchor");
        visualAnchor = anchorObject.transform;
        visualAnchor.SetParent(transform, false);
        visualAnchor.localPosition = visualLocalPosition;
        visualAnchor.localRotation = Quaternion.Euler(visualLocalEuler);
        visualAnchor.localScale = Vector3.one;

        zombieVisual.SetParent(visualAnchor, false);
        zombieVisual.localPosition = Vector3.zero;
        zombieVisual.localRotation = Quaternion.identity;
        zombieVisual.localScale = WorldScaleToLocalScale(visualAnchor, desiredWorldScale);
        zombieObject.SetActive(true);

        zombieAnimator = zombieObject.GetComponent<Animator>();
        if (zombieAnimator == null)
            zombieAnimator = zombieObject.GetComponentInChildren<Animator>(true);

        if (zombieAnimator == null)
        {
            Debug.LogError($"{name}: '{zombieObjectName}' has no Animator.", this);
            return false;
        }

        zombieAnimator.applyRootMotion = false;
        zombieAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        zombieAnimator.updateMode = AnimatorUpdateMode.Normal;

        PrepareCrawlState();
        ConfigureAnimationProbes();

        bodyPathFollower = GetComponent<CrawlerBodyPathFollower>();
        if (bodyPathFollower == null)
            bodyPathFollower = gameObject.AddComponent<CrawlerBodyPathFollower>();

        if (!bodyPathFollower.Configure(zombieVisual, zombieAnimator))
            Debug.LogWarning($"{name}: Zombie Crawl visual was aligned, but full path-following body bending could not be configured.", this);

        DisableLegacyVisuals();

        if (navigation != null)
            navigation.modelForwardOffset = Vector3.zero;

        var gate = GetComponent<MonsterActivationGate>();
        if (gate != null)
            gate.SetAnimator(zombieAnimator);

        initialized = true;
        previousPosition = transform.position;

        Debug.Log(
            $"{name}: Zombie Crawl visual is driven by the existing Crawler gameplay root at world scale {zombieVisual.lossyScale}; " +
            $"body-path corner following is {(bodyPathFollower != null && bodyPathFollower.HasUsableBodyChain ? "active" : "limited")}.",
            this);

        return true;
    }

    private void PrepareCrawlState()
    {
        crawlStateHash = Animator.StringToHash(crawlStateName);
        crawlShortStateHash = Animator.StringToHash("mixamo_com");

        zombieAnimator.Rebind();
        zombieAnimator.Update(0f);

        int stateHash = ResolveCrawlStateHash();
        if (stateHash != 0)
        {
            zombieAnimator.Play(stateHash, 0, 0f);
            zombieAnimator.Update(0f);
        }
        else
        {
            Debug.LogError(
                $"{name}: Zombie Crawl Animator does not contain '{crawlStateName}'/'mixamo_com'. " +
                "The Crawler cannot visibly crawl until its controller/clip binding is corrected.",
                this);
        }

        zombieAnimator.speed = 0f;
    }

    private int ResolveCrawlStateHash()
    {
        if (zombieAnimator == null)
            return 0;

        if (zombieAnimator.HasState(0, crawlStateHash))
            return crawlStateHash;

        if (zombieAnimator.HasState(0, crawlShortStateHash))
            return crawlShortStateHash;

        return 0;
    }

    private void EnsureCrawlState()
    {
        int stateHash = ResolveCrawlStateHash();
        if (stateHash == 0)
            return;

        AnimatorStateInfo state = zombieAnimator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != stateHash &&
            state.shortNameHash != crawlShortStateHash)
        {
            zombieAnimator.Play(stateHash, 0, 0f);
        }
    }

    private void ConfigureAnimationProbes()
    {
        Transform[] all = zombieVisual.GetComponentsInChildren<Transform>(true);
        Transform leftHand = FindTransform(all, "mixamoriglefthand", "lefthand", "handl");
        Transform rightHand = FindTransform(all, "mixamorigrighthand", "righthand", "handr");
        Transform leftForeArm = FindTransform(all, "mixamorigleftforearm", "leftforearm", "leftlowerarm", "lowerarml");
        Transform rightForeArm = FindTransform(all, "mixamorigrightforearm", "rightforearm", "rightlowerarm", "lowerarmr");
        Transform head = FindTransform(all, "mixamorighead", "head");

        Transform[] candidates = { leftHand, rightHand, leftForeArm, rightForeArm, head };
        int count = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null)
                count++;
        }

        animationProbeBones = new Transform[count];
        previousProbeRotations = new Quaternion[count];

        int index = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == null)
                continue;

            animationProbeBones[index] = candidates[i];
            previousProbeRotations[index] = candidates[i].localRotation;
            index++;
        }

        AnimatorStateInfo state = zombieAnimator.GetCurrentAnimatorStateInfo(0);
        previousNormalizedTime = state.normalizedTime;
    }

    private void VerifyAnimationMotion(bool shouldBeMoving)
    {
        if (!shouldBeMoving || zombieAnimator == null || !zombieAnimator.enabled)
        {
            ResetAnimationProbeWindow();
            return;
        }

        AnimatorStateInfo state = zombieAnimator.GetCurrentAnimatorStateInfo(0);
        float normalizedDelta = Mathf.Abs(state.normalizedTime - previousNormalizedTime);
        if (normalizedDelta > 0.0001f)
            probeSawStateAdvance = true;

        previousNormalizedTime = state.normalizedTime;

        if (animationProbeBones != null)
        {
            for (int i = 0; i < animationProbeBones.Length; i++)
            {
                Transform probe = animationProbeBones[i];
                if (probe == null) continue;

                Quaternion current = probe.localRotation;
                probeRotationDegrees += Quaternion.Angle(previousProbeRotations[i], current);
                previousProbeRotations[i] = current;
            }
        }

        probeElapsed += Time.deltaTime;
        if (probeElapsed < animationProbeWindow)
            return;

        bool hasBoneMotion = probeRotationDegrees >= minimumProbeRotationDegrees;
        if (probeSawStateAdvance && !hasBoneMotion)
        {
            if (!attemptedAnimationRecovery)
            {
                attemptedAnimationRecovery = true;
                Debug.LogWarning(
                    $"{name}: crawl state time advanced but the Zombie limb probes did not move; rebinding and restarting the crawl state once.",
                    this);

                PrepareCrawlState();
                ConfigureAnimationProbes();
                ResetAnimationProbeWindow();
                return;
            }

            if (!warnedAnimationBinding)
            {
                warnedAnimationBinding = true;
                Debug.LogError(
                    $"{name}: Zombie Crawl animation state is advancing but its limb bones are not changing. " +
                    "This indicates the standalone crawl clip is not bound to the imported Generic skeleton paths; " +
                    "the model will continue to path-follow, but the animation asset must be rebound/re-exported.",
                    this);
            }
        }
        else if (hasBoneMotion)
        {
            attemptedAnimationRecovery = false;
        }

        ResetAnimationProbeWindow();
    }

    private void ResetAnimationProbeWindow()
    {
        probeElapsed = 0f;
        probeRotationDegrees = 0f;
        probeSawStateAdvance = false;

        if (zombieAnimator != null)
            previousNormalizedTime = zombieAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;

        if (animationProbeBones == null || previousProbeRotations == null)
            return;

        for (int i = 0; i < animationProbeBones.Length; i++)
        {
            if (animationProbeBones[i] != null)
                previousProbeRotations[i] = animationProbeBones[i].localRotation;
        }
    }

    private GameObject FindInOwnScene(string objectName)
    {
        var scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != null && candidate.name == objectName)
                    return candidate.gameObject;
            }
        }

        return null;
    }

    private void ApplyNavigationTuning()
    {
        if (navigation == null) return;

        navigation.DetectionRange = detectionRange;
        navigation.MonsterSpeedWander = wanderSpeed;
        navigation.MonsterSpeedChase = chaseSpeed;
        navigation.detectionCheckInterval = detectionInterval;

        if (navigation.agent != null && navigation.agent.enabled)
            navigation.agent.speed = navigation.IsChasing ? chaseSpeed : wanderSpeed;
    }

    private static Vector3 WorldScaleToLocalScale(Transform parent, Vector3 desiredWorldScale)
    {
        Vector3 parentScale = parent.lossyScale;
        return new Vector3(
            SafeDivide(desiredWorldScale.x, parentScale.x),
            SafeDivide(desiredWorldScale.y, parentScale.y),
            SafeDivide(desiredWorldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        if (Mathf.Abs(divisor) < 0.0001f) return value;
        return value / Mathf.Abs(divisor);
    }

    private void DisableLegacyVisuals()
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.transform == zombieVisual || renderer.transform.IsChildOf(zombieVisual)) continue;
            renderer.enabled = false;
        }

        foreach (var animator in GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || animator == zombieAnimator) continue;
            animator.enabled = false;
        }
    }

    private static Transform FindTransform(Transform[] transforms, params string[] candidateNames)
    {
        for (int c = 0; c < candidateNames.Length; c++)
        {
            string candidate = Normalize(candidateNames[c]);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null && Normalize(transform.name) == candidate)
                    return transform;
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int length = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (!char.IsLetterOrDigit(ch))
                continue;

            buffer[length++] = char.ToLowerInvariant(ch);
        }

        return new string(buffer, 0, length);
    }
}
