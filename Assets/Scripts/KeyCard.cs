using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody), typeof(XRGrabInteractable))]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class KeyCard : MonoBehaviour
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

    private bool isInserted = false;
    private XRGrabInteractable grab;
    private BoxCollider physicalCollider;
    private BoxCollider grabAffordanceCollider;

    public bool IsInserted => isInserted;
    public bool WasHeldByLocalPlayer { get; private set; }
    public KeycardCredential Credential => credential;
    public Collider PhysicalCollider => physicalCollider;
    public Collider GrabAffordanceCollider => grabAffordanceCollider;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        ConfigurePhysicalCard();
    }

    private void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        if (grab != null)
            grab.selectEntered.AddListener(Selected);
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
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
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

        affordanceObject.layer = gameObject.layer;
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
            grab.colliders.Clear();
            grab.colliders.Add(grabAffordanceCollider);
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
