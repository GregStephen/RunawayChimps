using UnityEngine;
using RunawayChimps.Zones;

public class ZoneTrigger : MonoBehaviour
{
    public ZoneId zone = ZoneId.Hub;

    [Tooltip("If true, will also re-apply zone when you re-enter (even if same zone).")]
    public bool reapplyEvenIfSame = true;

    private void OnTriggerEnter(Collider other)
    {
        var marker = other.GetComponentInParent<LocalRigMarker>();
        Debug.Log($"[ZoneTrigger] Checking trigger for: {other.name}");
        if (marker == null)
            return;

        var zs = ZoneStateService.Instance;
        if (zs == null)
        {
            Debug.LogWarning($"[ZoneTrigger] {name} enter by {other.name}, but ZoneStateService.Instance is null");
            return;
        }

        if (!reapplyEvenIfSame && zs.LocalZone == zone)
            return;

        Debug.Log($"[ZoneTrigger] ENTER trigger={name} zone={zone} other={other.name} previousZone={zs.LocalZone}");
        zs.SetLocalZone(zone);
    }

    private void OnTriggerStay(Collider other)
    {
        var marker = other.GetComponentInParent<LocalRigMarker>();
        if (marker == null)
            return;

        Debug.Log($"[ZoneTrigger] STAY trigger={name} zone={zone} other={other.name}");
    }

    private void OnTriggerExit(Collider other)
    {
        var marker = other.GetComponentInParent<LocalRigMarker>();
        if (marker == null)
            return;

        Debug.Log($"[ZoneTrigger] EXIT trigger={name} zone={zone} other={other.name}");
    }
}