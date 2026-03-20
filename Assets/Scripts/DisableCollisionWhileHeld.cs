using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class HeldItemCollisionMode : MonoBehaviour
{
    public string heldLayerName = "HeldItem";

    int originalLayer;

    XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        originalLayer = gameObject.layer;
        int heldLayer = LayerMask.NameToLayer(heldLayerName);
        if (heldLayer != -1)
            SetLayerRecursively(transform, heldLayer);
    }

    void OnRelease(SelectExitEventArgs args)
    {
        SetLayerRecursively(transform, originalLayer);
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }
}
