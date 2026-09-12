using UnityEngine;

/// <summary>
/// Aligns the authored Zombie Crawl visual to the proven gameplay/NavMesh root without
/// modifying the imported animation skeleton. Earlier prototypes repositioned and then
/// independently rotated hierarchical torso bones to follow recent vent-path history;
/// headset testing showed that both approaches can visibly collapse/fold the Zombie rig.
///
/// The safe baseline is therefore deliberately rigid: the Animator owns every bone and
/// this component only corrects the complete visual hierarchy as one object. Proper
/// corner-body deformation remains a separate rig/IK task after the authored crawl is
/// visually proven in Unity and on headset.
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class CrawlerBodyPathFollower : MonoBehaviour
{
    [Header("Visual alignment")]
    [Tooltip("Small clearance between the rendered Crawler bounds and the NavMesh floor.")]
    [SerializeField, Min(0f)] private float floorClearance = 0.015f;

    private Transform visualRoot;
    private Transform frontAnchor;
    private bool configured;

    public Transform FrontAnchor => frontAnchor;

    // Retained for CrawlerVisualController's diagnostic message. Core-bone path bending
    // is intentionally disabled after runtime deformation failures.
    public bool HasUsableBodyChain => false;
    public bool PreservesAnimatorSkeleton => true;

    public bool Configure(Transform zombieVisualRoot, Animator zombieAnimator)
    {
        visualRoot = zombieVisualRoot;

        if (visualRoot == null)
        {
            Debug.LogError($"{name}: Crawler visual alignment cannot configure without a Zombie visual root.", this);
            configured = false;
            return false;
        }

        ResolveFrontAnchor();

        // Evaluate the authored pose before measuring its pivot/bounds. From this point on
        // this component never writes a bone position or rotation.
        if (zombieAnimator != null && zombieAnimator.enabled)
            zombieAnimator.Update(0f);

        AlignVisualToLeader();
        ResolveFrontAnchor();

        configured = frontAnchor != null;
        if (!configured)
        {
            Debug.LogError(
                $"{name}: Could not find Zombie Crawl's Mixamo chest/spine/hips anchor. " +
                "The visual will remain parented to the gameplay root, but automatic pivot alignment is unavailable.",
                this);
        }
        else
        {
            Debug.Log(
                $"{name}: Zombie Crawl is using the authored Animator skeleton without post-animation bone deformation. " +
                "Corner-body bending is disabled until a rig-safe implementation is validated.",
                this);
        }

        return configured;
    }

    /// <summary>
    /// Sector travel/controller handoff can move the gameplay root discontinuously. Because
    /// the Zombie is already parented to that root there is no trail to rebuild; this method
    /// intentionally performs no skeletal work and remains as the existing controller hook.
    /// </summary>
    public void ResetTrail()
    {
        // Intentionally empty. Do not restore per-bone path manipulation here.
    }

    private void ResolveFrontAnchor()
    {
        if (visualRoot == null)
        {
            frontAnchor = null;
            return;
        }

        Transform[] bones = visualRoot.GetComponentsInChildren<Transform>(true);
        frontAnchor = FindFirst(
            bones,
            "mixamorigspine2", "spine2", "upperchest", "chest",
            "mixamorigspine1", "spine1", "mixamorigspine", "spine",
            "mixamorighips", "hips");
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

        // Ground the complete rendered hierarchy once. The parent gameplay root carries
        // this calibrated offset through patrol/chase motion and turns.
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
