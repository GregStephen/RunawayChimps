using Photon.Pun;
using UnityEngine;

public class PlayerVentState : MonoBehaviour
{
    public static PlayerVentState Local { get; private set; }

    public bool IsInVent { get; private set; }

    private void Awake()
    {
        PhotonView view = GetComponentInParent<PhotonView>();
        if (view != null && view.IsMine)
        {
            Local = this;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Local != this) return;

        if (other.CompareTag("Vent"))    // we'll tag our vent volume "Vent"
        {
            IsInVent = true;
            // Debug.Log("Local player entered vent.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (Local != this) return;

        if (other.CompareTag("Vent"))
        {
            IsInVent = false;
            // Debug.Log("Local player exited vent.");
        }
    }

    public static bool LocalPlayerInVent =>
        Local != null && Local.IsInVent;
}

