using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Gorilla hands use casts rather than physical hand bodies. Loose objective cards
/// must receive a small contact impulse instead of becoming locomotion anchors.
/// Other props, walls, floors and kinematic cards retain their existing behavior.
/// </summary>
public static class KeycardHandPhysics
{
    const int QueryCapacity = 32;
    const float MaximumHandStep = 0.5f;
    const float MaximumHandSpeed = 2f;
    const float MaximumVelocityChange = 1.5f;
    static readonly RaycastHit[] hits = new RaycastHit[QueryCapacity];
    static readonly Collider[] overlaps = new Collider[QueryCapacity];
    static readonly HashSet<Rigidbody> pushedBodies = new HashSet<Rigidbody>();
    static readonly HashSet<Collider> initialContacts = new HashSet<Collider>();
    static float pushedPhysicsTime = float.NaN;

    public static bool SphereCastEnvironment(Vector3 origin, float radius, Vector3 direction,
        out RaycastHit hit, float distance, int layers)
    {
        hit = default;
        if (!TryNormalizeDirection(direction, out direction))
            return false;

        // Keep the common wall/floor path a single non-allocating query. If its
        // closest hit is a loose card, also find the actual support behind it.
        if (!Physics.SphereCast(origin, radius, direction, out hit, distance, layers, QueryTriggerInteraction.Ignore))
            return false;
        if (!TryGetLooseCardBody(hit.collider, out _))
            return true;

        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance, layers, QueryTriggerInteraction.Ignore);
        RaycastHit[] candidates = hits;
        if (count == hits.Length)
        {
            // A full NonAlloc buffer is unordered and might omit the real wall.
            // Allocate only for this uncommon crowded case; never drop geometry.
            candidates = Physics.SphereCastAll(origin, radius, direction, distance, layers, QueryTriggerInteraction.Ignore);
            count = candidates.Length;
        }
        return NearestEnvironment(candidates, count, out hit, true);
    }

    public static bool RaycastEnvironment(Vector3 origin, Vector3 direction,
        out RaycastHit hit, float distance, int layers)
    {
        hit = default;
        if (!TryNormalizeDirection(direction, out direction))
            return false;

        if (!Physics.Raycast(origin, direction, out hit, distance, layers, QueryTriggerInteraction.Ignore))
            return false;
        if (!TryGetLooseCardBody(hit.collider, out _))
            return true;

        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, layers, QueryTriggerInteraction.Ignore);
        RaycastHit[] candidates = hits;
        if (count == hits.Length)
        {
            candidates = Physics.RaycastAll(origin, direction, distance, layers, QueryTriggerInteraction.Ignore);
            count = candidates.Length;
        }
        return NearestEnvironment(candidates, count, out hit, false);
    }

    static bool TryNormalizeDirection(Vector3 direction, out Vector3 normalized)
    {
        normalized = default;
        // Unity 2022.3's buffered SphereCast passes direction to native physics
        // without normalizing it. Gorilla supplies displacement vectors, not
        // unit directions. Normalize once for all query paths, keeping the
        // caller's separate cast distance (including its collision skin).
        // Scale first so tiny finite deltas are not rounded to Vector3.zero by
        // Vector3.normalized, and large finite components cannot overflow.
        float scale = Mathf.Max(Mathf.Abs(direction.x),
            Mathf.Max(Mathf.Abs(direction.y), Mathf.Abs(direction.z)));
        if (!(scale > 0f) || float.IsInfinity(scale))
            return false;
        Vector3 scaled = direction / scale;
        float magnitude = scaled.magnitude;
        if (!(magnitude > 0f) || float.IsInfinity(magnitude))
            return false;
        normalized = scaled / magnitude;
        return true;
    }

    static bool NearestEnvironment(RaycastHit[] candidates, int count, out RaycastHit hit, bool sphereCast)
    {
        hit = default;
        float nearest = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            RaycastHit candidate = candidates[index];
            // Unlike the single SphereCast above, the multi-hit variants report
            // initial overlaps with distance 0 and point Vector3.zero. Match the
            // single-cast policy rather than feeding that world-origin sentinel
            // into Gorilla's hit.point + normal * handRadius position solver.
            if (sphereCast && candidate.distance <= 0f)
                continue;
            if (candidate.collider == null || candidate.distance >= nearest ||
                TryGetLooseCardBody(candidate.collider, out _))
                continue;
            nearest = candidate.distance;
            hit = candidate;
        }
        return hit.collider != null;
    }

    static bool TryGetLooseCardBody(Collider collider, out Rigidbody body)
    {
        body = collider != null ? collider.attachedRigidbody : null;
        if (collider == null || collider.isTrigger || !collider.enabled ||
            !collider.gameObject.activeInHierarchy || body == null ||
            body.isKinematic || !body.detectCollisions || body.constraints != RigidbodyConstraints.None)
            return false;

        KeyCard card = body.GetComponent<KeyCard>();
        if (card == null || !card.isActiveAndEnabled || card.IsInserted || card.PhysicalCollider != collider)
            return false;
        XRGrabInteractable grab = body.GetComponent<XRGrabInteractable>();
        return grab != null && !grab.isSelected;
    }

    /// <summary>
    /// Call once per hand after locomotion has resolved its collision-safe pose.
    /// The sweep is clipped against real world geometry; tracking/teleport jumps
    /// are rejected. Contact impulses affect only unheld, freely moving cards.
    /// </summary>
    public static void PushLooseCards(Vector3 start, Vector3 end, float radius, float deltaTime, int worldLayers)
    {
        Vector3 movement = end - start;
        float distance = movement.magnitude;
        if (!(deltaTime > 0f) || float.IsInfinity(deltaTime) ||
            !(distance > 0.0001f) || distance > MaximumHandStep)
            return;

        Vector3 direction = movement / distance;
        Vector3 handVelocity = direction * Mathf.Min(distance / deltaTime, MaximumHandSpeed);
        if (SphereCastEnvironment(start, radius, direction, out RaycastHit obstruction, distance, worldLayers))
            distance = Mathf.Min(distance, Mathf.Max(0f, obstruction.distance - 0.001f));
        if (distance <= 0.0001f)
            return;

        // Several render updates can precede one PhysX step on Quest. Forces are
        // accumulated until that step, so GetPointVelocity cannot by itself stop
        // repeated impulses stacking. Accept one hand contact per body per step.
        if (pushedPhysicsTime != Time.fixedTime)
        {
            pushedPhysicsTime = Time.fixedTime;
            pushedBodies.Clear();
        }
        initialContacts.Clear();
        // Include a card already touching the hand at the start of the sweep:
        // SphereCast alone deliberately does not report initial overlaps.
        int overlapCount = Physics.OverlapSphereNonAlloc(start, radius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Collider[] touching = overlaps;
        if (overlapCount == overlaps.Length)
        {
            touching = Physics.OverlapSphere(start, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            overlapCount = touching.Length;
        }
        for (int index = 0; index < overlapCount; index++)
        {
            Collider collider = touching[index];
            if (TryGetLooseCardBody(collider, out Rigidbody body))
            {
                initialContacts.Add(collider);
                Vector3 contact = collider.ClosestPoint(start);
                Vector3 towardCard = contact - start;
                if (towardCard.sqrMagnitude <= 0.000001f)
                    towardCard = body.worldCenterOfMass - start;
                // Retracting an ungripped hand must not pull the card along.
                if (Vector3.Dot(towardCard, direction) > 0.00001f)
                    Push(body, contact, handVelocity, direction);
            }
        }

        // All layers includes recently dropped cards still protected by HeldItem
        // until they separate from the rig. Only eligible card solids are nudged.
        int count = Physics.SphereCastNonAlloc(start, radius, direction, hits, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        RaycastHit[] candidates = hits;
        if (count == hits.Length)
        {
            candidates = Physics.SphereCastAll(start, radius, direction, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            count = candidates.Length;
        }
        for (int index = 0; index < count; index++)
        {
            RaycastHit candidate = candidates[index];
            if (!initialContacts.Contains(candidate.collider) && candidate.distance > 0.0001f &&
                TryGetLooseCardBody(candidate.collider, out Rigidbody body))
                Push(body, candidate.point, handVelocity, direction);
        }
    }

    static void Push(Rigidbody body, Vector3 contact, Vector3 handVelocity, Vector3 direction)
    {
        if (pushedBodies.Contains(body))
            return;
        float closingSpeed = Vector3.Dot(handVelocity - body.GetPointVelocity(contact), direction);
        float velocityChange = Mathf.Clamp(closingSpeed, 0f, MaximumVelocityChange);
        if (velocityChange <= 0f)
            return;
        pushedBodies.Add(body);
        // Applying at contact allows an off-center hand touch to tip the card.
        // Mass-scaled, capped impulses prevent a fast tracked hand launching it.
        body.AddForceAtPosition(direction * (body.mass * velocityChange), contact, ForceMode.Impulse);
    }
}
