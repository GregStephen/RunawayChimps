using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Travel;

namespace Photon.VR.Player
{
    public class PlayerSpawner : MonoBehaviourPunCallbacks
    {
        [Tooltip("The location of the player prefab")]
        public string PrefabLocation = "PhotonVR/Player";
        public string HubSceneName = "Hub_Base";
        public string HubSpawnObjectName = "HubSpawn";
        [Min(1f)] public float spawnWaitTimeout = 30f;

        private GameObject playerTemp;
        private Coroutine pendingSpawn;
        private int spawnAttempt;

        private void Awake() => DontDestroyOnLoad(gameObject);

        public void RetrySpawn()
        {
            if (!PhotonNetwork.InRoom) return;
            if (playerTemp == null) OnJoinedRoom();
            else
            {
                AppState.I?.MarkPhotonPlayerSpawned();
                playerTemp.GetComponentInChildren<PlayerVisualReadyReporter>(true)?.BeginReporting();
            }
        }

        public override void OnJoinedRoom()
        {
            CancelPendingSpawn();
            if (playerTemp != null || !PhotonNetwork.InRoom) return;
            AppState.I?.SetStatus("Waiting for environment...");
            pendingSpawn = StartCoroutine(CoSpawnWhenEnvironmentReady(spawnAttempt, PhotonNetwork.CurrentRoom));
        }

        private bool IsCurrentAttempt(int attempt, Room room) => isActiveAndEnabled &&
            attempt == spawnAttempt && PhotonNetwork.InRoom && ReferenceEquals(room, PhotonNetwork.CurrentRoom);

        private IEnumerator CoSpawnWhenEnvironmentReady(int attempt, Room room)
        {
            // Let the room callbacks and scene Awake/Start finish before spawning.
            yield return null;
            float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, spawnWaitTimeout);
            Vector3 position = default;
            Quaternion rotation = Quaternion.identity;
            while (IsCurrentAttempt(attempt, room))
            {
                if (TryGetSpawnPose(out position, out rotation)) break;
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogError("[PlayerSpawner] Environment did not become ready before the spawn timeout. Rejoin after resolving the loading error.", this);
                    AppState.I?.Fail("Player setup timed out. Please retry.");
                    pendingSpawn = null;
                    yield break;
                }
                yield return null;
            }
            if (!IsCurrentAttempt(attempt, room)) yield break;
            if (playerTemp != null) { pendingSpawn = null; yield break; }

            AppState.I?.SetStatus("Spawning player...");
            try
            {
                playerTemp = PhotonNetwork.Instantiate(PrefabLocation, position, rotation);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                AppState.I?.Fail("Player setup failed. Please retry.");
            }
            pendingSpawn = null;
            if (playerTemp == null || !IsCurrentAttempt(attempt, room)) yield break;
            AppState.I?.MarkPhotonPlayerSpawned();
            AppState.I?.TryMarkReady();
        }

        private bool TryGetSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            var hub = SceneManager.GetSceneByName(HubSceneName);
            var active = SceneManager.GetActiveScene();
            // Room switching/reconnecting may happen while the Hub is unloaded.
            // Startup still waits for Hub; an active level is also a valid environment.
            if (!hub.isLoaded && SectorScene.Find(active) == null) return false;
            var manager = PhotonVRManager.Manager;
            if (manager != null && manager.Head != null)
            {
                position = manager.Head.position;
                rotation = Quaternion.Euler(0, manager.Head.eulerAngles.y, 0);
                return true;
            }
            if (!hub.isLoaded) return false;
            foreach (var root in hub.GetRootGameObjects())
                foreach (var marker in root.GetComponentsInChildren<Transform>(true))
                    if (marker.name == HubSpawnObjectName)
                    {
                        position = marker.position;
                        rotation = Quaternion.Euler(0, marker.eulerAngles.y, 0);
                        return true;
                    }
            return false;
        }

        private void CancelPendingSpawn()
        {
            spawnAttempt++;
            if (pendingSpawn != null) StopCoroutine(pendingSpawn);
            pendingSpawn = null;
        }

        private void ClearLocalAvatar()
        {
            AppState.I?.ResetPlayerReady();
            CancelPendingSpawn();
            // PUN removes room-owned objects on leave. Dispose any local remainder
            // without sending PhotonNetwork.Destroy after membership has ended.
            if (playerTemp != null) Destroy(playerTemp);
            playerTemp = null;
        }

        public override void OnLeftRoom() => ClearLocalAvatar();
        public override void OnDisconnected(DisconnectCause cause) => ClearLocalAvatar();

        public override void OnDisable()
        {
            base.OnDisable();
            CancelPendingSpawn();
        }
    }
}
