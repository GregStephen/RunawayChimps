using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
[DisallowMultipleComponent]
public class HeldItemCollisionMode : MonoBehaviour
{
    public string heldLayerName = "HeldItem";

    readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
    readonly List<IgnoredCollisionPair> ignoredLocalRigPairs = new List<IgnoredCollisionPair>();
    readonly List<IgnoredCollisionPair> localRigContactPairs = new List<IgnoredCollisionPair>();
    readonly List<Collider> localItemColliders = new List<Collider>();

    XRGrabInteractable grab;
    Coroutine restoreRigCollisionsRoutine;
    bool restoreSafetyOnEnable;

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
        if (grab.isSelected || restoreSafetyOnEnable)
        {
            restoreSafetyOnEnable = false;
            ApplyHeldLayer();
            IgnoreLocalRigCollisions();
            if (!grab.isSelected)
                restoreRigCollisionsRoutine = StartCoroutine(RestoreRigCollisionsWhenSeparated());
        }
    }

    void OnDisable()
    {
        // Deactivating a recovering/held object must not discard its need for
        // separation when it is reactivated beside the local rig.
        restoreSafetyOnEnable |= originalLayers.Count > 0 || ignoredLocalRigPairs.Count > 0;
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

        // An inactive hierarchy cannot collide. If only this behaviour was
        // disabled, keep its safety state until re-enable/destruction rather than
        // restoring a solid card inside the player while XRI can still move it.
        if (!gameObject.activeInHierarchy)
        {
            RestoreLocalRigCollisions();
            RestoreLayers();
        }
    }

    void OnDestroy()
    {
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

    internal int GetUnheldLayer(Transform target)
    {
        return originalLayers.TryGetValue(target, out int original) ? original : target.gameObject.layer;
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
            // Keep trigger-only interaction sensors on their authored layer. HeldItem
            // excludes hand/fingertip layers, which would hide these sensors from XRI.
            if (child.GetComponentInParent<XRGrabInteractable>() != grab)
                continue;
            Collider[] childColliders = child.GetComponents<Collider>();
            bool triggerOnly = childColliders.Length > 0;
            foreach (Collider childCollider in childColliders)
                triggerOnly &= childCollider.isTrigger;
            if (triggerOnly)
                continue;

            originalLayers.Add(child, child.gameObject.layer);
            child.gameObject.layer = heldLayer;
        }
    }

    /// <summary>
    /// A physics-driven XRGrabInteractable must still collide with the world while held, but
    /// it must not collide with the same local Gorilla rig that is driving its hand pose.
    /// Otherwise a held prop can become wedged between the hand/body and a wall and PhysX
    /// resolves that overlap by pushing the player. Ignore only solid-to-solid local-rig
    /// pairs; item grab affordances and XR detection triggers must remain active during
    /// holding and delayed release recovery. Environment and reader interactions stay intact.
    /// </summary>
    void IgnoreLocalRigCollisions()
    {
        // Rebuild separation contacts on every grab, including an immediate
        // re-grab while old owned ignores are still active. Never undo pre-existing ignores.
        localRigContactPairs.Clear();
        localItemColliders.Clear();

        GorillaLocomotion.Player player = GorillaLocomotion.Player.Instance;
        XROrigin origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
        if (origin == null)
            return;

        Collider[] itemColliders = GetComponentsInChildren<Collider>(true);
        Collider[] rigColliders = origin.GetComponentsInChildren<Collider>(true);

        foreach (Collider itemCollider in itemColliders)
        {
            if (itemCollider == null || !itemCollider.enabled || itemCollider.isTrigger ||
                itemCollider.GetComponentInParent<XRGrabInteractable>() != grab)
                continue;

            localItemColliders.Add(itemCollider);
            foreach (Collider rigCollider in rigColliders)
            {
                if (rigCollider == null || !rigCollider.enabled || rigCollider.isTrigger || rigCollider == itemCollider)
                    continue;

                // Be defensive if a future held object is temporarily parented beneath the
                // XR rig: do not treat its own descendants as player colliders.
                if (rigCollider.transform.IsChildOf(transform) ||
                    rigCollider.GetComponentInParent<XRGrabInteractable>() != null)
                    continue;

                localRigContactPairs.Add(new IgnoredCollisionPair(itemCollider, rigCollider));

                // Only restore pairs this component actually changed. Do not undo
                // an ignore relationship that another system already owns.
                if (Physics.GetIgnoreCollision(itemCollider, rigCollider))
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
        localRigContactPairs.Clear();
        localItemColliders.Clear();
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (!isActiveAndEnabled)
            return; // OnDisable owns cleanup; never start a coroutine on an inactive object.
        // Pair ignores do not affect Gorilla's casts. Keep solid colliders on
        // HeldItem until separation as well; trigger-only grab sensors never moved.

        if (restoreRigCollisionsRoutine != null)
            StopCoroutine(restoreRigCollisionsRoutine);
        restoreRigCollisionsRoutine = StartCoroutine(RestoreRigCollisionsWhenSeparated());
    }

    IEnumerator RestoreRigCollisionsWhenSeparated()
    {
        // Defer until XRI has completed final selection teardown and the next
        // physics step. Never restore state from a half-completed exit callback.
        yield return new WaitForFixedUpdate();
        // The card/prop can still be intersecting the hand, head or body on the exact frame
        // the grab ends. Re-enabling that collision immediately recreates the same violent
        // depenetration impulse we avoid while held. Keep only the local-rig pairs ignored
        // until the released prop has physically separated; world collisions are unaffected.
        while (isActiveAndEnabled && grab != null && !grab.isSelected && HasLocalRigOverlap())
            yield return new WaitForFixedUpdate();

        if (grab == null || !grab.isSelected)
        {
            RestoreLocalRigCollisions();
            RestoreLayers();
        }

        restoreRigCollisionsRoutine = null;
    }

    bool HasLocalRigOverlap()
    {
        foreach (IgnoredCollisionPair pair in localRigContactPairs)
        {
            Collider item = pair.Item;
            Collider rig = pair.Rig;
            if (item == null || rig == null || !item.enabled || !rig.enabled ||
                item.isTrigger || rig.isTrigger ||
                !item.gameObject.activeInHierarchy || !rig.gameObject.activeInHierarchy)
                continue;

            if (Physics.ComputePenetration(
                    item, item.transform.position, item.transform.rotation,
                    rig, rig.transform.position, rig.transform.rotation,
                    out _, out _))
                return true;
        }

        GorillaLocomotion.Player player = GorillaLocomotion.Player.Instance;
        if (player != null)
        {
            float handRadius = Mathf.Max(0.05f, player.minimumRaycastDistance) + 0.005f;
            foreach (Collider item in localItemColliders)
            {
                if (item == null || item.isTrigger || !item.enabled || !item.gameObject.activeInHierarchy)
                    continue;
                if (HandStillNear(item, player.leftHandFollower, handRadius) ||
                    HandStillNear(item, player.rightHandFollower, handRadius))
                    return true;
            }
        }
        return false;
    }

    static bool HandStillNear(Collider item, Transform hand, float radius)
    {
        if (hand == null)
            return false;
        // Physics.ClosestPoint supports these shapes and accepts a current pose,
        // avoiding a global SyncTransforms just to check virtual-hand clearance.
        if (!(item is BoxCollider) && !(item is SphereCollider) && !(item is CapsuleCollider) &&
            !(item is MeshCollider mesh && mesh.convex))
            return false;
        Vector3 closest = Physics.ClosestPoint(hand.position, item, item.transform.position, item.transform.rotation);
        return (closest - hand.position).sqrMagnitude <= radius * radius;
    }

    void RestoreLayers()
    {
        foreach (var entry in originalLayers)
            if (entry.Key != null) entry.Key.gameObject.layer = entry.Value;
        originalLayers.Clear();
    }
}
