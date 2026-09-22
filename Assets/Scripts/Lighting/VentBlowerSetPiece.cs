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
    private const string RotorHubName = "FanHub";
    private const string RotorPivotName = "FanRotor_CenteredPivot";
    private const string HousingLeftLipName = "Housing_LeftLip";
    private const string HousingRightLipName = "Housing_RightLip";
    private const string RedLensName = "RedLightLens";
    private const float HousingLipCenterX = 0.662f;
    private const float FanDegreesPerSecond = 230f;
    private const float BaseRedLightIntensity = 0.85f;
    private const float MotorVolume = 0.72f;
    private const float MotorMinDistance = 1.5f;
    private const float MotorMaxDistance = 10f;

    // The approved Blender model's origin is its wall plane, so place that plane just inside
    // VentRoom's authored +Z wall (22.5) instead of using the old generated-housing center.
    // The tunnel route and VentRoom geometry remain untouched.
    private static readonly Vector3 BlowerLocalPosition = new Vector3(-0.25f, 0.72f, 22.46f);
    // The FBX's six blades are arranged around FanRotor's local Y axis. The imported
    // root supplies the Blender/Unity axis conversion; visual-root Z is not rotor Z.
    private static readonly Vector3 RotorSpinAxis = Vector3.up;

    private static AudioClip blowerLoop;

    private Transform rotorPivot;
    private Quaternion rotorRestRotation;
    private float rotorAngle;
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
        AnimateRotor(Time.deltaTime);

        if (maintenanceLight != null)
        {
            // Subtle electrical breathing rather than a full strobe; avoids distracting VR flicker.
            float pulse = 0.94f + 0.06f * Mathf.Sin(Time.time * 2.1f);
            maintenanceLight.intensity = BaseRedLightIntensity * pulse;
        }
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

        Transform importedRotor = FindDescendant(visualInstance.transform, RotorName);
        if (importedRotor == null)
            Debug.LogError($"[VentBlowerSetPiece] Imported blower is missing required '{RotorName}' transform; fan will not rotate.");
        else
            ConfigureRotor(importedRotor);

        Transform redLens = FindDescendant(visualInstance.transform, RedLensName);
        if (redLens == null)
            Debug.LogWarning($"[VentBlowerSetPiece] Imported blower is missing '{RedLensName}'; maintenance light will use the visual root.");

        CreateMaintenanceLight(redLens != null ? redLens : visualInstance.transform);
        CreateMotorAudio();
    }

    private void ConfigureRotor(Transform importedRotor)
    {
        Transform hub = FindDescendant(importedRotor, RotorHubName);
        if (hub == null)
        {
            Debug.LogError($"[VentBlowerSetPiece] Imported rotor is missing required '{RotorHubName}' transform; fan will not rotate.");
            return;
        }

        // FanRotor's origin is on the wall plane, while FanHub is on the spindle.
        // Insert a centered wrapper in the SAME imported coordinate frame. Copying
        // local TRS and cancelling the hub offset keeps every rotor mesh in place,
        // including the FBX's import scale/orientation, without touching its siblings.
        Vector3 hubLocalPosition = importedRotor.InverseTransformPoint(hub.position);
        rotorPivot = new GameObject(RotorPivotName).transform;
        rotorPivot.SetParent(importedRotor.parent, false);
        rotorPivot.localPosition = importedRotor.localPosition
            + importedRotor.localRotation * Vector3.Scale(importedRotor.localScale, hubLocalPosition);
        rotorPivot.localRotation = importedRotor.localRotation;
        rotorPivot.localScale = importedRotor.localScale;
        rotorRestRotation = rotorPivot.localRotation;
        rotorAngle = 0f;

        importedRotor.SetParent(rotorPivot, false);
        importedRotor.localPosition = -hubLocalPosition;
        importedRotor.localRotation = Quaternion.identity;
        importedRotor.localScale = Vector3.one;
    }

    private void AnimateRotor(float deltaTime)
    {
        if (rotorPivot == null)
            return;

        // Rebuild a bounded rotation from the rest pose; never integrate positions
        // or compound quaternions, so repeated turns cannot drift away from the hub.
        rotorAngle = Mathf.Repeat(rotorAngle - FanDegreesPerSecond * deltaTime, 360f);
        rotorPivot.localRotation = rotorRestRotation * Quaternion.AngleAxis(rotorAngle, RotorSpinAxis);
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

    private static void CorrectHousingLipOverlap(Transform visualRoot)
    {
        // The authored FBX places both side trim pieces partly inside the housing:
        // housing extends to +/-0.625 m while each 0.07 m lip is centered at +/-0.59 m.
        // Put the trim just outside the case with a small ~2 mm separation. Assigning an
        // absolute center makes this idempotent on repeated setup and avoids z-fighting.
        Transform leftLip = FindDescendant(visualRoot, HousingLeftLipName);
        Transform rightLip = FindDescendant(visualRoot, HousingRightLipName);

        if (leftLip != null)
        {
            Vector3 position = leftLip.localPosition;
            position.x = -HousingLipCenterX;
            leftLip.localPosition = position;
        }
        else
            Debug.LogWarning($"[VentBlowerSetPiece] Imported blower is missing '{HousingLeftLipName}'; left case trim cannot be corrected.");

        if (rightLip != null)
        {
            Vector3 position = rightLip.localPosition;
            position.x = HousingLipCenterX;
            rightLip.localPosition = position;
        }
        else
            Debug.LogWarning($"[VentBlowerSetPiece] Imported blower is missing '{HousingRightLipName}'; right case trim cannot be corrected.");
    }

    private void ConfigureImportedVisual(GameObject visualRoot)
    {
        CorrectHousingLipOverlap(visualRoot.transform);

        // The FBX is decorative environmental art. Never allow imported/generated colliders to
        // obstruct Gorilla locomotion or the Crawler's already-approved route through VentRoom.
        Collider[] colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        // Materials are serialized Unity Standard materials mapped directly by the FBX importer.
        // Runaway Chimps currently uses the built-in render pipeline; URP is installed but inactive.
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null || !material.shader.isSupported)
                    Debug.LogError($"[VentBlowerSetPiece] Imported blower material on '{renderer.name}' is missing or uses an unsupported shader.");
            }
        }
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
        humSource.volume = MotorVolume;
        humSource.spatialBlend = 1f;
        humSource.rolloffMode = AudioRolloffMode.Linear;
        humSource.minDistance = MotorMinDistance;
        humSource.maxDistance = MotorMaxDistance;
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
            // Keep every frequency on a 0.5 Hz boundary so the two-second loop closes cleanly.
            // The previous 42 Hz-heavy loop was easy to lose on small Quest speakers.
            float bladeThrum = 0.80f + 0.20f * Mathf.Sin(2f * Mathf.PI * 4f * t);
            float motor = 0.52f * Mathf.Sin(2f * Mathf.PI * 92f * t)
                        + 0.24f * Mathf.Sin(2f * Mathf.PI * 184f * t)
                        + 0.11f * Mathf.Sin(2f * Mathf.PI * 276f * t)
                        + 0.06f * Mathf.Sin(2f * Mathf.PI * 368f * t);
            float air = 0.07f * Mathf.Sin(2f * Mathf.PI * 640f * t)
                      + 0.05f * Mathf.Sin(2f * Mathf.PI * 912f * t)
                      + 0.03f * Mathf.Sin(2f * Mathf.PI * 1248f * t);
            samples[i] = Mathf.Clamp((motor * bladeThrum + air) * 0.34f, -0.45f, 0.45f);
        }

        blowerLoop = AudioClip.Create("Vent_Blower_ProceduralLoop", sampleCount, 1, sampleRate, false);
        blowerLoop.SetData(samples, 0);
        return blowerLoop;
    }
}
