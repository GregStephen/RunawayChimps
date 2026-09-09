using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public class RebindInteractorsToSceneManager : MonoBehaviour
{
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Rebind(); // in case we're already in a scene with a manager
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Rebind();

    private void Rebind()
    {
        var travel = RunawayChimps.Travel.SectorTravelService.I;
        var manager = travel != null ? travel.InteractionManager :
            Object.FindFirstObjectByType<XRInteractionManager>(FindObjectsInactive.Exclude);
        if (!manager) return;

        foreach (var interactor in GetComponentsInChildren<XRBaseInteractor>(true))
            interactor.interactionManager = manager;

        foreach (var interactable in Object.FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            interactable.interactionManager = manager;
    }
}
