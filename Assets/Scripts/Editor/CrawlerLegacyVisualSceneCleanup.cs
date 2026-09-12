using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-time scene migration for the Level 1 Crawler. The gameplay object historically
/// came from MiniGamesKidFirstRig, so replacing only its renderer left the old armature
/// serialized beneath Crawler. This migration unpacks that old model instance when needed
/// and removes only its visual rig while preserving the gameplay root/components.
/// </summary>
[InitializeOnLoad]
public static class CrawlerLegacyVisualSceneCleanup
{
    private const string LevelOneScenePath = "Assets/Scenes/Level1_Containment.unity";
    private const string LegacyModelPathFragment = "MiniGamesKidFirstRig.fbx";
    private const string SessionKey = "RunawayChimps.CrawlerLegacyVisualSceneCleanup.V1";

    static CrawlerLegacyVisualSceneCleanup()
    {
        EditorApplication.delayCall += TryAutomaticCleanup;
    }

    [MenuItem("Tools/Runaway Chimps/Clean Legacy Level 1 Crawler Rig")]
    public static void CleanFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CrawlerLegacyVisualSceneCleanup] Stop Play Mode before cleaning the serialized Level 1 Crawler rig.");
            return;
        }

        CleanLevelOneScene(forceLoadedDirtyScene: true, logWhenClean: true);
    }

    private static void TryAutomaticCleanup()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutomaticCleanup;
            return;
        }

        SessionState.SetBool(SessionKey, true);
        CleanLevelOneScene(forceLoadedDirtyScene: false, logWhenClean: false);
    }

    private static void CleanLevelOneScene(bool forceLoadedDirtyScene, bool logWhenClean)
    {
        Scene loadedScene = SceneManager.GetSceneByPath(LevelOneScenePath);
        bool openedTemporarily = !loadedScene.IsValid() || !loadedScene.isLoaded;

        if (!openedTemporarily && loadedScene.isDirty && !forceLoadedDirtyScene)
        {
            Debug.LogWarning(
                "[CrawlerLegacyVisualSceneCleanup] Level1_Containment has unsaved edits, so automatic legacy-rig cleanup was skipped. " +
                "Save/reopen the scene or run Tools > Runaway Chimps > Clean Legacy Level 1 Crawler Rig when ready.");
            return;
        }

        Scene scene = openedTemporarily
            ? EditorSceneManager.OpenScene(LevelOneScenePath, OpenSceneMode.Additive)
            : loadedScene;

        try
        {
            int removed = CleanScene(scene);
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    Debug.LogError("[CrawlerLegacyVisualSceneCleanup] Legacy rig was removed in memory but Level1_Containment could not be saved.");
                else
                    Debug.Log($"[CrawlerLegacyVisualSceneCleanup] Removed {removed} obsolete MiniGamesKid visual object/component(s) from Level1_Containment while preserving the Crawler gameplay root.");
            }
            else if (logWhenClean)
            {
                Debug.Log("[CrawlerLegacyVisualSceneCleanup] Level1_Containment contains no removable legacy Crawler visual rig.");
            }
        }
        finally
        {
            if (openedTemporarily && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static int CleanScene(Scene scene)
    {
        int removed = 0;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int r = 0; r < roots.Length; r++)
        {
            MonsterNavigation[] monsters = roots[r].GetComponentsInChildren<MonsterNavigation>(true);
            for (int i = 0; i < monsters.Length; i++)
            {
                MonsterNavigation monster = monsters[i];
                if (monster == null || monster.gameObject.scene != scene)
                    continue;

                // Level 1 currently has one Crawler navigation root. Avoid touching any
                // future monster that does not contain the known legacy MiniGamesKid rig.
                if (!CrawlerLegacyVisualCleaner.ContainsLegacyVisualRig(monster.gameObject))
                    continue;

                UnpackLegacyModelInstanceIfNeeded(monster.gameObject);
                removed += CrawlerLegacyVisualCleaner.RemoveLegacyVisuals(
                    monster.gameObject,
                    keepVisual: null,
                    immediate: true);
            }
        }

        return removed;
    }

    private static void UnpackLegacyModelInstanceIfNeeded(GameObject gameplayRoot)
    {
        if (gameplayRoot == null || !PrefabUtility.IsPartOfPrefabInstance(gameplayRoot))
            return;

        GameObject nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameplayRoot);
        if (nearestRoot == null)
            return;

        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameplayRoot);
        if (string.IsNullOrEmpty(assetPath) ||
            assetPath.IndexOf(LegacyModelPathFragment, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        PrefabUtility.UnpackPrefabInstance(
            nearestRoot,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);

        Debug.Log(
            $"[CrawlerLegacyVisualSceneCleanup] Unpacked obsolete model instance '{assetPath}' so its armature can be removed without deleting the Crawler gameplay root.",
            gameplayRoot);
    }
}
