using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class VentLandmarkSetPiece : MonoBehaviour
{
    private const string LevelOneScene = "Level1_Containment";
    private const string RuntimeRootName = "Level1_VentLandmarks";
    private const int LandmarkCount = 4;
    private const float FixtureHeight = 0.38f;

    private readonly List<Light> pulseLights = new List<Light>();
    private readonly List<float> pulseSeeds = new List<float>();

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

        EnsureLandmarks(scene);
    }

    private static void EnsureLandmarks(Scene scene)
    {
        if (FindNamedTransform(scene, RuntimeRootName) != null)
            return;

        MonsterNavigation navigation = FindMonsterNavigation(scene);
        if (navigation == null || navigation.points == null || navigation.points.Length == 0)
        {
            Debug.LogWarning("[VentLandmarkSetPiece] No Level 1 Crawler patrol points found; vent landmarks were not created.");
            return;
        }

        List<Transform> candidates = new List<Transform>();
        foreach (Transform point in navigation.points)
        {
            if (point != null && point.gameObject.scene == scene)
                candidates.Add(point);
        }

        List<Transform> selected = SelectSpreadPoints(candidates, LandmarkCount);
        if (selected.Count == 0)
            return;

        GameObject root = new GameObject(RuntimeRootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        VentLandmarkSetPiece controller = root.AddComponent<VentLandmarkSetPiece>();

        for (int i = 0; i < selected.Count; i++)
            controller.BuildLandmark(selected[i].position, i);
    }

    private static MonsterNavigation FindMonsterNavigation(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonsterNavigation navigation in root.GetComponentsInChildren<MonsterNavigation>(true))
                return navigation;
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

        // Deterministic farthest-point sampling. These are recognition anchors, not a route:
        // no ordering, objective direction, or "correct path" information is encoded.
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

    private void BuildLandmark(Vector3 worldPosition, int style)
    {
        GameObject anchor = new GameObject($"VentLandmark_{style + 1:00}");
        anchor.transform.SetParent(transform, false);
        anchor.transform.position = worldPosition + Vector3.up * FixtureHeight;

        switch (style % 4)
        {
            case 0:
                BuildTwinPipeFixture(anchor.transform, new Color(0.95f, 0.42f, 0.08f), 0.72f, false);
                break;
            case 1:
                BuildBoxFixture(anchor.transform, new Color(0.08f, 0.35f, 0.95f), 0.62f, false);
                break;
            case 2:
                BuildTwinPipeFixture(anchor.transform, new Color(0.20f, 0.72f, 0.30f), 0.48f, true);
                break;
            default:
                BuildBoxFixture(anchor.transform, new Color(0.82f, 0.84f, 0.78f), 0.38f, true);
                break;
        }
    }

    private void BuildTwinPipeFixture(Transform parent, Color lightColor, float intensity, bool pulse)
    {
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Pipe_A", new Vector3(-0.12f, 0f, 0f), new Vector3(0.035f, 0.16f, 0.035f), Quaternion.Euler(0f, 0f, 90f));
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Pipe_B", new Vector3(0.12f, 0f, 0f), new Vector3(0.035f, 0.16f, 0.035f), Quaternion.Euler(0f, 0f, 90f));
        CreatePrimitive(parent, PrimitiveType.Cube, "ServicePlate", new Vector3(0f, 0.055f, 0f), new Vector3(0.34f, 0.025f, 0.16f), Quaternion.identity);
        CreateLight(parent, lightColor, intensity, pulse);
    }

    private void BuildBoxFixture(Transform parent, Color lightColor, float intensity, bool pulse)
    {
        CreatePrimitive(parent, PrimitiveType.Cube, "ElectricalBox", Vector3.zero, new Vector3(0.24f, 0.10f, 0.16f), Quaternion.identity);
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Conduit", new Vector3(0.18f, 0f, 0f), new Vector3(0.025f, 0.18f, 0.025f), Quaternion.Euler(0f, 0f, 90f));
        CreateLight(parent, lightColor, intensity, pulse);
    }

    private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string objectName, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = localRotation;
        primitive.transform.localScale = localScale;

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        return primitive;
    }

    private void CreateLight(Transform parent, Color color, float intensity, bool pulse)
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
            pulseLights.Add(light);
            pulseSeeds.Add(pulseSeeds.Count * 1.73f + 0.61f);
        }
    }

    private void Update()
    {
        for (int i = 0; i < pulseLights.Count; i++)
        {
            Light light = pulseLights[i];
            if (light == null)
                continue;

            float baseIntensity = i == 0 ? 0.48f : 0.38f;
            float wave = 0.90f + 0.10f * Mathf.Sin(Time.time * 1.35f + pulseSeeds[i]);
            light.intensity = baseIntensity * wave;
        }
    }
}
