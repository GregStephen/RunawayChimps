using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;
using Photon.VR;
using Photon.VR.Player;
using RunawayChimps.Travel;
using RunawayChimps.Zones;

public class MonsterNavigation : MonoBehaviour
{
    [Header("Monster Settings")]
    [Tooltip("Detection range (vent path or straight-line, depending on useVentGraph).")]
    public float DetectionRange = 5f;
    public float MonsterSpeedWander = 5f;
    public float MonsterSpeedChase = 7.5f;
    public string tagString = "Player";
    public Transform[] points;

    [Header("Detection Mode")]
    [Tooltip("If true, use VentGraph path distance instead of straight-line.")]
    public bool useVentGraph = false;

    [Header("Rotation Settings")]
    public float rotationSpeed = 5f;
    public Vector3 modelForwardOffset = Vector3.zero;

    [Header("Vent Graph Settings")]
    [Tooltip("How often to re-check players (seconds).")]
    public float detectionCheckInterval = 0.2f;

    [HideInInspector]
    public NavMeshAgent agent;

    // Exposed for other components (e.g. AudioScaler)
    public bool IsChasing { get; private set; }

    // Just for debugging, so you can see it in the Inspector
    [SerializeField] private bool isChasingDebug;

    private float detectionTimer;
    private Transform currentTarget;
    private SectorMonsterSync sectorSync;
    private bool hadAuthority;
    private bool warnedNoNavMesh;
    private Photon.Realtime.Player targetOwner;
    private bool warnedNoPatrolPoints;

    private void OnDisable()
    {
        hadAuthority = false;
        currentTarget = null;
        targetOwner = null;
        IsChasing = isChasingDebug = false;
        if (agent != null) agent.enabled = false;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        sectorSync = GetComponent<SectorMonsterSync>();
        if (agent == null) { enabled = false; return; }
        agent.speed = MonsterSpeedWander;
        agent.updateRotation = false;
    }

    private void Update()
    {
        bool isMaster = sectorSync != null ? sectorSync.HasAuthority : PhotonNetwork.IsMasterClient;

        if (!isMaster)
        {
            agent.enabled = false;
            hadAuthority = false;
            return;
        }
        if (!hadAuthority)
        {
            agent.enabled = true;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(transform.position, out var hit, 1f, filter) || !agent.Warp(hit.position))
            {
                if (!warnedNoNavMesh) Debug.LogError("Monster cannot reach its baked NavMesh. Check the containment NavMeshSurface.", this);
                warnedNoNavMesh = true;
                agent.enabled = false;
                return;
            }
            hadAuthority = true;
            currentTarget = null;
            detectionTimer = 0;
            agent.speed = MonsterSpeedWander;
            Wander();
        }
        if (!agent.isOnNavMesh) return;
        bool wasChasing = IsChasing;

        // Safe-room/sector changes invalidate a chase immediately, independently
        // of the slower nearest-player search interval.
        if (sectorSync != null && currentTarget != null && !IsEligible(targetOwner))
        {
            currentTarget = null;
            targetOwner = null;
            detectionTimer = 0;
        }

        // Always compute chasing state (or at least update IsChasing)
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0f)
        {
            detectionTimer = detectionCheckInterval;
            currentTarget = FindClosestPlayer();
        }

        IsChasing = (currentTarget != null);
        isChasingDebug = IsChasing;

        // Only the controller actually present in this sector drives movement.
        if (currentTarget != null)
        {
            agent.speed = MonsterSpeedChase;
            agent.destination = currentTarget.position;
        }
        else if (wasChasing || (!agent.pathPending && agent.remainingDistance < 0.5f))
        {
            if (wasChasing) agent.ResetPath();
            agent.speed = MonsterSpeedWander;
            Wander();
        }

        RotateTowardsMovement();
    }


    public void ApplyRemoteChasing(bool chasing)
    {
        IsChasing = chasing;
        isChasingDebug = chasing;
    }

    private Transform FindClosestPlayer()
    {
        var targets = new List<Transform>();
        var owners = new Dictionary<Transform, Photon.Realtime.Player>();
        targetOwner = null;
        if (sectorSync != null)
        {
            var zones = ZoneStateService.Instance;
            foreach (var avatar in FindObjectsOfType<PhotonVRPlayer>())
            {
                var owner = avatar.photonView.Owner;
                if (!IsEligible(owner)) continue;
                var head = owner.IsLocal && PhotonVRManager.Manager != null ? PhotonVRManager.Manager.Head : avatar.Head;
                if (head != null) { targets.Add(head); owners[head] = owner; }
            }
        }
        else
            foreach (var target in GameObject.FindGameObjectsWithTag(tagString)) targets.Add(target.transform);

        Transform closest = null;
        float minDistance = float.MaxValue;

        bool hasVentGraph = useVentGraph && VentGraph.Instance != null;

        foreach (Transform player in targets)
        {
            if (player == null) continue;

            float distance;

            if (hasVentGraph)
            {
                distance = VentGraph.Instance.GetPathDistance(
                    transform.position,
                    player.position
                );

                if (float.IsInfinity(distance))
                    continue;
            }
            else
            {
                distance = Vector3.Distance(transform.position, player.position);
            }

            if (distance < DetectionRange && distance < minDistance)
            {
                minDistance = distance;
                closest = player;
            }
        }

        if (closest != null) owners.TryGetValue(closest, out targetOwner);
        return closest;
    }

    private bool IsEligible(Photon.Realtime.Player owner)
    {
        var zones = ZoneStateService.Instance;
        return owner != null && sectorSync != null && SectorPresence.Get(owner) == sectorSync.sector &&
            zones != null && zones.TryGetZone(owner.ActorNumber, out var zone) && zone == ZoneId.Level1_Vents;
    }

    private void Wander()
    {
        if (points == null || points.Length == 0)
        {
            if (!warnedNoPatrolPoints) Debug.LogError($"{name}: Monster has no wander points assigned!", this);
            warnedNoPatrolPoints = true;
            return;
        }

        int destPoint = Random.Range(0, points.Length);
        for (int offset = 0; offset < points.Length; offset++)
        {
            var point = points[(destPoint + offset) % points.Length];
            if (point == null) continue;
            agent.SetDestination(point.position);
            return;
        }
        if (!warnedNoPatrolPoints) Debug.LogError("Monster patrol points are all missing.", this);
        warnedNoPatrolPoints = true;
    }

    private void RotateTowardsMovement()
    {
        Vector3 velocity = agent.velocity;
        velocity.y = 0;

        if (velocity.sqrMagnitude > 0.05f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized);

            if (modelForwardOffset != Vector3.zero)
                targetRotation *= Quaternion.Euler(modelForwardOffset);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
    }
}
