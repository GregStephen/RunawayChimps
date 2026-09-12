using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunawayChimps.Travel
{
    /// <summary>
    /// Adds an opaque overscanned backdrop behind the existing Loading-scene UI.
    /// Screen-space camera canvases can expose a thin edge in XR when their visible
    /// content is authored exactly to the nominal viewport. Overscan keeps both eye
    /// views covered without replacing the existing loading text/presentation.
    /// </summary>
    internal static class LoadingCanvasOverscan
    {
        private const string LoadingSceneName = "Loading";
        private const string BackdropName = "XR_Loading_Backdrop";
        private const float Overscan = 0.12f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.isLoaded || scene.name != LoadingSceneName)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                    EnsureBackdrop(canvas);
            }
        }

        private static void EnsureBackdrop(Canvas canvas)
        {
            if (canvas == null)
                return;

            Transform existing = canvas.transform.Find(BackdropName);
            RectTransform rect;
            Image image;

            if (existing != null)
            {
                rect = existing as RectTransform;
                image = existing.GetComponent<Image>();
            }
            else
            {
                GameObject backdrop = new GameObject(
                    BackdropName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                rect = backdrop.GetComponent<RectTransform>();
                image = backdrop.GetComponent<Image>();
                rect.SetParent(canvas.transform, false);
            }

            if (rect == null || image == null)
                return;

            // Stretch beyond all four viewport edges. The extra horizontal coverage is
            // intentional for stereo/XR where each eye can expose a sliver at an exact edge.
            rect.anchorMin = new Vector2(-Overscan, -Overscan);
            rect.anchorMax = new Vector2(1f + Overscan, 1f + Overscan);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();

            image.color = Color.black;
            image.raycastTarget = false;
            image.maskable = false;
        }
    }
}
