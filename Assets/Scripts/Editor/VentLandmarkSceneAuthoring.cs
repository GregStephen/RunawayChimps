using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class VentLandmarkSceneAuthoring
{
    private const string LevelOneScenePath = "Assets/Scenes/Level1_Containment.unity";
    private const string RootName = "Level1_VentLandmarks";
    private const string VentRoomName = "VentRoom";
    private const string PrefabFolder = "Assets/RunawayChimps/Environment/VentLandmarks/Prefabs";
    private const string SharedMaterialPath = "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_DullSteel.mat";
    private const int LandmarkCount = 4;
    private const float FixtureHeight = 0.38f;
    private const float ExistingLandmarkExclusionRadius = 2.25f;

    private static readonly string[] PrefabNames =
    {
        "VentLandmark_Amber_TwinPipes",
        "VentLandmark_Blue_ElectricalBox",
        "VentLandmark_Green_TwinPipes",
        "VentLandmark_White_ElectricalBox",
    };

    [MenuItem("Tools/Runaway Chimps/Level 1/Author Vent Landmarks")]
    public static void AuthorFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[VentLandmarkSceneAuthoring] Stop Play Mode before authoring Level 1 vent landmarks.");
            return;
        }

        Scene loadedScene = SceneManager.GetSceneByPath(LevelOneScenePath);
        bool openedTemporarily = !loadedScene.IsValid() || !loadedScene.isLoaded;

        if (!openedTemporarily && loadedScene.isDirty)
        {
            Debug.LogWarning(
                "[VentLandmarkSceneAuthoring] Level1_Containment has unsaved edits. Save or revert them first; authoring will not modify a dirty scene.");
            return;
        }

        Scene scene = openedTemporarily
            ? EditorSceneManager.OpenScene(LevelOneScenePath, OpenSceneMode.Additive)
            : loadedScene;

        bool authored = false;
        try
        {
            Transform existingRoot = FindNamedTransform(scene, RootName);
            if (existingRoot != null)
            {
                Selection.activeGameObject = existingRoot.gameObject;
                Debug.Log(
                    "[VentLandmarkSceneAuthoring] Authored vent landmarks already exist. Existing scene objects were preserved; move/tune them directly instead of rebuilding.");
                return;
            }

            MonsterNavigation navigation = FindMonsterNavigation(scene);
            if (navigation == null || navigation.points == null || navigation.points.Length == 0)
            {
                Debug.LogError("[VentLandmarkSceneAuthoring] No Level 1 Crawler patrol points were found; nothing was authored.");
                return;
            }

            List<Transform> candidates = new List<Transform>();
            foreach (Transform point in navigation.points)
            {
                if (point != null && point.gameObject.scene == scene)
                    candidates.Add(point);
            }

            Transform ventRoom = FindNamedTransform(scene, VentRoomName);
            if (ventRoom != null)
            {
                candidates.RemoveAll(point =>
                    HorizontalDistance(point.position, ventRoom.position) < ExistingLandmarkExclusionRadius);
            }

            List<Transform> selected = SelectSpreadPoints(candidates, LandmarkCount);
            if (selected.Count < LandmarkCount)
            {
                Debug.LogError(
                    $"[VentLandmarkSceneAuthoring] Only {selected.Count} usable patrol anchors remain after filtering; expected {LandmarkCount}. No scene changes were saved.");
                return;
            }

            GameObject[] prefabs = EnsurePrefabs();
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                {
                    Debug.LogError("[VentLandmarkSceneAuthoring] One or more landmark prefabs could not be created/loaded.");
                    return;
                }
            }

            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Author Level 1 Vent Landmarks");

            for (int i = 0; i < LandmarkCount; i++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
                if (instance == null)
                {
                    Debug.LogError($"[VentLandmarkSceneAuthoring] Failed to instantiate {PrefabNames[i]}.");
                    Object.DestroyImmediate(root);
                    return;
                }

                instance.name = PrefabNames[i];
                instance.transform.SetParent(root.transform, true);
                instance.transform.position = selected[i].position + Vector3.up * FixtureHeight;
                instance.transform.rotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(instance, "Author Level 1 Vent Landmark");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("[VentLandmarkSceneAuthoring] Landmarks were created in memory but Level1_Containment could not be saved.");
                return;
            }

            authored = true;
            Selection.activeGameObject = root;
            Debug.Log(
                "[VentLandmarkSceneAuthoring] Authored four editable vent landmark prefab instances in Level1_Containment. They are now ordinary scene objects: move, rotate, duplicate, delete, or retune them directly.");
        }
        finally
        {
            if (openedTemporarily && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);

            if (authored)
                AssetDatabase.SaveAssets();
        }
    }

    private static GameObject[] EnsurePrefabs()
    {
        EnsureFolder("Assets/RunawayChimps", "Environment");
        EnsureFolder("Assets/RunawayChimps/Environment", "VentLandmarks");
        EnsureFolder("Assets/RunawayChimps/Environment/VentLandmarks", "Prefabs");

        Material sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
        if (sharedMaterial == null)
        {
            Debug.LogError($"[VentLandmarkSceneAuthoring] Missing shared material at {SharedMaterialPath}.");
            return new GameObject[LandmarkCount];
        }

        GameObject[] prefabs = new GameObject[LandmarkCount];
        for (int i = 0; i < LandmarkCount; i++)
        {
            string path = $"{PrefabFolder}/{PrefabNames[i]}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                prefabs[i] = existing;
                continue;
            }

            GameObject template = BuildTemplate(i, sharedMaterial);
            prefabs[i] = PrefabUtility.SaveAsPrefabAsset(template, path);
            Object.DestroyImmediate(template);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefabs;
    }

    private static GameObject BuildTemplate(int style, Material sharedMaterial)
    {
        GameObject root = new GameObject(PrefabNames[style]);

        switch (style)
        {
            case 0:
                BuildTwinPipeFixture(root.transform, sharedMaterial, new Color(0.95f, 0.42f, 0.08f), 0.72f, false, style);
                break;
            case 1:
                BuildBoxFixture(root.transform, sharedMaterial, new Color(0.08f, 0.35f, 0.95f), 0.62f, false, style);
                break;
            case 2:
                BuildTwinPipeFixture(root.transform, sharedMaterial, new Color(0.20f, 0.72f, 0.30f), 0.48f, true, style);
                break;
            default:
                BuildBoxFixture(root.transform, sharedMaterial, new Color(0.82f, 0.84f, 0.78f), 0.38f, true, style);
                break;
        }

        return root;
    }

    private static void BuildTwinPipeFixture(
        Transform parent,
        Material sharedMaterial,
        Color lightColor,
        float intensity,
        bool pulse,
        int style)
    {
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Pipe_A", new Vector3(-0.12f, 0f, 0f), new Vector3(0.035f, 0.16f, 0.035f), Quaternion.Euler(0f, 0f, 90f), sharedMaterial);
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Pipe_B", new Vector3(0.12f, 0f, 0f), new Vector3(0.035f, 0.16f, 0.035f), Quaternion.Euler(0f, 0f, 90f), sharedMaterial);
        CreatePrimitive(parent, PrimitiveType.Cube, "ServicePlate", new Vector3(0f, 0.055f, 0f), new Vector3(0.34f, 0.025f, 0.16f), Quaternion.identity, sharedMaterial);
        CreateLight(parent, lightColor, intensity, pulse, style);
    }

    private static void BuildBoxFixture(
        Transform parent,
        Material sharedMaterial,
        Color lightColor,
        float intensity,
        bool pulse,
        int style)
    {
        CreatePrimitive(parent, PrimitiveType.Cube, "ElectricalBox", Vector3.zero, new Vector3(0.24f, 0.10f, 0.16f), Quaternion.identity, sharedMaterial);
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Conduit", new Vector3(0.18f, 0f, 0f), new Vector3(0.025f, 0.18f, 0.025f), Quaternion.Euler(0f, 0f, 90f), sharedMaterial);
        CreateLight(parent, lightColor, intensity, pulse, style);
    }

    private static GameObject CreatePrimitive(
        Transform parent,
        PrimitiveType type,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material sharedMaterial)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = localRotation;
        primitive.transform.localScale = localScale;

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        return primitive;
    }

    private static void CreateLight(Transform parent, Color color, float intensity, bool pulse, int style)
    {
        GameObject lightObject = new GameObject("LandmarkLight");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = new Vector3(0f, -0.08f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = 1.55f;
        light.shadows = LightShadows.None;
        light.bounceIntensity = 0f;
        light.renderMode = LightRenderMode.ForceVertex;

        if (pulse)
        {
            VentLandmarkPulse pulseComponent = lightObject.AddComponent<VentLandmarkPulse>();
            pulseComponent.Configure(0.10f, 1.35f, style * 1.73f + 0.61f);
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static MonsterNavigation FindMonsterNavigation(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MonsterNavigation[] navigations = root.GetComponentsInChildren<MonsterNavigation>(true);
            foreach (MonsterNavigation navigation in navigations)
            {
                if (navigation != null && navigation.gameObject.scene == scene)
                    return navigation;
            }
        }

        return null;
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

    private static List<Transform> SelectSpreadPoints(List<Transform> candidates, int count)
    {
        List<Transform> selected = new List<Transform>();
        if (candidates.Count == 0)
            return selected;

        Transform first = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            Vector3 a = candidates[i].position;
            Vector3 b = first.position;
            if (a.x < b.x || (Mathf.Approximately(a.x, b.x) && a.z < b.z))
                first = candidates[i];
        }
        selected.Add(first);

        while (selected.Count < count && selected.Count < candidates.Count)
        {
            Transform best = null;
            float bestDistance = -1f;

            foreach (Transform candidate in candidates)
            {
                if (selected.Contains(candidate))
                    continue;

                float nearest = float.PositiveInfinity;
                foreach (Transform chosen in selected)
                    nearest = Mathf.Min(nearest, HorizontalDistance(candidate.position, chosen.position));

                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = candidate;
                }
            }

            if (best == null)
                break;

            selected.Add(best);
        }

        return selected;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector2 delta = new Vector2(a.x - b.x, a.z - b.z);
        return delta.magnitude;
    }
}
