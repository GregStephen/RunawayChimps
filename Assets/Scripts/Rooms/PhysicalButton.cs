using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

public class PhysicalButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform buttonVisual;

    [Header("Movement")]
    public float pressDepth = 0.015f;
    public float returnSpeed = 12f;

    [Header("Press Behavior")]
    [Tooltip("Prevents spamming when hand stays inside trigger.")]
    public float cooldown = 0.25f;

    [Header("Audio")]
    [Tooltip("Optional. If null, will try GetComponent<AudioSource>() on this object.")]
    public AudioSource audioSource;
    public AudioClip clickClip;
    [Range(0f, 1f)] public float clickVolume = 1f;

    [Header("Haptics (OpenXR/Quest)")]
    public bool hapticsEnabled = true;
    [Range(0f, 1f)] public float hapticAmplitude = 0.5f;
    [Tooltip("Seconds. Keep short for a 'click'.")]
    public float hapticDuration = 0.06f;
    [Tooltip("If true, pulses BOTH controllers. Simple + reliable.")]
    public bool pulseBothHands = true;

    [Header("Events")]
    public UnityEvent OnPressed;

    private Vector3 initialLocalPos;
    private bool isPressed;
    private float nextPressTime;
    private Collider pressingCollider;

    private void Awake()
    {
        if (buttonVisual == null)
        {
            Debug.LogError("PhysicalButton: ButtonVisual not assigned.");
            enabled = false;
            return;
        }

        initialLocalPos = buttonVisual.localPosition;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        // Smoothly return to original position
        if (!isPressed)
        {
            buttonVisual.localPosition = Vector3.Lerp(
                buttonVisual.localPosition,
                initialLocalPos,
                Time.deltaTime * returnSpeed
            );
        }
    }



    private void OnTriggerEnter(Collider other)
    {
        // Already pressed and still held by someone
        if (pressingCollider != null) return;

        // cooldown protection
        if (Time.time < nextPressTime) return;

        // (Optional later) filter by layer/tag here

        pressingCollider = other;
        Press();
    }

    private void OnTriggerExit(Collider other)
    {
        // Only release if the SAME collider that pressed is leaving
        if (other != pressingCollider) return;

        pressingCollider = null;
        isPressed = false;
    }

    private void Press()
    {
        nextPressTime = Time.time + cooldown;
        isPressed = true;

        // Move button down
        buttonVisual.localPosition = initialLocalPos - new Vector3(0, pressDepth, 0);

        // Click sound
        PlayClick();

        // Haptics
        if (hapticsEnabled)
            PulseHaptics();

        // Gameplay event
        OnPressed?.Invoke();
    }

    private void PlayClick()
    {
        if (audioSource == null || clickClip == null) return;

        audioSource.PlayOneShot(clickClip, clickVolume);
    }

    private void PulseHaptics()
    {
        if (pulseBothHands)
        {
            TryPulse(XRNode.LeftHand);
            TryPulse(XRNode.RightHand);
            return;
        }

        // If you later want "pulse the hand that pressed", we can add hand detection.
        // For now, default to both or choose one.
        TryPulse(XRNode.RightHand);
    }

    private void TryPulse(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;

        if (device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
        {
            // channel 0 is standard for controllers
            device.SendHapticImpulse(0, hapticAmplitude, hapticDuration);
        }
    }
}
