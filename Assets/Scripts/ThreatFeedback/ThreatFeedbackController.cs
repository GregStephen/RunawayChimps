using System.Collections.Generic;
using GorillaLocomotion;
using Photon.Pun;
using RunawayChimps.Travel;
using RunawayChimps.Zones;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.ThreatFeedback
{
    [DisallowMultipleComponent]
    public sealed class ThreatFeedbackController : MonoBehaviour
    {
        public static ThreatFeedbackController Instance { get; private set; }
        private readonly List<MonsterThreatSource> sources = new();
        private ThreatVignetteView vignette;
        private float displayedThreat;

        public float CurrentThreat => displayedThreat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInstalled();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstalled();

        public static ThreatFeedbackController EnsureInstalled()
        {
            var player = Player.Instance;
            var origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
            if (origin == null) return null;
            var controller = origin.GetComponent<ThreatFeedbackController>();
            if (controller == null) controller = origin.gameObject.AddComponent<ThreatFeedbackController>();
            controller.EnsureView(origin.Camera);
            return controller;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Register(MonsterThreatSource source)
        {
            if (source != null && !sources.Contains(source)) sources.Add(source);
        }

        public void Unregister(MonsterThreatSource source) => sources.Remove(source);

        private void Update()
        {
            if (Instance != this) return;
            var player = Player.Instance;
            var origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
            EnsureView(origin != null ? origin.Camera : null);

            float desired = 0f;
            bool contextValid = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null &&
                SectorTravelService.I != null && !SectorTravelService.I.IsBusy &&
                SectorTravelService.I.CurrentSector != SectorId.None &&
                ZoneStateService.Instance != null && ZoneStateService.Instance.LocalZone != ZoneId.None;

            if (contextValid)
            {
                for (int i = sources.Count - 1; i >= 0; i--)
                {
                    var source = sources[i];
                    if (source == null) { sources.RemoveAt(i); continue; }
                    desired = Mathf.Max(desired, source.EvaluateLocalThreat());
                }
            }

            float seconds = desired > displayedThreat ? 0.18f : 0.35f;
            float rate = 1f / Mathf.Max(0.01f, seconds);
            displayedThreat = Mathf.MoveTowards(displayedThreat, desired, rate * Time.unscaledDeltaTime);
            if (vignette != null) vignette.SetThreat(displayedThreat);
        }

        private void EnsureView(Camera camera)
        {
            if (camera == null) return;
            if (vignette == null || vignette.transform.parent != camera.transform)
                vignette = ThreatVignetteView.Ensure(camera);
        }

        private void OnDisable()
        {
            displayedThreat = 0f;
            if (vignette != null) vignette.SetThreat(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
