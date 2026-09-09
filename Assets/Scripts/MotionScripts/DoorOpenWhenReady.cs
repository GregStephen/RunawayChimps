using UnityEngine;

public class DoorOpenWhenReady : MonoBehaviour
{
    [Header("Streaming")]
    public string requiredScene = "Level1";

    [Header("Behavior")]
    [Tooltip("Legacy field retained for serialization; unlocking now requires activation.")]
    public bool unlockOnPreload = false;

    [Tooltip("Open the door when scene is ACTIVATED (fully done)")]
    public bool openOnActivated = true;

    [Header("Door Lock")]
    public Collider blockingCollider; // collider that blocks walking through
    public bool startLocked = true;

    [Header("Optional Animation")]
    public Animator animator;
    public string openTriggerName = "Open";

    [Header("Optional Simple Move (if no animator)")]
    public Transform doorTransform;
    public Vector3 openLocalOffset = new Vector3(0, 2f, 0);
    public float openSpeed = 3f;

    private bool _open;
    private Vector3 _closedLocalPos;
    private Vector3 _openLocalPos;

    private void Awake()
    {
        if (doorTransform == null) doorTransform = transform;
        _closedLocalPos = doorTransform.localPosition;
        _openLocalPos = _closedLocalPos + openLocalOffset;

        SetLocked(startLocked);
    }

    private void OnEnable()
    {
        if (LevelStreamService.I != null)
        {
            LevelStreamService.I.OnScenePreloaded += HandlePreloaded;
            LevelStreamService.I.OnSceneActivated += HandleActivated;

            // Catch up if we entered play mid-state
            if (LevelStreamService.I.IsPreloaded(requiredScene))
                HandlePreloaded(requiredScene);

            if (LevelStreamService.I.IsActivated(requiredScene))
                HandleActivated(requiredScene);
        }
        else
        {
            Debug.LogWarning("[DoorOpenWhenReady] LevelStreamService.I is null on enable.");
        }
    }

    private void OnDisable()
    {
        if (LevelStreamService.I != null)
        {
            LevelStreamService.I.OnScenePreloaded -= HandlePreloaded;
            LevelStreamService.I.OnSceneActivated -= HandleActivated;
        }
    }

    private void HandlePreloaded(string sceneName)
    {
        if (sceneName != requiredScene) return;

        // Held preloads have no usable colliders. Unlock only after activation.
    }

    private void HandleActivated(string sceneName)
    {
        if (sceneName != requiredScene) return;

        if (openOnActivated)
            Open();
    }

    private void SetLocked(bool locked)
    {
        if (blockingCollider != null)
            blockingCollider.enabled = locked;
    }

    public void Open()
    {
        if (_open) return;
        _open = true;

        // Ensure unlocked when opening
        SetLocked(false);

        if (animator != null)
            animator.SetTrigger(openTriggerName);
    }

    // Optional: let the button call this directly
    public void ActivateAndOpen()
    {
        if (LevelStreamService.I == null)
        {
            Debug.LogError("[DoorOpenWhenReady] No LevelStreamService.I");
            return;
        }

        LevelStreamService.I.Activate(requiredScene);
        // Door will open when activation completes via event.
    }

    private void Update()
    {
        if (_open && animator == null && doorTransform != null)
        {
            doorTransform.localPosition =
                Vector3.Lerp(doorTransform.localPosition, _openLocalPos, Time.deltaTime * openSpeed);
        }
    }
}
