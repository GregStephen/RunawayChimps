using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ProximityManager : MonoBehaviour
{
    public static ProximityManager Instance { get; private set; }

    private List<ProximityReactor> reactors = new List<ProximityReactor>();
    private Transform localPlayer;

    public float checkInterval = 0.1f;
    private float timer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        FindLocalPlayer();
    }

    void Update()
    {
        if (localPlayer == null)
        {
            FindLocalPlayer();
            return;
        }

        timer += Time.deltaTime;
        if (timer < checkInterval) return;
        timer = 0f;

        foreach (var reactor in reactors)
        {
            if (reactor == null) continue;
            float distance = Vector3.Distance(localPlayer.position, reactor.transform.position);
            reactor.UpdateProximity(distance, localPlayer.transform);
        }
    }

    private void FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (var player in players)
        {
            PhotonView view = player.GetComponentInParent<PhotonView>();
            if (view != null && view.IsMine)
            {
                localPlayer = player.transform;
                Debug.Log($"Local player found: {localPlayer.name}");
                return;
            }
        }

        Debug.LogWarning("No local player found yet — waiting for PhotonVR to spawn player.");
    }

    public void Register(ProximityReactor reactor)
    {
        if (!reactors.Contains(reactor))
        {
            reactors.Add(reactor);
        }
    }

    public void Unregister(ProximityReactor reactor)
    {
        reactors.Remove(reactor);
    }
}
