using Photon.Realtime;

namespace RunawayChimps.Travel
{
    // A sector is a loaded environment; safe rooms and vents remain separate ZoneIds.
    public enum SectorId { None = 0, Hub = 1, Containment = 2, Conditioning = 3 }

    public static class SectorPresence
    {
        public const string PropertyKey = "sector";

        public static SectorId Get(Player player)
        {
            if (player == null || player.IsInactive) return SectorId.None;
            if (player.IsLocal && SectorTravelService.I != null)
                return SectorTravelService.I.CurrentSector;
            if (player.CustomProperties.TryGetValue(PropertyKey, out object value) && value is int id &&
                (id == (int)SectorId.Hub || id == (int)SectorId.Containment || id == (int)SectorId.Conditioning))
                return (SectorId)id;
            return SectorId.None;
        }

        public static int ElectController(Player[] players, SectorId sector)
        {
            int actor = 0;
            if (players == null || sector == SectorId.None) return actor;
            foreach (var player in players)
                if (player != null && Get(player) == sector && (actor == 0 || player.ActorNumber < actor))
                    actor = player.ActorNumber;
            return actor;
        }
    }
}
