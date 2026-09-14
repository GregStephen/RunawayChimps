using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunawayChimps.Loading
{
    /// <summary>
    /// Cold-start-only, local presentation set. It owns no gameplay, Photon, XR or readiness state.
    /// Geometry is intentionally simple so composition can be judged before final Blender props exist.
    /// </summary>
    public sealed class SecurityWorkstationVignette : MonoBehaviour
    {
        // 1080 x 820 -> ~0.886 x 0.672 m, leaving a deliberate dark margin inside the 1.08 x 0.70 m screen inset.
        private const float MonitorCanvasScale = 0.00082f;
        private readonly List<Material> runtimeMaterials = new List<Material>();
        private Camera startupCamera;
        private TMP_FontAsset font;
        private int contentLayer;

        public RectTransform MonitorCanvasRoot { get; private set; }

        public static SecurityWorkstationVignette Create(
            Scene scene,
            Camera camera,
            TMP_FontAsset fontAsset,
            int layer)
        {
            if (camera == null) return null;

            var root = new GameObject("Security Workstation Vignette");
            root.layer = layer;
            SceneManager.MoveGameObjectToScene(root, scene);

            var vignette = root.AddComponent<SecurityWorkstationVignette>();
            vignette.Build(camera, fontAsset, layer);
            return vignette;
        }

        private void Build(Camera camera, TMP_FontAsset fontAsset, int layer)
        {
            startupCamera = camera;
            font = fontAsset;
            contentLayer = layer;

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            // Place once from the initial tracked pose. The desk is intentionally world-stationary after creation.
            transform.position = camera.transform.position + forward * 1.95f + Vector3.down * 0.14f;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            Material desk = CreateMaterial("Workstation dark laminate", new Color(0.055f, 0.070f, 0.064f));
            Material metal = CreateMaterial("Workstation monitor shell", new Color(0.070f, 0.095f, 0.084f));
            Material inset = CreateMaterial("Workstation monitor inset", new Color(0.016f, 0.029f, 0.024f));
            Material keys = CreateMaterial("Workstation keyboard", new Color(0.085f, 0.105f, 0.095f));
            Material paper = CreateMaterial("Workstation paper", new Color(0.50f, 0.48f, 0.37f));
            Material mug = CreateMaterial("Workstation mug", new Color(0.18f, 0.24f, 0.21f));
            Material badge = CreateMaterial("Workstation badge", new Color(0.28f, 0.45f, 0.37f));

            // Desk and pedestals. No colliders survive construction.
            Box("Desk top", new Vector3(0f, -0.72f, -0.08f), new Vector3(1.78f, 0.08f, 0.72f), desk);
            Box("Left desk pedestal", new Vector3(-0.62f, -1.14f, 0.02f), new Vector3(0.42f, 0.80f, 0.57f), desk);
            Box("Right desk pedestal", new Vector3(0.62f, -1.14f, 0.02f), new Vector3(0.42f, 0.80f, 0.57f), desk);
            Box("Desk modesty panel", new Vector3(0f, -1.03f, 0.29f), new Vector3(0.92f, 0.48f, 0.045f), metal);

            // Monitor body, stand and inset screen.
            Box("Security monitor shell", new Vector3(0f, -0.05f, 0.03f), new Vector3(1.22f, 0.84f, 0.14f), metal);
            Box("Security monitor inset", new Vector3(0f, -0.05f, -0.043f), new Vector3(1.08f, 0.70f, 0.012f), inset);
            Box("Monitor neck", new Vector3(0f, -0.53f, 0.055f), new Vector3(0.16f, 0.23f, 0.12f), metal);
            Box("Monitor base", new Vector3(0f, -0.655f, 0.02f), new Vector3(0.50f, 0.05f, 0.29f), metal);

            // Lived-in desk dressing. These are visual-only and intentionally non-interactive.
            Box("Keyboard", new Vector3(0f, -0.652f, -0.31f), new Vector3(0.76f, 0.035f, 0.22f), keys);
            Box("Keyboard space bar", new Vector3(0f, -0.630f, -0.355f), new Vector3(0.26f, 0.012f, 0.035f), metal);
            Cylinder("Night shift mug", new Vector3(0.61f, -0.57f, -0.19f), new Vector3(0.090f, 0.115f, 0.090f), mug);
            Box("Security badge", new Vector3(0.37f, -0.672f, -0.37f), new Vector3(0.19f, 0.018f, 0.12f), badge,
                Quaternion.Euler(0f, -12f, 0f));
            Box("Clipboard", new Vector3(-0.50f, -0.664f, -0.25f), new Vector3(0.36f, 0.022f, 0.26f), paper,
                Quaternion.Euler(0f, 11f, 0f));
            Box("Clipboard clip", new Vector3(-0.50f, -0.646f, -0.34f), new Vector3(0.12f, 0.012f, 0.035f), metal,
                Quaternion.Euler(0f, 11f, 0f));

            BuildMonitorCanvas();

            // Optional flavor only: none of this text is a puzzle/code/progression requirement.
            // These two notes sit on the physical bezel instead of floating beyond the monitor shell.
            BuildNote("Camera note", "CAM 04\nSTILL DEAD", new Vector3(-0.535f, 0.16f, -0.055f),
                new Vector2(0.145f, 0.105f), new Color(0.58f, 0.54f, 0.28f), Quaternion.Euler(0f, 180f, -5f));
            BuildNote("Vent note", "VENT B\nAGAIN?", new Vector3(0.535f, -0.02f, -0.055f),
                new Vector2(0.145f, 0.105f), new Color(0.43f, 0.54f, 0.36f), Quaternion.Euler(0f, 180f, 4f));
            BuildNote("Quit note", "IF THEY GET OUT\nI QUIT.", new Vector3(-0.47f, -0.635f, -0.27f),
                new Vector2(0.29f, 0.12f), new Color(0.53f, 0.45f, 0.31f), Quaternion.Euler(72f, 180f, 9f));
        }

        private void BuildMonitorCanvas()
        {
            var obj = new GameObject("Security Monitor World Canvas", typeof(RectTransform), typeof(Canvas));
            obj.layer = contentLayer;
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.localPosition = new Vector3(0f, -0.05f, -0.053f);
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

        private void BuildNote(
            string name,
            string text,
            Vector3 localPosition,
            Vector2 worldSize,
            Color background,
            Quaternion localRotation)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            obj.layer = contentLayer;
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.localPosition = localPosition;
            rect.localRotation = localRotation;
            rect.sizeDelta = new Vector2(240f, 150f);
            rect.localScale = new Vector3(worldSize.x / 240f, worldSize.y / 150f, 1f);

            var canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = startupCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;

            var backgroundImage = obj.AddComponent<Image>();
            backgroundImage.color = background;
            backgroundImage.raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.layer = contentLayer;
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 10f);
            textRect.offsetMax = new Vector2(-12f, -10f);

            var label = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = 31f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.10f, 0.12f, 0.10f, 0.94f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.richText = false;
            label.raycastTarget = false;
        }

        private Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogWarning("Security workstation could not find an Unlit/Color or Standard shader.", this);
                return null;
            }

            var material = new Material(shader)
            {
                name = name,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            runtimeMaterials.Add(material);
            return material;
        }

        private GameObject Box(
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            return Primitive(name, PrimitiveType.Cube, localPosition, localScale, material, localRotation);
        }

        private GameObject Cylinder(
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            return Primitive(name, PrimitiveType.Cylinder, localPosition, localScale, material, localRotation);
        }

        private GameObject Primitive(
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation)
        {
            GameObject obj = GameObject.CreatePrimitive(primitiveType);
            obj.name = name;
            obj.layer = contentLayer;
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = localRotation ?? Quaternion.identity;
            obj.transform.localScale = localScale;

            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                if (material != null) renderer.sharedMaterial = material;
            }

            return obj;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null) Destroy(runtimeMaterials[i]);
            }
            runtimeMaterials.Clear();
        }
    }
}
