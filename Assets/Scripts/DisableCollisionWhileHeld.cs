using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class DisableCollisionWhileHeld : MonoBehaviour
{
    private XRGrabInteractable grab;
    private Collider col;
    private Rigidbody rb;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (rb != null)
            rb.isKinematic = true;      // no physics forces while held

        if (col != null)
            col.isTrigger = true;       // no physical collisions while held
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (rb != null)
            rb.isKinematic = false;

        if (col != null)
            col.isTrigger = false;
    }
}
