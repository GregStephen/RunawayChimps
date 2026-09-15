using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody), typeof(XRGrabInteractable))]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class KeyCard : MonoBehaviour
{
    [Header("Credential")]
    [Tooltip("Assign the card's color/symbol identity explicitly. Auto is retained only for legacy/name-authored compatibility.")]
    [SerializeField] private KeycardCredential credential = KeycardCredential.Auto;

    [Header("Physical handling")]
    [Tooltip("Gameplay cards are light props. Keeping their mass low prevents an XR-held card from imparting large impulses to other dynamic objects.")]
    [SerializeField, Min(0.01f)] private float physicalMassKg = 0.05f;
    [Tooltip("Small world-space padding added around the rendered card when fitting its BoxCollider at startup.")]
    [SerializeField, Min(0f)] private float colliderPaddingMeters = 0.002f;
    [Tooltip("Minimum world-space collider thickness so the thin card still has reliable contacts and reader-trigger delivery.")]
    [SerializeField, Min(0.001f)] private float minimumColliderThicknessMeters = 0.006f;

    private bool isInserted = false;
    private XRGrabInteractable grab;

    public bool IsInserted => isInserted;
    public bool WasHeldByLocalPlayer { get; private set; }
    public KeycardCredential Credential => credential;

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
    /// The imported card FBX instances were originally given a 1x1x1 BoxCollider. At the
    /// authored card scales that creates a roughly metre-scale invisible physics cube, which
    /// can hold the visible card above the floor and violently depenetrate the Gorilla rig
    /// when the card is grabbed. Fit the collider to the actual rendered card before the
    /// first physics step and normalize the tiny prop's Rigidbody settings.
    /// </summary>
    private void ConfigurePhysicalCard()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            FitColliderToVisualBounds(box);

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

        Vector3 scale = transform.lossyScale;
        scale = new Vector3(
            Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

        float padding = Mathf.Max(0f, colliderPaddingMeters);
        float minimumThickness = Mathf.Max(0.001f, minimumColliderThicknessMeters);
        Vector3 localPadding = new Vector3(padding / scale.x, padding / scale.y, padding / scale.z);
        Vector3 localMinimum = new Vector3(
            minimumThickness / scale.x,
            minimumThickness / scale.y,
            minimumThickness / scale.z);

        Vector3 size = localBounds.size + localPadding * 2f;
        size = new Vector3(
            Mathf.Max(size.x, localMinimum.x),
            Mathf.Max(size.y, localMinimum.y),
            Mathf.Max(size.z, localMinimum.z));

        box.center = localBounds.center;
        box.size = size;
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
