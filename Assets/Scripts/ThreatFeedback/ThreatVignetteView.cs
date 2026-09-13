using UnityEngine;
using UnityEngine.UI;

namespace RunawayChimps.ThreatFeedback
{
    /// <summary>
    /// Cheap camera-specific XR presentation. ScreenSpaceCamera keeps the overlay on the
    /// tracked local camera instead of placing red world geometry that security cameras
    /// or other local cameras could accidentally render.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatVignetteView : MonoBehaviour
    {
        private const string ViewName = "Local_Threat_Vignette";
        private const string GraphicName = "ThreatVignetteGraphic";
        private static readonly int ThreatId = Shader.PropertyToID("_Threat");

        private Canvas canvas;
        private RawImage graphic;
        private Material material;
        private Camera boundCamera;

        public static ThreatVignetteView Ensure(Camera camera)
        {
            if (camera == null)
                return null;

            Transform existing = camera.transform.Find(ViewName);
            ThreatVignetteView view = existing != null ? existing.GetComponent<ThreatVignetteView>() : null;
            if (view == null)
            {
                GameObject root = new GameObject(ViewName, typeof(RectTransform), typeof(Canvas), typeof(ThreatVignetteView));
                root.transform.SetParent(camera.transform, false);
                root.layer = camera.gameObject.layer;
                view = root.GetComponent<ThreatVignetteView>();
            }

            view.BindCamera(camera);
            return view;
        }

        private void Awake()
        {
            Camera camera = GetComponentInParent<Camera>();
            if (camera != null)
                BindCamera(camera);
        }

        private void BindCamera(Camera camera)
        {
            if (camera == null)
                return;

            boundCamera = camera;
            canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(0.6f, camera.nearClipPlane + 0.1f);
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;

            EnsureGraphic();
            EnsureMaterial();
            SetThreat(0f);
        }

        private void EnsureGraphic()
        {
            if (graphic != null)
                return;

            Transform existing = transform.Find(GraphicName);
            if (existing != null)
                graphic = existing.GetComponent<RawImage>();

            if (graphic == null)
            {
                GameObject child = new GameObject(GraphicName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                child.transform.SetParent(transform, false);
                child.layer = gameObject.layer;
                graphic = child.GetComponent<RawImage>();
            }

            RectTransform rect = graphic.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            graphic.raycastTarget = false;
            graphic.color = Color.white;
        }

        private void EnsureMaterial()
        {
            if (material != null)
                return;

            Shader shader = Resources.Load<Shader>("ThreatFeedback/ThreatVignette");
            if (shader == null)
                shader = Shader.Find("RunawayChimps/ThreatVignette");
            if (shader == null)
            {
                Debug.LogError("Threat vignette shader is missing.", this);
                enabled = false;
                return;
            }

            material = new Material(shader) { name = "Runtime Threat Vignette" };
            if (graphic != null)
                graphic.material = material;
        }

        public void SetThreat(float threat)
        {
            if (boundCamera == null)
                boundCamera = GetComponentInParent<Camera>();
            if (boundCamera != null && (canvas == null || graphic == null || material == null))
            {
                canvas = GetComponent<Canvas>();
                EnsureGraphic();
                EnsureMaterial();
            }

            threat = Mathf.Clamp01(threat);
            if (material != null)
                material.SetFloat(ThreatId, threat);
            if (graphic != null)
                graphic.enabled = threat > 0.001f;
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
