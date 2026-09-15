using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class HeldItemCollisionMode : MonoBehaviour
{
    public string heldLayerName = "HeldItem";

    readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
    readonly List<IgnoredCollisionPair> ignoredLocalRigPairs = new List<IgnoredCollisionPair>();

    XRGrabInteractable grab;
    Coroutine restoreRigCollisionsRoutine;

    struct IgnoredCollisionPair
    {
        public Collider Item;
        public Collider Rig;

        public IgnoredCollisionPair(Collider item, Collider rig)
        {
            Item = item;
            Rig = rig;
        }
    }

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        grab.firstSelectEntered.AddListener(OnGrab);
        grab.lastSelectExited.AddListener(OnRelease);
        if (grab.isSelected)
        {
            ApplyHeldLayer();
            IgnoreLocalRigCollisions();
        }
    }

    void OnDisable()
    {
        if (grab != null)
        {
            grab.firstSelectEntered.RemoveListener(OnGrab);
            grab.lastSelectExited.RemoveListener(OnRelease);
        }

        if (restoreRigCollisionsRoutine != null)
        {
            StopCoroutine(restoreRigCollisionsRoutine);
            restoreRigCollisionsRoutine = null;
        }

        RestoreLocalRigCollisions();
        RestoreLayers();
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (restoreRigCollisionsRoutine != null)
        {
            StopCoroutine(restoreRigCollisionsRoutine);
            restoreRigCollisionsRoutine = null;
        }

        ApplyHeldLayer();
        IgnoreLocalRigCollisions();
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

    /// <summary>
    /// A physics-driven XRGrabInteractable must still collide with the world while held, but
    /// it must not collide with the same local Gorilla rig that is driving its hand pose.
    /// Otherwise a held prop can become wedged between the hand/body and a wall and PhysX
    /// resolves that overlap by pushing the player. Ignore only local-rig collider pairs for
    /// the duration of the grab; environment, reader-trigger, and remote-world collisions
    /// remain available.
    /// </summary>
    void IgnoreLocalRigCollisions()
    {
        if (ignoredLocalRigPairs.Count != 0)
            return;

        GorillaLocomotion.Player player = GorillaLocomotion.Player.Instance;
        XROrigin origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
        if (origin == null)
            return;

        Collider[] itemColliders = GetComponentsInChildren<Collider>(true);
        Collider[] rigColliders = origin.GetComponentsInChildren<Collider>(true);

        foreach (Collider itemCollider in itemColliders)
        {
            if (itemCollider == null || !itemCollider.enabled)
                continue;

            foreach (Collider rigCollider in rigColliders)
            {
                if (rigCollider == null || !rigCollider.enabled || rigCollider == itemCollider)
                    continue;

                // Be defensive if a future held object is temporarily parented beneath the
                // XR rig: do not treat its own descendants as player colliders.
                if (rigCollider.transform.IsChildOf(transform))
                    continue;

                Physics.IgnoreCollision(itemCollider, rigCollider, true);
                ignoredLocalRigPairs.Add(new IgnoredCollisionPair(itemCollider, rigCollider));
            }
        }
    }

    void RestoreLocalRigCollisions()
    {
        foreach (IgnoredCollisionPair pair in ignoredLocalRigPairs)
        {
            if (pair.Item != null && pair.Rig != null)
                Physics.IgnoreCollision(pair.Item, pair.Rig, false);
        }

        ignoredLocalRigPairs.Clear();
    }

    void OnRelease(SelectExitEventArgs args)
    {
        RestoreLayers();

        if (restoreRigCollisionsRoutine != null)
            StopCoroutine(restoreRigCollisionsRoutine);
        restoreRigCollisionsRoutine = StartCoroutine(RestoreRigCollisionsWhenSeparated());
    }

    IEnumerator RestoreRigCollisionsWhenSeparated()
    {
        // The card/prop can still be intersecting the hand, head or body on the exact frame
        // the grab ends. Re-enabling that collision immediately recreates the same violent
        // depenetration impulse we avoid while held. Keep only the local-rig pairs ignored
        // until the released prop has physically separated; world collisions are unaffected.
        while (isActiveAndEnabled && grab != null && !grab.isSelected && HasLocalRigOverlap())
            yield return new WaitForFixedUpdate();

        if (grab == null || !grab.isSelected)
            RestoreLocalRigCollisions();

        restoreRigCollisionsRoutine = null;
    }

    bool HasLocalRigOverlap()
    {
        foreach (IgnoredCollisionPair pair in ignoredLocalRigPairs)
        {
            Collider item = pair.Item;
            Collider rig = pair.Rig;
            if (item == null || rig == null || !item.enabled || !rig.enabled ||
                !item.gameObject.activeInHierarchy || !rig.gameObject.activeInHierarchy)
                continue;

            if (Physics.ComputePenetration(
                    item, item.transform.position, item.transform.rotation,
                    rig, rig.transform.position, rig.transform.rotation,
                    out _, out _))
                return true;
        }

        return false;
    }

    void RestoreLayers()
    {
        foreach (var entry in originalLayers)
            if (entry.Key != null) entry.Key.gameObject.layer = entry.Value;
        originalLayers.Clear();
    }
}
