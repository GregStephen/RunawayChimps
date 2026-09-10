using UnityEngine;

public class HallwayPreloadTrigger : MonoBehaviour
{
    public string levelSceneName = "Level1";
    public bool preloadOnce = true;

    private bool _done;

    private void OnTriggerEnter(Collider other)
    {
        // Works with hands/camera/body because we search up to the rig root
        var marker = other.GetComponentInParent<LocalRigMarker>();
        if (marker == null)
        {
            Debug.Log($"[HallwayPreloadTrigger] Ignoring '{other.name}' (not local rig). tag={other.tag}");
            return;
        }

        if (preloadOnce && _done) return;

        if (LevelStreamService.I == null)
        {
            Debug.LogError("[HallwayPreloadTrigger] LevelStreamService.I is NULL (missing in Bootstrap/DDOL).");
            return;
        }

        Debug.Log($"[HallwayPreloadTrigger] Local rig entered. Preloading '{levelSceneName}'...");
        if (!Application.CanStreamedLevelBeLoaded(levelSceneName))
        {
            Debug.LogWarning("Preload scene is not enabled: " + levelSceneName, this);
            return;
        }
        _done = LevelStreamService.I.Preload(levelSceneName);
    }
}
