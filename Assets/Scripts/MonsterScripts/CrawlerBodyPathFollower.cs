using UnityEngine;

/// <summary>
/// Aligns the authored Zombie Crawl visual to the proven gameplay/NavMesh root without
/// deforming the imported animation skeleton. Earlier prototypes repositioned and then
/// independently rotated hierarchical torso bones to follow the vent path; headset testing
/// showed that both approaches can visibly collapse/fold the Zombie rig.
///
/// The Animator still owns the imported Zombie hierarchy and every bone pose. The standalone
/// Generic Mixamo crawl clip can contain translation curves on its root/Hips, so
/// applyRootMotion=false alone is not a sufficient ownership boundary. This component inserts
/// a non-animated CrawlerMotionCompensation parent between CrawlerVisualAnchor and Zombie Crawl.
/// All automatic pivot alignment and in-place travel correction move only that wrapper. The
/// correction never writes a bone position or rotation and never writes the Animator-owned
/// Zombie root. Vertical animation motion remains authored; only horizontal travel drift is
/// cancelled.
/// </summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class CrawlerBodyPathFollower : MonoBehaviour
{
    private const string MotionCompensationName = "CrawlerMotionCompensation";

    [Header("Visual alignment")]
    [Tooltip("Small clearance between the rendered Crawler bounds and the NavMesh floor.")]
    [SerializeField, Min(0f)] private float floorClearance = 0.015f;
    [Tooltip("Log once if the crawl clip's total horizontal root travel exceeds this many world meters.")]
    [SerializeField, Min(0.01f)] private float animatedRootDriftWarning = 0.25f;

    private Transform visualRoot;
    private Transform visualAnchor;
    private Transform motionCompensationRoot;
    private Transform frontAnchor;
    private Transform animatedRootAnchor;
    private Vector3 animatedRootReferenceInMotionSpace;
    private Vector3 calibratedMotionLocalPosition;
    private bool hasAnimatedRootReference;
    private bool warnedAnimatedRootDrift;
    private bool configured;

    public Transform FrontAnchor => frontAnchor;
    public Transform VisualAnchor => visualAnchor;

    // Retained for CrawlerVisualController's diagnostic message. Core-bone path bending
    // is intentionally disabled after runtime deformation failures.
    public bool HasUsableBodyChain => false;
    public bool PreservesAnimatorSkeleton => true;
    public bool MaintainsAnimatedRootInPlace => true;

    public bool Configure(Transform zombieVisualRoot, Animator zombieAnimator)
    {
        visualRoot = zombieVisualRoot;
        hasAnimatedRootReference = false;
        warnedAnimatedRootDrift = false;
        configured = false;

        if (visualRoot == null || !EnsureMotionCompensationRoot())
        {
            Debug.LogError(
                $"{name}: Crawler visual alignment requires Zombie Crawl below CrawlerVisualAnchor.",
                this);
            return false;
        }

        ResolveAnchors();

        // Evaluate the authored starting pose before measuring pivot/bounds and before taking
        // the in-place motion reference. From this point on this component never writes the
        // Animator-owned visual root or any skeleton transform.
        if (zombieAnimator != null && zombieAnimator.enabled)
            zombieAnimator.Update(0f);

        AlignVisualToLeader();
        ResolveAnchors();
        CaptureAnimatedRootReference();

        configured = frontAnchor != null &&
                     animatedRootAnchor != null &&
                     motionCompensationRoot != null &&
                     hasAnimatedRootReference;

        if (!configured)
        {
            Debug.LogError(
                $"{name}: Could not configure Zombie Crawl rigid alignment/in-place motion. " +
                "Expected a Mixamo chest/spine anchor plus mixamorig:Hips under the Zombie visual.",
                this);
        }
        else
        {
            Debug.Log(
                $"{name}: Zombie Crawl Animator owns the imported visual hierarchy while gameplay owns travel. " +
                "CrawlerMotionCompensation cancels horizontal animation-root drift without writing the Animator root or torso bones.",
                this);
        }

        return configured;
    }

    /// <summary>
    /// Sector travel/controller handoff can move the gameplay root discontinuously. The
    /// calibration is local to the stable visual wrapper hierarchy, so it follows that root
    /// automatically and does not need a world-space trail reset.
    /// </summary>
    public void ResetTrail()
    {
        // Intentionally empty. Do not restore per-bone path manipulation here.
    }

    private void LateUpdate()
    {
        if (!configured)
            return;

        // Execution order is intentionally after CrawlerVisualHeadingStabilizer (250) and
        // before CrawlerSurfaceContactIK (350): heading first, rigid in-place correction next,
        // then arm-only environmental contact.
        MaintainAnimatedRootInPlace();
    }

    private bool EnsureMotionCompensationRoot()
    {
        Transform currentParent = visualRoot != null ? visualRoot.parent : null;
        if (currentParent == null)
            return false;

        if (currentParent.name == MotionCompensationName)
        {
            motionCompensationRoot = currentParent;
            visualAnchor = motionCompensationRoot.parent;
        }
        else
        {
            visualAnchor = currentParent;
            Transform existing = visualAnchor.Find(MotionCompensationName);
            if (existing != null)
            {
                motionCompensationRoot = existing;
            }
            else
            {
                GameObject compensationObject = new GameObject(MotionCompensationName);
                motionCompensationRoot = compensationObject.transform;
                motionCompensationRoot.SetParent(visualAnchor, false);
            }

            // Keep the Animator-owned Zombie hierarchy untouched. The new parent starts as an
            // identity transform, so preserving world pose here preserves the current authored
            // pose while moving future rigid correction responsibility outside the Animator.
            motionCompensationRoot.localPosition = Vector3.zero;
            motionCompensationRoot.localRotation = Quaternion.identity;
            motionCompensationRoot.localScale = Vector3.one;
            visualRoot.SetParent(motionCompensationRoot, true);
        }

        if (visualAnchor == null || motionCompensationRoot == null)
            return false;

        // Configure is allowed to retry. Always return the non-animated wrapper to a known
        // identity basis before recalibrating; only its localPosition is allowed to vary later.
        motionCompensationRoot.localPosition = Vector3.zero;
        motionCompensationRoot.localRotation = Quaternion.identity;
        motionCompensationRoot.localScale = Vector3.one;
        return true;
    }

    private void ResolveAnchors()
    {
        if (visualRoot == null)
        {
            frontAnchor = null;
            animatedRootAnchor = null;
            return;
        }

        Transform[] bones = visualRoot.GetComponentsInChildren<Transform>(true);
        frontAnchor = FindFirst(
            bones,
            "mixamorigspine2", "spine2", "upperchest", "chest",
            "mixamorigspine1", "spine1", "mixamorigspine", "spine",
            "mixamorighips", "hips");

        // Mixamo commonly stores Generic clip translation on Hips. Measuring Hips relative to
        // the wrapper also catches translation on an Animator-owned transform above Hips.
        animatedRootAnchor = FindFirst(bones, "mixamorighips", "hips");
    }

    private void CaptureAnimatedRootReference()
    {
        if (motionCompensationRoot == null || animatedRootAnchor == null)
        {
            hasAnimatedRootReference = false;
            return;
        }

        calibratedMotionLocalPosition = motionCompensationRoot.localPosition;
        animatedRootReferenceInMotionSpace =
            motionCompensationRoot.InverseTransformPoint(animatedRootAnchor.position);
        hasAnimatedRootReference = true;
    }

    private void MaintainAnimatedRootInPlace()
    {
        if (!hasAnimatedRootReference || motionCompensationRoot == null ||
            animatedRootAnchor == null || visualRoot == null)
        {
            return;
        }

        // This local-space measurement is independent of the wrapper's own translation. It is
        // therefore the animation's TOTAL drift from the calibrated pose, not merely the delta
        // left over from last frame's correction.
        Vector3 currentAnimatedRootInMotionSpace =
            motionCompensationRoot.InverseTransformPoint(animatedRootAnchor.position);
        Vector3 totalLocalDrift = currentAnimatedRootInMotionSpace - animatedRootReferenceInMotionSpace;

        // Gameplay owns travel across the vent plane, but the crawl is still allowed to raise
        // and lower the body naturally. Cancelling Y would flatten authored crawl bob and could
        // create unnecessary hand/floor corrections, so only horizontal X/Z drift is removed.
        Vector3 localTravelDrift = new Vector3(totalLocalDrift.x, 0f, totalLocalDrift.z);
        float worldTravelDrift = motionCompensationRoot.TransformVector(localTravelDrift).magnitude;

        if (!warnedAnimatedRootDrift && worldTravelDrift >= animatedRootDriftWarning)
        {
            warnedAnimatedRootDrift = true;
            Debug.LogWarning(
                $"{name}: crawl animation attempted {worldTravelDrift:0.00} m of total horizontal internal root travel; " +
                "CrawlerMotionCompensation is cancelling it so Zombie Crawl stays on the gameplay root.",
                this);
        }

        // Absolute assignment is intentional. It prevents accumulated error and, crucially,
        // restores the calibrated wrapper position when a looping clip's horizontal drift
        // returns to zero. The Animator-owned visualRoot is never repositioned by this component.
        motionCompensationRoot.localPosition = calibratedMotionLocalPosition - localTravelDrift;
    }

    private void AlignVisualToLeader()
    {
        if (visualRoot == null || motionCompensationRoot == null)
            return;

        Vector3 anchorPosition;
        if (frontAnchor != null)
        {
            anchorPosition = frontAnchor.position;
        }
        else if (TryGetVisualBounds(out Bounds fallbackBounds))
        {
            anchorPosition = fallbackBounds.center;
        }
        else
        {
            return;
        }

        // Correct the FBX/skeleton pivot by translating only the non-animated wrapper.
        // No Animator-owned root, skeleton transform, or segment length is changed.
        Vector3 horizontalDelta = transform.position - anchorPosition;
        horizontalDelta.y = 0f;
        motionCompensationRoot.position += horizontalDelta;

        // Ground the complete rendered hierarchy once by moving the same wrapper. Subsequent
        // in-place compensation is absolute around this calibrated wrapper position.
        if (TryGetVisualBounds(out Bounds alignedBounds))
        {
            float desiredBottom = transform.position.y + floorClearance;
            motionCompensationRoot.position += Vector3.up * (desiredBottom - alignedBounds.min.y);
        }

        float correction = horizontalDelta.magnitude;
        if (correction > 0.25f)
        {
            Debug.LogWarning(
                $"{name}: corrected Zombie Crawl visual pivot by {correction:0.00} m while preserving its authored skeleton.",
                this);
        }
    }

    private bool TryGetVisualBounds(out Bounds bounds)
    {
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static Transform FindFirst(Transform[] transforms, params string[] candidateNames)
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
