using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private PlayFabConfig playFabConfig;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        AppState.I?.ResetReady();
        AppState.I?.SetStatus("Logging in...");

        // Ensure PlayFab TitleId is set once
        if (playFabConfig != null && !string.IsNullOrWhiteSpace(playFabConfig.TitleId))
        {
            PlayFab.PlayFabSettings.staticSettings.TitleId = playFabConfig.TitleId;
        }

        // Find the ONE AuthOrchestrator in the scene
        var orchestrator = FindObjectOfType<AuthOrchestrator>();
        if (orchestrator == null)
        {
            Debug.LogError("GameBootstrap: AuthOrchestrator not found in scene!");
            return;
        }

        orchestrator.Run(
            this,
            onReady: msg =>
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(msg);
                #endif

                AppState.I?.SetStatus("Connecting to multiplayer...");
                Photon.VR.PhotonVRManager.Connect();
            },
            onFatal: err =>
            {
                Debug.LogError(err);
                AppState.I?.SetStatus("Login failed");

                // Optional for shipping:
                // Application.Quit();
            }
        );
    }
}
