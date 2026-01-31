using UnityEngine;

using Photon.Pun;

namespace Photon.VR.Player
{
    public class PlayerSpawner : MonoBehaviourPunCallbacks
    {
        [Tooltip("The location of the player prefab")]
        public string PrefabLocation = "PhotonVR/Player";
        private GameObject playerTemp;

        private void Awake() => DontDestroyOnLoad(gameObject);

        public override void OnJoinedRoom()
        {
            Debug.Log("[PlayerSpawner] OnJoinedRoom fired");
            AppState.I?.SetStatus("Spawning player...");
            playerTemp = PhotonNetwork.Instantiate(PrefabLocation, Vector3.zero, Quaternion.identity);

            // At this point: joined room + local player object exists
            AppState.I?.SetStatus("Entering hub...");
            Debug.Log("[PlayerSpawner] Player instantiated, marking ready");
            AppState.I?.MarkReady();
        }

        public override void OnLeftRoom()
        {
            PhotonNetwork.Destroy(playerTemp);
        }
    }

}