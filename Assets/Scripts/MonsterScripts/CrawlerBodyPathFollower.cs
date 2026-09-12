using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the long Zombie Crawl visual inside the vent route by making its torso follow
/// the recent path traveled by the authoritative gameplay/NavMesh root. The root is the
/// leader at the front of the creature; chest/spine/hips progressively sample older
/// positions so the body bends through corners instead of rotating as one rigid object.
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class CrawlerBodyPathFollower : MonoBehaviour
{
    [Header("Path following")]
    [SerializeField, Min(0.02f)] private float trailSampleSpacing = 0.08f;
    [SerializeField, Min(1f)] private float retainedTrailLength = 4.5f;
    [SerializeField, Min(0.5f)] private float teleportResetDistance = 2f;
    [SerializeField, Range(0f, 1f)] private float bodyFollowWeight = 0.9f;
    [SerializeField, Min(0.02f)] private float minimumCoreBoneSpacing = 0.08f;
    [SerializeField, Min(0.1f)] private float maximumCoreBoneSpacing = 0.75f;
    [SerializeField, Min(0.01f)] private float tangentSampleDistance = 0.12f;

    [Header("Visual alignment")]
    [Tooltip("Small clearance between the rendered Crawler bounds and the NavMesh floor.")]
    [SerializeField, Min(0f)] private float floorClearance = 0.015f;

    [Header("Hand containment")]
    [SerializeField] private bool constrainHandsToVent = true;
    [SerializeField] private LayerMask ventCollisionMask = ~0;
    [SerializeField, Min(0f)] private float handWallInset = 0.035f;
    [SerializeField, Range(0f, 1f)] private float handContainmentWeight = 1f;
    [SerializeField, Min(0.05f)] private float maximumHandCorrection = 0.6f;

    private readonly List<Vector3> trail = new List<Vector3>();
    private readonly List<BodyBoneBinding> bodyBones = new List<BodyBoneBinding>();
    private readonly RaycastHit[] handHits = new RaycastHit[16];

    private Transform visualRoot;
    private Animator animator;
    private Transform frontAnchor;
    private Transform leftHand;
    private Transform rightHand;
    private Transform leftArmOrigin;
    private Transform rightArmOrigin;
    private bool configured;
    private bool warnedShortBodyChain;

    public Transform FrontAnchor => frontAnchor;
    public bool HasUsableBodyChain => bodyBones.Count >= 2;

    private sealed class BodyBoneBinding
    {
        public Transform bone;
        public float distanceBehind;
        public float heightAboveRoot;
        public Quaternion headingOffset;
    }

    public bool Configure(Transform zombieVisualRoot, Animator zombieAnimator)
    {
        visualRoot = zombieVisualRoot;
        animator = zombieAnimator;

        if (visualRoot == null)
        {
            Debug.LogError($"{name}: Crawler body follower cannot configure without a visual root.", this);
            configured = false;
            return false;
        }

        ResolveBones();

        // Animator evaluation establishes the crawl pose before we measure body spacing.
        if (animator != null && animator.enabled)
            animator.Update(0f);

        AlignVisualToLeader();
        ResolveBones();
        CalibrateBodyChain();
        ResetTrail();

        configured = frontAnchor != null;
        if (!configured)
            Debug.LogError($"{name}: Could not find a chest/spine/hips anchor on Zombie Crawl; corner-body following is disabled.", this);

        return configured;
    }

    private void Update()
    {
        if (!configured) return;
        RecordTrail();
    }

    private void LateUpdate()
    {
        if (!configured) return;

        ApplyBodyPath();
        if (constrainHandsToVent)
        {
            ConstrainHand(leftArmOrigin, leftHand);
            ConstrainHand(rightArmOrigin, rightHand);
        }
    }

    public void ResetTrail()
    {
        trail.Clear();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        float bodyLength = 1f;
        if (bodyBones.Count > 0)
            bodyLength = Mathf.Max(bodyLength, bodyBones[bodyBones.Count - 1].distanceBehind + tangentSampleDistance * 2f);

        float seedLength = Mathf.Min(retainedTrailLength, bodyLength);
        int steps = Mathf.Max(1, Mathf.CeilToInt(seedLength / trailSampleSpacing));

        // Seed a straight section behind the leader so the torso has valid samples on
        // frame one and after teleports. Without this, every body bone would sample the
        // same root point until enough movement history accumulated and the rig would
        // collapse into itself.
        for (int i = steps; i >= 0; i--)
        {
            float distance = Mathf.Min(seedLength, i * trailSampleSpacing);
            trail.Add(transform.position - forward * distance);
        }
    }

    private void RecordTrail()
    {
        Vector3 current = transform.position;
        if (trail.Count == 0)
        {
            trail.Add(current);
            return;
        }

        Vector3 newest = trail[trail.Count - 1];
        float distance = Vector3.Distance(current, newest);

        if (distance >= teleportResetDistance)
        {
            ResetTrail();
            return;
        }

        if (distance < trailSampleSpacing)
            return;

        trail.Add(current);
        PruneTrail();
    }

    private void PruneTrail()
    {
        while (trail.Count > 2 && GetTrailLength() > retainedTrailLength)
            trail.RemoveAt(0);
    }

    private float GetTrailLength()
    {
        float length = 0f;
        for (int i = 1; i < trail.Count; i++)
            length += Vector3.Distance(trail[i - 1], trail[i]);
        return length;
    }

    private void ResolveBones()
    {
        Transform[] bones = visualRoot.GetComponentsInChildren<Transform>(true);

        frontAnchor = FindFirst(bones,
            "mixamorigspine2", "spine2", "upperchest", "chest",
            "mixamorigspine1", "spine1", "mixamorigspine", "spine",
            "mixamorighips", "hips");

        // A Generic rig does not provide reliable HumanBodyBones mappings, so names are
        // resolved leniently after stripping Mixamo punctuation/prefix formatting.
        leftHand = FindFirst(bones, "mixamoriglefthand", "lefthand", "handl");
        rightHand = FindFirst(bones, "mixamorigrighthand", "righthand", "handr");

        leftArmOrigin = FindFirst(bones,
            "mixamorigleftarm", "leftarm", "leftupperarm", "upperarml");
        rightArmOrigin = FindFirst(bones,
            "mixamorigrightarm", "rightarm", "rightupperarm", "upperarmr");

        if (leftArmOrigin == null) leftArmOrigin = frontAnchor;
        if (rightArmOrigin == null) rightArmOrigin = frontAnchor;
    }

    private void AlignVisualToLeader()
    {
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

        // Correct the imported FBX/skeleton pivot without depending on its object origin:
        // center the chosen chest/spine anchor over the invisible gameplay leader.
        Vector3 horizontalDelta = transform.position - anchorPosition;
        horizontalDelta.y = 0f;
        visualRoot.position += horizontalDelta;

        // Then place the lowest rendered point just above the NavMesh floor so animation
        // starts from a consistent floor contact even when the FBX pivot is far away.
        if (TryGetVisualBounds(out Bounds alignedBounds))
        {
            float desiredBottom = transform.position.y + floorClearance;
            visualRoot.position += Vector3.up * (desiredBottom - alignedBounds.min.y);
        }

        float correction = horizontalDelta.magnitude;
        if (correction > 0.25f)
            Debug.LogWarning($"{name}: corrected Zombie Crawl visual pivot by {correction:0.00} m to align its body with the gameplay root.", this);
    }

    private bool TryGetVisualBounds(out Bounds bounds)
    {
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;

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

    private void CalibrateBodyChain()
    {
        bodyBones.Clear();
        if (frontAnchor == null) return;

        List<Transform> frontToRear = new List<Transform>();
        Transform cursor = frontAnchor;

        while (cursor != null && cursor != visualRoot)
        {
            if (IsCoreBodyBone(cursor))
                AddUnique(frontToRear, cursor);

            if (IsHips(cursor))
                break;

            cursor = cursor.parent;
        }

        // Some imported rigs insert helper transforms. Fall back to named core bones if
        // walking the parent chain did not expose enough of the torso.
        if (frontToRear.Count < 2)
        {
            Transform[] all = visualRoot.GetComponentsInChildren<Transform>(true);
            AddUnique(frontToRear, FindFirst(all, "mixamorigspine2", "spine2", "upperchest", "chest"));
            AddUnique(frontToRear, FindFirst(all, "mixamorigspine1", "spine1"));
            AddUnique(frontToRear, FindFirst(all, "mixamorigspine", "spine"));
            AddUnique(frontToRear, FindFirst(all, "mixamorighips", "hips"));
        }

        // Ensure the selected leader is the first sample even if helper naming caused
        // the parent-chain pass to omit it.
        frontToRear.Remove(frontAnchor);
        frontToRear.Insert(0, frontAnchor);

        float distanceBehind = 0f;
        Quaternion rootHeading = FlatRotation(transform.forward);

        for (int i = 0; i < frontToRear.Count; i++)
        {
            Transform bone = frontToRear[i];
            if (bone == null) continue;

            if (i > 0)
            {
                float spacing = Vector3.Distance(frontToRear[i - 1].position, bone.position);
                distanceBehind += Mathf.Clamp(spacing, minimumCoreBoneSpacing, maximumCoreBoneSpacing);
            }

            bodyBones.Add(new BodyBoneBinding
            {
                bone = bone,
                distanceBehind = distanceBehind,
                heightAboveRoot = bone.position.y - transform.position.y,
                headingOffset = Quaternion.Inverse(rootHeading) * bone.rotation
            });
        }

        if (bodyBones.Count < 2 && !warnedShortBodyChain)
        {
            warnedShortBodyChain = true;
            Debug.LogWarning($"{name}: Zombie Crawl torso chain is too short for full corner bending; visual alignment still applies.", this);
        }
    }

    private void ApplyBodyPath()
    {
        if (bodyBones.Count == 0 || bodyFollowWeight <= 0f)
            return;

        // Work rear-to-front because Mixamo's Hips is the torso ancestor. Re-applying
        // each descendant afterwards prevents a parent correction from dragging the
        // already-targeted front of the Crawler off the vent centerline.
        for (int i = bodyBones.Count - 1; i >= 0; i--)
        {
            BodyBoneBinding binding = bodyBones[i];
            if (binding.bone == null) continue;

            Vector3 desiredPosition = SamplePosition(binding.distanceBehind);
            desiredPosition.y = transform.position.y + binding.heightAboveRoot;

            Vector3 tangent = SampleForward(binding.distanceBehind);
            Quaternion desiredRotation = FlatRotation(tangent) * binding.headingOffset;

            binding.bone.position = Vector3.Lerp(binding.bone.position, desiredPosition, bodyFollowWeight);
            binding.bone.rotation = Quaternion.Slerp(binding.bone.rotation, desiredRotation, bodyFollowWeight);
        }
    }

    private Vector3 SamplePosition(float distanceBehind)
    {
        Vector3 from = transform.position;
        float remaining = Mathf.Max(0f, distanceBehind);

        for (int i = trail.Count - 1; i >= 0; i--)
        {
            Vector3 to = trail[i];
            float segment = Vector3.Distance(from, to);

            if (segment > 0.0001f)
            {
                if (remaining <= segment)
                    return Vector3.Lerp(from, to, remaining / segment);

                remaining -= segment;
            }

            from = to;
        }

        return trail.Count > 0 ? trail[0] : transform.position;
    }

    private Vector3 SampleForward(float distanceBehind)
    {
        float delta = Mathf.Max(tangentSampleDistance, trailSampleSpacing);
        Vector3 ahead = SamplePosition(Mathf.Max(0f, distanceBehind - delta));
        Vector3 behind = SamplePosition(distanceBehind + delta);
        Vector3 forward = ahead - behind;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private void ConstrainHand(Transform armOrigin, Transform hand)
    {
        if (armOrigin == null || hand == null || handContainmentWeight <= 0f)
            return;

        Vector3 origin = armOrigin.position;
        Vector3 toHand = hand.position - origin;
        float distance = toHand.magnitude;
        if (distance < 0.02f)
            return;

        Vector3 direction = toHand / distance;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            handHits,
            distance,
            ventCollisionMask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        RaycastHit nearest = default;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = handHits[i];
            if (hit.collider == null || IsOwnCollider(hit.collider))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearest = hit;
                found = true;
            }
        }

        if (!found)
            return;

        Vector3 safePosition = nearest.point + nearest.normal * handWallInset;
        Vector3 correction = safePosition - hand.position;

        if (correction.magnitude > maximumHandCorrection)
            safePosition = hand.position + correction.normalized * maximumHandCorrection;

        hand.position = Vector3.Lerp(hand.position, safePosition, handContainmentWeight);
    }

    private bool IsOwnCollider(Collider collider)
    {
        Transform colliderTransform = collider.transform;
        if (colliderTransform == transform || colliderTransform.IsChildOf(transform))
            return true;

        return visualRoot != null &&
               (colliderTransform == visualRoot || colliderTransform.IsChildOf(visualRoot));
    }

    private static Quaternion FlatRotation(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private static bool IsCoreBodyBone(Transform bone)
    {
        string normalized = Normalize(bone.name);
        return normalized.Contains("spine") || normalized.EndsWith("hips");
    }

    private static bool IsHips(Transform bone)
    {
        return Normalize(bone.name).EndsWith("hips");
    }

    private static void AddUnique(List<Transform> list, Transform item)
    {
        if (item != null && !list.Contains(item))
            list.Add(item);
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

    private void OnDrawGizmosSelected()
    {
        if (trail.Count < 2)
            return;

        for (int i = 1; i < trail.Count; i++)
            Gizmos.DrawLine(trail[i - 1], trail[i]);
    }
}
