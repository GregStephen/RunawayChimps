using RunawayChimps.Travel;
using RunawayChimps.Zones;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.ThreatFeedback
{
    /// <summary>
    /// Crawler-specific composition root. The threat system itself remains monster-agnostic;
    /// this adapter only maps the authored Level 1 Crawler root into the generic source contract.
    /// </summary>
    internal static class CrawlerThreatSourceInstaller
    {
        private const string LevelOneScene = "Level1_Containment";
        private const int CrawlerMonsterId = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Setup(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Setup(scene);

        private static void Setup(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != LevelOneScene)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonsterNavigation navigation in root.GetComponentsInChildren<MonsterNavigation>(true))
                {
                    SectorMonsterSync sync = navigation.GetComponent<SectorMonsterSync>();
                    ProximityReactor proximity = navigation.GetComponent<ProximityReactor>();
                    if (sync == null || sync.sector != SectorId.Containment ||
                        sync.monsterId != CrawlerMonsterId || proximity == null)
                        continue;

                    MonsterThreatSource source = navigation.GetComponent<MonsterThreatSource>();
                    if (source != null)
                        continue;

                    source = navigation.gameObject.AddComponent<MonsterThreatSource>();
                    float startDistance = Mathf.Max(0.2f, navigation.DetectionRange);
                    source.Configure(
                        proximity,
                        ZoneId.Level1_Vents,
                        new ThreatProfile
                        {
                            beginDistance = startDistance,
                            maximumDistance = Mathf.Min(1.25f, startDistance - 0.1f),
                            pursuitBaseline = 0.10f,
                            maximumThreat = 1f,
                            response = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                        });
                }
            }
        }
    }
}
