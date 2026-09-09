using UnityEngine;
using UnityEngine.Events;
using RunawayChimps.Zones;

public class ProximityReactor : MonoBehaviour
{
    [Header("Zone (optional but recommended)")]
    [Tooltip("If set, ProximityManager can ignore this reactor when the local player is in another zone.")]
    public ZoneId ZoneId = ZoneId.None;

    [Header("Settings")]
    public float triggerDistance = 3f;

    [Tooltip("True when this reactor is considered to be inside vents (used by ProximityManager).")]
    public bool IsInVents = false;

    [Tooltip("Extra distance beyond triggerDistance required to fire exit (prevents flicker).")]
    public float exitBuffer = 0.5f;

    [Header("Events")]
    public UnityEvent OnEnterRange;
    public UnityEvent OnExitRange;
    public UnityEvent<float> OnProximityValue; // 0–1 normalized proximity

    private bool inRange;
    private bool registered;

    private void OnEnable()
    {
        TryRegister();
    }
    private void Update()
    {
        if (!registered)
            TryRegister();
    }


    private void OnDisable()
    {
        Unregister();
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void TryRegister()
    {
        if (registered) return;

        var mgr = ProximityManager.Instance;
        if (mgr == null)
        {
            // No spam logs; it will register next enable / when manager exists.
            return;
        }

        mgr.Register(this);
        registered = true;
    }

    private void Unregister()
    {
        if (!registered) return;

        var mgr = ProximityManager.Instance;
        if (mgr != null)
            mgr.Unregister(this);

        registered = false;
    }

    /// <summary>
    /// Called by ProximityManager on its interval with distance and local player transform.
    /// </summary>
    public void UpdateProximity(float distance, Transform playerTransform)
    {

        bool nowInRange = distance <= triggerDistance;

        if (nowInRange && !inRange)
        {
            inRange = true;
            OnEnterRange?.Invoke();
        }
        else if (!nowInRange && inRange && distance > triggerDistance + exitBuffer)
        {
            inRange = false;
            OnExitRange?.Invoke();
        }

        float normalized = (triggerDistance <= 0.0001f)
            ? 0f
            : Mathf.Clamp01(1f - (distance / triggerDistance));
#if UNITY_EDITOR
        Debug.Log($"[ProximityReactor] {name} dist={distance:F2} norm={normalized:F2} registered={registered}");
#endif

        OnProximityValue?.Invoke(normalized);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, triggerDistance + exitBuffer);
    }
}
