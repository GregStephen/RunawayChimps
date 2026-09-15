using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace RunawayChimps.Loading
{
    /// <summary>
    /// Cold-start-only, local presentation anchor for the approved flat green security boot.
    /// It owns no gameplay, Photon, XR or readiness state and never follows ordinary head look.
    /// </summary>
    public sealed class SecurityWorkstationVignette : MonoBehaviour
    {
        // Keep the same physical terminal size as the previous monitor canvas, but place it at
        // twice the former 1.95 m workstation distance so it reads as a calmer, farther panel.
        private const float TerminalDistance = 3.90f;
        private const float MonitorCanvasScale = 0.00082f;

        private Camera startupCamera;
        private int contentLayer;

        public RectTransform MonitorCanvasRoot { get; private set; }

        public static SecurityWorkstationVignette Create(
            Camera camera,
            TMP_FontAsset fontAsset,
            int layer)
        {
            if (camera == null) return null;
            var origin = camera.GetComponentInParent<XROrigin>();
            if (origin == null) return null;

            var root = new GameObject("Security Boot Panel Vignette");
            root.layer = layer;
            DontDestroyOnLoad(root);
            root.transform.SetParent(origin.transform, true);

            var vignette = root.AddComponent<SecurityWorkstationVignette>();
            vignette.Build(camera, layer);
            if (vignette.MonitorCanvasRoot != null) return vignette;
            Destroy(root);
            return null;
        }

        private void Build(Camera camera, int layer)
        {
            startupCamera = camera;
            contentLayer = layer;

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            // The panel is fixed to the initial horizontal viewing direction. Hidden XROrigin
            // relocation carries it during startup, but ordinary head/room-scale motion does not.
            transform.position = camera.transform.position + forward * TerminalDistance;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            BuildMonitorCanvas();
        }

        private void BuildMonitorCanvas()
        {
            var obj = new GameObject("Security Boot World Canvas", typeof(RectTransform), typeof(Canvas));
            obj.layer = contentLayer;

            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rect.localScale = Vector3.one * MonitorCanvasScale;
            rect.sizeDelta = new Vector2(1080f, 820f);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            var canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = startupCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;

            MonitorCanvasRoot = rect;
        }
    }
}
