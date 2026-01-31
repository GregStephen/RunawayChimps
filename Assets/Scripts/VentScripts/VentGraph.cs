using System.Collections.Generic;
using UnityEngine;

public class VentGraph : MonoBehaviour
{
    public static VentGraph Instance { get; private set; }

    private readonly List<VentNode> nodes = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Collect all nodes under this object
        nodes.AddRange(GetComponentsInChildren<VentNode>());
    }

    public VentNode GetClosestNode(Vector3 position)
    {
        VentNode closest = null;
        float closestDistSqr = Mathf.Infinity;

        foreach (var node in nodes)
        {
            float d = (node.transform.position - position).sqrMagnitude;
            if (d < closestDistSqr)
            {
                closestDistSqr = d;
                closest = node;
            }
        }

        return closest;
    }

    /// <summary>
    /// Approximate path distance along the vent network between two positions.
    /// </summary>
    public float GetPathDistance(Vector3 from, Vector3 to)
    {
        var startNode = GetClosestNode(from);
        var endNode = GetClosestNode(to);

        if (startNode == null || endNode == null)
            return Mathf.Infinity;

        float startOffset = Vector3.Distance(from, startNode.transform.position);
        float endOffset = Vector3.Distance(to, endNode.transform.position);

        float graphDistance = DijkstraDistance(startNode, endNode);

        if (float.IsPositiveInfinity(graphDistance))
            return Mathf.Infinity;

        return startOffset + graphDistance + endOffset;
    }

    private float DijkstraDistance(VentNode start, VentNode goal)
    {
        var dist = new Dictionary<VentNode, float>();
        var visited = new HashSet<VentNode>();

        foreach (var n in nodes)
            dist[n] = float.PositiveInfinity;

        dist[start] = 0f;

        // Naive priority queue using a list (fine for small graphs)
        var open = new List<VentNode> { start };

        while (open.Count > 0)
        {
            // Find node with smallest distance
            VentNode current = null;
            float bestDist = float.PositiveInfinity;
            foreach (var n in open)
            {
                if (dist[n] < bestDist)
                {
                    bestDist = dist[n];
                    current = n;
                }
            }

            if (current == goal)
                return dist[current];

            open.Remove(current);
            visited.Add(current);

            foreach (var neighbor in current.neighbors)
            {
                if (visited.Contains(neighbor)) continue;

                float edgeCost = Vector3.Distance(
                    current.transform.position,
                    neighbor.transform.position);

                float newDist = dist[current] + edgeCost;

                if (newDist < dist[neighbor])
                {
                    dist[neighbor] = newDist;
                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }
        }

        // No path found
        return float.PositiveInfinity;
    }
}
