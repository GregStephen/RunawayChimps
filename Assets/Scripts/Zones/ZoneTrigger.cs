using UnityEngine;
using RunawayChimps.Zones;

public class ZoneTrigger : MonoBehaviour
{
    public ZoneId zone = ZoneId.Hub;

    [Tooltip("If true, will also re-apply zone when you re-enter (even if same zone).")]
    public bool reapplyEvenIfSame = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<LocalRigMarker>() == null)
            return;

        var zs = ZoneStateService.Instance;
        if (zs == null) return;

        // If you only want changes, set reapplyEvenIfSame=false
        if (!reapplyEvenIfSame && zs.LocalZone == zone)
            return;

        zs.SetLocalZone(zone);

        Debug.Log($"[ZoneTrigger] Local zone entered: {zone}");
    }
}
