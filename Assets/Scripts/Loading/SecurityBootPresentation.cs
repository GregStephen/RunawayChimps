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
        private static readonly string[] StageNames =
        {
            "FACILITY LINK", "SECURITY SECTOR", "ARRIVAL ALIGNMENT", "AVATAR LINK", "VISUAL SYSTEMS"
        };

        private const string PresentationLayerName = "LoadingPresentation";
        private const float DesignWidth = 1080f;
        private const float DesignHeight = 820f;
        private const int StaticWidth = 64;
        private const int StaticHeight = 48;
        private const float StaticRefreshInterval = 0.10f;
        private const float InterferenceSweepDuration = 0.42f;

        private Canvas hostCanvas;
        private CanvasGroup backdropGroup;
        private CanvasGroup terminalGroup;
        private RectTransform terminal;
        private TMP_FontAsset font;
        private TMP_Text headline;
        private TMP_Text detail;
        private TMP_Text instruction;
        private Image cursor;
        private RawImage staticNoise;
        private Image interferenceLine;
        private Texture2D staticTexture;
        private Color32[] staticPixels;
        private SecurityWorkstationVignette workstation;
        private readonly TMP_Text[] stageLabels = new TMP_Text[5];
        private readonly Image[] stageLights = new Image[5];
        private readonly bool[] stages = new bool[5];
        private readonly float[] stageReachedAt = { -1f, -1f, -1f, -1f, -1f };
        private AudioSource bootAudio;
        private AudioClip tick;
        private Camera boundCamera;
        private bool cameraMasked;
        private int presentationLayer = -1;
        private int savedCullingMask;
        private CameraClearFlags savedClearFlags;
        private Color savedBackground;
        private bool startupMode;
        private bool fading;
        private bool readyLogged;
        private float appearedAt;
        private float nextSoundAt;
        private float nextStaticRefreshAt;
        private float staticBurstUntil;
        private float nextInterferenceAt;
        private float interferenceStartedAt = -1f;
        private int previousBits;
        private string previousError;
        private uint noiseState = 0x6D2B79F5u;

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
            hostCanvas.overrideSorting = true;
            hostCanvas.sortingOrder = 32000;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (LoadingDebugText debug in root.GetComponentsInChildren<LoadingDebugText>(true))
                    debug.enabled = false;
                foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                    label.enabled = false;
            }

            // Normal sector travel intentionally stops here: black authored backdrop only, no workstation/boot/audio.
            if (!startupMode) return;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1400f, 1000f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            BindStartupCamera();
            if (sounds) BuildAudio();
        }

        private void EnsureWorkstation()
        {
            if (!startupMode || workstation != null || boundCamera == null || presentationLayer < 0) return;

            workstation = SecurityWorkstationVignette.Create(
                gameObject.scene,
                boundCamera,
                font,
                presentationLayer);
            if (workstation == null || workstation.MonitorCanvasRoot == null) return;

            BuildTerminal(workstation.MonitorCanvasRoot);
            // Camera clear is black and only the dedicated presentation layer is visible, so the desk floats in safe darkness.
            SetBackdropOpacity(0f);
        }

        private void BuildTerminal(Transform parent)
        {
            terminal = Rect("Security Boot Terminal", parent, 0, 0, DesignWidth, DesignHeight);
            terminal.anchorMin = terminal.anchorMax = terminal.pivot = new Vector2(0.5f, 0.5f);
            terminal.anchoredPosition = Vector2.zero;
            terminal.localScale = Vector3.one;
            terminalGroup = terminal.gameObject.AddComponent<CanvasGroup>();
            terminalGroup.alpha = 0f;

            Box("CRT face", terminal, 0, 0, 1080, 820, new Color(0.10f, 0.16f, 0.14f));
            Box("Screen", terminal, 8, 8, 1064, 804, new Color(0.018f, 0.034f, 0.029f));
            BuildCrtTreatment();
            Box("Power strip", terminal, 8, 8, 1064, 5, Muted);
            Label("Asset tag", "RC  /  SECURITY CONTROL", 48, 40, 730, 32, 22, Muted);
            Label("Revision", "BOOT / 01", 822, 40, 210, 32, 22, Amber, TextAlignmentOptions.TopRight);
            Label("Title", "RUNAWAY CHIMPS", 44, 94, 990, 72, 55, Phosphor);
            Label("System", "FACILITY SECURITY NETWORK", 48, 172, 984, 36, 27, Phosphor);
            Box("Header divider", terminal, 48, 220, 984, 2, Muted);
            Label("Stage heading", "STARTUP DIAGNOSTICS", 48, 244, 710, 28, 20, Muted);
            Label("State heading", "STATUS", 804, 244, 228, 28, 20, Muted, TextAlignmentOptions.TopRight);

            for (int i = 0; i < StageNames.Length; i++)
            {
                float y = 287 + i * 52;
                Label("Stage " + i, StageNames[i], 48, y, 680, 38, 28, Phosphor);
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

        private void BuildCrtTreatment()
        {
            staticTexture = new Texture2D(StaticWidth, StaticHeight, TextureFormat.RGBA32, false)
            {
                name = "Security Boot Procedural Static",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.DontSave
            };
            staticPixels = new Color32[StaticWidth * StaticHeight];
            RefreshStaticTexture();

            var noiseRect = Rect("Low-level CRT static", terminal, 12, 14, 1056, 788);
            staticNoise = noiseRect.gameObject.AddComponent<RawImage>();
            staticNoise.texture = staticTexture;
            staticNoise.uvRect = new Rect(0f, 0f, 11f, 8f);
            staticNoise.color = new Color(0.46f, 0.82f, 0.60f, 0.025f);
            staticNoise.raycastTarget = false;

            for (int y = 28; y < 790; y += 30)
                Box("CRT scanline " + y, terminal, 12, y, 1056, 1, new Color(0.46f, 0.78f, 0.58f, 0.017f));

            interferenceLine = Box("CRT interference sweep", terminal, 12, 20, 1056, 3,
                new Color(0.62f, 0.92f, 0.72f, 0f));
            nextStaticRefreshAt = Time.unscaledTime;
            staticBurstUntil = Time.unscaledTime + 0.25f;
            nextInterferenceAt = Time.unscaledTime + 2.0f + NextNoise01() * 2.5f;
        }

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
                if (stages[i])
                {
                    bits |= 1 << i;
                    if (stageReachedAt[i] < 0f) stageReachedAt[i] = Time.unscaledTime - appearedAt;
                }
                SetLabel(stageLabels[i], stages[i] ? "ONLINE" : "WAITING", stages[i] ? Phosphor : Muted);
                stageLights[i].color = stages[i] ? Phosphor : new Color(0.10f, 0.17f, 0.14f);
            }

            bool stageAdvanced = (bits & ~previousBits) != 0;
            if (stageAdvanced)
                staticBurstUntil = Mathf.Max(staticBurstUntil, Time.unscaledTime + 0.12f);

            if (bits != previousBits && stageAdvanced && !failed && Time.unscaledTime >= nextSoundAt)
            {
                if (bootAudio != null && tick != null) bootAudio.PlayOneShot(tick);
                nextSoundAt = Time.unscaledTime + 0.18f;
            }

            previousBits = bits;
            previousError = error;
            if (ready && !readyLogged)
            {
                readyLogged = true;
                LogReadyTiming();
            }

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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogReadyTiming()
        {
            Debug.Log(
                $"[SecurityBoot] Ready in {Time.unscaledTime - appearedAt:0.00}s | " +
                $"FacilityLink={FormatTiming(stageReachedAt[0])}, SecuritySector={FormatTiming(stageReachedAt[1])}, " +
                $"ArrivalAlignment={FormatTiming(stageReachedAt[2])}, AvatarLink={FormatTiming(stageReachedAt[3])}, " +
                $"VisualSystems={FormatTiming(stageReachedAt[4])}", this);
        }

        private static string FormatTiming(float seconds) => seconds < 0f ? "n/a" : seconds.ToString("0.00") + "s";

        private void LateUpdate()
        {
            if (!startupMode) return;
            BindStartupCamera();
            if (terminal == null) return;

            if (!fading) terminalGroup.alpha = Mathf.Clamp01((Time.unscaledTime - appearedAt) / 0.3f);

            Color c = string.IsNullOrEmpty(previousError) ? Phosphor : Amber;
            c.a = 0.55f + 0.18f * Mathf.Sin(Time.unscaledTime * 2f);
            cursor.color = c;
            UpdateCrtTreatment();
        }

        private void UpdateCrtTreatment()
        {
            float now = Time.unscaledTime;
            if (staticNoise != null && now >= nextStaticRefreshAt)
            {
                RefreshStaticTexture();
                nextStaticRefreshAt = now + StaticRefreshInterval + NextNoise01() * 0.025f;
            }

            if (staticNoise != null)
            {
                float alpha = now < staticBurstUntil ? 0.045f : 0.025f;
                if (!string.IsNullOrEmpty(previousError)) alpha = 0.033f;
                staticNoise.color = new Color(0.46f, 0.82f, 0.60f, alpha);
            }

            if (interferenceLine == null) return;
            if (interferenceStartedAt >= 0f)
            {
                float phase = (now - interferenceStartedAt) / InterferenceSweepDuration;
                if (phase >= 1f)
                {
                    interferenceStartedAt = -1f;
                    interferenceLine.color = new Color(0.62f, 0.92f, 0.72f, 0f);
                    nextInterferenceAt = now + 3.0f + NextNoise01() * 4.5f;
                }
                else
                {
                    float y = Mathf.Lerp(20f, 790f, phase);
                    interferenceLine.rectTransform.anchoredPosition = new Vector2(12f, -y);
                    float alpha = Mathf.Sin(phase * Mathf.PI) * 0.065f;
                    interferenceLine.color = new Color(0.62f, 0.92f, 0.72f, alpha);
                }
            }
            else if (now >= nextInterferenceAt)
            {
                interferenceStartedAt = now;
                staticBurstUntil = Mathf.Max(staticBurstUntil, now + 0.10f);
            }
        }

        private void RefreshStaticTexture()
        {
            if (staticTexture == null || staticPixels == null) return;
            for (int i = 0; i < staticPixels.Length; i++)
            {
                byte value = (byte)(54 + NextNoise01() * 174f);
                staticPixels[i] = new Color32(value, value, value, 255);
            }
            staticTexture.SetPixels32(staticPixels);
            staticTexture.Apply(false, false);
        }

        private float NextNoise01()
        {
            uint x = noiseState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            noiseState = x == 0 ? 0x6D2B79F5u : x;
            return (noiseState & 0x00FFFFFFu) / 16777215f;
        }

        private void BindStartupCamera()
        {
            if (boundCamera != null && boundCamera.isActiveAndEnabled)
            {
                EnsureWorkstation();
                return;
            }

            var player = GorillaLocomotion.Player.Instance;
            var origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
            if (origin == null || origin.Camera == null || !origin.Camera.isActiveAndEnabled) return;

            RestoreCameraForReveal();
            int layer = LayerMask.NameToLayer(PresentationLayerName);
            if (layer < 0)
            {
                Debug.LogError($"Security boot requires the '{PresentationLayerName}' layer.", this);
                return;
            }

            boundCamera = origin.Camera;
            presentationLayer = layer;
            SetLayerRecursively(hostCanvas.gameObject, presentationLayer);
            hostCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hostCanvas.worldCamera = boundCamera;
            hostCanvas.planeDistance = Mathf.Max(1.5f, boundCamera.nearClipPlane + 0.1f);
            MaskStartupCamera();
            EnsureWorkstation();

            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (LoadingFallbackCamera fallback in root.GetComponentsInChildren<LoadingFallbackCamera>(true))
                {
                    fallback.enabled = false;
                    fallback.GetComponent<Camera>().enabled = false;
                }
        }

        private void MaskStartupCamera()
        {
            if (boundCamera == null || presentationLayer < 0) return;
            if (!cameraMasked)
            {
                savedCullingMask = boundCamera.cullingMask;
                savedClearFlags = boundCamera.clearFlags;
                savedBackground = boundCamera.backgroundColor;
                cameraMasked = true;
            }

            // Always reapply this mask. An interrupted Hub reveal can call this while the saved-state flag is still true.
            boundCamera.cullingMask = 1 << presentationLayer;
            boundCamera.clearFlags = CameraClearFlags.SolidColor;
            boundCamera.backgroundColor = Color.black;
        }

        public void PrepareCameraForHubReveal()
        {
            if (!cameraMasked || boundCamera == null || presentationLayer < 0) return;
            // Render the Hub and the black LoadingPresentation overlay together during the reveal fade.
            boundCamera.cullingMask = savedCullingMask | (1 << presentationLayer);
            boundCamera.clearFlags = savedClearFlags;
            boundCamera.backgroundColor = savedBackground;
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
            alpha = Mathf.Clamp01(alpha);
            if (terminalGroup != null) terminalGroup.alpha = alpha;

            if (workstation != null)
            {
                // Let the authored black full-FOV backdrop cover the entire 3D vignette before it is hidden.
                SetBackdropOpacity(1f - alpha);
                if (alpha <= 0.001f) workstation.gameObject.SetActive(false);
                else if (!workstation.gameObject.activeSelf) workstation.gameObject.SetActive(true);
            }
        }

        public void SetBackdropOpacity(float alpha)
        {
            if (backdropGroup != null) backdropGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void RestoreAfterInterruptedEntry()
        {
            fading = false;
            if (workstation != null)
            {
                workstation.gameObject.SetActive(true);
                SetBackdropOpacity(0f);
            }
            else
            {
                SetBackdropOpacity(1f);
            }

            MaskStartupCamera();
            if (terminalGroup != null) terminalGroup.alpha = 1f;
        }

        public void RestartAttempt()
        {
            RestoreAfterInterruptedEntry();
            appearedAt = Time.unscaledTime;
            nextSoundAt = 0f;
            previousBits = 0;
            previousError = null;
            readyLogged = false;
            staticBurstUntil = Time.unscaledTime + 0.25f;
            nextStaticRefreshAt = Time.unscaledTime;
            nextInterferenceAt = Time.unscaledTime + 1.8f + NextNoise01() * 2.2f;
            interferenceStartedAt = -1f;
            if (interferenceLine != null)
                interferenceLine.color = new Color(0.62f, 0.92f, 0.72f, 0f);
            for (int i = 0; i < stageReachedAt.Length; i++) stageReachedAt[i] = -1f;
            if (terminalGroup != null) terminalGroup.alpha = 0f;
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

        private void OnDisable()
        {
            RestoreCameraForReveal();
        }

        private void OnDestroy()
        {
            RestoreCameraForReveal();
            if (bootAudio != null) bootAudio.Stop();
            if (tick != null) Destroy(tick);
            if (staticTexture != null) Destroy(staticTexture);
            if (workstation != null) Destroy(workstation.gameObject);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
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
