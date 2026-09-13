using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit one-time scene migration for the Level 1 Crawler. It never runs automatically:
/// scene mutation/saving must be requested from the Tools menu so opening Unity cannot silently
/// rewrite Level1_Containment or fold unrelated unsaved work into a migration commit.
/// </summary>
public static class CrawlerLegacyVisualSceneCleanup
{
    private const string LevelOneScenePath = "Assets/Scenes/Level1_Containment.unity";
    private const string LegacyModelPathFragment = "MiniGamesKidFirstRig.fbx";

    [MenuItem("Tools/Runaway Chimps/Clean Legacy Level 1 Crawler Rig")]
    public static void CleanFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CrawlerLegacyVisualSceneCleanup] Stop Play Mode before cleaning the serialized Level 1 Crawler rig.");
            return;
        }

        Scene loadedScene = SceneManager.GetSceneByPath(LevelOneScenePath);
        bool openedTemporarily = !loadedScene.IsValid() || !loadedScene.isLoaded;

        if (!openedTemporarily && loadedScene.isDirty)
        {
            Debug.LogWarning(
                "[CrawlerLegacyVisualSceneCleanup] Level1_Containment has unsaved edits. Save or revert them first; the cleanup will not mutate/save a dirty scene automatically.");
            return;
        }

        Scene scene = openedTemporarily
            ? EditorSceneManager.OpenScene(LevelOneScenePath, OpenSceneMode.Additive)
            : loadedScene;

        try
        {
            int removed = CleanScene(scene);
            if (removed <= 0)
            {
                Debug.Log("[CrawlerLegacyVisualSceneCleanup] No explicit MiniGamesKid marker rig remains in Level1_Containment.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                Debug.LogError("[CrawlerLegacyVisualSceneCleanup] Legacy rig was removed in memory but Level1_Containment could not be saved.");
            else
                Debug.Log($"[CrawlerLegacyVisualSceneCleanup] Removed {removed} verified MiniGamesKid visual object/component(s) while preserving the Crawler gameplay root and unrelated visuals.");
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
            return;

        PrefabUtility.UnpackPrefabInstance(
            nearestRoot,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);

        Debug.Log(
            $"[CrawlerLegacyVisualSceneCleanup] Unpacked verified legacy model instance '{assetPath}' so its armature can be removed without deleting the gameplay root.",
            gameplayRoot);
    }
}
