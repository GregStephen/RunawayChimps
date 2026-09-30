using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.SocialSafety
{
    // Instantiate authored assets under the existing room root. This preserves
    // sector loading visibility and scene-owned destruction without editing Hub_Base.
    public sealed class PlayerBoardHubPresenter : MonoBehaviour
    {
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                EnsureBoard(SceneManager.GetSceneAt(i));
        }

        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { EnsureBoard(scene); }

        private static void EnsureBoard(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "Hub_Base") return;
            Transform anchor = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                // A manually placed copy takes precedence, even while hidden by travel.
                foreach (var board in root.GetComponentsInChildren<PlayerBoard>(true))
                    if (!board.portable) return;
                if (root.name == "SpawnRoom") anchor = root.transform;
            }
            if (anchor == null)
            {
                Debug.LogError("Player board: Hub SpawnRoom root missing. Portable board remains available.");
                return;
            }
            var prefab = Resources.Load<GameObject>("SocialSafety/PlayerBoardHub");
            if (prefab == null)
            {
                Debug.LogError("Player board: Hub placement prefab missing. Portable board remains available.");
                return;
            }
            Instantiate(prefab, anchor, false);
        }
    }
}
