using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class VentBlowerSetPiece : MonoBehaviour
{
    private const string LevelOneScene = "Level1_Containment";
    private const string VentRoomName = "VentRoom";
    private const string RuntimeRootName = "Level1_VentRoom_Blower";
    private const string BlowerResourcePath = "RunawayChimps_VentBlower";
    private const string RotorName = "FanRotor";
    private const string RedLensName = "RedLightLens";
    private const float FanDegreesPerSecond = 230f;
    private const float BaseRedLightIntensity = 0.85f;

    // The approved Blender model's origin is its wall plane, so place that plane just inside
    // VentRoom's authored +Z wall (22.5) instead of using the old generated-housing center.
    // The tunnel route and VentRoom geometry remain untouched.
    private static readonly Vector3 BlowerLocalPosition = new Vector3(-0.25f, 0.72f, 22.46f);

    private static AudioClip blowerLoop;

    private readonly List<Material> ownedMaterials = new List<Material>(5);
    private Transform rotor;
    private Light maintenanceLight;
    private AudioSource humSource;
    private GameObject visualInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        blowerLoop = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != LevelOneScene)
            return;

        EnsureBlower(scene);
    }

    private static void EnsureBlower(Scene scene)
    {
        Transform ventRoom = FindNamedTransform(scene, VentRoomName);
        if (ventRoom == null)
        {
            Debug.LogWarning($"[VentBlowerSetPiece] Could not find {VentRoomName} in {scene.name}; blower not created.");
            return;
        }

        if (ventRoom.Find(RuntimeRootName) != null)
            return;

        var root = new GameObject(RuntimeRootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.SetParent(ventRoom, false);
        root.transform.localPosition = BlowerLocalPosition;
        root.transform.localRotation = Quaternion.identity;
        root.AddComponent<VentBlowerSetPiece>();
    }

    private static Transform FindNamedTransform(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDescendant(root.transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate.name == objectName)
                return candidate;
        }

        return null;
    }

    private void Awake()
    {
        BuildBlower();
    }

    private void Update()
    {
        if (rotor != null)
            rotor.Rotate(0f, 0f, -FanDegreesPerSecond * Time.deltaTime, Space.Self);

        if (maintenanceLight != null)
        {
            // Subtle electrical breathing rather than a full strobe; avoids distracting VR flicker.
            float pulse = 0.94f + 0.06f * Mathf.Sin(Time.time * 2.1f);
            maintenanceLight.intensity = BaseRedLightIntensity * pulse;
        }
    }

    private void OnDestroy()
    {
        foreach (Material material in ownedMaterials)
        {
            if (material != null)
                Destroy(material);
        }
        ownedMaterials.Clear();
    }

    private void BuildBlower()
    {
        GameObject blowerPrefab = Resources.Load<GameObject>(BlowerResourcePath);
        if (blowerPrefab == null)
        {
            Debug.LogError($"[VentBlowerSetPiece] Missing Resources asset '{BlowerResourcePath}'. The approved Blender blower cannot be created.");
            enabled = false;
            return;
        }

        visualInstance = Instantiate(blowerPrefab, transform, false);
        visualInstance.name = "VentBlower_Visual";
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;
        visualInstance.transform.localScale = Vector3.one;

        // Blender's asset faces away from its wall plane. FBX axis conversion can flip which local
        // Z direction that becomes in Unity, so determine it from the imported render bounds and
        // guarantee that the model projects into the room (-Z), never through the +Z wall.
        OrientVisualIntoVentRoom(visualInstance.transform);
        ConfigureImportedVisual(visualInstance);

        rotor = FindDescendant(visualInstance.transform, RotorName);
        if (rotor == null)
            Debug.LogError($"[VentBlowerSetPiece] Imported blower is missing required '{RotorName}' transform; fan will not rotate.");

        Transform redLens = FindDescendant(visualInstance.transform, RedLensName);
        if (redLens == null)
            Debug.LogWarning($"[VentBlowerSetPiece] Imported blower is missing '{RedLensName}'; maintenance light will use the visual root.");

        CreateMaintenanceLight(redLens != null ? redLens : visualInstance.transform);
        CreateMotorAudio();
    }

    private void OrientVisualIntoVentRoom(Transform visualRoot)
    {
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float localCenterZ = transform.InverseTransformPoint(bounds.center).z;
        if (localCenterZ > 0f)
            visualRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private void ConfigureImportedVisual(GameObject visualRoot)
    {
        // The FBX is decorative environmental art. Never allow imported/generated colliders to
        // obstruct Gorilla locomotion or the Crawler's already-approved route through VentRoom.
        Collider[] colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Material dark = CreateRuntimeMaterial("VentBlower_DarkPaintedMetal", new Color(0.075f, 0.082f, 0.085f), 0.25f, 0.18f);
        Material steel = CreateRuntimeMaterial("VentBlower_DullSteel", new Color(0.17f, 0.18f, 0.18f), 0.55f, 0.26f);
        Material blade = CreateRuntimeMaterial("VentBlower_FanBlade", new Color(0.12f, 0.125f, 0.12f), 0.45f, 0.21f);
        Material conduit = CreateRuntimeMaterial("VentBlower_Conduit", new Color(0.035f, 0.038f, 0.04f), 0.10f, 0.10f);
        Material red = CreateRuntimeMaterial("VentBlower_RedMaintenanceLens", new Color(0.35f, 0.01f, 0.008f), 0f, 0.40f,
            new Color(2.6f, 0.035f, 0.02f));

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Material replacement = ResolveMaterial(renderer.gameObject.name, dark, steel, blade, conduit, red);
            Material[] replacements = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < replacements.Length; i++)
                replacements[i] = replacement;
            renderer.sharedMaterials = replacements;
        }
    }

    private static Material ResolveMaterial(string objectName, Material dark, Material steel, Material blade,
        Material conduit, Material red)
    {
        if (objectName == RedLensName)
            return red;
        if (objectName.StartsWith("Blade_"))
            return blade;
        if (objectName.Contains("Conduit"))
            return conduit;
        if (objectName.StartsWith("Guard") || objectName.StartsWith("LampGuard") ||
            objectName.StartsWith("MountBolt") || objectName == "FanOuterRing" ||
            objectName == "FanHub" || objectName == "Housing_LeftLip" ||
            objectName == "Housing_RightLip" || objectName == "MotorCap")
            return steel;

        return dark;
    }

    private Material CreateRuntimeMaterial(string materialName, Color baseColor, float metallic, float smoothness,
        Color? emission = null)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Diffuse");

        if (shader == null)
        {
            Debug.LogError("[VentBlowerSetPiece] No compatible lit shader found for the imported blower.");
            return null;
        }

        var material = new Material(shader) { name = materialName, enableInstancing = true };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        if (emission.HasValue && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
        }

        ownedMaterials.Add(material);
        return material;
    }

    private void CreateMaintenanceLight(Transform anchor)
    {
        GameObject lightObject = new GameObject("Maintenance_RedLight");
        lightObject.transform.SetParent(anchor, false);
        lightObject.transform.localPosition = Vector3.zero;
        maintenanceLight = lightObject.AddComponent<Light>();
        maintenanceLight.type = LightType.Point;
        maintenanceLight.color = new Color(0.95f, 0.035f, 0.02f);
        maintenanceLight.intensity = BaseRedLightIntensity;
        maintenanceLight.range = 2.35f;
        maintenanceLight.shadows = LightShadows.None;
        maintenanceLight.bounceIntensity = 0f;
        maintenanceLight.renderMode = LightRenderMode.ForceVertex;
    }

    private void CreateMotorAudio()
    {
        // This is a fixed environmental source on every client, not a Photon-synchronized gameplay event.
        humSource = gameObject.AddComponent<AudioSource>();
        humSource.clip = GetOrCreateBlowerLoop();
        humSource.loop = true;
        humSource.playOnAwake = false;
        humSource.volume = 0.43f;
        humSource.spatialBlend = 1f;
        humSource.rolloffMode = AudioRolloffMode.Linear;
        humSource.minDistance = 0.75f;
        humSource.maxDistance = 6.5f;
        humSource.dopplerLevel = 0f;
        humSource.spread = 45f;
        humSource.priority = 180;
        humSource.Play();
    }

    private static AudioClip GetOrCreateBlowerLoop()
    {
        if (blowerLoop != null)
            return blowerLoop;

        const int sampleRate = 22050;
        const float seconds = 2f;
        int sampleCount = Mathf.RoundToInt(sampleRate * seconds);
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float bladeThrum = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * 4f * t);
            float motor = 0.55f * Mathf.Sin(2f * Mathf.PI * 42f * t)
                        + 0.28f * Mathf.Sin(2f * Mathf.PI * 84f * t)
                        + 0.12f * Mathf.Sin(2f * Mathf.PI * 126f * t);
            float bearing = 0.05f * Mathf.Sin(2f * Mathf.PI * 168f * t);
            samples[i] = (motor * bladeThrum + bearing) * 0.16f;
        }

        blowerLoop = AudioClip.Create("Vent_Blower_ProceduralLoop", sampleCount, 1, sampleRate, false);
        blowerLoop.SetData(samples, 0);
        return blowerLoop;
    }
}
