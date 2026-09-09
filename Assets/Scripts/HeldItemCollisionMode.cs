using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class HeldItemCollisionMode : MonoBehaviour
{
    public string heldLayerName = "HeldItem";

    readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();

    XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        grab.firstSelectEntered.AddListener(OnGrab);
        grab.lastSelectExited.AddListener(OnRelease);
        if (grab.isSelected) ApplyHeldLayer();
    }

    void OnDisable()
    {
        if (grab != null)
        {
            grab.firstSelectEntered.RemoveListener(OnGrab);
            grab.lastSelectExited.RemoveListener(OnRelease);
        }
        RestoreLayers();
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        ApplyHeldLayer();
    }

    void ApplyHeldLayer()
    {
        if (originalLayers.Count != 0) return;
        int heldLayer = LayerMask.NameToLayer(heldLayerName);
        if (heldLayer == -1) return;

        // Each child can have a different collision layer. Snapshot only once,
        // and keep the held layer until the final selecting hand releases.
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            originalLayers.Add(child, child.gameObject.layer);
            child.gameObject.layer = heldLayer;
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        RestoreLayers();
    }

    void RestoreLayers()
    {
        foreach (var entry in originalLayers)
            if (entry.Key != null) entry.Key.gameObject.layer = entry.Value;
        originalLayers.Clear();
    }
}
