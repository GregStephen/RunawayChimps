using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime safety net for projects/scenes that have not yet had the editor migration saved.
/// It waits until CrawlerVisualController has attached Zombie Crawl, then removes the old
/// MiniGamesKid renderer/Animator/armature while preserving gameplay components.
/// </summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class CrawlerLegacyVisualRuntimeCleanup : MonoBehaviour
{
    private const string LevelOneSceneName = "Level1_Containment";
    private const int MaximumWaitFrames = 180;

    private CrawlerVisualController visualController;
    private int waitFrames;
    private bool finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != LevelOneSceneName)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            MonsterNavigation[] monsters = roots[r].GetComponentsInChildren<MonsterNavigation>(true);
            for (int i = 0; i < monsters.Length; i++)
            {
                MonsterNavigation monster = monsters[i];
                if (monster == null || monster.gameObject.scene != scene)
                    continue;

                if (monster.GetComponent<CrawlerLegacyVisualRuntimeCleanup>() == null)
                    monster.gameObject.AddComponent<CrawlerLegacyVisualRuntimeCleanup>();
            }
        }
    }

    private void Awake()
    {
        visualController = GetComponent<CrawlerVisualController>();
    }

    private void LateUpdate()
    {
        if (finished)
            return;

        if (visualController == null)
            visualController = GetComponent<CrawlerVisualController>();

        Transform zombie = visualController != null ? visualController.VisualRoot : null;
        if (zombie == null)
        {
            waitFrames++;
            if (waitFrames < MaximumWaitFrames)
                return;

            Debug.LogError(
                $"{name}: Zombie Crawl was not attached after {MaximumWaitFrames} frames; legacy visual rig was left intact so the failure stays visible for diagnosis.",
                this);
            finished = true;
            return;
        }

        int removed = CrawlerLegacyVisualCleaner.RemoveLegacyVisuals(
            gameObject,
            zombie,
            immediate: false);

        bool legacyStillPresent = CrawlerLegacyVisualCleaner.ContainsLegacyVisualRig(gameObject, zombie);
        if (legacyStillPresent)
        {
            Debug.LogWarning(
                $"{name}: removed {removed} obsolete legacy visual object/component(s), but part of the old rig was retained because it also owns a gameplay/physics/audio component. Inspect the remaining branch before deleting it manually.",
                this);
        }
        else if (removed > 0)
        {
            Debug.Log(
                $"{name}: removed {removed} obsolete MiniGamesKid visual object/component(s). Runtime hierarchy is now the Crawler gameplay root plus Zombie Crawl visual rig.",
                this);
        }

        finished = true;
        Destroy(this);
    }
}
