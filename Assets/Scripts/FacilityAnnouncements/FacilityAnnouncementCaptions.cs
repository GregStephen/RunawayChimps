using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RunawayChimps.FacilityAnnouncements
{
    [DisallowMultipleComponent]
    public sealed class FacilityAnnouncementCaptions : MonoBehaviour
    {
        public Canvas canvas;
        public RectTransform panel;
        public TMP_Text label;
        public Image backdrop;
        private string currentText;

        public static bool CaptionsEnabled
        {
            get => PlayerPrefs.GetInt("rc.facility.captions", 1) != 0;
            set => PlayerPrefs.SetInt("rc.facility.captions", value ? 1 : 0);
        }
        public static float TextScale
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat("rc.facility.captionScale", 1), 0.8f, 1.5f);
            set => PlayerPrefs.SetFloat("rc.facility.captionScale", Mathf.Clamp(value, 0.8f, 1.5f));
        }
        public static float VerticalPosition
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat("rc.facility.captionY", 0.25f), 0.18f, 0.42f);
            set => PlayerPrefs.SetFloat("rc.facility.captionY", Mathf.Clamp(value, 0.18f, 0.42f));
        }

        public bool Bind(Camera camera)
        {
            if (camera == null || canvas == null || panel == null || label == null || backdrop == null)
                return false;
            transform.SetParent(camera.transform, false);
            gameObject.layer = camera.gameObject.layer;
            panel.gameObject.layer = camera.gameObject.layer;
            label.gameObject.layer = camera.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(1.2f, camera.nearClipPlane + 0.2f);
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            label.richText = false;
            label.raycastTarget = false;
            backdrop.raycastTarget = false;
            Clear();
            return true;
        }

        public void Show(string transcript)
        {
            currentText = transcript;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            bool visible = CaptionsEnabled && !string.IsNullOrEmpty(currentText) &&
                canvas != null && canvas.worldCamera != null && canvas.worldCamera.isActiveAndEnabled;
            if (panel != null) panel.gameObject.SetActive(visible);
            if (!visible || label == null) return;
            label.text = currentText;
            label.fontSize = 26 * TextScale;
            panel.anchorMin = new Vector2(0.19f, VerticalPosition - 0.12f);
            panel.anchorMax = new Vector2(0.81f, VerticalPosition + 0.12f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
        }

        public void Clear()
        {
            currentText = null;
            if (label != null) label.text = string.Empty;
            if (panel != null) panel.gameObject.SetActive(false);
        }
        private void OnDisable() => Clear();
    }
}
