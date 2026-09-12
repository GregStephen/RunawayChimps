using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class Level1LightingBootstrap
{
    private const string LevelOneScene = "Level1_Containment";
    private const string FillLightName = "Level1_Runtime_Fill";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != LevelOneScene)
            return;

        EnsureFillLight(scene);
        if (SceneManager.GetActiveScene() == scene)
            ApplyAmbient(scene);
    }

    private static void OnActiveSceneChanged(Scene previous, Scene current)
    {
        if (current.name == LevelOneScene)
            ApplyAmbient(current);
    }

    private static void ApplyAmbient(Scene scene)
    {
        // Level 1 was split out of Hub_Base without serialized Light components.
        // The original emergency fallback was still too dark in actual play. Keep
        // the cool horror baseline, but raise the floor enough to read walls, vents,
        // junctions and the Crawler without requiring a flashlight just to navigate.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.25f, 1f);
        RenderSettings.ambientIntensity = 1f;
        EnsureFillLight(scene);
    }

    private static void EnsureFillLight(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != FillLightName) continue;

            var existing = root.GetComponent<Light>();
            if (existing != null) ConfigureFill(existing);
            return;
        }

        var lightObject = new GameObject(FillLightName);
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        ConfigureFill(lightObject.AddComponent<Light>());
    }

    private static void ConfigureFill(Light light)
    {
        light.type = LightType.Directional;
        light.color = new Color(0.50f, 0.56f, 0.68f, 1f);
        light.intensity = 0.55f;
        light.shadows = LightShadows.None;
    }
}
