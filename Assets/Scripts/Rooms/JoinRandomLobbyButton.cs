using Photon.Pun;
using Photon.VR;
using UnityEngine;

public class JoinRandomLobbyButton : MonoBehaviour
{

    // Call this from your physical button press event
    public void Press()
    {
        var s = RoomSwitchService.Instance;
        if (s == null)
        {
            Debug.LogError("[JoinRandomLobbyButton] RoomSwitchService.Instance is null. " +
                           "Make sure RoomSwitchService exists in Bootstrap and is DontDestroyOnLoad.");
            return;
        }
        if (s.IsSwitchingRooms)
        {
            Debug.Log("[JoinRandomLobbyButton] Ignored press: switching rooms.");
            return;
        }

        Debug.Log("[JoinRandomLobbyButton] Pressed -> Join random public lobby");
        s.JoinRandomPublicLobby();
    }

}
