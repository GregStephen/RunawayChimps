using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;
using Photon.VR;
using Photon.VR.Player;
using RunawayChimps.ThreatFeedback;
using RunawayChimps.Travel;
using RunawayChimps.Zones;

public class MonsterNavigation : MonoBehaviour, IMonsterPursuitProvider
{
    [Header("Monster Settings")]
    public float DetectionRange = 5f;
    public float MonsterSpeedWander = 5f;
    public float MonsterSpeedChase = 7.5f;
    public string tagString = "Player";
    public Transform[] points;
    [Header("Detection Mode")]
    public bool useVentGraph = false;
    [Header("Rotation Settings")]
    public float rotationSpeed = 5f;
    public Vector3 modelForwardOffset = Vector3.zero;
    [Header("Vent Graph Settings")]
    public float detectionCheckInterval = 0.2f;
    [HideInInspector] public NavMeshAgent agent;

    public bool IsChasing { get; private set; }
    public bool IsPursuing => IsChasing && TargetActorNumber > 0;
    public int TargetActorNumber { get; private set; }
    public SectorId ThreatSector => sectorSync != null ? sectorSync.sector : SectorId.None;
    [SerializeField] private bool isChasingDebug;
    [SerializeField] private int targetActorDebug;

    private float detectionTimer;
    private Transform currentTarget;
    private SectorMonsterSync sectorSync;
    private bool hadAuthority;
    private bool warnedNoNavMesh;
    private Photon.Realtime.Player targetOwner;
    private bool warnedNoPatrolPoints;

    private void OnDisable()
    {
        hadAuthority = false; currentTarget = null; targetOwner = null;
        SetPursuit(false, 0);
        if (agent != null) agent.enabled = false;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        sectorSync = GetComponent<SectorMonsterSync>();
        if (agent == null) { enabled = false; return; }
        if (GetComponent<CrawlerVisualController>() == null) gameObject.AddComponent<CrawlerVisualController>();
        var threat = GetComponent<MonsterThreatSource>() ?? gameObject.AddComponent<MonsterThreatSource>();
        threat.Configure(GetComponent<ProximityReactor>(), ZoneId.Level1_Vents);
        agent.speed = MonsterSpeedWander;
        agent.updateRotation = false;
    }

    private void Update()
    {
        bool isMaster = sectorSync != null ? sectorSync.HasAuthority : PhotonNetwork.IsMasterClient;
        if (!isMaster) { agent.enabled = false; hadAuthority = false; return; }
        if (!hadAuthority)
        {
            agent.enabled = true;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(transform.position, out var hit, 1f, filter) || !agent.Warp(hit.position))
            {
                if (!warnedNoNavMesh) Debug.LogError("Monster cannot reach its baked NavMesh. Check the containment NavMeshSurface.", this);
                warnedNoNavMesh = true; agent.enabled = false; return;
            }
            hadAuthority = true; currentTarget = null; targetOwner = null; detectionTimer = 0;
            SetPursuit(false, 0); agent.speed = MonsterSpeedWander; Wander();
        }
        if (!agent.isOnNavMesh) return;
        bool wasChasing = IsChasing;

        if (sectorSync != null && currentTarget != null && !IsEligible(targetOwner))
        {
            currentTarget = null; targetOwner = null; detectionTimer = 0;
        }

        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0f) { detectionTimer = detectionCheckInterval; currentTarget = FindClosestPlayer(); }
        SetPursuit(currentTarget != null, targetOwner != null ? targetOwner.ActorNumber : 0);

        if (currentTarget != null) { agent.speed = MonsterSpeedChase; agent.destination = currentTarget.position; }
        else if (wasChasing || (!agent.pathPending && agent.remainingDistance < 0.5f))
        {
            if (wasChasing) agent.ResetPath();
            agent.speed = MonsterSpeedWander; Wander();
        }
        RotateTowardsMovement();
    }

    private void SetPursuit(bool pursuing, int actorNumber)
    {
        IsChasing = pursuing;
        TargetActorNumber = pursuing ? Mathf.Max(0, actorNumber) : 0;
        isChasingDebug = IsChasing;
        targetActorDebug = TargetActorNumber;
    }

    public void ApplyRemotePursuit(bool chasing, int targetActorNumber) => SetPursuit(chasing, targetActorNumber);
    public void ApplyRemoteChasing(bool chasing) => SetPursuit(chasing, 0); // legacy fail-closed: no target means no personal threat.

    private Transform FindClosestPlayer()
    {
        var targets = new List<Transform>();
        var owners = new Dictionary<Transform, Photon.Realtime.Player>();
        targetOwner = null;
        if (sectorSync != null)
        {
            foreach (var avatar in FindObjectsOfType<PhotonVRPlayer>())
            {
                var owner = avatar.photonView.Owner;
                if (!IsEligible(owner)) continue;
                var head = owner.IsLocal && PhotonVRManager.Manager != null ? PhotonVRManager.Manager.Head : avatar.Head;
                if (head != null) { targets.Add(head); owners[head] = owner; }
            }
        }
        else foreach (var target in GameObject.FindGameObjectsWithTag(tagString)) targets.Add(target.transform);

        Transform closest = null;
        float minDistance = float.MaxValue;
        bool hasVentGraph = useVentGraph && VentGraph.Instance != null;
        foreach (Transform player in targets)
        {
            if (player == null) continue;
            float distance = hasVentGraph ? VentGraph.Instance.GetPathDistance(transform.position, player.position) : Vector3.Distance(transform.position, player.position);
            if (float.IsInfinity(distance)) continue;
            if (distance < DetectionRange && distance < minDistance) { minDistance = distance; closest = player; }
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
            warnedNoPatrolPoints = true; return;
        }
        int destPoint = Random.Range(0, points.Length);
        for (int offset = 0; offset < points.Length; offset++)
        {
            var point = points[(destPoint + offset) % points.Length];
            if (point == null) continue;
            agent.SetDestination(point.position); return;
        }
        if (!warnedNoPatrolPoints) Debug.LogError("Monster patrol points are all missing.", this);
        warnedNoPatrolPoints = true;
    }

    private void RotateTowardsMovement()
    {
        Vector3 velocity = agent.velocity; velocity.y = 0;
        if (velocity.sqrMagnitude > 0.05f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized);
            if (modelForwardOffset != Vector3.zero) targetRotation *= Quaternion.Euler(modelForwardOffset);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, DetectionRange); }
}
