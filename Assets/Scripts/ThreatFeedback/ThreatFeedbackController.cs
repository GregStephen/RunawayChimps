using System;
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
    /// <summary>
    /// Persistent local-only threat aggregator. Sources report personal threat demand;
    /// this service combines them with max aggregation and drives local presentation.
    /// No value produced here is sent over Photon.
    /// </summary>
    [DefaultExecutionOrder(600)]
    [DisallowMultipleComponent]
    public sealed class ThreatFeedbackController : MonoBehaviour
    {
        public static ThreatFeedbackController Instance { get; private set; }

        private readonly List<MonsterThreatSource> sources = new List<MonsterThreatSource>();
        private ThreatVignetteView vignette;
        private Camera trackedCamera;
        private float displayedThreat;
        private float desiredThreat;

        public float CurrentThreat => displayedThreat;
        public float DesiredThreat => desiredThreat;
        public event Action<float> ThreatChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInstalled();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstalled();
        }

        public static ThreatFeedbackController EnsureInstalled()
        {
            Player player = Player.Instance;
            XROrigin origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
            if (origin == null)
                return null;

            ThreatFeedbackController controller = origin.GetComponent<ThreatFeedbackController>();
            if (controller == null)
                controller = origin.gameObject.AddComponent<ThreatFeedbackController>();
            controller.EnsureView(origin.Camera);
            return controller;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public void Register(MonsterThreatSource source)
        {
            if (source != null && !sources.Contains(source))
                sources.Add(source);
        }

        public void Unregister(MonsterThreatSource source)
        {
            sources.Remove(source);
        }

        private void Update()
        {
            if (Instance != this)
                return;

            if (trackedCamera == null)
            {
                Player player = Player.Instance;
                XROrigin origin = player != null ? player.GetComponentInParent<XROrigin>() : null;
                EnsureView(origin != null ? origin.Camera : null);
            }

            // Travel owns the complete XR view. Hide personal threat immediately so the
            // black fade/loading presentation can never have red UI composited over it.
            if (SectorTravelService.I != null && SectorTravelService.I.IsBusy)
            {
                if (displayedThreat > 0f || desiredThreat > 0f)
                    ResetPresentation();
                else if (vignette != null)
                    vignette.SetThreat(0f);
                return;
            }

            desiredThreat = EvaluateDesiredThreat();
            float previous = displayedThreat;
            float transitionSeconds = desiredThreat > displayedThreat ? 0.18f : 0.35f;
            float rate = 1f / Mathf.Max(0.01f, transitionSeconds);
            displayedThreat = Mathf.MoveTowards(
                displayedThreat,
                desiredThreat,
                rate * Time.unscaledDeltaTime);

            if (vignette != null)
                vignette.SetThreat(displayedThreat);
            if (!Mathf.Approximately(previous, displayedThreat))
                ThreatChanged?.Invoke(displayedThreat);
        }

        private float EvaluateDesiredThreat()
        {
            bool contextValid = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null &&
                SectorTravelService.I != null && !SectorTravelService.I.IsBusy &&
                SectorTravelService.I.CurrentSector != SectorId.None &&
                ZoneStateService.Instance != null && ZoneStateService.Instance.LocalZone != ZoneId.None;
            if (!contextValid)
                return 0f;

            float strongest = 0f;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                MonsterThreatSource source = sources[i];
                if (source == null)
                {
                    sources.RemoveAt(i);
                    continue;
                }
                strongest = Mathf.Max(strongest, source.EvaluateLocalThreat());
            }
            return Mathf.Clamp01(strongest);
        }

        private void EnsureView(Camera camera)
        {
            if (camera == null)
                return;
            if (trackedCamera == camera && vignette != null)
                return;

            trackedCamera = camera;
            vignette = ThreatVignetteView.Ensure(camera);
            if (vignette != null)
                vignette.SetThreat(displayedThreat);
        }

        private void ResetPresentation()
        {
            desiredThreat = 0f;
            displayedThreat = 0f;
            if (vignette != null)
                vignette.SetThreat(0f);
            ThreatChanged?.Invoke(0f);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                ResetPresentation();
        }

        private void OnDisable()
        {
            ResetPresentation();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
