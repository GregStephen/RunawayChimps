using TMPro;
using UnityEngine;
using Photon.Pun;
using Photon.VR;

public class LoadingDebugText : MonoBehaviour
{
    public TMP_Text debugText;

    void Update()
    {
        if (!debugText) return;

        string app = AppState.I == null ? "AppState: NULL" : $"AppState Ready: {AppState.I.IsReady}";
        string pun = $"PUN Connected:{PhotonNetwork.IsConnected} InRoom:{PhotonNetwork.InRoom} State:{PhotonNetwork.NetworkClientState}";
        string pvr = PhotonVRManager.Manager == null ? "PVR: NULL" : $"PVR State:{PhotonVRManager.GetConnectionState()}";

        debugText.text = $"{app}\n{pun}\n{pvr}";
    }
}
