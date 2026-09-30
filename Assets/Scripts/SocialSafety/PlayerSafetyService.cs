using System;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace RunawayChimps.SocialSafety
{
    // Lives outside streamed environments; local mutes are never sent to other players.
    public sealed class PlayerSafetyService : MonoBehaviourPunCallbacks
    {
        public const string AccountProperty = "rcReportId";
        public static PlayerSafetyService Instance { get; private set; }
        public readonly PlayerSafetyState State = new PlayerSafetyState();
        public string LocalAccountId { get; private set; }
        public string Status { get; private set; } = "Mute only affects what you hear.";
        public event Action Changed;
        private Room room;
        private float reportDeadline;
        private int pendingRequest;
        private int pendingRoom;
        private string pendingTarget;

        public sealed class ReportTarget
        {
            public int RoomGeneration;
            public int Actor;
            public string AccountId;
            public string Name;
            public string Sector;
            public string RequestId;
        }

        [Serializable]
        private sealed class ReportContext
        {
            public int schema = 1;
            public string reason;
            public int actor;
            public string displayName;
            public string sector;
            public string utc;
            public string build;
            public string requestId;
            public string identity = "client-claimed";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Instance == null) new GameObject("Player Safety").AddComponent<PlayerSafetyService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<PlayerBoardShortcut>();
            gameObject.AddComponent<PlayerBoardHubPresenter>();
        }

        public void SetLocalAccount(string id)
        {
            if (!string.Equals(LocalAccountId, id, StringComparison.Ordinal))
            {
                State.ChangeRoom();
                LocalAccountId = PlayerSafetyState.ValidAccountId(id) ? id : null;
            }
            PublishIdentity();
        }

        private void PublishIdentity()
        {
            if (PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { AccountProperty, LocalAccountId } });
        }

        private void SyncRoom()
        {
            var current = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null;
            if (ReferenceEquals(room, current)) return;
            room = current;
            State.ChangeRoom();
            Status = current == null ? "Not in a room." : "Mute only affects what you hear.";
            Changed?.Invoke();
        }

        private void Update()
        {
            SyncRoom();
            if (State.IsSending && Time.realtimeSinceStartup >= reportDeadline)
                Finish(pendingRequest, pendingRoom, pendingTarget, false,
                    "No confirmation received. Wait, then retry; your report may have arrived.");
        }

        public static bool IsMuted(Player player) => player != null && !player.IsLocal &&
            Instance != null && ReferenceEquals(Instance.room, PhotonNetwork.CurrentRoom) && Instance.State.IsMuted(player.ActorNumber);

        public void ToggleMute(Player player)
        {
            SyncRoom();
            if (!IsCurrentPlayer(player) || player.IsLocal) return;
            bool muted = !State.IsMuted(player.ActorNumber);
            State.SetMuted(player.ActorNumber, PhotonNetwork.LocalPlayer.ActorNumber, muted);
            Status = PlayerSafetyState.SafeName(player.NickName) + (muted ? " muted for you." : " unmuted for you.");
            Changed?.Invoke();
        }

        private bool IsCurrentPlayer(Player player) => PhotonNetwork.InRoom && player != null &&
            ReferenceEquals(PhotonNetwork.CurrentRoom.GetPlayer(player.ActorNumber), player);

        public ReportTarget CaptureTarget(Player player)
        {
            SyncRoom();
            if (!IsCurrentPlayer(player) || player.IsLocal) return null;
            player.CustomProperties.TryGetValue(AccountProperty, out object value);
            return new ReportTarget
            {
                RoomGeneration = State.RoomGeneration, Actor = player.ActorNumber,
                AccountId = value as string, Name = PlayerSafetyState.SafeName(player.NickName),
                Sector = RunawayChimps.Travel.SectorPresence.Get(player).ToString(), RequestId = Guid.NewGuid().ToString("N")
            };
        }

        public void Submit(ReportTarget target, PlayerReportReason reason)
        {
            SyncRoom();
            if (target == null || State.IsSending) return;
            if (target.RoomGeneration != State.RoomGeneration) { Show("Room changed. Select the player again."); return; }
            if (!Enum.IsDefined(typeof(PlayerReportReason), reason)) return;
            if (!PlayerSafetyState.ValidAccountId(target.AccountId)) { Show("Reporting unavailable for this player. You can still mute them."); return; }
            if (string.Equals(target.AccountId, LocalAccountId, StringComparison.OrdinalIgnoreCase)) { Show("Cannot report your own account."); return; }
            if (!PlayFabClientAPI.IsClientLoggedIn()) { Show("Sign-in required to send a report. Please reconnect."); return; }
            // A departed player's frozen target remains reportable; never retarget a recycled row.
            var current = PhotonNetwork.CurrentRoom.GetPlayer(target.Actor);
            if (current != null)
            {
                current.CustomProperties.TryGetValue(AccountProperty, out object id);
                if (!string.Equals(id as string, target.AccountId, StringComparison.Ordinal))
                { Show("Player identity changed. Select the player again."); return; }
            }
            if (State.WasReported(target.AccountId)) { Show("Already submitted for this player in this room."); return; }
            int request = State.BeginReport(target.AccountId, target.RoomGeneration, Time.realtimeSinceStartup);
            if (request == 0) { Show("Please wait a moment before retrying."); return; }
            pendingRequest = request; pendingRoom = target.RoomGeneration; pendingTarget = target.AccountId;
            reportDeadline = Time.realtimeSinceStartup + 20f;
            Show("Sending report...");
            try
            {
                PlayFabClientAPI.ReportPlayer(new ReportPlayerClientRequest
                {
                    ReporteeId = target.AccountId,
                    Comment = JsonUtility.ToJson(new ReportContext
                    {
                        reason = reason.ToString(), actor = target.Actor, displayName = target.Name,
                        sector = target.Sector, utc = DateTime.UtcNow.ToString("O"),
                        build = Application.version, requestId = target.RequestId
                    })
                }, result =>
                {
                    // This SDK exposes no Updated flag. Zero remaining is ambiguous (fifth
                    // accepted report OR a capped request); never turn it into a false success.
                    bool accepted = result != null && result.SubmissionsRemaining > 0;
                    Finish(request, target.RoomGeneration, target.AccountId, accepted, accepted
                        ? "Report submitted. Thank you."
                        : "Daily report limit reached. This submission could not be confirmed.");
                }, error => Finish(request, target.RoomGeneration, target.AccountId, false,
                    "Report could not be sent. Check your connection and retry."));
            }
            catch (Exception)
            {
                Finish(request, target.RoomGeneration, target.AccountId, false, "Report could not be sent. Please retry.");
            }
        }

        private void Finish(int request, int generation, string target, bool accepted, string message)
        {
            if (this == null) return;
            SyncRoom();
            if (State.CompleteReport(request, generation, target, accepted)) Show(message);
        }

        private void Show(string message) { Status = message; Changed?.Invoke(); }
        public override void OnJoinedRoom() { SyncRoom(); PublishIdentity(); }
        public override void OnLeftRoom() => SyncRoom();
        public override void OnDisconnected(DisconnectCause cause) => SyncRoom();
        public override void OnPlayerEnteredRoom(Player newPlayer) => Changed?.Invoke();
        public override void OnPlayerLeftRoom(Player otherPlayer) => Changed?.Invoke();
        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) => Changed?.Invoke();
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
