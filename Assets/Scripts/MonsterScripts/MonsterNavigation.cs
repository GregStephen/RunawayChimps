using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

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
    private GameObject currentTarget;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = MonsterSpeedWander;
        agent.updateRotation = false;
        Wander();
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            agent.enabled = false;
            return;
        }

        agent.enabled = true;

        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0f)
        {
            detectionTimer = detectionCheckInterval;
            currentTarget = FindClosestPlayer();
        }

        if (currentTarget != null)
        {
            agent.speed = MonsterSpeedChase;
            agent.destination = currentTarget.transform.position;
        }
        else if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            agent.speed = MonsterSpeedWander;
            Wander();
        }

        IsChasing = (currentTarget != null);
        isChasingDebug = IsChasing; // show in inspector

        RotateTowardsMovement();
    }

    private GameObject FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(tagString);
        if (players == null || players.Length == 0)
            return null;

        GameObject closest = null;
        float minDistance = float.MaxValue;

        bool hasVentGraph = useVentGraph && VentGraph.Instance != null;

        foreach (GameObject player in players)
        {
            if (player == null) continue;

            float distance;

            if (hasVentGraph)
            {
                distance = VentGraph.Instance.GetPathDistance(
                    transform.position,
                    player.transform.position
                );

                if (float.IsInfinity(distance))
                    continue;
            }
            else
            {
                distance = Vector3.Distance(transform.position, player.transform.position);
            }

            if (distance < DetectionRange && distance < minDistance)
            {
                minDistance = distance;
                closest = player;
            }
        }

        return closest;
    }

    private void Wander()
    {
        if (points == null || points.Length == 0)
        {
            Debug.LogError($"{name}: Monster has no wander points assigned!");
            return;
        }

        int destPoint = Random.Range(0, points.Length);
        agent.SetDestination(points[destPoint].position);
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
