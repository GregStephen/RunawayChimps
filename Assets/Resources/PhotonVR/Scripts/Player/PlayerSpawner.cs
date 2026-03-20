using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

namespace Photon.VR.Player
{
    public class PlayerSpawner : MonoBehaviourPunCallbacks
    {
        [Tooltip("The location of the player prefab")]
        public string PrefabLocation = "PhotonVR/Player";

        public string HubSceneName = "Hub_Base";
        public string HubSpawnObjectName = "HubSpawn";

        private GameObject playerTemp;

        private void Awake() => DontDestroyOnLoad(gameObject);

        public override void OnJoinedRoom()
        {
            Debug.Log("[PlayerSpawner] OnJoinedRoom fired");
            AppState.I?.SetStatus("Waiting for hub...");
            StartCoroutine(CoSpawnWhenHubReady());
        }

        private IEnumerator CoSpawnWhenHubReady()
        {
            // 1) Wait until Hub_Base is LOADED (not active)
            Scene hubScene;
            do
            {
                hubScene = SceneManager.GetSceneByName(HubSceneName);
                yield return null;
            }
            while (!hubScene.isLoaded);

            // 2) Let scene Awake/Start run
            yield return null;

            // 3) Find spawn
            var spawnGo = GameObject.Find(HubSpawnObjectName);
            if (spawnGo == null)
            {
                Debug.LogError($"[PlayerSpawner] Could not find '{HubSpawnObjectName}' after hub loaded.");
            }

            Vector3 pos = spawnGo != null ? spawnGo.transform.position : Vector3.zero;
            Quaternion rot = spawnGo != null
                ? Quaternion.Euler(0f, spawnGo.transform.rotation.eulerAngles.y, 0f)
                : Quaternion.identity;

            AppState.I?.SetStatus("Spawning player...");
            playerTemp = PhotonNetwork.Instantiate(PrefabLocation, pos, rot);

            AppState.I?.MarkPhotonPlayerSpawned();
            AppState.I?.TryMarkReady();

            // Give PhotonVRPlayer/manager/binders 1–2 frames to wire up
            yield return null;
            yield return new WaitForFixedUpdate();

            AppState.I?.SetStatus("Finalizing...");
            AppState.I?.TryMarkReady();

            Debug.Log("[PlayerSpawner] Spawn complete.");

        }

        public override void OnLeftRoom()
        {
            if (playerTemp != null)
                PhotonNetwork.Destroy(playerTemp);

            playerTemp = null;
        }
    }
}
