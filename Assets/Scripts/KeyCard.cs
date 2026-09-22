using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody), typeof(XRGrabInteractable))]
[RequireComponent(typeof(HeldItemCollisionMode))]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class KeyCard : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
{
    private const string GrabAffordanceObjectName = "Keycard_GrabAffordance";

    [Header("Credential")]
    [Tooltip("Assign the card's color/symbol identity explicitly. Auto is retained only for legacy/name-authored compatibility.")]
    [SerializeField] private KeycardCredential credential = KeycardCredential.Auto;

    [Header("Physical handling")]
    [Tooltip("Gameplay cards are light props. Keeping their mass low prevents an XR-held card from imparting large impulses to other dynamic objects.")]
    [SerializeField, Min(0.01f)] private float physicalMassKg = 0.05f;
    [Tooltip("Small world-space padding added around the rendered card when fitting its solid BoxCollider at startup.")]
    [SerializeField, Min(0f)] private float colliderPaddingMeters = 0.002f;
    [Tooltip("Minimum world-space thickness for the solid card collider so the thin card still has reliable contacts and reader-trigger delivery.")]
    [SerializeField, Min(0.001f)] private float minimumColliderThicknessMeters = 0.006f;

    [Header("Grab affordance")]
    [Tooltip("World-space padding around the visible card used only for XR hover/grab detection. This volume is a trigger and never blocks the world.")]
    [SerializeField, Min(0f)] private float grabPaddingMeters = 0.035f;
    [Tooltip("Minimum world-space thickness of the trigger-only grab volume so a dropped flat card remains easy to acquire with a Gorilla hand.")]
    [SerializeField, Min(0.01f)] private float minimumGrabThicknessMeters = 0.06f;
    [Tooltip("Maximum palm-to-solid-card distance for a new pickup. This also limits oversized or swept interactor volumes; an existing hold is retained.")]
    [SerializeField, Range(0.02f, 0.15f)] private float maximumPickupDistanceMeters = 0.10f;

    private readonly CardConsumptionState consumption = new CardConsumptionState();
    private XRGrabInteractable grab;
    private BoxCollider physicalCollider;
    private BoxCollider grabAffordanceCollider;

    public bool IsInserted => consumption.IsConsumed;
    public bool WasHeldByLocalPlayer { get; private set; }
    public KeycardCredential Credential => credential;
    public Collider PhysicalCollider => physicalCollider;
    public Collider GrabAffordanceCollider => grabAffordanceCollider;
    public bool canProcess => true;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            foreach (IXRSelectInteractor interactor in grab.interactorsSelecting)
                RecordLocalHolder(interactor);
        // RequireComponent covers newly authored cards; this covers older serialized
        // cards that predate the requirement without depending on a scene-specific fix.
        if (GetComponent<HeldItemCollisionMode>() == null)
            gameObject.AddComponent<HeldItemCollisionMode>();
        ConfigurePhysicalCard();
    }

    private void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        if (grab != null)
        {
            grab.hoverFilters.Add(this);
            grab.selectFilters.Add(this);
            grab.selectEntered.AddListener(Selected);
            // Re-enabling KeyCard while already held must not require another pickup.
            foreach (IXRSelectInteractor interactor in grab.interactorsSelecting)
                RecordLocalHolder(interactor);
        }
    }

    /// <summary>
    /// Keep the solid card collider tight to the visible badge, then use a separate
    /// trigger-only volume for XR hover/grab acquisition. A dropped thin card can therefore
    /// be easy to reacquire without recreating the oversized invisible physics body that
    /// previously made cards float and pushed the Gorilla rig.
    /// </summary>
    private void ConfigurePhysicalCard()
    {
        physicalCollider = GetComponent<BoxCollider>();
        if (physicalCollider != null)
        {
            physicalCollider.isTrigger = false;
            FitColliderToVisualBounds(physicalCollider);
            ConfigureGrabAffordance(physicalCollider);
        }

        // The imported cards used Instantaneous (Transform teleport) movement,
        // which cannot provide the promised held-card/world collision behavior.
        if (grab != null)
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            return;

        body.mass = Mathf.Max(0.01f, physicalMassKg);
        body.useGravity = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void FitColliderToVisualBounds(BoxCollider box)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool foundBounds = false;
        Bounds localBounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) ||
                renderer.GetComponentInParent<KeyCard>() != this)
                continue;

            // Do not inverse-transform a world AABB: rotating a thin card
            // would inflate its collider thickness. Transform renderer-local corners directly.
            Bounds rendererBounds = renderer.localBounds;
            Vector3 center = rendererBounds.center;
            Vector3 extents = rendererBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = renderer.transform.TransformPoint(
                            center + Vector3.Scale(extents, new Vector3(x, y, z)));
                        Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                        if (!foundBounds)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            foundBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        if (!foundBounds)
        {
            Debug.LogWarning($"[KeyCard] '{name}' has no Renderer bounds; leaving its authored BoxCollider unchanged.", this);
            return;
        }

        Vector3 scale = SafeLossyScale();
        float padding = Mathf.Max(0f, colliderPaddingMeters);
        float minimumThickness = Mathf.Max(0.001f, minimumColliderThicknessMeters);
        Vector3 localPadding = new Vector3(padding / scale.x, padding / scale.y, padding / scale.z);
        Vector3 localMinimum = new Vector3(
            minimumThickness / scale.x,
            minimumThickness / scale.y,
            minimumThickness / scale.z);

        Vector3 size = localBounds.size + localPadding * 2f;
        box.center = localBounds.center;
        box.size = new Vector3(
            Mathf.Max(size.x, localMinimum.x),
            Mathf.Max(size.y, localMinimum.y),
            Mathf.Max(size.z, localMinimum.z));
    }

    private void ConfigureGrabAffordance(BoxCollider solidCollider)
    {
        Transform existing = transform.Find(GrabAffordanceObjectName);
        GameObject affordanceObject;
        if (existing != null)
        {
            affordanceObject = existing.gameObject;
        }
        else
        {
            affordanceObject = new GameObject(GrabAffordanceObjectName);
            affordanceObject.transform.SetParent(transform, false);
        }

        HeldItemCollisionMode collisionMode = GetComponent<HeldItemCollisionMode>();
        // A runtime-added card may already be held. Do not permanently give its
        // newly created acquisition trigger the temporary solid HeldItem layer.
        affordanceObject.layer = collisionMode != null ? collisionMode.GetUnheldLayer(transform) : gameObject.layer;
        affordanceObject.transform.localPosition = Vector3.zero;
        affordanceObject.transform.localRotation = Quaternion.identity;
        affordanceObject.transform.localScale = Vector3.one;

        grabAffordanceCollider = affordanceObject.GetComponent<BoxCollider>();
        if (grabAffordanceCollider == null)
            grabAffordanceCollider = affordanceObject.AddComponent<BoxCollider>();

        grabAffordanceCollider.isTrigger = true;
        grabAffordanceCollider.center = solidCollider.center;

        Vector3 scale = SafeLossyScale();
        float padding = Mathf.Max(0f, grabPaddingMeters);
        float minimumThickness = Mathf.Max(0.01f, minimumGrabThicknessMeters);
        Vector3 localPadding = new Vector3(padding / scale.x, padding / scale.y, padding / scale.z);
        Vector3 localMinimum = new Vector3(
            minimumThickness / scale.x,
            minimumThickness / scale.y,
            minimumThickness / scale.z);

        Vector3 size = solidCollider.size + localPadding * 2f;
        grabAffordanceCollider.size = new Vector3(
            Mathf.Max(size.x, localMinimum.x),
            Mathf.Max(size.y, localMinimum.y),
            Mathf.Max(size.z, localMinimum.z));

        if (grab != null)
        {
            // XRI interaction uses only the generous trigger volume. The tight solid collider
            // remains responsible for world physics and reader scanning.
            // Normal scene Awake precedes XRI registration. Adding KeyCard to an
            // already-live XRGrabInteractable must refresh the manager's collider map.
            XRInteractionManager manager = grab.interactionManager;
            bool registered = manager != null && manager.IsRegistered((IXRInteractable)grab);
            if (registered)
                manager.UnregisterInteractable((IXRInteractable)grab);
            grab.colliders.Clear();
            grab.colliders.Add(grabAffordanceCollider);
            if (registered && grab != null && grab.isActiveAndEnabled && manager != null)
                manager.RegisterInteractable((IXRInteractable)grab);
        }
    }

    private Vector3 SafeLossyScale()
    {
        Vector3 scale = transform.lossyScale;
        return new Vector3(
            Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }

    private void Selected(SelectEnterEventArgs args)
    {
        RecordLocalHolder(args != null ? args.interactorObject : null);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return CanReachForPickup(interactor);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        // Selection filters are also evaluated while holding. A wall resisting a held
        // card must not make it drop merely because the tracked hand moved farther away.
        return CanReachForPickup(interactor);
    }

    private bool CanReachForPickup(IXRInteractor interactor)
    {
        if (!isActiveAndEnabled || IsInserted || grab == null || physicalCollider == null ||
            !physicalCollider.enabled || !(interactor is XRDirectInteractor hand) ||
            hand.GetComponentInParent<LocalRigMarker>() == null)
            return false;

        if (interactor is IXRSelectInteractor holder && grab.interactorsSelecting.Contains(holder))
            return true;

        // Use the real palm attach point, never a ray's distant hit or a swept contact
        // from an earlier hand pose. Test the solid card, not its padded grab trigger.
        Transform palm = hand.GetAttachTransform(null);
        Vector3 point = palm != null ? palm.position : hand.transform.position;
        Vector3 closest = Physics.ClosestPoint(point, physicalCollider,
            physicalCollider.transform.position, physicalCollider.transform.rotation);
        float reach = Mathf.Clamp(maximumPickupDistanceMeters, 0.02f, 0.15f);
        return (closest - point).sqrMagnitude <= reach * reach;
    }

    private void RecordLocalHolder(IXRSelectInteractor interactor)
    {
        if (interactor != null && interactor.transform != null &&
            interactor.transform.GetComponentInParent<LocalRigMarker>() != null)
            WasHeldByLocalPlayer = true;
    }

    private void OnDisable()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(Selected);
            grab.hoverFilters.Remove(this);
            grab.selectFilters.Remove(this);
        }
    }

    /// <summary>
    /// Runs the normal keycard acceptance lifecycle through the supplied KeyBox.
    /// Reader-driven submissions use this so the existing local-player, scene,
    /// duplicate-card, completion and travel guards remain authoritative.
    /// </summary>
    public bool TryInsertInto(KeyBox box)
    {
        return TryInsertCore(box, null);
    }

    internal bool TryInsertFromReader(KeyBox box, KeycardReaderLightController reader)
    {
        return TryInsertCore(box, reader);
    }

    internal bool IsInsertionPendingFor(KeyBox box) => consumption.IsPendingFor(box);
    internal bool CommitInsertion(KeyBox box) => consumption.Commit(box);

    private bool TryInsertCore(KeyBox box, KeycardReaderLightController reader)
    {
        if (!isActiveAndEnabled || box == null || !consumption.TryBegin(box))
            return false;

        try
        {
            if (!box.TryAcceptKey(this, reader))
                return false;

            // The box commits consumption BEFORE it notifies observers. A callback
            // may already have destroyed this object or started scene travel.
            if (this != null)
            {
                try
                {
                    if (grab != null)
                        grab.enabled = false;
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, this);
                }
                finally
                {
                    // A throwing select-exit listener cannot leave a spent card
                    // in the world. Consumption is already committed at this point.
                    if (this != null)
                    {
                        if (physicalCollider != null) physicalCollider.enabled = false;
                        if (grabAffordanceCollider != null) grabAffordanceCollider.enabled = false;
                        Destroy(gameObject);
                    }
                }
            }
            return true;
        }
        finally
        {
            consumption.End(box);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsInserted)
            return;

        KeyBox box = other.GetComponent<KeyBox>();
        if (box == null)
            return;

        // Any reader-backed objective, in any level, requires a matching reader.
        // Unbound legacy boxes retain their explicitly supported direct path.
        if (!box.RequiresMatchingReader)
            TryInsertInto(box);
    }
}
