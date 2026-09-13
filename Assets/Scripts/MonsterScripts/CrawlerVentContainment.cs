using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps representative Zombie Crawl body regions inside static Level 1 vent geometry without
/// deforming the imported skeleton. Head/chest/hips/elbow/knee probes request one rigid world
/// translation from CrawlerBodyPathFollower's non-animated motion wrapper. Hands and feet remain
/// the responsibility of CrawlerSurfaceContactIK, which runs afterward.
/// </summary>
[DefaultExecutionOrder(325)]
[DisallowMultipleComponent]
public sealed class CrawlerVentContainment : MonoBehaviour
{
    private const string LevelOneSceneName = "Level1_Containment";
    private const int MaximumWaitFrames = 180;

    [Header("Rigid vent containment")]
    [SerializeField, Min(0.01f)] private float headRadius = 0.09f;
    [SerializeField, Min(0.01f)] private float chestRadius = 0.11f;
    [SerializeField, Min(0.01f)] private float hipsRadius = 0.10f;
    [SerializeField, Min(0.01f)] private float jointRadius = 0.055f;
    [SerializeField, Min(0f)] private float surfaceClearance = 0.008f;
    [SerializeField, Min(0.05f)] private float maximumContainmentOffset = 0.32f;
    [SerializeField, Min(0.01f)] private float releaseSpeed = 0.45f;
    [SerializeField, Range(1, 8)] private int penetrationIterations = 4;
    [SerializeField, Min(0.25f)] private float discontinuityDistance = 1.25f;
    [SerializeField] private LayerMask environmentMask = ~0;

    private readonly Collider[] overlapHits = new Collider[32];
    private readonly RaycastHit[] castHits = new RaycastHit[32];
    private readonly List<BodyProbe> probes = new List<BodyProbe>(8);

    private CrawlerVisualController visualController;
    private CrawlerBodyPathFollower bodyFollower;
    private Transform zombieVisual;
    private GameObject penetrationProbeObject;
    private SphereCollider penetrationProbe;
    private Vector3 previousGameplayPosition;
    private bool gameplayPositionInitialized;
    private bool configured;
    private bool warnedOffsetLimit;
    private bool loggedFirstContact;
    private int waitFrames;

    private sealed class BodyProbe
    {
        public readonly string label;
        public readonly Transform bone;
        public readonly float radius;
        public Vector3 lastSafePosition;
        public bool initialized;

        public BodyProbe(string label, Transform bone, float radius)
        {
            this.label = label;
            this.bone = bone;
            this.radius = radius;
        }
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

                if (monster.GetComponent<CrawlerVentContainment>() == null)
                    monster.gameObject.AddComponent<CrawlerVentContainment>();
            }
        }
    }

    private void Awake()
    {
        visualController = GetComponent<CrawlerVisualController>();
        bodyFollower = GetComponent<CrawlerBodyPathFollower>();
        EnsurePenetrationProbe();
    }

    private void OnEnable()
    {
        configured = false;
        waitFrames = 0;
        warnedOffsetLimit = false;
        loggedFirstContact = false;
        gameplayPositionInitialized = false;
        probes.Clear();
    }

    private void OnDestroy()
    {
        if (penetrationProbeObject != null)
            Destroy(penetrationProbeObject);
    }

    private void LateUpdate()
    {
        if (!configured && !TryConfigure())
        {
            SeedGameplayPosition();
            return;
        }

        if (GameplayRootDiscontinued())
        {
            bodyFollower.SetVentContainmentOffsetWorld(Vector3.zero, applyImmediately: true);
            ResetProbeHistory();
            return;
        }

        Vector3 currentOffset = bodyFollower.VentContainmentOffsetWorld;
        Vector3 desiredOffset = Vector3.MoveTowards(
            currentOffset,
            Vector3.zero,
            releaseSpeed * Time.deltaTime);

        bool touchedEnvironment = false;
        bool offsetLimitedThisFrame = false;
        string firstContactLabel = null;

        // Iterate a few times because one rigid correction can reveal another contact on a
        // different side of the body. At the runtime-tested 0.6 visual scale the body should fit
        // within the duct, so this should converge without opposing floor/ceiling corrections.
        for (int iteration = 0; iteration < penetrationIterations; iteration++)
        {
            bool adjustedThisPass = false;
            Vector3 proposedDelta = desiredOffset - currentOffset;

            for (int i = 0; i < probes.Count; i++)
            {
                BodyProbe probe = probes[i];
                if (probe == null || probe.bone == null)
                    continue;

                Vector3 proposedCenter = probe.bone.position + proposedDelta;
                if (!TryKeepProbeInside(probe, proposedCenter, out Vector3 correction))
                    continue;

                desiredOffset += correction;
                adjustedThisPass = true;
                touchedEnvironment = true;
                if (firstContactLabel == null)
                    firstContactLabel = probe.label;

                if (desiredOffset.magnitude > maximumContainmentOffset)
                {
                    desiredOffset = Vector3.ClampMagnitude(desiredOffset, maximumContainmentOffset);
                    offsetLimitedThisFrame = true;
                    if (!warnedOffsetLimit)
                    {
                        warnedOffsetLimit = true;
                        Debug.LogWarning(
                            $"{name}: Crawler vent containment reached its {maximumContainmentOffset:0.00} m rigid-offset limit. " +
                            "The visual may still be too large for this duct/turn; keep scale/route fit under review.",
                            this);
                    }
                }

                // Re-evaluate every probe from the updated common rigid offset.
                break;
            }

            if (!adjustedThisPass)
                break;
        }

        bodyFollower.SetVentContainmentOffsetWorld(desiredOffset, applyImmediately: true);
        UpdateProbeHistory(preservePreviousSafePoints: offsetLimitedThisFrame);

        if (touchedEnvironment && !loggedFirstContact)
        {
            loggedFirstContact = true;
            Debug.Log(
                $"{name}: rigid vent-body containment corrected {firstContactLabel ?? "a body probe"} without changing Zombie torso bones.",
                this);
        }
    }

    private bool TryConfigure()
    {
        if (visualController == null)
            visualController = GetComponent<CrawlerVisualController>();
        if (bodyFollower == null)
            bodyFollower = GetComponent<CrawlerBodyPathFollower>();

        zombieVisual = visualController != null ? visualController.VisualRoot : null;
        if (zombieVisual == null || bodyFollower == null || !bodyFollower.IsConfigured)
        {
            waitFrames++;
            if (waitFrames == MaximumWaitFrames)
            {
                Debug.LogError(
                    $"{name}: Zombie Crawl/body follower was unavailable after {MaximumWaitFrames} frames; rigid vent containment could not initialize.",
                    this);
            }
            return false;
        }

        EnsurePenetrationProbe();
        if (penetrationProbe == null)
            return false;

        probes.Clear();
        Transform[] bones = zombieVisual.GetComponentsInChildren<Transform>(true);

        Transform head = FindFirst(bones, "mixamorighead", "head");
        Transform chest = FindFirst(
            bones,
            "mixamorigspine2", "spine2", "upperchest", "chest",
            "mixamorigspine1", "spine1");
        Transform hips = FindFirst(bones, "mixamorighips", "hips");

        // Head/core containment is the minimum safe contract. Do not silently claim full-body
        // containment if one of these central probes cannot be resolved on the imported rig.
        if (head == null || chest == null || hips == null)
        {
            Debug.LogError(
                $"{name}: vent-body containment requires explicit head/chest/hips probes on Zombie Crawl; " +
                $"resolved head={head != null}, chest={chest != null}, hips={hips != null}.",
                this);
            return false;
        }

        AddProbe("head", head, headRadius);
        AddProbe("chest", chest, chestRadius);
        AddProbe("hips", hips, hipsRadius);
        AddProbe("left elbow", FindFirst(bones, "mixamorigleftforearm", "leftforearm", "leftlowerarm", "lowerarml"), jointRadius);
        AddProbe("right elbow", FindFirst(bones, "mixamorigrightforearm", "rightforearm", "rightlowerarm", "lowerarmr"), jointRadius);
        AddProbe("left knee", FindFirst(bones, "mixamorigleftleg", "leftleg", "leftcalf", "calfl"), jointRadius);
        AddProbe("right knee", FindFirst(bones, "mixamorigrightleg", "rightleg", "rightcalf", "calfr"), jointRadius);

        bodyFollower.SetVentContainmentOffsetWorld(Vector3.zero, applyImmediately: true);
        ResetProbeHistory();
        previousGameplayPosition = transform.position;
        gameplayPositionInitialized = true;
        configured = true;

        Debug.Log(
            $"{name}: rigid Crawler vent containment active with {probes.Count} head/core/joint probes; hands/feet remain limb-IK controlled.",
            this);
        return true;
    }

    private void EnsurePenetrationProbe()
    {
        if (penetrationProbe != null)
            return;

        penetrationProbeObject = new GameObject("CrawlerVentContainmentProbe");
        penetrationProbeObject.hideFlags = HideFlags.HideAndDontSave;
        penetrationProbeObject.transform.localScale = Vector3.one;

        if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            SceneManager.MoveGameObjectToScene(penetrationProbeObject, gameObject.scene);

        penetrationProbe = penetrationProbeObject.AddComponent<SphereCollider>();
        penetrationProbe.isTrigger = true;
        penetrationProbe.enabled = false;
    }

    private void AddProbe(string label, Transform bone, float radius)
    {
        if (bone != null)
            probes.Add(new BodyProbe(label, bone, Mathf.Max(0.01f, radius)));
    }

    private bool TryKeepProbeInside(BodyProbe probe, Vector3 desiredCenter, out Vector3 correction)
    {
        correction = Vector3.zero;

        // Sweep from the last actual clear/rendered location so a fast limb/body movement cannot
        // tunnel completely through a thin vent wall and end outside without overlapping it.
        if (probe.initialized)
        {
            Vector3 delta = desiredCenter - probe.lastSafePosition;
            if (TrySphereCastToStaticSurface(
                probe.lastSafePosition,
                delta,
                probe.radius,
                out RaycastHit sweepHit))
            {
                Vector3 safeCenter = sweepHit.point + sweepHit.normal * (probe.radius + surfaceClearance);
                correction = safeCenter - desiredCenter;
                return correction.sqrMagnitude > 0.0000001f;
            }
        }

        return TryResolvePenetration(desiredCenter, probe.radius, out correction);
    }

    private bool TryResolvePenetration(Vector3 center, float radius, out Vector3 correction)
    {
        correction = Vector3.zero;
        if (penetrationProbe == null)
            return false;

        penetrationProbe.radius = Mathf.Max(0.005f, radius);
        Vector3 candidate = center;
        bool moved = false;

        for (int iteration = 0; iteration < penetrationIterations; iteration++)
        {
            int count = Physics.OverlapSphereNonAlloc(
                candidate,
                penetrationProbe.radius,
                overlapHits,
                environmentMask,
                QueryTriggerInteraction.Ignore);

            Vector3 deepestDirection = Vector3.zero;
            float deepestDistance = 0f;

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapHits[i];
                if (!IsStaticEnvironmentCollider(collider))
                    continue;

                if (!Physics.ComputePenetration(
                        penetrationProbe,
                        candidate,
                        Quaternion.identity,
                        collider,
                        collider.transform.position,
                        collider.transform.rotation,
                        out Vector3 direction,
                        out float distance))
                {
                    continue;
                }

                if (distance > deepestDistance)
                {
                    deepestDistance = distance;
                    deepestDirection = direction;
                }
            }

            if (deepestDistance <= 0f || deepestDirection.sqrMagnitude <= 0.000001f)
                break;

            candidate += deepestDirection.normalized * (deepestDistance + surfaceClearance);
            moved = true;
        }

        correction = candidate - center;
        return moved && correction.sqrMagnitude > 0.0000001f;
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

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.005f, radius),
            delta / distance,
            castHits,
            distance,
            environmentMask,
            QueryTriggerInteraction.Ignore);

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
            center,
            Mathf.Max(0.005f, radius),
            overlapHits,
            environmentMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (IsStaticEnvironmentCollider(overlapHits[i]))
                return true;
        }
        return false;
    }

    private bool IsStaticEnvironmentCollider(Collider collider)
    {
        if (collider == null || collider == penetrationProbe || !collider.enabled || collider.isTrigger)
            return false;
        if (collider.gameObject.scene != gameObject.scene)
            return false;
        if (collider.transform == transform || collider.transform.IsChildOf(transform))
            return false;
        if (collider.attachedRigidbody != null)
            return false;
        return true;
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

    private void ResetProbeHistory()
    {
        for (int i = 0; i < probes.Count; i++)
        {
            BodyProbe probe = probes[i];
            if (probe == null || probe.bone == null)
                continue;
            probe.lastSafePosition = probe.bone.position;
            probe.initialized = true;
        }
    }

    private void UpdateProbeHistory(bool preservePreviousSafePoints)
    {
        if (preservePreviousSafePoints)
            return;

        for (int i = 0; i < probes.Count; i++)
        {
            BodyProbe probe = probes[i];
            if (probe == null || probe.bone == null)
                continue;

            Vector3 current = probe.bone.position;
            if (OverlapsStaticEnvironment(current, probe.radius))
                continue;

            probe.lastSafePosition = current;
            probe.initialized = true;
        }
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