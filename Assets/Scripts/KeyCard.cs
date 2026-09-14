using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DisallowMultipleComponent]
public class KeyCard : MonoBehaviour
{
    [Header("Credential")]
    [Tooltip("Assign the card's color/symbol identity explicitly. Auto is retained only for legacy/name-authored compatibility.")]
    [SerializeField] private KeycardCredential credential = KeycardCredential.Auto;

    private bool isInserted = false;
    private XRGrabInteractable grab;

    public bool IsInserted => isInserted;
    public bool WasHeldByLocalPlayer { get; private set; }
    public KeycardCredential Credential => credential;

    private void OnEnable()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.AddListener(Selected);
    }

    private void Selected(SelectEnterEventArgs args)
    {
        if (args.interactorObject.transform.GetComponentInParent<LocalRigMarker>() != null)
            WasHeldByLocalPlayer = true;
    }

    private void OnDisable()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(Selected);
    }

    /// <summary>
    /// Runs the normal keycard acceptance lifecycle through the supplied KeyBox.
    /// Reader-driven submissions use this so the existing local-player, scene,
    /// duplicate-card, completion and travel guards remain authoritative.
    /// </summary>
    public bool TryInsertInto(KeyBox box)
    {
        if (isInserted || box == null || !box.TryAddKey(this))
            return false;

        isInserted = true;

        // Accepted objective cards keep the existing consumed-card behavior.
        Destroy(gameObject);
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isInserted)
            return;

        KeyBox box = other.GetComponent<KeyBox>();
        if (box == null)
            return;

        // The Level 1 completion KeyBox is reader-driven: a card must be
        // presented to its matching color/symbol reader rather than merely
        // touching the legacy KeyBox trigger. Keep direct insertion available
        // for older non-travel KeyBox uses.
        if (!box.travelToLevelTwoOnComplete)
            TryInsertInto(box);
    }
}
