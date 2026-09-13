using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Zombie Crawl's animated hands and feet from visually passing through static vent
/// geometry. The authored Animator owns the base pose. This component runs after rigid body
/// containment and may rotate only two-bone arm/leg chains; it never edits torso/core bones or
/// authoritative gameplay/NavMesh/Photon transforms.
/// </summary>
[DefaultExecutionOrder(350)]
[DisallowMultipleComponent]
public sealed class CrawlerSurfaceContactIK : MonoBehaviour
{
    private const string LevelOneSceneName = "Level1_Containment";
    private const int MaximumWaitFrames = 180;
    private const int BoundarySearchIterations = 8;

    [Header("Limb surface contact")]
    [SerializeField, Min(0.01f)] private float handRadius = 0.055f;
    [SerializeField, Min(0.01f)] private float footRadius = 0.05f;
    [SerializeField, Min(0f)] private float surfaceClearance = 0.008f;
    [SerializeField, Min(0.1f)] private float contactReleaseSpeed = 10f;
    [SerializeField, Range(0.25f, 1f)] private float emergencyProbeRadiusScale = 0.8f;
    [SerializeField, Min(0.25f)] private float discontinuityDistance = 1.25f;
    [SerializeField] private LayerMask environmentMask = ~0;

    private readonly RaycastHit[] castHits = new RaycastHit[32];
    private readonly Collider[] overlapHits = new Collider[32];

    private CrawlerVisualController visualController;
    private Animator zombieAnimator;
    private Transform zombieVisual;
    private ArmContact leftArm;
    private ArmContact rightArm;
    private ArmContact leftLeg;
    private ArmContact rightLeg;
    private int waitFrames;
    private bool configured;
    private Vector3 previousGameplayPosition;
    private bool gameplayPositionInitialized;

    // The original hand-contact implementation used this two-bone structure for arms. Legs use
    // the same math: upper=thigh, lower=calf, hand=foot. Keep one solver so both contact systems
    // share identical discontinuity/recovery behavior.
    private sealed class ArmContact
    {
        public readonly string label;
        public readonly float endRadius;
        public Transform upper;
        public Transform lower;
        public Transform hand;
        public Vector3 lastSafePosition;
        public Vector3 contactTarget;
        public float contactWeight;
        public bool initialized;

        public ArmContact(string label, float endRadius)
        {
            this.label = label;
            this.endRadius = endRadius;
        }

        public bool IsUsable => upper != null && lower != null && hand != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != LevelOneSceneName)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            MonsterNavigation[] monsters = roots[r].GetComponentsInChildren<MonsterNavigation>(true);
            for (int i = 0; i < monsters.Length; i++)
            {
                MonsterNavigation monster = monsters[i];
                if (monster == null || monster.gameObject.scene != scene)
                    continue;
                if (monster.GetComponent<CrawlerSurfaceContactIK>() == null)
                    monster.gameObject.AddComponent<CrawlerSurfaceContactIK>();
            }
        }
    }

    private void Awake() => visualController = GetComponent<CrawlerVisualController>();

    private void OnEnable()
    {
        configured = false;
        waitFrames = 0;
        gameplayPositionInitialized = false;
        ResetArmState(leftArm);
        ResetArmState(rightArm);
        ResetArmState(leftLeg);
        ResetArmState(rightLeg);
    }

    private void LateUpdate()
    {
        if (!configured && !TryConfigure())
        {
            SeedGameplayPosition();
            return;
        }

        if (zombieAnimator == null || !zombieAnimator.enabled)
        {
            ResetAllLimbState();
            SeedGameplayPosition();
            return;
        }

        if (GameplayRootDiscontinued())
        {
            // Never sphere-cast from stale world-space limb points after NavMesh warp,
            // controller handoff, scene recovery, or a Photon correction.
            ResetAllLimbState();
        }

        SolveArm(leftArm);
        SolveArm(rightArm);
        SolveArm(leftLeg);
        SolveArm(rightLeg);
    }

    private bool TryConfigure()
    {
        if (visualController == null)
            visualController = GetComponent<CrawlerVisualController>();

        zombieVisual = visualController != null ? visualController.VisualRoot : null;
        zombieAnimator = visualController != null ? visualController.VisualAnimator : null;
        if (zombieVisual == null || zombieAnimator == null)
        {
            waitFrames++;
            if (waitFrames == MaximumWaitFrames)
                Debug.LogError($"{name}: Zombie Crawl visual/Animator was unavailable after {MaximumWaitFrames} frames; limb surface-contact IK could not initialize.", this);
            return false;
        }

        Transform[] bones = zombieVisual.GetComponentsInChildren<Transform>(true);
        leftArm = ResolveArm("left hand", handRadius, bones,
            new[] { "mixamorigleftarm", "leftarm", "leftupperarm", "upperarml" },
            new[] { "mixamorigleftforearm", "leftforearm", "leftlowerarm", "lowerarml" },
            new[] { "mixamoriglefthand", "lefthand", "handl" });
        rightArm = ResolveArm("right hand", handRadius, bones,
            new[] { "mixamorigrightarm", "rightarm", "rightupperarm", "upperarmr" },
            new[] { "mixamorigrightforearm", "rightforearm", "rightlowerarm", "lowerarmr" },
            new[] { "mixamorigrighthand", "righthand", "handr" });
        leftLeg = ResolveArm("left foot", footRadius, bones,
            new[] { "mixamorigleftupleg", "leftupleg", "leftthigh", "thighl" },
            new[] { "mixamorigleftleg", "leftleg", "leftcalf", "calfl" },
            new[] { "mixamorigleftfoot", "leftfoot", "footl" });
        rightLeg = ResolveArm("right foot", footRadius, bones,
            new[] { "mixamorigrightupleg", "rightupleg", "rightthigh", "thighr" },
            new[] { "mixamorigrightleg", "rightleg", "rightcalf", "calfr" },
            new[] { "mixamorigrightfoot", "rightfoot", "footr" });

        if (!leftArm.IsUsable && !rightArm.IsUsable && !leftLeg.IsUsable && !rightLeg.IsUsable)
        {
            Debug.LogError($"{name}: surface-contact IK could not resolve any Mixamo arm/leg chain; authored animation remains untouched.", this);
            return false;
        }

        WarnMissing(leftArm);
        WarnMissing(rightArm);
        WarnMissing(leftLeg);
        WarnMissing(rightLeg);

        configured = true;
        previousGameplayPosition = transform.position;
        gameplayPositionInitialized = true;
        Debug.Log(
            $"{name}: Crawler hand/foot surface-contact IK active (hand radius {handRadius:0.000} m, foot radius {footRadius:0.000} m). " +
            "Only two-bone limb joints are corrected; the Zombie torso remains Animator-owned.",
            this);
        return true;
    }

    private ArmContact ResolveArm(
        string label,
        float endRadius,
        Transform[] bones,
        string[] upperNames,
        string[] lowerNames,
        string[] handNames)
    {
        var arm = new ArmContact(label, endRadius)
        {
            upper = FindFirst(bones, upperNames),
            lower = FindFirst(bones, lowerNames),
            hand = FindFirst(bones, handNames),
        };

        if (arm.IsUsable && (!arm.lower.IsChildOf(arm.upper) || !arm.hand.IsChildOf(arm.lower)))
        {
            Debug.LogWarning($"{name}: {label} names do not form a two-bone root->mid->end hierarchy; contact IK is disabled for that limb.", this);
            arm.upper = arm.lower = arm.hand = null;
        }
        return arm;
    }

    private void WarnMissing(ArmContact arm)
    {
        if (arm != null && !arm.IsUsable)
            Debug.LogWarning($"{name}: {arm.label} chain not found; that limb will remain authored-animation only.", this);
    }

    private bool GameplayRootDiscontinued()
    {
        Vector3 current = transform.position;
        if (!gameplayPositionInitialized)
        {
            previousGameplayPosition = current;
            gameplayPositionInitialized = true;
            return false;
        }

        float distance = Vector3.Distance(previousGameplayPosition, current);
        previousGameplayPosition = current;
        return distance >= discontinuityDistance;
    }

    private void SeedGameplayPosition()
    {
        previousGameplayPosition = transform.position;
        gameplayPositionInitialized = true;
    }

    private void SolveArm(ArmContact arm)
    {
        if (arm == null || !arm.IsUsable)
            return;

        Vector3 animatedEndPosition = arm.hand.position;
        Quaternion animatedEndRotation = arm.hand.rotation;

        if (!arm.initialized)
        {
            arm.lastSafePosition = animatedEndPosition;
            arm.contactTarget = animatedEndPosition;
            arm.contactWeight = 0f;
            arm.initialized = true;
        }

        bool blocked = TryClampHandToEnvironment(arm, animatedEndPosition, out Vector3 clampedTarget);
        if (blocked)
        {
            arm.contactTarget = clampedTarget;
            arm.contactWeight = 1f;
        }
        else
        {
            arm.contactWeight = Mathf.MoveTowards(arm.contactWeight, 0f, contactReleaseSpeed * Time.deltaTime);
        }

        if (arm.contactWeight > 0.0001f)
        {
            Vector3 effectiveTarget = Vector3.Lerp(animatedEndPosition, arm.contactTarget, arm.contactWeight);
            ApplyTwoBoneIK(arm, effectiveTarget, animatedEndRotation);
            // IK clamps unreachable targets to the real limb length. Cache the solved end
            // position, not the requested surface target, so the next sweep begins where the
            // hand/foot actually rendered.
            arm.lastSafePosition = arm.hand.position;
        }
        else
        {
            arm.lastSafePosition = animatedEndPosition;
            arm.contactTarget = animatedEndPosition;
        }
    }

    private bool TryClampHandToEnvironment(ArmContact arm, Vector3 desiredEndPosition, out Vector3 clampedTarget)
    {
        clampedTarget = desiredEndPosition;
        Vector3 primaryDelta = desiredEndPosition - arm.lastSafePosition;
        if (TrySphereCastToStaticSurface(arm.lastSafePosition, primaryDelta, arm.endRadius, out RaycastHit primaryHit))
        {
            clampedTarget = SurfaceCenter(primaryHit, arm.endRadius);
            return true;
        }

        if (!OverlapsStaticEnvironment(desiredEndPosition, arm.endRadius))
            return false;

        // Recover an already-overlapping first pose from both middle- and upper-limb origins.
        // If the cast API cannot return an entry normal, a bounded binary search finds the last
        // clear center along the authored limb.
        if (TryEmergencyProbe(arm, arm.lower.position, desiredEndPosition, out clampedTarget) ||
            TryEmergencyProbe(arm, arm.upper.position, desiredEndPosition, out clampedTarget) ||
            TryFindLastClearPoint(arm, arm.lower.position, desiredEndPosition, out clampedTarget) ||
            TryFindLastClearPoint(arm, arm.upper.position, desiredEndPosition, out clampedTarget))
            return true;

        return false;
    }

    private bool TryEmergencyProbe(ArmContact arm, Vector3 origin, Vector3 desired, out Vector3 target)
    {
        Vector3 delta = desired - origin;
        if (TrySphereCastToStaticSurface(origin, delta, arm.endRadius * emergencyProbeRadiusScale, out RaycastHit hit))
        {
            target = SurfaceCenter(hit, arm.endRadius);
            return true;
        }
        target = desired;
        return false;
    }

    private bool TryFindLastClearPoint(ArmContact arm, Vector3 origin, Vector3 desired, out Vector3 target)
    {
        target = desired;
        float probeRadius = arm.endRadius * emergencyProbeRadiusScale;
        if (OverlapsStaticEnvironment(origin, probeRadius) || !OverlapsStaticEnvironment(desired, arm.endRadius))
            return false;

        float clearT = 0f;
        float blockedT = 1f;
        for (int i = 0; i < BoundarySearchIterations; i++)
        {
            float mid = (clearT + blockedT) * 0.5f;
            Vector3 sample = Vector3.Lerp(origin, desired, mid);
            if (OverlapsStaticEnvironment(sample, arm.endRadius)) blockedT = mid;
            else clearT = mid;
        }

        target = Vector3.Lerp(origin, desired, clearT);
        Vector3 backTowardLimb = origin - desired;
        if (backTowardLimb.sqrMagnitude > 0.000001f)
            target += backTowardLimb.normalized * surfaceClearance;
        return true;
    }

    private bool TrySphereCastToStaticSurface(Vector3 origin, Vector3 delta, float radius, out RaycastHit bestHit)
    {
        bestHit = default;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        int hitCount = Physics.SphereCastNonAlloc(
            origin, Mathf.Max(0.005f, radius), delta / distance, castHits, distance,
            environmentMask, QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];
            if (!IsStaticEnvironmentCollider(hit.collider) || hit.distance >= bestDistance)
                continue;
            found = true;
            bestDistance = hit.distance;
            bestHit = hit;
        }
        return found;
    }

    private bool OverlapsStaticEnvironment(Vector3 center, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(
            center, Mathf.Max(0.005f, radius), overlapHits, environmentMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (IsStaticEnvironmentCollider(overlapHits[i])) return true;
        return false;
    }

    private bool IsStaticEnvironmentCollider(Collider collider)
    {
        if (collider == null || !collider.enabled || collider.isTrigger)
            return false;
        if (collider.gameObject.scene != gameObject.scene)
            return false;
        if (collider.transform == transform || collider.transform.IsChildOf(transform))
            return false;
        if (collider.attachedRigidbody != null)
            return false;
        return true;
    }

    private Vector3 SurfaceCenter(RaycastHit hit, float radius) =>
        hit.point + hit.normal * (radius + surfaceClearance);

    private static void ApplyTwoBoneIK(ArmContact arm, Vector3 target, Quaternion authoredEndRotation)
    {
        Vector3 root = arm.upper.position;
        Vector3 mid = arm.lower.position;
        Vector3 tip = arm.hand.position;
        float upperLength = Vector3.Distance(root, mid);
        float lowerLength = Vector3.Distance(mid, tip);
        if (upperLength <= 0.0001f || lowerLength <= 0.0001f)
            return;

        Vector3 toTarget = target - root;
        float rawDistance = toTarget.magnitude;
        if (rawDistance <= 0.0001f)
            return;

        Vector3 direction = toTarget / rawDistance;
        float minimumReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
        float maximumReach = Mathf.Max(minimumReach, upperLength + lowerLength - 0.001f);
        float reach = Mathf.Clamp(rawDistance, minimumReach, maximumReach);
        Vector3 reachableTarget = root + direction * reach;

        Vector3 bendDirection = Vector3.ProjectOnPlane(mid - root, direction);
        if (bendDirection.sqrMagnitude <= 0.000001f) bendDirection = Vector3.ProjectOnPlane(arm.upper.up, direction);
        if (bendDirection.sqrMagnitude <= 0.000001f) bendDirection = Vector3.ProjectOnPlane(arm.upper.right, direction);
        if (bendDirection.sqrMagnitude <= 0.000001f) return;
        bendDirection.Normalize();

        float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
        float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 desiredMid = root + direction * along + bendDirection * height;

        Vector3 currentUpperDirection = mid - root;
        Vector3 desiredUpperDirection = desiredMid - root;
        if (currentUpperDirection.sqrMagnitude > 0.000001f && desiredUpperDirection.sqrMagnitude > 0.000001f)
            arm.upper.rotation = Quaternion.FromToRotation(currentUpperDirection, desiredUpperDirection) * arm.upper.rotation;

        Vector3 updatedMid = arm.lower.position;
        Vector3 currentLowerDirection = arm.hand.position - updatedMid;
        Vector3 desiredLowerDirection = reachableTarget - updatedMid;
        if (currentLowerDirection.sqrMagnitude > 0.000001f && desiredLowerDirection.sqrMagnitude > 0.000001f)
            arm.lower.rotation = Quaternion.FromToRotation(currentLowerDirection, desiredLowerDirection) * arm.lower.rotation;

        arm.hand.rotation = authoredEndRotation;
    }

    private void ResetAllLimbState()
    {
        ResetArmState(leftArm);
        ResetArmState(rightArm);
        ResetArmState(leftLeg);
        ResetArmState(rightLeg);
    }

    private static Transform FindFirst(Transform[] transforms, string[] names)
    {
        for (int n = 0; n < names.Length; n++)
        {
            string wanted = Normalize(names[n]);
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] != null && Normalize(transforms[i].name) == wanted) return transforms[i];
        }
        return null;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        char[] buffer = new char[value.Length];
        int length = 0;
        for (int i = 0; i < value.Length; i++)
            if (char.IsLetterOrDigit(value[i])) buffer[length++] = char.ToLowerInvariant(value[i]);
        return new string(buffer, 0, length);
    }

    private static void ResetArmState(ArmContact arm)
    {
        if (arm == null) return;
        arm.initialized = false;
        arm.contactWeight = 0f;
    }
}