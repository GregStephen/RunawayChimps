using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private PlayFabConfig playFabConfig;

    [Header("Shipping")]
    [Tooltip("Enable to require Meta entitlement + identity on Quest builds.")]
    [SerializeField] private bool enforceQuestAuth = false;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        AppState.I?.ResetReady();
        AppState.I?.SetStatus("Logging in...");
        var playFab = new PlayFabAuthService();
        playFab.Initialize(playFabConfig != null ? playFabConfig.TitleId : null);

        var orchestrator = new AuthOrchestrator(playFab, enforceQuestAuth);

        orchestrator.Run(
            this,
            onReady: msg =>
            {
                Debug.Log(msg);

                AppState.I?.SetStatus("Connecting to multiplayer...");
                Photon.VR.PhotonVRManager.Connect();
            },

            onFatal: err =>
            {
                Debug.LogError(err);

                // In shipping, you might show a UI, then quit:
                // Application.Quit();
            }
        );
    }
}
