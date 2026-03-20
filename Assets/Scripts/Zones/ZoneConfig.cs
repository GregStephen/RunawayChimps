using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunawayChimps.Zones
{
    [CreateAssetMenu(menuName = "Runaway Chimps/Zones/Zone Config", fileName = "ZoneConfig")]
    public class ZoneConfig : ScriptableObject
    {
        [Serializable]
        public class ZoneDefinition
        {
            public ZoneId ZoneId;

            [Tooltip("Additive scene to load for this zone. Leave empty if zone uses only the base scene.")]
            public string AdditiveSceneName;

            [Tooltip("Logical spawn key; travel service will find a SpawnPoint with matching key.")]
            public string SpawnKey;

            [Tooltip("If true, this is a short-lived transition space like a hallway.")]
            public bool IsTransition;

            [Tooltip("Optional cap for UI/validation; not enforced by Photon in single-room mode.")]
            public int MaxPlayersHint;
        }

        [SerializeField]
        private List<ZoneDefinition> zones = new();

        private Dictionary<ZoneId, ZoneDefinition> _cache;

        public IReadOnlyList<ZoneDefinition> Zones => zones;

        public ZoneDefinition Get(ZoneId id)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<ZoneId, ZoneDefinition>();
                foreach (var z in zones)
                {
                    if (z == null) continue;
                    _cache[z.ZoneId] = z;
                }
            }

            return _cache.TryGetValue(id, out var def) ? def : null;
        }
    }
}
