using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class LoadingFallbackCamera : MonoBehaviour
{
    public string loadingSceneName = "Loading";

    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Apply(SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        Apply(newScene.name);
    }

    private void Apply(string activeSceneName)
    {
        // Enabled only while Loading is active
        _cam.enabled = activeSceneName == loadingSceneName &&
            !(RunawayChimps.Travel.SectorTravelService.I != null &&
              RunawayChimps.Travel.SectorTravelService.I.IsBusy);
    }
}
