using UnityEngine;
using UnityEngine.SceneManagement;
using RunawayChimps.Zones;

namespace RunawayChimps.Travel
{
    public enum ArrivalRoute { Terminal, Door }

    public sealed class SectorScene : MonoBehaviour
    {
        public SectorId sector;
        public ZoneId entryZone;
        [Tooltip("Floor marker: X/Z and yaw define arrival; the destination floor determines height.")]
        public Transform arrivalSpawn;
        [Tooltip("Optional arrival for a physical door route. The Hub uses its hallway marker.")]
        public Transform doorArrivalSpawn;

        public Transform GetArrival(ArrivalRoute route) =>
            route == ArrivalRoute.Door && doorArrivalSpawn != null ? doorArrivalSpawn : arrivalSpawn;

        public static SectorScene Find(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var context = root.GetComponentInChildren<SectorScene>(true);
                if (context != null) return context;
            }
            return null;
        }
    }
}
