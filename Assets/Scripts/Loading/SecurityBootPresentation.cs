using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunawayChimps.Loading
{
    /// <summary>Scene-owned, local-only terminal. Observes startup without advancing it.</summary>
    public sealed class SecurityBootPresentation : MonoBehaviour
    {
        private static readonly Color Phosphor = new Color(0.52f, 0.82f, 0.66f);
        private static readonly Color Muted = new Color(0.30f, 0.45f, 0.39f);
        private static readonly Color Amber = new Color(0.88f, 0.65f, 0.31f);
        private static readonly Color Fault = new Color(0.94f, 0.53f, 0.38f);
        private const float DesignWidth = 1080f;
        private const float DesignHeight = 820f;
        private Canvas hostCanvas;
        private CanvasGroup backdropGroup;
        private CanvasGroup terminalGroup;
        private RectTransform terminal;
        private TMP_FontAsset font;
        private TMP_Text headline;
        private TMP_Text detail;
        private TMP_Text instruction;
        private Image cursor;
        private readonly TMP_Text[] stageLabels = new TMP_Text[5];
        private readonly Image[] stageLights = new Image[5];
        private readonly bool[] stages = new bool[5];
        private AudioSource bootAudio;
        private AudioClip tick;
        private Camera boundCamera;
        private bool cameraMasked;
        private int savedCullingMask;
        private CameraClearFlags savedClearFlags;
        private Color savedBackground;
        private bool startupMode;
        private bool fading;
        private float appearedAt;
        private float nextSoundAt;
        private int previousBits;
        private string previousError;

        public static SecurityBootPresentation Install(Scene scene, TMP_Text legacyStatus, bool startup, bool sounds)
        {
            Canvas canvas = legacyStatus != null ? legacyStatus.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    canvas = root.GetComponentInChildren<Canvas>(true);
                    if (canvas != null) break;
                }
            }
            if (canvas == null)
            {
                Debug.LogWarning("Security boot needs the Loading canvas; retaining the legacy status fallback.");
                return null;
            }
            var view = canvas.GetComponent<SecurityBootPresentation>();
            if (view != null) return view;
            view = canvas.gameObject.AddComponent<SecurityBootPresentation>();
            view.Configure(scene, canvas, legacyStatus, startup, sounds);
            return view;
        }

        private void Configure(Scene scene, Canvas canvas, TMP_Text legacyStatus, bool startup, bool sounds)
        {
            hostCanvas = canvas;
            startupMode = startup;
            appearedAt = Time.unscaledTime;
            font = legacyStatus != null ? legacyStatus.font : TMP_Settings.defaultFontAsset;
            backdropGroup = canvas.GetComponent<CanvasGroup>();
            if (backdropGroup == null) backdropGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            backdropGroup.alpha = 1f;
            backdropGroup.interactable = false;
            backdropGroup.blocksRaycasts = false;
            // Disable renderers, not only strings: no stale daily tip/debug/status flash.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (LoadingDebugText debug in root.GetComponentsInChildren<LoadingDebugText>(true))
                    debug.enabled = false;
                foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                    label.enabled = false;
            }
            // Travel keeps the existing black background and XR overscan, with no terminal or audio.
            if (!startupMode) return;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1400f, 1000f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            BuildTerminal();
            BindStartupCamera();
            if (sounds) BuildAudio();
        }

        private void BuildTerminal()
        {
            terminal = Rect("Security Boot Prototype", hostCanvas.transform, 0, 0, DesignWidth, DesignHeight);
            terminal.anchorMin = terminal.anchorMax = terminal.pivot = new Vector2(0.5f, 0.5f);
            terminal.anchoredPosition = Vector2.zero;
            terminalGroup = terminal.gameObject.AddComponent<CanvasGroup>();
            terminalGroup.alpha = 0f;
            Box("Bezel", terminal, 0, 0, 1080, 820, new Color(0.10f, 0.16f, 0.14f));
            Box("Screen", terminal, 8, 8, 1064, 804, new Color(0.018f, 0.034f, 0.029f));
            Box("Power strip", terminal, 8, 8, 1064, 5, Muted);
            Label("Asset tag", "RC  /  SECURITY CONTROL", 48, 40, 730, 32, 22, Muted);
            Label("Revision", "BOOT / 01", 822, 40, 210, 32, 22, Amber, TextAlignmentOptions.TopRight);
            Label("Title", "RUNAWAY CHIMPS", 44, 94, 990, 72, 55, Phosphor);
            Label("System", "FACILITY SECURITY NETWORK", 48, 172, 984, 36, 27, Phosphor);
            Box("Header divider", terminal, 48, 220, 984, 2, Muted);
            Label("Stage heading", "STARTUP DIAGNOSTICS", 48, 244, 710, 28, 20, Muted);
            Label("State heading", "STATUS", 804, 244, 228, 28, 20, Muted, TextAlignmentOptions.TopRight);
            string[] names = { "FACILITY LINK", "SECURITY SECTOR", "ARRIVAL ALIGNMENT", "AVATAR LINK", "VISUAL SYSTEMS" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = 287 + i * 52;
                Label("Stage " + i, names[i], 48, y, 680, 38, 28, Phosphor);
                stageLabels[i] = Label("State " + i, "WAITING", 744, y, 288, 38, 28, Muted, TextAlignmentOptions.TopRight);
                stageLights[i] = Box("Progress " + i, terminal, 48 + i * 200, 558, 184, 8, Muted);
            }
            headline = Label("Headline", "ESTABLISHING FACILITY LINK", 48, 594, 984, 34, 25, Amber);
            detail = Label("Detail", "Starting facility systems...", 48, 637, 984, 79, 24, Phosphor);
            detail.enableWordWrapping = true;
            instruction = Label("Instruction", "AUTOMATIC ENTRY WHEN READY", 48, 731, 984, 34, 23, Muted);
            Label("Footer", "LOCAL ACCESS TERMINAL  /  RECOVERY SESSION", 48, 780, 920, 26, 18, Muted);
            cursor = Box("Activity cursor", terminal, 1008, 787, 22, 7, Phosphor);
        }

        /// <summary>Segments are real prerequisites, not an estimated load percentage.</summary>
        public void Present(AppState state, bool hubLoaded, bool inRoom, string error, bool reviewHeld)
        {
            if (!startupMode || terminal == null) return;
            stages[0] = inRoom;
            stages[1] = hubLoaded;
            stages[2] = state != null && state.RigSnapped;
            stages[3] = state != null && state.PhotonPlayerSpawned;
            stages[4] = state != null && state.PlayerVisualsReady;
            bool failed = !string.IsNullOrEmpty(error);
            bool ready = !failed && state != null && state.IsReady && hubLoaded && inRoom;
            int bits = 0;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i]) bits |= 1 << i;
                SetLabel(stageLabels[i], stages[i] ? "ONLINE" : "WAITING", stages[i] ? Phosphor : Muted);
                stageLights[i].color = stages[i] ? Phosphor : new Color(0.10f, 0.17f, 0.14f);
            }
            if (bits != previousBits && (bits & ~previousBits) != 0 && !failed && Time.unscaledTime >= nextSoundAt)
            {
                if (bootAudio != null && tick != null) bootAudio.PlayOneShot(tick);
                nextSoundAt = Time.unscaledTime + 0.18f;
            }
            previousBits = bits;
            previousError = error;
            SetLabel(headline, failed ? "STARTUP INTERRUPTED" : ready ?
                (reviewHeld ? "ACCESS READY / EDITOR REVIEW HOLD" : "ACCESS GRANTED") : "RESTORING FACILITY ACCESS",
                failed ? Fault : ready ? Phosphor : Amber);
            string status = failed ? error : ready ? "Security control is ready." :
                !hubLoaded ? "Loading security sector..." : state != null ? state.Status : "Waiting for Bootstrap...";
            if (string.IsNullOrEmpty(status)) status = "Waiting for startup services...";
            if (status.Length > 240) status = status.Substring(0, 237) + "...";
            SetLabel(detail, status, failed ? Fault : Phosphor);
            SetLabel(instruction, failed ? "RETRY: EITHER TRIGGER  /  DESKTOP: R" : reviewHeld ?
                "EDITOR REVIEW HOLD: TOGGLE THE TOOLS MENU OFF TO CONTINUE" : "AUTOMATIC ENTRY WHEN READY",
                failed ? Amber : Muted);
        }

        private void LateUpdate()
        {
            if (!startupMode || terminal == null) return;
            BindStartupCamera();
            Vector2 size = ((RectTransform)hostCanvas.transform).rect.size;
            float scale = Mathf.Min(size.x * 0.72f / DesignWidth, size.y * 0.84f / DesignHeight);
            terminal.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            if (!fading) terminalGroup.alpha = Mathf.Clamp01((Time.unscaledTime - appearedAt) / 0.3f);
            // Slow, low-contrast activity cue; no scanline shader, strobe or camera motion.
            Color c = string.IsNullOrEmpty(previousError) ? Phosphor : Amber;
            c.a = 0.55f + 0.18f * Mathf.Sin(Time.unscaledTime * 2f);
            cursor.color = c;
        }

        private void BindStartupCamera()
        {
            if (boundCamera != null && boundCamera.isActiveAndEnabled) return;
            var player = GorillaLocomotion.Player.Instance;
            var origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
            if (origin == null || origin.Camera == null || !origin.Camera.isActiveAndEnabled) return;
            RestoreCameraForReveal();
            boundCamera = origin.Camera;
            hostCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hostCanvas.worldCamera = boundCamera;
            hostCanvas.planeDistance = Mathf.Max(1.5f, boundCamera.nearClipPlane + 0.1f);
            MaskStartupCamera();
            // Use the persistent tracked camera for both eyes and desktop; no second rig.
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (LoadingFallbackCamera fallback in root.GetComponentsInChildren<LoadingFallbackCamera>(true))
                {
                    fallback.enabled = false;
                    fallback.GetComponent<Camera>().enabled = false;
                }
        }

        private void MaskStartupCamera()
        {
            if (boundCamera == null || cameraMasked) return;
            savedCullingMask = boundCamera.cullingMask;
            savedClearFlags = boundCamera.clearFlags;
            savedBackground = boundCamera.backgroundColor;
            cameraMasked = true;
            boundCamera.cullingMask = 1 << hostCanvas.gameObject.layer;
            boundCamera.clearFlags = CameraClearFlags.SolidColor;
            boundCamera.backgroundColor = Color.black;
        }

        public void RestoreCameraForReveal()
        {
            if (!cameraMasked) return;
            if (boundCamera != null)
            {
                boundCamera.cullingMask = savedCullingMask;
                boundCamera.clearFlags = savedClearFlags;
                boundCamera.backgroundColor = savedBackground;
            }
            cameraMasked = false;
        }

        public void SetTerminalOpacity(float alpha)
        {
            fading = true;
            if (terminalGroup != null) terminalGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void SetBackdropOpacity(float alpha)
        {
            if (backdropGroup != null) backdropGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void RestoreAfterInterruptedEntry()
        {
            fading = false;
            SetBackdropOpacity(1f);
            MaskStartupCamera();
            if (terminalGroup != null) terminalGroup.alpha = 1f;
        }

        private void BuildAudio()
        {
            const int rate = 22050;
            const int count = 1323;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Sin(Mathf.PI * i / (count - 1)) * Mathf.Exp(-45f * t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 680f * t) * envelope;
            }
            tick = AudioClip.Create("Security boot soft relay", count, 1, rate, false);
            tick.SetData(samples, 0);
            bootAudio = gameObject.AddComponent<AudioSource>();
            bootAudio.playOnAwake = false;
            bootAudio.loop = false;
            bootAudio.spatialBlend = 0f;
            bootAudio.volume = 0.045f;
            bootAudio.dopplerLevel = 0f;
        }

        private void OnDestroy()
        {
            RestoreCameraForReveal();
            if (bootAudio != null) bootAudio.Stop();
            if (tick != null) Destroy(tick);
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private static Image Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(string name, string text, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var label = Rect(name, terminal, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size;
            label.fontStyle = FontStyles.Normal;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = false;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static void SetLabel(TMP_Text label, string text, Color color)
        {
            if (label == null) return;
            if (label.text != text) label.text = text;
            if (label.color != color) label.color = color;
        }
    }
}
