using GorillaLocomotion;
using RunawayChimps.Zones;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Local-only Level 1 vent headlamp. The actual Spot Light is parented to the tracked
/// XR camera and is never networked, so one player's gameplay light cannot change
/// another player's horror lighting. Multiplayer-visible equipment presentation is a
/// separate concern from this local illumination effect.
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class VentHeadlampController : MonoBehaviour
{
    private const string LevelOneScene = "Level1_Containment";
    private const string RuntimeLightName = "Local_Vent_Headlamp";

    [Header("Beam")]
    [SerializeField, Min(0.5f)] private float beamRange = 8f;
    [SerializeField, Min(0f)] private float beamIntensity = 1.8f;
    [SerializeField, Range(20f, 80f)] private float outerSpotAngle = 46f;
    [SerializeField, Range(10f, 70f)] private float innerSpotAngle = 30f;
    [SerializeField] private Color beamColor = new Color(0.78f, 0.84f, 1f, 1f);
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(0f, -0.035f, 0.055f);

    [Header("Transition")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.14f;
    [SerializeField] private bool debugLogs;

    // The Level 1 prototype starts with the headlamp equipped. Keeping this state
    // separate from zone activation lets a future inventory/tool wheel equip or stow
    // the utility without changing the lighting implementation.
    [SerializeField] private bool toolEquipped = true;

    private XROrigin origin;
    private Camera trackedCamera;
    private Light headlamp;
    private ZoneStateService zoneService;
    private bool wantsBeam;
    private bool warnedMissingRig;

    public bool ToolEquipped => toolEquipped;
    public bool BeamActive => headlamp != null && headlamp.enabled && headlamp.intensity > 0.01f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == LevelOneScene)
            EnsureInstalled();
    }

    private static void HandleActiveSceneChanged(Scene previous, Scene current)
    {
        VentHeadlampController controller = FindInstalled();
        if (current.name == LevelOneScene && controller == null)
            controller = EnsureInstalled();

        controller?.RefreshDesiredState();
    }

    /// <summary>
    /// Installs the controller on the persistent XROrigin when Level 1 is loaded.
    /// No scene/prefab serialization is required, which keeps the split-scene travel
    /// path from accumulating duplicate lights after repeated visits.
    /// </summary>
    public static VentHeadlampController EnsureInstalled()
    {
        Player player = Player.Instance;
        XROrigin xrOrigin = player != null ? player.GetComponentInParent<XROrigin>() : null;
        if (xrOrigin == null)
            return null;

        VentHeadlampController controller = xrOrigin.GetComponent<VentHeadlampController>();
        if (controller == null)
            controller = xrOrigin.gameObject.AddComponent<VentHeadlampController>();

        controller.BindOrigin(xrOrigin);
        return controller;
    }

    private static VentHeadlampController FindInstalled()
    {
        Player player = Player.Instance;
        XROrigin xrOrigin = player != null ? player.GetComponentInParent<XROrigin>() : null;
        return xrOrigin != null ? xrOrigin.GetComponent<VentHeadlampController>() : null;
    }

    public void SetToolEquipped(bool equipped)
    {
        if (toolEquipped == equipped)
            return;

        toolEquipped = equipped;
        RefreshDesiredState();
    }

    private void Awake()
    {
        BindOrigin(GetComponent<XROrigin>() ?? GetComponentInParent<XROrigin>());
    }

    private void OnEnable()
    {
        BindZoneService();
        EnsureRuntimeLight();
        RefreshDesiredState();
    }

    private void OnDisable()
    {
        UnbindZoneService();
        wantsBeam = false;
        if (headlamp != null)
        {
            headlamp.intensity = 0f;
            headlamp.enabled = false;
        }
    }

    private void OnDestroy()
    {
        UnbindZoneService();
    }

    private void Update()
    {
        if (zoneService != ZoneStateService.Instance)
            BindZoneService();

        if (origin == null || origin.Camera == null || trackedCamera != origin.Camera || headlamp == null)
            EnsureRuntimeLight();

        RefreshDesiredState();
        UpdateBeamFade();
    }

    private void BindOrigin(XROrigin xrOrigin)
    {
        if (xrOrigin != null)
            origin = xrOrigin;

        EnsureRuntimeLight();
        BindZoneService();
        RefreshDesiredState();
    }

    private void BindZoneService()
    {
        ZoneStateService current = ZoneStateService.Instance;
        if (zoneService == current)
            return;

        UnbindZoneService();
        zoneService = current;
        if (zoneService != null)
            zoneService.OnLocalZoneChanged += HandleLocalZoneChanged;
    }

    private void UnbindZoneService()
    {
        if (zoneService != null)
            zoneService.OnLocalZoneChanged -= HandleLocalZoneChanged;
        zoneService = null;
    }

    private void HandleLocalZoneChanged(ZoneId zone)
    {
        RefreshDesiredState();
    }

    private void RefreshDesiredState()
    {
        bool shouldEnable =
            toolEquipped &&
            SceneManager.GetActiveScene().name == LevelOneScene &&
            zoneService != null &&
            zoneService.LocalZone == ZoneId.Level1_Vents;

        if (wantsBeam == shouldEnable)
            return;

        wantsBeam = shouldEnable;
        if (debugLogs)
            Debug.Log($"[VentHeadlamp] {(wantsBeam ? "Enabled" : "Disabled")} for local zone {(zoneService != null ? zoneService.LocalZone.ToString() : "None")}.", this);
    }

    private void EnsureRuntimeLight()
    {
        if (origin == null)
        {
            Player player = Player.Instance;
            if (player != null)
                origin = player.GetComponentInParent<XROrigin>();
        }

        if (origin == null || origin.Camera == null)
        {
            if (!warnedMissingRig && SceneManager.GetActiveScene().name == LevelOneScene)
            {
                warnedMissingRig = true;
                Debug.LogWarning("[VentHeadlamp] Waiting for the persistent XROrigin camera before creating the local vent light.", this);
            }
            return;
        }

        warnedMissingRig = false;
        trackedCamera = origin.Camera;

        Transform existing = trackedCamera.transform.Find(RuntimeLightName);
        if (existing != null)
        {
            headlamp = existing.GetComponent<Light>();
            if (headlamp == null)
                headlamp = existing.gameObject.AddComponent<Light>();
        }
        else
        {
            GameObject lightObject = new GameObject(RuntimeLightName);
            lightObject.transform.SetParent(trackedCamera.transform, false);
            headlamp = lightObject.AddComponent<Light>();
        }

        Transform lightTransform = headlamp.transform;
        lightTransform.localPosition = cameraLocalOffset;
        lightTransform.localRotation = Quaternion.identity;
        lightTransform.localScale = Vector3.one;

        ConfigureLight(headlamp);
    }

    private void ConfigureLight(Light light)
    {
        if (light == null)
            return;

        light.type = LightType.Spot;
        light.color = beamColor;
        light.range = beamRange;
        light.spotAngle = outerSpotAngle;
        light.innerSpotAngle = Mathf.Min(innerSpotAngle, outerSpotAngle - 1f);
        light.shadows = LightShadows.None;
        light.bounceIntensity = 0f;
        light.renderMode = LightRenderMode.Auto;

        if (!wantsBeam)
        {
            light.intensity = 0f;
            light.enabled = false;
        }
    }

    private void UpdateBeamFade()
    {
        if (headlamp == null)
            return;

        float target = wantsBeam ? beamIntensity : 0f;
        if (fadeSeconds <= 0f)
        {
            headlamp.intensity = target;
        }
        else
        {
            float speed = beamIntensity / Mathf.Max(0.01f, fadeSeconds);
            headlamp.intensity = Mathf.MoveTowards(
                headlamp.intensity,
                target,
                speed * Time.unscaledDeltaTime);
        }

        bool shouldRender = wantsBeam || headlamp.intensity > 0.01f;
        if (headlamp.enabled != shouldRender)
            headlamp.enabled = shouldRender;
    }
}
