using System;
using Photon.Pun;
using Photon.Realtime;
using Photon.VR;
using UnityEngine;

public class RoomSwitchService : MonoBehaviourPunCallbacks
{
    public static RoomSwitchService Instance { get; private set; }

    [Header("Public Lobby Settings")]
    [Tooltip("Must match PhotonVRManager.PublicQueue")]
    public string publicQueue = "lobby";

    [Tooltip("If blank, uses PhotonVRManager.DefaultRoomLimit")]
    public int maxPlayersOverride = 0;

    private enum PendingJoinType { None, RandomPublic, PrivateCode }
    private PendingJoinType pendingType = PendingJoinType.None;
    private string pendingPrivateCode = null;
    public bool IsSwitchingRooms { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -------------------------
    // Public API
    // -------------------------

    public void JoinRandomPublicLobby()
    {
        QueueJoin(PendingJoinType.RandomPublic, null);
    }

    public void JoinPrivateRoom(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            return;

        QueueJoin(PendingJoinType.PrivateCode, roomCode.Trim().ToUpperInvariant());
    }

    // -------------------------
    // Core logic
    // -------------------------

    private void QueueJoin(PendingJoinType type, string privateCode)
    {
        IsSwitchingRooms = true;
        pendingType = type;
        pendingPrivateCode = privateCode;

        // If not connected, connect and wait for OnConnectedToMaster
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log($"[RoomSwitchService] Not connected. Connecting then will join: {pendingType}");
            PhotonVRManager.Connect();
            return;
        }

        // If in a room, leave first. Join after OnLeftRoom -> OnConnectedToMaster.
        if (PhotonNetwork.InRoom)
        {
            Debug.Log($"[RoomSwitchService] Leaving room '{PhotonNetwork.CurrentRoom.Name}' to join: {pendingType}");
            PhotonVRManager.SuppressAutoLobbyJoinOnce = true; // important: prevents auto lobby join while we switch
            PhotonNetwork.LeaveRoom(false);
            return;
        }

        // Already connected and not in a room: join now (or wait if still transitioning)
        TryExecutePending();
    }

    private void TryExecutePending()
    {
        if (pendingType == PendingJoinType.None)
            return;

        // Must be on master and ready
        if (!PhotonNetwork.IsConnectedAndReady ||
            PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer)
        {
            Debug.Log($"[RoomSwitchService] Waiting for Master (state={PhotonNetwork.NetworkClientState})");
            return;
        }

        int maxPlayers = (maxPlayersOverride > 0) ? maxPlayersOverride : PhotonVRManager.Manager.DefaultRoomLimit;

        if (pendingType == PendingJoinType.RandomPublic)
        {
            Debug.Log("[RoomSwitchService] Joining random PUBLIC lobby...");
            PhotonVRManager.JoinRandomRoom(publicQueue, maxPlayers);
        }
        else if (pendingType == PendingJoinType.PrivateCode)
        {
            Debug.Log($"[RoomSwitchService] Joining PRIVATE room: {pendingPrivateCode}");
            PhotonVRManager.JoinPrivateRoom(pendingPrivateCode, maxPlayers);
        }

        pendingType = PendingJoinType.None;
        pendingPrivateCode = null;
    }

    // -------------------------
    // Photon callbacks
    // -------------------------

    public override void OnLeftRoom()
    {
        Debug.Log("[RoomSwitchService] Left room. Waiting for Master to rejoin...");
        // Photon will go back to Master automatically, then OnConnectedToMaster will fire
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[RoomSwitchService] Joined room.");
        IsSwitchingRooms = false;
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[RoomSwitchService] OnConnectedToMaster");
        TryExecutePending();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        IsSwitchingRooms = false;
        Debug.LogError($"[RoomSwitchService] OnJoinRoomFailed ({returnCode}): {message}");
        pendingType = PendingJoinType.None;
        pendingPrivateCode = null;
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[RoomSwitchService] OnJoinRandomFailed ({returnCode}): {message}");
        // Your PhotonVRManager already creates a new lobby room on random join fail.
        // So we don't do anything special here.
        pendingType = PendingJoinType.None;
        pendingPrivateCode = null;
        IsSwitchingRooms = false;

    }
}
