using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class KeyCard : MonoBehaviour
{
    private bool isInserted = false;
    private XRGrabInteractable grab;
    public bool IsInserted => isInserted;
    public bool WasHeldByLocalPlayer { get; private set; }

    private void OnEnable()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectEntered.AddListener(Selected);
    }

    private void Selected(SelectEnterEventArgs args)
    {
        if (args.interactorObject.transform.GetComponentInParent<LocalRigMarker>() != null)
            WasHeldByLocalPlayer = true;
    }

    private void OnDisable()
    {
        if (grab != null) grab.selectEntered.RemoveListener(Selected);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isInserted) return;

        KeyBox box = other.GetComponent<KeyBox>();
        if (box != null && box.TryAddKey(this))
        {
            isInserted = true;

            // Remove the card (destroy locally)
            Destroy(gameObject);
        }
    }
}
