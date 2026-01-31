using UnityEngine;

public class KeyboardKey : MonoBehaviour
{
    public enum Key
    {
        Up, Down, Left, Right, Enter, Backspace,
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Num0, Num1, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9,
        Space, Dash, Underscore
    }

    [Header("Key Identity")]
    public Key keyValue;

    [Header("Press Detection")]
    [Tooltip("Only colliders on these layers can press (your fingertip layer).")]
    public LayerMask allowedPressLayers = ~0;

    [Tooltip("Finger must be moving down at least this fast (meters/sec) to count as a press.")]
    public float minDownSpeed = 0.10f;

    [Tooltip("How far the finger must push into the key trigger (meters) before it counts as pressed.")]
    public float requiredPushDepth = 0.006f;

    [Tooltip("Debounce so one touch doesn't press repeatedly.")]
    public float pressCooldown = 0.12f;

    [Header("Key Travel Visual")]
    [Tooltip("How far the key moves down visually when pressed (meters).")]
    public float keyTravel = 0.0025f;

    [Tooltip("How quickly the key moves toward the target position.")]
    public float travelLerpSpeed = 25f;

    private KeyboardController keyboard;
    private Collider keyTrigger;
    private Vector3 startLocalPos;

    // Current finger pressing this key (we track one finger at a time for stability)
    private Collider finger;
    private Vector3 fingerEnterPos;   // world position when finger entered
    private Vector3 fingerPrevPos;    // last frame finger position (for velocity)
    private float lastPressTime = -999f;
    private bool hasFiredThisTouch;

    private void Awake()
    {
        keyboard = GetComponentInParent<KeyboardController>();
        keyTrigger = GetComponent<Collider>();
        startLocalPos = transform.localPosition;

        if (keyTrigger == null)
            Debug.LogWarning($"[KeyboardKey] '{name}' has no Collider. Add a BoxCollider and set IsTrigger=true.");

        if (keyTrigger != null && !keyTrigger.isTrigger)
            Debug.LogWarning($"[KeyboardKey] '{name}' collider is not a trigger. Set IsTrigger=true.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & allowedPressLayers.value) == 0)
            return;

        // If a finger is already interacting, ignore additional overlaps.
        if (finger != null)
            return;

        finger = other;
        fingerEnterPos = other.transform.position;
        fingerPrevPos = fingerEnterPos;
        hasFiredThisTouch = false;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != finger) return;

        finger = null;
        hasFiredThisTouch = false;
    }

    private void Update()
    {
        // Default: return key to start position
        float targetDepth01 = 0f;

        if (finger != null)
        {
            // Compute downward speed (world)
            Vector3 fingerPos = finger.transform.position;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float downSpeed = (fingerPrevPos.y - fingerPos.y) / dt; // positive when moving down
            fingerPrevPos = fingerPos;

            // How far has the finger moved downward since entering?
            float pushedDown = (fingerEnterPos.y - fingerPos.y); // positive if finger went down

            // Convert finger push depth into a 0..1 for key travel animation
            targetDepth01 = Mathf.Clamp01(pushedDown / requiredPushDepth);

            bool cooldownOk = (Time.time - lastPressTime) >= pressCooldown;
            bool movingDownEnough = downSpeed >= minDownSpeed;
            bool deepEnough = pushedDown >= requiredPushDepth;

            if (!hasFiredThisTouch && cooldownOk && movingDownEnough && deepEnough)
            {
                hasFiredThisTouch = true;
                lastPressTime = Time.time;

                if (keyboard != null)
                    keyboard.Press(keyValue);
                else
                    Debug.LogWarning($"[KeyboardKey] '{name}' has no KeyboardController in parents.");
            }
        }

        // Visual key travel (smooth)
        Vector3 targetLocalPos = startLocalPos + Vector3.down * (keyTravel * targetDepth01);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, travelLerpSpeed * Time.deltaTime);
    }
}
