using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RunawayChimps.Surveillance
{
    public class SecurityMonitorDisplay : MonoBehaviour
    {
        [Header("Display references")]
        [SerializeField] private RawImage footageImage;
        [SerializeField] private RawImage grainOverlay;
        [SerializeField] private TMP_Text sectorLabelText;
        [SerializeField] private TMP_Text sectorNameText;
        [SerializeField] private TMP_Text recText;
        [SerializeField] private CanvasGroup displayCanvasGroup;

        [Header("Presentation")]
        [Range(0f, 1f)]
        [SerializeField] private float normalGrainOpacity = 0.24f;
        [Range(0f, 1f)]
        [SerializeField] private float transitionGrainOpacity = 0.95f;
        [SerializeField] private bool animateGrain = true;
        [Min(0f)]
        [SerializeField] private float grainScrollSpeed = 0.08f;

        private Rect _baseGrainUv = new(0f, 0f, 1f, 1f);

        public float TransitionGrainOpacity => transitionGrainOpacity;

        private void Awake()
        {
            if (grainOverlay != null)
            {
                _baseGrainUv = grainOverlay.uvRect;
            }

            SetGrainOpacity(normalGrainOpacity);
        }

        private void Update()
        {
            if (!animateGrain || grainOverlay == null || grainScrollSpeed <= 0f)
            {
                return;
            }

            float offset = Mathf.Repeat(Time.unscaledTime * grainScrollSpeed, 1f);
            Rect uv = _baseGrainUv;
            uv.x = _baseGrainUv.x + offset;
            uv.y = _baseGrainUv.y + (offset * 0.61f);
            grainOverlay.uvRect = uv;
        }

        public void ShowFeed(SecurityMonitorFeed feed, Texture image)
        {
            if (displayCanvasGroup != null)
            {
                displayCanvasGroup.alpha = 1f;
            }

            if (footageImage != null)
            {
                footageImage.texture = image;
                footageImage.enabled = image != null;
            }

            if (sectorLabelText != null)
            {
                sectorLabelText.text = feed != null ? feed.SectorLabel : string.Empty;
            }

            if (sectorNameText != null)
            {
                sectorNameText.text = feed != null ? feed.SectorName : string.Empty;
            }

            if (recText != null)
            {
                recText.text = feed != null ? "REC" : string.Empty;
            }

            SetGrainOpacity(normalGrainOpacity);
        }

        public void ShowNoSignal()
        {
            if (displayCanvasGroup != null)
            {
                displayCanvasGroup.alpha = 1f;
            }

            if (footageImage != null)
            {
                footageImage.texture = null;
                footageImage.enabled = false;
            }

            if (sectorLabelText != null)
            {
                sectorLabelText.text = "NO SIGNAL";
            }

            if (sectorNameText != null)
            {
                sectorNameText.text = string.Empty;
            }

            if (recText != null)
            {
                recText.text = string.Empty;
            }

            SetGrainOpacity(transitionGrainOpacity);
        }

        public void SetTransitionStatic(bool active)
        {
            SetGrainOpacity(active ? transitionGrainOpacity : normalGrainOpacity);
        }

        private void SetGrainOpacity(float opacity)
        {
            if (grainOverlay == null)
            {
                return;
            }

            Color color = grainOverlay.color;
            color.a = Mathf.Clamp01(opacity);
            grainOverlay.color = color;
        }
    }
}
