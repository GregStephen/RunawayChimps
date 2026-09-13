using UnityEngine;

/// <summary>
/// Aligns the authored Zombie Crawl visual to the proven gameplay/NavMesh root without
/// deforming the imported animation skeleton. Earlier prototypes repositioned and then
/// independently rotated hierarchical torso bones to follow the vent path; headset testing
/// showed that both approaches can visibly collapse/fold the Zombie rig.
///
/// The Animator still owns every bone rotation/pose. However, the standalone Generic Mixamo
/// crawl clip can contain translation curves on mixamorig:Hips. applyRootMotion=false does not
/// make those bone curves in-place, so allowing them through can make the skinned Zombie walk
/// away from its Crawler parent and snap back when the clip loops. This component therefore
/// counter-translates the COMPLETE Zombie visual after animation so the animated Hips stays at
/// its calibrated parent-relative position. This automatic pivot alignment and in-place
/// correction never writes a bone position or rotation.
/// </summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class CrawlerBodyPathFollower : MonoBehaviour
{
    [Header("Visual alignment")]
    [Tooltip("Small clearance between the rendered Crawler bounds and the NavMesh floor.")]
    [SerializeField, Min(0f)] private float floorClearance = 0.015f;
    [Tooltip("Log once if the crawl clip tries to translate its animated root farther than this in one evaluated pose.")]
    [SerializeField, Min(0.01f)] private float animatedRootDriftWarning = 0.25f;

    private Transform visualRoot;
    private Transform alignmentFrame;
    private Transform frontAnchor;
    private Transform animatedRootAnchor;
    private Vector3 animatedRootReferenceInParentSpace;
    private bool hasAnimatedRootReference;
    private bool warnedAnimatedRootDrift;
    private bool configured;

    public Transform FrontAnchor => frontAnchor;

    // Retained for CrawlerVisualController's diagnostic message. Core-bone path bending
    // is intentionally disabled after runtime deformation failures.
    public bool HasUsableBodyChain => false;
    public bool PreservesAnimatorSkeleton => true;
    public bool MaintainsAnimatedRootInPlace => true;

    public bool Configure(Transform zombieVisualRoot, Animator zombieAnimator)
    {
        visualRoot = zombieVisualRoot;
        alignmentFrame = visualRoot != null ? visualRoot.parent : null;
        hasAnimatedRootReference = false;
        warnedAnimatedRootDrift = false;

        if (visualRoot == null || alignmentFrame == null)
        {
            Debug.LogError(
                $"{name}: Crawler visual alignment requires Zombie Crawl below its visual anchor.",
                this);
            configured = false;
            return false;
        }

        ResolveAnchors();

        // Evaluate the authored starting pose before measuring pivot/bounds and before taking
        // the in-place motion reference. From this point on this component never writes a bone.
        if (zombieAnimator != null && zombieAnimator.enabled)
            zombieAnimator.Update(0f);

        AlignVisualToLeader();
        ResolveAnchors();
        CaptureAnimatedRootReference();

        configured = frontAnchor != null && animatedRootAnchor != null && hasAnimatedRootReference;
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
                $"{name}: Zombie Crawl uses its authored Animator skeleton while gameplay owns translation. " +
                "Animated Hips translation is rigidly cancelled after animation; corner-body bending remains disabled.",
                this);
        }

        return configured;
    }

    /// <summary>
    /// Sector travel/controller handoff can move the gameplay root discontinuously. The
    /// calibrated reference lives in the visual-anchor's local space, so it follows that root
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

        // Mixamo commonly stores clip translation on Hips for Generic rigs. This is the only
        // animation transform used to measure translational drift; it is never modified.
        animatedRootAnchor = FindFirst(bones, "mixamorighips", "hips");
    }

    private void CaptureAnimatedRootReference()
    {
        if (alignmentFrame == null || animatedRootAnchor == null)
        {
            hasAnimatedRootReference = false;
            return;
        }

        animatedRootReferenceInParentSpace =
            alignmentFrame.InverseTransformPoint(animatedRootAnchor.position);
        hasAnimatedRootReference = true;
    }

    private void MaintainAnimatedRootInPlace()
    {
        if (!hasAnimatedRootReference || alignmentFrame == null ||
            animatedRootAnchor == null || visualRoot == null)
        {
            return;
        }

        // The expected Hips point is attached to CrawlerVisualAnchor/gameplay travel, not to
        // visualRoot itself. Translating visualRoot therefore CAN cancel the descendant Hips
        // position curve while preserving every bone's authored local pose and segment length.
        Vector3 expectedWorldPosition =
            alignmentFrame.TransformPoint(animatedRootReferenceInParentSpace);
        Vector3 correction = expectedWorldPosition - animatedRootAnchor.position;

        if (correction.sqrMagnitude <= 0.00000001f)
            return;

        if (!warnedAnimatedRootDrift && correction.magnitude >= animatedRootDriftWarning)
        {
            warnedAnimatedRootDrift = true;
            Debug.LogWarning(
                $"{name}: crawl animation attempted {correction.magnitude:0.00} m of internal root translation; " +
                "the complete Zombie visual is being counter-translated so it stays on the Crawler gameplay root.",
                this);
        }

        // Move the complete visual hierarchy as one rigid object. Do NOT set Hips/chest/spine
        // positions: those hierarchical bone writes caused the earlier crushed/folded monster.
        visualRoot.position += correction;
    }

    private void AlignVisualToLeader()
    {
        if (visualRoot == null)
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

        // Correct the FBX/skeleton pivot by translating the complete visual hierarchy.
        // No skeleton transforms are modified independently.
        Vector3 horizontalDelta = transform.position - anchorPosition;
        horizontalDelta.y = 0f;
        visualRoot.position += horizontalDelta;

        // Ground the complete rendered hierarchy once. Subsequent in-place compensation keeps
        // the animated root at this calibrated height instead of allowing clip translation to
        // carry the body above/below the vent floor.
        if (TryGetVisualBounds(out Bounds alignedBounds))
        {
            float desiredBottom = transform.position.y + floorClearance;
            visualRoot.position += Vector3.up * (desiredBottom - alignedBounds.min.y);
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
