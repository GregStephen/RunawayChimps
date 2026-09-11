using UnityEngine;
using RunawayChimps.Zones;

public class ZoneTrigger : MonoBehaviour
{
    public ZoneId zone = ZoneId.Hub;

    [Tooltip("If true, will also re-apply zone when you re-enter (even if same zone).")]
    public bool reapplyEvenIfSame = true;
    public bool debugLogs = false;

    private bool IsLevelOneSafeBoundary =>
        gameObject.scene.name == "Level1_Containment" && zone == ZoneId.Level1_Antechamber;

    private void OnTriggerEnter(Collider other)
    {
        var travel = RunawayChimps.Travel.SectorTravelService.I;
        if (travel != null && (travel.IsBusy || gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene())) return;
        var marker = other.GetComponentInParent<LocalRigMarker>();
        if (marker == null)
            return;

        // The local rig has separate hand, body and head colliders. A reaching hand
        // should not make the whole player safe while their tracked head is still in
        // the vent, so Level 1 safe boundaries use the head as the crossing authority.
        if (IsLevelOneSafeBoundary && !other.CompareTag("MainCamera"))
            return;

        var zs = ZoneStateService.Instance;
        if (zs == null)
        {
            Debug.LogWarning($"[ZoneTrigger] {name} enter by {other.name}, but ZoneStateService.Instance is null");
            return;
        }

        if (!reapplyEvenIfSame && zs.LocalZone == zone)
            return;

        if (debugLogs)
            Debug.Log($"[ZoneTrigger] ENTER trigger={name} zone={zone} other={other.name} previousZone={zs.LocalZone}");
        zs.SetLocalZone(zone);
    }

    private void OnTriggerExit(Collider other)
    {
        var marker = other.GetComponentInParent<LocalRigMarker>();
        if (marker == null)
            return;

        var zs = ZoneStateService.Instance;
        if (zs == null)
            return;

        if (debugLogs)
            Debug.Log($"[ZoneTrigger] EXIT trigger={name} zone={zone} other={other.name} currentZone={zs.LocalZone}");

        // Level 1 has two safe-room boundaries but only one separately-authored vent
        // trigger. Both safe boundaries are oriented with local +X pointing back into
        // the vent network. When the tracked head leaves a safe trigger on that side,
        // publish the vent zone here. Leaving deeper into the safe room stays safe.
        if (!IsLevelOneSafeBoundary ||
            !other.CompareTag("MainCamera") ||
            zs.LocalZone != ZoneId.Level1_Antechamber)
            return;

        var box = GetComponent<BoxCollider>();
        if (box == null)
            return;

        Vector3 localCenter = transform.InverseTransformPoint(other.bounds.center);
        if (localCenter.x > box.center.x)
        {
            if (debugLogs)
                Debug.Log($"[ZoneTrigger] Safe boundary exited toward vents by {other.name}; applying {ZoneId.Level1_Vents}.");
            zs.SetLocalZone(ZoneId.Level1_Vents);
        }
    }
}
