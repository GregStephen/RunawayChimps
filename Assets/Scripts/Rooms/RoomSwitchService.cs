using Photon.Pun;
using Photon.Realtime;
using Photon.VR;
using UnityEngine;

public class RoomSwitchService : MonoBehaviourPunCallbacks
{
    public static RoomSwitchService Instance { get; private set; }
    public string publicQueue = "lobby";
    public int maxPlayersOverride;
    [Min(5f)] public float joinTimeout = 30f;
    public bool IsSwitchingRooms { get; private set; }
    public string LastError { get; private set; }
    private enum PendingJoinType { None, RandomPublic, PrivateCode }
    private PendingJoinType pendingType;
    private string pendingPrivateCode;
    private bool joinIssued;
    private float deadline;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void JoinRandomPublicLobby() => QueueJoin(PendingJoinType.RandomPublic, null);
    public void JoinPrivateRoom(string roomCode)
    {
        if (!string.IsNullOrWhiteSpace(roomCode))
            QueueJoin(PendingJoinType.PrivateCode, roomCode.Trim().ToUpperInvariant());
    }

    private void QueueJoin(PendingJoinType type, string code)
    {
        if (IsSwitchingRooms || (RunawayChimps.Travel.SectorTravelService.I != null &&
            RunawayChimps.Travel.SectorTravelService.I.IsBusy)) return;
        if (PhotonVRManager.Manager == null) { Fail("Multiplayer setup is not ready."); return; }
        if (PhotonNetwork.InRoom && type == PendingJoinType.PrivateCode && PhotonNetwork.CurrentRoom.Name == code) return;
        LastError = null;
        AppState.I?.ClearFailure();
        IsSwitchingRooms = true;
        joinIssued = false;
        pendingType = type;
        pendingPrivateCode = code;
        deadline = Time.realtimeSinceStartup + Mathf.Max(5f, joinTimeout);
        // Remains true through completion, preventing public autojoin regardless of callback order.
        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.LeaveRoom(false)) Fail("Could not leave the current room.");
        }
        else if (!PhotonNetwork.IsConnected)
        {
            if (!PhotonVRManager.Connect()) Fail("Could not start the multiplayer connection.");
        }
        else TryExecutePending();
    }

    private void TryExecutePending()
    {
        if (!IsSwitchingRooms || joinIssued || pendingType == PendingJoinType.None ||
            !PhotonNetwork.IsConnectedAndReady ||
            PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer) return;
        var manager = PhotonVRManager.Manager;
        if (manager == null) { Fail("Multiplayer setup is not ready."); return; }
        int capacity = Mathf.Clamp(maxPlayersOverride > 0 ? maxPlayersOverride : manager.DefaultRoomLimit, 1, 10);
        joinIssued = true;
        bool accepted = pendingType == PendingJoinType.RandomPublic
            ? PhotonVRManager.JoinRandomRoom(publicQueue, capacity)
            : PhotonVRManager.JoinPrivateRoom(pendingPrivateCode, capacity);
        if (!accepted) Fail("Could not start room joining. Please try again.");
    }

    private void Update()
    {
        if (!IsSwitchingRooms || Time.realtimeSinceStartup < deadline) return;
        // Cancel the native attempt before permitting a retry with different intent.
        PhotonNetwork.Disconnect();
        Fail("Room joining timed out. Please try again.");
    }

    private void ClearRequest()
    {
        IsSwitchingRooms = false;
        pendingType = PendingJoinType.None;
        pendingPrivateCode = null;
        joinIssued = false;
        PhotonVRManager.SuppressAutoLobbyJoinOnce = false;
    }

    private void Fail(string message)
    {
        ClearRequest();
        LastError = message;
        AppState.I?.SetStatus(message);
        if (AppState.I != null && !AppState.I.IsReady) AppState.I.Fail(message);
        Debug.LogWarning("[RoomSwitchService] " + message, this);
    }

    public override void OnConnectedToMaster() => TryExecutePending();
    public override void OnJoinedRoom() { ClearRequest(); LastError = null; }
    public override void OnJoinRoomFailed(short code, string message) => Fail("Could not join room: " + message);
    public override void OnCreateRoomFailed(short code, string message) => Fail("Could not create room: " + message);
    public override void OnJoinRandomFailed(short code, string message)
    {
        // The manager creates a fallback public room. Stay locked until its result.
    }
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (IsSwitchingRooms) Fail("Disconnected while joining: " + cause);
    }
    public override void OnDisable()
    {
        base.OnDisable();
        if (Instance == this) ClearRequest();
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
