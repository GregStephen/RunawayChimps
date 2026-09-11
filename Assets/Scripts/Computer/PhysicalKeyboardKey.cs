using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(Collider))]
public class PhysicalKeyboardKey : MonoBehaviour
{
    [Header("Visual (shared)")]
    public KeyboardPressVisual keyboardVisual;
    public Transform visualAnchor; // empty child transform at key top (local space)
    public Vector3 localPressDirection = new Vector3(0, 0, -1);
    public float pressDepth = 0.001f;

    [Header("Press Behavior")]
    public float cooldown = 0.08f;

    [Header("Filtering")]
    public LayerMask pressLayers;
    public string requiredTag = "HandTag";
    public bool requireTag = true;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip clickClip;
    [Range(0f, 1f)] public float clickVolume = 1f;

    [Header("Haptics")]
    public bool hapticsEnabled = true;
    [Range(0f, 1f)] public float hapticAmplitude = 0.25f;
    public float hapticDuration = 0.04f;

    private float _nextPressTime;
    private Collider _pressingCollider;

    private KeyboardKey _key;

    private void Awake()
    {
        _key = GetComponent<KeyboardKey>();

        if (keyboardVisual == null)
            keyboardVisual = GetComponentInParent<KeyboardPressVisual>();

        if (visualAnchor == null)
            visualAnchor = transform; // fallback (works if key transform is placed at top)

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>(); // optional

        // Ensure this collider is trigger
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
            Debug.LogWarning($"[PhysicalKeyboardKey] '{name}' collider is not Trigger. Set it to Trigger.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<LocalRigMarker>() == null) return;
        if (_pressingCollider != null) return;
        if (Time.time < _nextPressTime) return;

        if (requireTag && !other.CompareTag(requiredTag)) return;
        if (((1 << other.gameObject.layer) & pressLayers) == 0) return;

        _pressingCollider = other;
        DoPress();
    }

    private void Update()
    {
        if (_pressingCollider == null || !_pressingCollider.enabled ||
            !_pressingCollider.gameObject.activeInHierarchy) _pressingCollider = null;
    }

    private void OnDisable() => _pressingCollider = null;

    private void OnTriggerExit(Collider other)
    {
        if (other != _pressingCollider) return;
        _pressingCollider = null;
    }

    private void DoPress()
    {
        _nextPressTime = Time.time + cooldown;
        Debug.Log($"[PhysicalKeyboardKey] Pressed {name}. keyboardVisual={(keyboardVisual ? keyboardVisual.name : "NULL")} anchor={(visualAnchor ? visualAnchor.name : "NULL")}");

        // visual: shared press cap
        if (keyboardVisual != null)
            keyboardVisual.PressAt(visualAnchor, localPressDirection, pressDepth);

        // sound
        if (audioSource != null && clickClip != null)
            audioSource.PlayOneShot(clickClip, clickVolume);

        // haptics
        if (hapticsEnabled)
        {
            TryPulse(XRNode.LeftHand);
            TryPulse(XRNode.RightHand);
        }

        // gameplay
        _key?.OnKeyPressed();
    }

    private void TryPulse(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;

        if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            device.SendHapticImpulse(0, hapticAmplitude, hapticDuration);
    }
}
