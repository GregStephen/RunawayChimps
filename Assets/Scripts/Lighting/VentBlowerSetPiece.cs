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
    private const float FanDegreesPerSecond = 230f;
    private const float BaseRedLightIntensity = 0.85f;

    // VentRoom is a ProBuilder mesh whose widened chamber is authored in this local-space range.
    // Keep the blower against the +Z wall so the existing player/Crawler center path remains clear.
    private static readonly Vector3 BlowerLocalPosition = new Vector3(-0.25f, 0.72f, 22.08f);

    private static AudioClip blowerLoop;

    private readonly List<Material> ownedMaterials = new List<Material>(4);
    private Transform rotor;
    private Light maintenanceLight;
    private AudioSource humSource;

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
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                    return candidate;
            }
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
        Material housing = CreateMaterial("VentBlower_Housing", new Color(0.12f, 0.13f, 0.14f), 0.55f, 0.18f);
        Material steel = CreateMaterial("VentBlower_Steel", new Color(0.24f, 0.26f, 0.27f), 0.72f, 0.24f);
        Material dark = CreateMaterial("VentBlower_Dark", new Color(0.035f, 0.038f, 0.042f), 0.25f, 0.08f);
        Material red = CreateMaterial("VentBlower_RedLens", new Color(0.36f, 0.015f, 0.01f), 0.2f, 0.4f, new Color(2.6f, 0.035f, 0.02f));

        CreatePrimitive("Housing", PrimitiveType.Cube, transform, new Vector3(0f, 0f, 0.20f),
            Quaternion.identity, new Vector3(1.38f, 1.22f, 0.30f), housing);

        CreatePrimitive("Intake_Backplate", PrimitiveType.Cylinder, transform, new Vector3(0f, 0f, -0.005f),
            Quaternion.Euler(90f, 0f, 0f), new Vector3(0.57f, 0.055f, 0.57f), dark);

        Transform fanRotor = new GameObject("Fan_Rotor").transform;
        fanRotor.SetParent(transform, false);
        fanRotor.localPosition = new Vector3(0f, 0f, -0.105f);
        rotor = fanRotor;

        const int bladeCount = 6;
        for (int i = 0; i < bladeCount; i++)
        {
            var bladePivot = new GameObject($"BladePivot_{i + 1:00}").transform;
            bladePivot.SetParent(fanRotor, false);
            bladePivot.localRotation = Quaternion.Euler(0f, 0f, i * (360f / bladeCount));

            CreatePrimitive($"Blade_{i + 1:00}", PrimitiveType.Cube, bladePivot, new Vector3(0f, 0.27f, 0f),
                Quaternion.Euler(0f, 0f, -12f), new Vector3(0.18f, 0.45f, 0.045f), steel);
        }

        CreatePrimitive("Fan_Hub", PrimitiveType.Cylinder, fanRotor, Vector3.zero,
            Quaternion.Euler(90f, 0f, 0f), new Vector3(0.17f, 0.065f, 0.17f), housing);

        Transform guard = new GameObject("Fan_Guard").transform;
        guard.SetParent(transform, false);
        guard.localPosition = new Vector3(0f, 0f, -0.165f);

        const int ringSegments = 14;
        const float ringRadius = 0.565f;
        for (int i = 0; i < ringSegments; i++)
        {
            float angle = i * (360f / ringSegments);
            float radians = angle * Mathf.Deg2Rad;
            Vector3 position = new Vector3(Mathf.Cos(radians) * ringRadius, Mathf.Sin(radians) * ringRadius, 0f);
            CreatePrimitive($"Guard_Ring_{i + 1:00}", PrimitiveType.Cube, guard, position,
                Quaternion.Euler(0f, 0f, -angle), new Vector3(0.038f, 0.265f, 0.028f), steel);
        }

        const int guardSpokes = 6;
        for (int i = 0; i < guardSpokes; i++)
        {
            float angle = i * (360f / guardSpokes);
            var spokePivot = new GameObject($"GuardSpokePivot_{i + 1:00}").transform;
            spokePivot.SetParent(guard, false);
            spokePivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            CreatePrimitive($"Guard_Spoke_{i + 1:00}", PrimitiveType.Cube, spokePivot, new Vector3(0f, 0.285f, 0f),
                Quaternion.identity, new Vector3(0.025f, 0.57f, 0.025f), steel);
        }

        // A chunky wall conduit visually ties the unit into the facility instead of making it feel placed at random.
        CreatePrimitive("Power_Conduit", PrimitiveType.Cylinder, transform, new Vector3(0.52f, 0.61f, 0.18f),
            Quaternion.identity, new Vector3(0.045f, 0.30f, 0.045f), steel);

        Transform lampBracket = CreatePrimitive("Maintenance_Light_Housing", PrimitiveType.Cube, transform,
            new Vector3(-0.51f, 0.47f, -0.005f), Quaternion.identity, new Vector3(0.18f, 0.13f, 0.10f), dark).transform;
        CreatePrimitive("Maintenance_Light_Lens", PrimitiveType.Sphere, lampBracket,
            new Vector3(0f, 0f, -0.075f), Quaternion.identity, new Vector3(0.095f, 0.095f, 0.055f), red);

        GameObject lightObject = new GameObject("Maintenance_RedLight");
        lightObject.transform.SetParent(lampBracket, false);
        lightObject.transform.localPosition = new Vector3(0f, 0f, -0.13f);
        maintenanceLight = lightObject.AddComponent<Light>();
        maintenanceLight.type = LightType.Point;
        maintenanceLight.color = new Color(0.95f, 0.035f, 0.02f);
        maintenanceLight.intensity = BaseRedLightIntensity;
        maintenanceLight.range = 2.35f;
        maintenanceLight.shadows = LightShadows.None;
        maintenanceLight.bounceIntensity = 0f;
        maintenanceLight.renderMode = LightRenderMode.ForceVertex;

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

    private Material CreateMaterial(string materialName, Color baseColor, float metallic, float smoothness, Color? emission = null)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Diffuse");

        if (shader == null)
        {
            Debug.LogError("[VentBlowerSetPiece] No compatible lit shader found.");
            return null;
        }

        var material = new Material(shader) { name = materialName };
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

    private static GameObject CreatePrimitive(string objectName, PrimitiveType primitiveType, Transform parent,
        Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(primitiveType);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;

        Collider primitiveCollider = go.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            // Decorative moving machinery must never become a first-frame locomotion/NavMesh obstacle.
            primitiveCollider.enabled = false;
            Destroy(primitiveCollider);
        }

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        return go;
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
