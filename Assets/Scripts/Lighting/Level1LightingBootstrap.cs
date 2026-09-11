using UnityEngine;
using UnityEngine.Rendering;

public static class Level1LightingBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Apply()
    {
        if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName("Level1_Containment").isLoaded)
            return;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.16f, 1f);
        RenderSettings.ambientIntensity = 1f;

        var existing = GameObject.Find("Level1_Runtime_Fill");
        if (existing != null)
            return;

        var lightObject = new GameObject("Level1_Runtime_Fill");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.42f, 0.48f, 0.60f, 1f);
        light.intensity = 0.22f;
        light.shadows = LightShadows.None;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }
}
