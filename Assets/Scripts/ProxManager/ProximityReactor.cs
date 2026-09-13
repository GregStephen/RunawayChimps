using UnityEngine;
using UnityEngine.Events;
using RunawayChimps.Zones;

public class ProximityReactor : MonoBehaviour
{
    [Header("Zone (optional but recommended)")]
    public ZoneId ZoneId = ZoneId.None;
    [Header("Settings")]
    public float triggerDistance = 3f;
    public bool IsInVents = false;
    public float exitBuffer = 0.5f;
    public bool debugLogs = false;
    [Header("Events")]
    public UnityEvent OnEnterRange;
    public UnityEvent OnExitRange;
    public UnityEvent<float> OnProximityValue;

    private bool inRange;
    private bool registered;
    public bool HasValidSample { get; private set; }
    public float LastDistance { get; private set; } = float.PositiveInfinity;

    private void OnEnable() => TryRegister();
    private void Update() { if (!registered) TryRegister(); }
    private void OnDisable() { Unregister(); ClearProximity(); }
    private void OnDestroy() => Unregister();

    private void TryRegister()
    {
        if (registered) return;
        var mgr = ProximityManager.Instance;
        if (mgr == null) return;
        mgr.Register(this);
        registered = true;
    }

    private void Unregister()
    {
        if (!registered) return;
        ProximityManager.Instance?.Unregister(this);
        registered = false;
    }

    public void UpdateProximity(float distance, Transform playerTransform)
    {
        HasValidSample = !float.IsNaN(distance) && !float.IsInfinity(distance) && distance >= 0f;
        LastDistance = HasValidSample ? distance : float.PositiveInfinity;
        if (!HasValidSample) { ClearProximity(); return; }

        bool nowInRange = distance <= triggerDistance;
        if (nowInRange && !inRange) { inRange = true; OnEnterRange?.Invoke(); }
        else if (!nowInRange && inRange && distance > triggerDistance + exitBuffer) { inRange = false; OnExitRange?.Invoke(); }

        float normalized = triggerDistance <= 0.0001f ? 0f : Mathf.Clamp01(1f - distance / triggerDistance);
#if UNITY_EDITOR
        if (debugLogs) Debug.Log($"[ProximityReactor] {name} dist={distance:F2} norm={normalized:F2} registered={registered}");
#endif
        OnProximityValue?.Invoke(normalized);
    }

    public void ClearProximity()
    {
        bool wasInRange = inRange;
        inRange = false;
        HasValidSample = false;
        LastDistance = float.PositiveInfinity;
        if (wasInRange) OnExitRange?.Invoke();
        OnProximityValue?.Invoke(0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, triggerDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(transform.position, triggerDistance + exitBuffer);
    }
}
