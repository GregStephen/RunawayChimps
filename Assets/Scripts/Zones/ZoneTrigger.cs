using UnityEngine;
using RunawayChimps.Zones;

public class ZoneTrigger : MonoBehaviour
{
    public ZoneId zone = ZoneId.Hub;

    [Tooltip("If true, will also re-apply zone when you re-enter (even if same zone).")]
    public bool reapplyEvenIfSame = true;
    public bool debugLogs = false;

    private void OnTriggerEnter(Collider other)
    {
        var travel = RunawayChimps.Travel.SectorTravelService.I;
        if (travel != null && (travel.IsBusy || gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene())) return;
        var marker = other.GetComponentInParent<LocalRigMarker>();
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
        // the vent network. When the local rig leaves a safe trigger on that side,
        // publish the vent zone here so chase, crawl presentation and capture cannot
        // remain incorrectly gated as "safe" on the second route. Leaving the same
        // trigger deeper into the safe room is local -X and intentionally stays safe.
        if (gameObject.scene.name != "Level1_Containment" ||
            zone != ZoneId.Level1_Antechamber ||
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
