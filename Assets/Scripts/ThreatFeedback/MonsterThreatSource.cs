using System.Collections.Generic;
using Photon.Pun;
using RunawayChimps.Monsters;
using RunawayChimps.Travel;
using RunawayChimps.Zones;
using UnityEngine;

namespace RunawayChimps.ThreatFeedback
{
    /// <summary>
    /// Adapts one monster's pursuit identity and cached local-player distance into a
    /// local presentation value. It never owns AI decisions or networks presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterThreatSource : MonoBehaviour
    {
        private const float ResolveRetrySeconds = 0.5f;

        [SerializeField] private ProximityReactor proximity;
        [SerializeField] private ThreatProfile profile = new ThreatProfile();
        [SerializeField] private ZoneId requiredLocalZone = ZoneId.None;

        private readonly List<MonoBehaviour> providerBuffer = new List<MonoBehaviour>();
        private IMonsterPursuitProvider pursuit;
        private ThreatFeedbackController controller;
        private float nextResolveTime;

        private void Awake()
        {
            if (proximity == null)
                proximity = GetComponent<ProximityReactor>();
            ResolveProvider();
        }

        private void OnEnable()
        {
            nextResolveTime = 0f;
            TryAttach();
        }

        private void Update()
        {
            if (pursuit != null && controller != null)
                return;
            if (Time.unscaledTime < nextResolveTime)
                return;

            nextResolveTime = Time.unscaledTime + ResolveRetrySeconds;
            TryAttach();
        }

        private void OnDisable()
        {
            controller?.Unregister(this);
            controller = null;
        }

        private void OnDestroy()
        {
            controller?.Unregister(this);
        }

        private void TryAttach()
        {
            if (pursuit == null)
                ResolveProvider();

            ThreatFeedbackController installed = ThreatFeedbackController.EnsureInstalled();
            if (installed == null)
                return;

            if (controller != installed)
            {
                controller?.Unregister(this);
                controller = installed;
            }
            controller.Register(this);
        }

        private void ResolveProvider()
        {
            providerBuffer.Clear();
            GetComponents(providerBuffer);
            for (int i = 0; i < providerBuffer.Count; i++)
            {
                if (providerBuffer[i] is IMonsterPursuitProvider provider)
                {
                    pursuit = provider;
                    providerBuffer.Clear();
                    return;
                }
            }
            providerBuffer.Clear();
            pursuit = null;
        }

        public float EvaluateLocalThreat()
        {
            if (pursuit == null || !PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
                return 0f;

            MonsterPursuitState state = pursuit.PursuitState;
            if (!state.IsPursuing || state.TargetActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                return 0f;

            SectorTravelService travel = SectorTravelService.I;
            ZoneStateService zones = ZoneStateService.Instance;
            if (travel == null || travel.IsBusy || travel.CurrentSector == SectorId.None ||
                pursuit.ThreatSector != travel.CurrentSector || zones == null || zones.LocalZone == ZoneId.None)
                return 0f;
            if (requiredLocalZone != ZoneId.None && zones.LocalZone != requiredLocalZone)
                return 0f;
            if (proximity == null || !proximity.HasValidSample)
                return 0f;

            return profile != null ? profile.Evaluate(proximity.LastDistance) : 0f;
        }

        public void Configure(ProximityReactor reactor, ZoneId requiredZone, ThreatProfile threatProfile = null)
        {
            proximity = reactor;
            requiredLocalZone = requiredZone;
            if (threatProfile != null)
                profile = threatProfile;
            ResolveProvider();
            TryAttach();
        }
    }
}
