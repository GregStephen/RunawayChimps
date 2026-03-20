using UnityEngine;

namespace RunawayChimps.Zones
{
    /// <summary>
    /// Simple global accessor to the ZoneConfig asset.
    /// Put ZoneConfig.asset under a Resources folder for easy loading.
    /// </summary>
    public static class ZoneRegistry
    {
        private const string ResourcePath = "ZoneConfig"; // Resources/ZoneConfig.asset
        private static ZoneConfig _config;

        public static ZoneConfig Config
        {
            get
            {
                if (_config == null)
                    _config = Resources.Load<ZoneConfig>(ResourcePath);
                return _config;
            }
        }

        public static ZoneConfig.ZoneDefinition Get(ZoneId id)
        {
            var cfg = Config;
            return cfg != null ? cfg.Get(id) : null;
        }
    }
}
