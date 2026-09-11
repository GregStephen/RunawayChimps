using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

using Photon.VR.Player;
using Photon.Pun;
using Photon.Realtime;

using RunawayChimps.Zones;
using Photon.VR.Saving;

namespace Photon.VR
{
    public class PhotonVRManager : MonoBehaviourPunCallbacks
    {
        public static PhotonVRManager Manager { get; private set; }

        /// <summary>
        /// Invoked when a PhotonVRManager instance is ready (Manager set).
        /// </summary>
        public static event System.Action ManagerReady;

        [Header("Photon")]
        public string AppId;
        public string VoiceAppId;
        [Tooltip("Please read https://doc.photonengine.com/en-us/pun/current/connection-and-authentication/regions for more information")]
        public string Region = "eu";

        [Header("Player Rig (Local XR)")]
        public Transform Head;
        public Transform LeftHand;
        public Transform RightHand;

        [Header("Player Settings")]
        public Color Colour;

        /// <summary>
        /// Local cache of cosmetics for this client (persisted via PhotonVRValueSaver).
        /// Synced to other clients via CustomProperties as a Photon Hashtable.
        /// </summary>
        public Dictionary<string, string> Cosmetics { get; private set; } = new Dictionary<string, string>();

        [Header("Networking")]
        [Tooltip("Queue name for the PUBLIC lobby matchmaking.")]
        public string PublicQueue = "lobby";

        [Tooltip("Max players per room (public + private).")]
        public int DefaultRoomLimit = 10;

        [Header("Other")]
        [Tooltip("If the user shall connect when this object has awoken")]
        public bool ConnectOnAwake = true;

        [Tooltip("If the user shall join a PUBLIC room automatically when connected")]
        public bool JoinRoomOnConnect = true;

        [NonSerialized]
        public PhotonVRPlayer LocalPlayer;

        /// <summary>
        /// Raised when the local player's head transform becomes available.
        /// Subscribers can bind to this to get the head transform.
        /// </summary>
        public event System.Action<Transform> LocalHeadBound;

        private RoomOptions _lastMatchmakingOptions;
        private ConnectionState _state = ConnectionState.Disconnected;

        /// <summary>
        /// Optional: if you are switching rooms (leave -> join private), set this to true
        /// before leaving so the manager doesn't auto-join the public lobby when it reconnects to Master.
        /// </summary>
        public static bool SuppressAutoLobbyJoinOnce { get; set; }

        // -------------------------
        // Unity lifecycle
        // -------------------------

        private void Awake()
        {
            if (Manager == null)
            {
                Manager = this;
            }
            else if (Manager != this)
            {
                Debug.LogError("There can't be multiple PhotonVRManagers in a scene");
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            // Notify watchers that the manager is ready
            ManagerReady?.Invoke();
        }

        /// <summary>
        /// Notify the manager that the local player's head is available (and update internal Head reference).
        /// This will raise the LocalHeadBound event for subscribers.
        /// </summary>
        public void NotifyLocalHead(Transform head)
        {
            if (head == null) return;
            Head = head;
            Debug.Log($"[PhotonVRManager] NotifyLocalHead called with head={head.name}");
            LocalHeadBound?.Invoke(head);
        }

        private void Start()
        {
            // Load saved values first (so the initial broadcast is correct)
            if (!string.IsNullOrEmpty(PlayerPrefs.GetString("Colour")))
            {
                try { Colour = JsonUtility.FromJson<Color>(PlayerPrefs.GetString("Colour")); }
                catch (ArgumentException) { Debug.LogWarning("Saved avatar color is invalid; using the default.", this); }
            }

            if (!string.IsNullOrEmpty(PlayerPrefs.GetString("Cosmetics")))
                Cosmetics = PhotonVRValueSaver.GetDictionary("Cosmetics");

            if (ConnectOnAwake)
                Connect();
        }

#if UNITY_EDITOR
        public void CheckDefaultValues()
        {
            bool b = CheckForRig(this);
            if (b)
            {
                if (string.IsNullOrEmpty(AppId))
                    AppId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime;

                if (string.IsNullOrEmpty(VoiceAppId))
                    VoiceAppId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice;

                Debug.Log("Attempted to set default values");
            }
        }

        private bool CheckForRig(PhotonVRManager manager)
        {
            GameObject[] objects = FindObjectsOfType<GameObject>();
            bool b = false;

            if (manager.Head == null)
            {
                b = true;
                foreach (GameObject obj in objects)
                {
                    if (obj.name.Contains("Camera") || obj.name.Contains("Head"))
                    {
                        manager.Head = obj.transform;
                        break;
                    }
                }
            }

            if (manager.LeftHand == null)
            {
                b = true;
                foreach (GameObject obj in objects)
                {
                    if (obj.name.Contains("Left") && (obj.name.Contains("Hand") || obj.name.Contains("Controller")))
                    {
                        manager.LeftHand = obj.transform;
                        break;
                    }
                }
            }

            if (manager.RightHand == null)
            {
                b = true;
                foreach (GameObject obj in objects)
                {
                    if (obj.name.Contains("Right") && (obj.name.Contains("Hand") || obj.name.Contains("Controller")))
                    {
                        manager.RightHand = obj.transform;
                        break;
                    }
                }
            }

            return b;
        }
#endif

        // -------------------------
        // Helpers
        // -------------------------

        private static ExitGames.Client.Photon.Hashtable CosmeticsToHashtable(Dictionary<string, string> cosmetics)
        {
            var ht = new ExitGames.Client.Photon.Hashtable();
            if (cosmetics == null) return ht;

            foreach (var kv in cosmetics)
                ht[kv.Key] = kv.Value;

            return ht;
        }

        private static string GetUsernameLocal()
        {
            return PlayerPrefs.GetString("Username", "Player");
        }

        private static void SaveUsernameLocal(string name)
        {
            PlayerPrefs.SetString("Username", name);
            PlayerPrefs.Save();
        }

        // -------------------------
        // Connect
        // -------------------------

        public static bool Connect()
        {
            if (Manager == null)
            {
                Debug.LogError("PhotonVRManager.Manager is null (no instance in scene).");
                return false;
            }

            if (string.IsNullOrEmpty(Manager.AppId) || string.IsNullOrEmpty(Manager.VoiceAppId))
            {
                Debug.LogError("Please input an app id");
                return false;
            }

            if (PhotonNetwork.IsConnected || Manager._state == ConnectionState.Connecting) return true;
            PhotonNetwork.AuthValues = null;

            Manager._state = ConnectionState.Connecting;
            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = Manager.AppId;
            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = Manager.VoiceAppId;
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = Manager.Region;

            bool started = PhotonNetwork.ConnectUsingSettings();
            if (!started) Manager._state = ConnectionState.Disconnected;
            Debug.Log($"Connecting - AppId: {PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime} VoiceAppId: {PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice}");
            return started;
        }

        public static bool ConnectAuthenticated(string username, string token)
        {
            if (Manager == null)
            {
                Debug.LogError("PhotonVRManager.Manager is null (no instance in scene).");
                return false;
            }

            if (string.IsNullOrEmpty(Manager.AppId) || string.IsNullOrEmpty(Manager.VoiceAppId))
            {
                Debug.LogError("Please input an app id");
                return false;
            }

            AuthenticationValues authentication = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
            authentication.AddAuthParameter("username", username);
            authentication.AddAuthParameter("token", token);
            PhotonNetwork.AuthValues = authentication;

            Manager._state = ConnectionState.Connecting;
            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = Manager.AppId;
            PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = Manager.VoiceAppId;
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = Manager.Region;

            bool started = PhotonNetwork.ConnectUsingSettings();
            if (!started) Manager._state = ConnectionState.Disconnected;
            Debug.Log($"Connecting (auth) - AppId: {PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime} VoiceAppId: {PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice}");
            return started;
        }

        public void Disconnect()
        {
            PhotonNetwork.Disconnect();
        }

        // -------------------------
        // User properties (live sync)
        // -------------------------

        public static void SetUsername(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Player";

            PhotonNetwork.LocalPlayer.NickName = name;
            SaveUsernameLocal(name);

            var props = new ExitGames.Client.Photon.Hashtable
            {
                ["DisplayName"] = name
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            if (PhotonNetwork.InRoom && Manager != null && Manager.LocalPlayer != null)
                Manager.LocalPlayer.RefreshPlayerValues();
        }

        public static void SetColour(Color playerColour)
        {
            Manager.Colour = playerColour;

            PlayerPrefs.SetString("Colour", JsonUtility.ToJson(playerColour));
            PlayerPrefs.Save();

            var props = new ExitGames.Client.Photon.Hashtable
            {
                ["Colour"] = JsonUtility.ToJson(playerColour)
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            if (PhotonNetwork.InRoom && Manager != null && Manager.LocalPlayer != null)
                Manager.LocalPlayer.RefreshPlayerValues();
        }

        public static void SetCosmetics(Dictionary<string, string> playerCosmetics)
        {
            Manager.Cosmetics = playerCosmetics ?? new Dictionary<string, string>();

            var props = new ExitGames.Client.Photon.Hashtable
            {
                ["Cosmetics"] = CosmeticsToHashtable(Manager.Cosmetics)
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            PhotonVRValueSaver.SaveDictionary("Cosmetics", Manager.Cosmetics);

            if (PhotonNetwork.InRoom && Manager != null && Manager.LocalPlayer != null)
                Manager.LocalPlayer.RefreshPlayerValues();
        }

        public static void SetCosmetic(string type, string cosmeticId)
        {
            if (Manager.Cosmetics == null)
                Manager.Cosmetics = new Dictionary<string, string>();

            Manager.Cosmetics[type] = cosmeticId;

            var props = new ExitGames.Client.Photon.Hashtable
            {
                ["Cosmetics"] = CosmeticsToHashtable(Manager.Cosmetics)
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            PhotonVRValueSaver.SaveDictionary("Cosmetics", Manager.Cosmetics);

            if (PhotonNetwork.InRoom && Manager != null && Manager.LocalPlayer != null)
                Manager.LocalPlayer.RefreshPlayerValues();
        }

        // -------------------------
        // Callbacks
        // -------------------------

        public override void OnConnectedToMaster()
        {
            _state = ConnectionState.Connected;
            Debug.Log("ConnectedToMaster");

            // Initial identity (local cache -> Photon)
            var username = GetUsernameLocal();
            PhotonNetwork.LocalPlayer.NickName = username;

            // Initial broadcast (ensures all clients have baseline values)
            var props = new ExitGames.Client.Photon.Hashtable
            {
                ["DisplayName"] = username,
                ["Colour"] = JsonUtility.ToJson(Colour),
                ["Cosmetics"] = CosmeticsToHashtable(Cosmetics)
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            if (RoomSwitchService.Instance != null && RoomSwitchService.Instance.IsSwitchingRooms) return;
            if (!JoinRoomOnConnect) return;

            if (SuppressAutoLobbyJoinOnce)
            {
                SuppressAutoLobbyJoinOnce = false;
                Debug.Log("[PhotonVRManager] Suppressing auto lobby join (one time).");
                return;
            }

            // Public lobby matchmaking: join any room matching queue+version, else create one.
            _JoinRandomRoom(PublicQueue, DefaultRoomLimit);
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"JoinedRoom: {(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "(null)")}");
            _state = ConnectionState.InRoom;
            ZoneStateService.Instance?.HandleJoinedRoom();
        }
        public override void OnPlayerEnteredRoom(Realtime.Player newPlayer)
        {
            base.OnPlayerEnteredRoom(newPlayer);
            ZoneStateService.Instance?.HandlePlayerEntered(newPlayer);
        }

        public override void OnPlayerLeftRoom(Realtime.Player otherPlayer)
        {
            base.OnPlayerLeftRoom(otherPlayer);
            ZoneStateService.Instance?.HandlePlayerLeft(otherPlayer);
        }

        public override void OnPlayerPropertiesUpdate(Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);
            ZoneStateService.Instance?.HandlePlayerPropertiesUpdated(targetPlayer, changedProps);
        }


        public override void OnDisconnected(DisconnectCause cause)
        {
            base.OnDisconnected(cause);
            _state = ConnectionState.Disconnected;
            Debug.Log("Disconnected from server: " + cause);
        }

        public override void OnLeftRoom() => _state = ConnectionState.Connected;
        public override void OnJoinRoomFailed(short code, string message) => _state = ConnectionState.Connected;
        public override void OnCreateRoomFailed(short code, string message) => _state = ConnectionState.Connected;

        private void OnDestroy()
        {
            if (Manager == this) { Manager = null; SuppressAutoLobbyJoinOnce = false; }
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"OnJoinRandomFailed ({returnCode}): {message}");
            HandleJoinError();
        }

        // -------------------------
        // Rooms / matchmaking
        // -------------------------

        public static ConnectionState GetConnectionState()
        {
            return Manager != null ? Manager._state : ConnectionState.Disconnected;
        }

        public static void SwitchScenes(int sceneIndex, int maxPlayers)
        {
            SwitchScenes(sceneIndex);
        }

        public static void SwitchScenes(int sceneIndex)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
            var travel = RunawayChimps.Travel.SectorTravelService.I;
            if (travel == null || string.IsNullOrEmpty(path) ||
                !travel.TravelTo(System.IO.Path.GetFileNameWithoutExtension(path)))
                Debug.LogWarning("Use an available sector destination after starting from Bootstrap.");
        }

        public static bool JoinRandomRoom(string queue, int maxPlayers) => _JoinRandomRoom(queue, maxPlayers);
        public static bool JoinRandomRoom(string queue) => Manager != null && _JoinRandomRoom(queue, Manager.DefaultRoomLimit);

        private static bool _JoinRandomRoom(string queue, int maxPlayers)
        {
            if (Manager == null) return false;
            maxPlayers = Mathf.Clamp(maxPlayers, 1, 10);
            Manager._state = ConnectionState.JoiningRoom;

            var roomProps = new ExitGames.Client.Photon.Hashtable
            {
                { "queue", queue },
                { "version", Application.version }
            };

            var roomOptions = new RoomOptions
            {
                MaxPlayers = (byte)maxPlayers,
                IsVisible = true,
                IsOpen = true,
                CustomRoomProperties = roomProps,
                CustomRoomPropertiesForLobby = new[] { "queue", "version" }
            };

            // Save so HandleJoinError can create a matching room if needed.
            Manager._lastMatchmakingOptions = roomOptions;

            bool started = PhotonNetwork.JoinRandomRoom(roomProps, (byte)maxPlayers, MatchmakingMode.RandomMatching, null, null, null);
            if (!started) Manager._state = ConnectionState.Connected;
            Debug.Log($"Joining random room (queue={queue}, version={Application.version})");
            return started;
        }

        private void HandleJoinError()
        {
            Debug.Log("Failed to join a public room - creating a new one");

            // Ensure options exist and match our public queue
            if (_lastMatchmakingOptions == null)
            {
                var roomProps = new ExitGames.Client.Photon.Hashtable
                {
                    { "queue", PublicQueue },
                    { "version", Application.version }
                };

                _lastMatchmakingOptions = new RoomOptions
                {
                    MaxPlayers = (byte)Mathf.Clamp(DefaultRoomLimit, 1, 10),
                    IsVisible = true,
                    IsOpen = true,
                    CustomRoomProperties = roomProps,
                    CustomRoomPropertiesForLobby = new[] { "queue", "version" }
                };
            }

            string roomName = $"LOBBY_{CreateRoomCode()}";
            Debug.Log($"Creating public room: {roomName}");

            if (!PhotonNetwork.CreateRoom(roomName, _lastMatchmakingOptions, null, null))
            {
                _state = ConnectionState.Connected;
                RoomSwitchService.Instance?.OnCreateRoomFailed(0, "Could not start fallback room creation.");
            }
        }

        public static bool JoinPrivateRoom(string roomId, int maxPlayers) => _JoinPrivateRoom(roomId, maxPlayers);
        public static bool JoinPrivateRoom(string roomId) => Manager != null && _JoinPrivateRoom(roomId, Manager.DefaultRoomLimit);

        public static bool _JoinPrivateRoom(string roomId, int maxPlayers)
        {
            if (Manager == null || string.IsNullOrWhiteSpace(roomId)) return false;
            maxPlayers = Mathf.Clamp(maxPlayers, 1, 10);
            Manager._state = ConnectionState.JoiningRoom;
            bool started = PhotonNetwork.JoinOrCreateRoom(
                roomId,
                new RoomOptions
                {
                    IsVisible = false,
                    IsOpen = true,
                    MaxPlayers = (byte)maxPlayers
                },
                null,
                null
            );

            if (!started) Manager._state = ConnectionState.Connected;
            Debug.Log($"Joining a private room: {roomId}");
            return started;
        }

        public string CreateRoomCode()
        {
            // Use more spread than 0-99999 to reduce collisions
            return UnityEngine.Random.Range(10000, 99999).ToString();
        }
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        JoiningRoom,
        InRoom
    }
}
