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
        // Level 1 was split out of Hub_Base without any serialized Light components.
        // Give the standalone scene a dim, cool baseline so it remains readable after
        // the Hub unloads while leaving room for authored horror lighting later.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.16f, 1f);
        RenderSettings.ambientIntensity = 1f;
        EnsureFillLight(scene);
    }

    private static void EnsureFillLight(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == FillLightName)
                return;

        var lightObject = new GameObject(FillLightName);
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.42f, 0.48f, 0.60f, 1f);
        light.intensity = 0.22f;
        light.shadows = LightShadows.None;
    }
}
