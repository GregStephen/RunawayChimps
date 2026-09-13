using Photon.Pun;
using RunawayChimps.Travel;
using RunawayChimps.Zones;
using UnityEngine;

namespace RunawayChimps.ThreatFeedback
{
    public interface IMonsterPursuitProvider
    {
        bool IsPursuing { get; }
        int TargetActorNumber { get; }
        SectorId ThreatSector { get; }
    }

    [DisallowMultipleComponent]
    public sealed class MonsterThreatSource : MonoBehaviour
    {
        [SerializeField] private ProximityReactor proximity;
        [SerializeField] private ThreatProfile profile = new ThreatProfile();
        [SerializeField] private ZoneId requiredLocalZone = ZoneId.None;
        private IMonsterPursuitProvider pursuit;

        private void Awake()
        {
            if (proximity == null) proximity = GetComponent<ProximityReactor>();
            ResolveProvider();
        }

        private void OnEnable()
        {
            ResolveProvider();
            ThreatFeedbackController.EnsureInstalled()?.Register(this);
        }

        private void Update()
        {
            if (ThreatFeedbackController.Instance == null) ThreatFeedbackController.EnsureInstalled()?.Register(this);
            if (pursuit == null) ResolveProvider();
        }

        private void OnDisable() => ThreatFeedbackController.Instance?.Unregister(this);

        private void ResolveProvider()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IMonsterPursuitProvider provider) { pursuit = provider; return; }
            pursuit = null;
        }

        public float EvaluateLocalThreat()
        {
            if (pursuit == null || !pursuit.IsPursuing || pursuit.TargetActorNumber <= 0 ||
                !PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null ||
                pursuit.TargetActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return 0f;

            var travel = SectorTravelService.I;
            var zones = ZoneStateService.Instance;
            if (travel == null || travel.IsBusy || travel.CurrentSector == SectorId.None ||
                pursuit.ThreatSector != travel.CurrentSector || zones == null || zones.LocalZone == ZoneId.None) return 0f;
            if (requiredLocalZone != ZoneId.None && zones.LocalZone != requiredLocalZone) return 0f;
            if (proximity == null || !proximity.HasValidSample) return 0f;

            return profile.Evaluate(proximity.LastDistance);
        }

        public void Configure(ProximityReactor reactor, ZoneId requiredZone)
        {
            proximity = reactor;
            requiredLocalZone = requiredZone;
            ResolveProvider();
        }
    }

    [System.Serializable]
    public sealed class ThreatProfile
    {
        [Min(0.1f)] public float beginDistance = 10f;
        [Min(0f)] public float maximumDistance = 1.25f;
        [Range(0f, 1f)] public float pursuitBaseline = 0.12f;
        [Range(0f, 1f)] public float maximumThreat = 1f;
        public AnimationCurve response = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public float Evaluate(float distance)
        {
            if (distance > beginDistance) return pursuitBaseline * maximumThreat;
            float span = Mathf.Max(0.01f, beginDistance - maximumDistance);
            float closeness = Mathf.Clamp01((beginDistance - distance) / span);
            float curved = response != null ? Mathf.Clamp01(response.Evaluate(closeness)) : closeness;
            return Mathf.Clamp01(Mathf.Lerp(pursuitBaseline, 1f, curved) * maximumThreat);
        }
    }
}
