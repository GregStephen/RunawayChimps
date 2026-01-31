using Photon.VR;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Renderer))]
public class ProximityReactor : MonoBehaviour
{
    [Header("Settings")]
    public float triggerDistance = 3f;
    public float exitBuffer = 0.5f;

    [Header("Events")]
    public UnityEvent OnEnterRange;
    public UnityEvent OnExitRange;
    public UnityEvent<float> OnProximityValue; // 0–1 normalized proximity

    private bool inRange;
    private bool registered;

    private void Awake() => TryRegister();
    private void Start() => TryRegister();
    private void OnEnable() => TryRegister();

    private void OnDisable()
    {
        if (registered && ProximityManager.Instance != null)
        {
            ProximityManager.Instance.Unregister(this);
            registered = false;
        }
    }

    private void TryRegister()
    {
        if (registered) return;

        if (ProximityManager.Instance != null)
        {
            ProximityManager.Instance.Register(this);
            registered = true;
            Debug.Log($"{name} successfully registered with ProximityManager.");
        }
        else
        {
            Debug.LogWarning($"{name} cannot register: ProximityManager.Instance is null. Will retry.");
        }
    }

    /// <summary>
    /// Called by ProximityManager each frame with distance and player transform.
    /// </summary>
    public void UpdateProximity(float distance, Transform playerTransform)
    {
        bool nowInRange = distance <= triggerDistance;

        if (nowInRange && !inRange)
        {
            inRange = true;
            OnEnterRange.Invoke();
        }
        else if (!nowInRange && inRange && distance > triggerDistance + exitBuffer)
        {
            inRange = false;
            OnExitRange.Invoke();
        }

        float normalized = Mathf.Clamp01(1f - (distance / triggerDistance));
        OnProximityValue.Invoke(normalized);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
    }
}
