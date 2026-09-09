using Photon.Pun;
using RunawayChimps.Zones;
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

    private void OnDestroy()
    {
        if (Local == this)
            Local = null;
    }

    public static bool LocalPlayerInVent
    {
        get
        {
            // Hub_Base already uses ZoneTrigger on the persistent local rig.
            // That rig has no parent PhotonView, so the legacy Local reference
            // is not a reliable signal in this scene.
            var zones = ZoneStateService.Instance;
            if (zones != null)
                return PhotonNetwork.InRoom && zones.LocalZone == ZoneId.Level1_Vents;

            // Preserve support for older scenes that only use Vent-tagged volumes.
            return Local != null && Local.IsInVent;
        }
    }
}
