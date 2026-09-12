using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Zombie Crawl's animated hands from visually passing through static vent geometry.
/// The Animator remains the source of the authored crawl pose. After animation and whole-
/// visual heading have evaluated, this component treats each hand as a small collision sphere.
/// If the animated hand would cross a wall/floor/ceiling, the hand target is clamped to the
/// first static surface and a two-bone arm solve rotates only upper-arm/forearm joints to reach
/// that target. No torso, spine, hip, gameplay-root, NavMesh, capture, or Photon transform is
/// modified here.
/// </summary>
[DefaultExecutionOrder(350)]
[DisallowMultipleComponent]
public sealed class CrawlerSurfaceContactIK : MonoBehaviour
{
    private const string LevelOneSceneName = "Level1_Containment";
    private const int MaximumWaitFrames = 180;

    [Header("Hand surface contact")]
    [SerializeField, Min(0.01f)] private float handRadius = 0.055f;
    [SerializeField, Min(0f)] private float surfaceClearance = 0.008f;
    [SerializeField, Min(0.1f)] private float contactReleaseSpeed = 10f;
    [SerializeField, Range(0.25f, 1f)] private float emergencyProbeRadiusScale = 0.8f;
    [SerializeField] private LayerMask environmentMask = ~0;

    private readonly RaycastHit[] castHits = new RaycastHit[16];
    private readonly Collider[] overlapHits = new Collider[16];

    private CrawlerVisualController visualController;
    private Animator zombieAnimator;
    private Transform zombieVisual;
    private ArmContact leftArm;
    private ArmContact rightArm;
    private int waitFrames;
    private bool configured;

    private sealed class ArmContact
    {
        public readonly string label;
        public Transform upper;
        public Transform lower;
        public Transform hand;
        public Vector3 lastSafePosition;
        public Vector3 contactTarget;
        public float contactWeight;
        public bool initialized;

        public ArmContact(string label)
        {
            this.label = label;
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

    private void Awake()
    {
        visualController = GetComponent<CrawlerVisualController>();
    }

    private void OnEnable()
    {
        configured = false;
        waitFrames = 0;
        ResetArmState(leftArm);
        ResetArmState(rightArm);
    }

    private void LateUpdate()
    {
        if (!configured && !TryConfigure())
            return;

        if (zombieAnimator == null || !zombieAnimator.enabled)
        {
            ResetArmState(leftArm);
            ResetArmState(rightArm);
            return;
        }

        SolveArm(leftArm);
        SolveArm(rightArm);
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
            {
                Debug.LogError(
                    $"{name}: Zombie Crawl visual/Animator was not available after {MaximumWaitFrames} frames; hand surface-contact IK could not initialize.",
                    this);
            }
            return false;
        }

        Transform[] bones = zombieVisual.GetComponentsInChildren<Transform>(true);
        leftArm = ResolveArm(
            "left",
            bones,
            new[] { "mixamorigleftarm", "leftarm", "leftupperarm", "upperarml" },
            new[] { "mixamorigleftforearm", "leftforearm", "leftlowerarm", "lowerarml" },
            new[] { "mixamoriglefthand", "lefthand", "handl" });
        rightArm = ResolveArm(
            "right",
            bones,
            new[] { "mixamorigrightarm", "rightarm", "rightupperarm", "upperarmr" },
            new[] { "mixamorigrightforearm", "rightforearm", "rightlowerarm", "lowerarmr" },
            new[] { "mixamorigrighthand", "righthand", "handr" });

        if (!leftArm.IsUsable && !rightArm.IsUsable)
        {
            Debug.LogError(
                $"{name}: Zombie Crawl hand surface-contact IK could not find either Mixamo arm chain. The authored animation will remain untouched.",
                this);
            return false;
        }

        if (!leftArm.IsUsable)
            Debug.LogWarning($"{name}: left Zombie arm chain was not found; only right-hand surface contact will run.", this);
        if (!rightArm.IsUsable)
            Debug.LogWarning($"{name}: right Zombie arm chain was not found; only left-hand surface contact will run.", this);

        configured = true;
        Debug.Log(
            $"{name}: Crawler hand surface-contact IK is active (radius {handRadius:0.000} m). " +
            "Only upper-arm/forearm joints are corrected; the Zombie torso remains Animator-owned.",
            this);
        return true;
    }

    private ArmContact ResolveArm(
        string label,
        Transform[] bones,
        string[] upperNames,
        string[] lowerNames,
        string[] handNames)
    {
        var arm = new ArmContact(label)
        {
            upper = FindFirst(bones, upperNames),
            lower = FindFirst(bones, lowerNames),
            hand = FindFirst(bones, handNames),
        };

        if (arm.IsUsable && (!arm.lower.IsChildOf(arm.upper) || !arm.hand.IsChildOf(arm.lower)))
        {
            Debug.LogWarning(
                $"{name}: {label} Zombie arm names resolved but do not form upper->forearm->hand hierarchy; surface-contact IK is disabled for that arm.",
                this);
            arm.upper = null;
            arm.lower = null;
            arm.hand = null;
        }

        return arm;
    }

    private void SolveArm(ArmContact arm)
    {
        if (arm == null || !arm.IsUsable)
            return;

        Vector3 animatedHandPosition = arm.hand.position;
        Quaternion animatedHandRotation = arm.hand.rotation;

        if (!arm.initialized)
        {
            arm.lastSafePosition = animatedHandPosition;
            arm.contactTarget = animatedHandPosition;
            arm.contactWeight = 0f;
            arm.initialized = true;
        }

        bool blocked = TryClampHandToEnvironment(arm, animatedHandPosition, out Vector3 clampedTarget);
        if (blocked)
        {
            // Contact should behave like a hard surface: do not intentionally blend farther
            // through the wall. Release is smoothed when the authored animation pulls clear.
            arm.contactTarget = clampedTarget;
            arm.contactWeight = 1f;
        }
        else
        {
            arm.contactWeight = Mathf.MoveTowards(
                arm.contactWeight,
                0f,
                contactReleaseSpeed * Time.deltaTime);
        }

        if (arm.contactWeight > 0.0001f)
        {
            Vector3 effectiveTarget = Vector3.Lerp(
                animatedHandPosition,
                arm.contactTarget,
                arm.contactWeight);

            ApplyTwoBoneIK(arm, effectiveTarget, animatedHandRotation);
            arm.lastSafePosition = effectiveTarget;
        }
        else
        {
            // With no contact the Animator owns the arm completely.
            arm.lastSafePosition = animatedHandPosition;
            arm.contactTarget = animatedHandPosition;
        }
    }

    private bool TryClampHandToEnvironment(ArmContact arm, Vector3 desiredHandPosition, out Vector3 clampedTarget)
    {
        clampedTarget = desiredHandPosition;

        Vector3 primaryDelta = desiredHandPosition - arm.lastSafePosition;
        if (TrySphereCastToStaticSurface(
                arm.lastSafePosition,
                primaryDelta,
                handRadius,
                out RaycastHit primaryHit))
        {
            clampedTarget = SurfaceCenter(primaryHit);
            return true;
        }

        // SphereCast does not report a collider already overlapping its origin. If a large
        // visual-heading step or first evaluated animation pose starts the hand touching a
        // surface, confirm overlap and probe from the forearm toward the hand to recover a
        // usable surface normal without turning every forearm-near-wall pose into contact.
        if (!OverlapsStaticEnvironment(desiredHandPosition, handRadius))
            return false;

        Vector3 emergencyDelta = desiredHandPosition - arm.lower.position;
        if (TrySphereCastToStaticSurface(
                arm.lower.position,
                emergencyDelta,
                handRadius * emergencyProbeRadiusScale,
                out RaycastHit emergencyHit))
        {
            clampedTarget = SurfaceCenter(emergencyHit);
            return true;
        }

        return false;
    }

    private bool TrySphereCastToStaticSurface(
        Vector3 origin,
        Vector3 delta,
        float radius,
        out RaycastHit bestHit)
    {
        bestHit = default;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector3 direction = delta / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.005f, radius),
            direction,
            castHits,
            distance,
            environmentMask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];
            if (!IsStaticEnvironmentCollider(hit.collider))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            found = true;
            bestDistance = hit.distance;
            bestHit = hit;
        }

        return found;
    }

    private bool OverlapsStaticEnvironment(Vector3 center, float radius)
    {
        int overlapCount = Physics.OverlapSphereNonAlloc(
            center,
            Mathf.Max(0.005f, radius),
            overlapHits,
            environmentMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            if (IsStaticEnvironmentCollider(overlapHits[i]))
                return true;
        }

        return false;
    }

    private bool IsStaticEnvironmentCollider(Collider collider)
    {
        if (collider == null || !collider.enabled || collider.isTrigger)
            return false;

        if (collider.gameObject.scene != gameObject.scene)
            return false;

        if (collider.transform.IsChildOf(transform))
            return false;

        // The requested contact is against the vent/world shell, not players, keycards,
        // monster hitboxes, or other moving Rigidbody objects.
        if (collider.attachedRigidbody != null)
            return false;

        return true;
    }

    private Vector3 SurfaceCenter(RaycastHit hit)
    {
        return hit.point + hit.normal * (handRadius + surfaceClearance);
    }

    private static void ApplyTwoBoneIK(ArmContact arm, Vector3 target, Quaternion authoredHandRotation)
    {
        Vector3 rootPosition = arm.upper.position;
        Vector3 midPosition = arm.lower.position;
        Vector3 tipPosition = arm.hand.position;

        float upperLength = Vector3.Distance(rootPosition, midPosition);
        float lowerLength = Vector3.Distance(midPosition, tipPosition);
        if (upperLength <= 0.0001f || lowerLength <= 0.0001f)
            return;

        Vector3 toTarget = target - rootPosition;
        float rawDistance = toTarget.magnitude;
        if (rawDistance <= 0.0001f)
            return;

        Vector3 direction = toTarget / rawDistance;
        float minimumReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
        float maximumReach = Mathf.Max(minimumReach, upperLength + lowerLength - 0.001f);
        float reach = Mathf.Clamp(rawDistance, minimumReach, maximumReach);
        Vector3 reachableTarget = rootPosition + direction * reach;

        // Preserve the Animator's existing elbow side as the pole/hint. This prevents the
        // contact correction from arbitrarily flipping the elbow across the arm plane.
        Vector3 bendDirection = Vector3.ProjectOnPlane(midPosition - rootPosition, direction);
        if (bendDirection.sqrMagnitude <= 0.000001f)
            bendDirection = Vector3.ProjectOnPlane(arm.upper.up, direction);
        if (bendDirection.sqrMagnitude <= 0.000001f)
            bendDirection = Vector3.ProjectOnPlane(arm.upper.right, direction);
        if (bendDirection.sqrMagnitude <= 0.000001f)
            return;
        bendDirection.Normalize();

        float along =
            (upperLength * upperLength - lowerLength * lowerLength + reach * reach) /
            (2f * reach);
        float heightSquared = Mathf.Max(0f, upperLength * upperLength - along * along);
        float height = Mathf.Sqrt(heightSquared);
        Vector3 desiredMid = rootPosition + direction * along + bendDirection * height;

        Vector3 currentUpperDirection = midPosition - rootPosition;
        Vector3 desiredUpperDirection = desiredMid - rootPosition;
        if (currentUpperDirection.sqrMagnitude > 0.000001f && desiredUpperDirection.sqrMagnitude > 0.000001f)
        {
            arm.upper.rotation =
                Quaternion.FromToRotation(currentUpperDirection, desiredUpperDirection) *
                arm.upper.rotation;
        }

        Vector3 updatedMid = arm.lower.position;
        Vector3 updatedTip = arm.hand.position;
        Vector3 currentLowerDirection = updatedTip - updatedMid;
        Vector3 desiredLowerDirection = reachableTarget - updatedMid;
        if (currentLowerDirection.sqrMagnitude > 0.000001f && desiredLowerDirection.sqrMagnitude > 0.000001f)
        {
            arm.lower.rotation =
                Quaternion.FromToRotation(currentLowerDirection, desiredLowerDirection) *
                arm.lower.rotation;
        }

        // Parent rotations change the hand's world orientation. Restore the crawl clip's
        // wrist orientation so contact bends the arm without inventing a new hand pose.
        arm.hand.rotation = authoredHandRotation;
    }

    private static Transform FindFirst(Transform[] transforms, string[] candidateNames)
    {
        for (int c = 0; c < candidateNames.Length; c++)
        {
            string candidate = Normalize(candidateNames[c]);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidateTransform = transforms[i];
                if (candidateTransform != null && Normalize(candidateTransform.name) == candidate)
                    return candidateTransform;
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

    private static void ResetArmState(ArmContact arm)
    {
        if (arm == null)
            return;

        arm.initialized = false;
        arm.contactWeight = 0f;
    }
}
