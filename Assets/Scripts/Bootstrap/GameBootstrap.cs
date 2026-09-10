using Photon.Pun;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap I { get; private set; }
    [SerializeField] private PlayFabConfig playFabConfig;
    private AuthOrchestrator orchestrator;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() => RetryStartup();

    public void RetryStartup()
    {
        AppState.I?.ClearFailure();
        AppState.I?.SetStatus("Signing in...");
        if (AppState.I != null && !AppState.I.RigSnapped) FindObjectOfType<RigSpawnSnapper>()?.RetrySnap();
        if (playFabConfig != null && !string.IsNullOrWhiteSpace(playFabConfig.TitleId))
            PlayFab.PlayFabSettings.staticSettings.TitleId = playFabConfig.TitleId;
        if (orchestrator == null) orchestrator = FindObjectOfType<AuthOrchestrator>();
        if (orchestrator == null) { AppState.I?.Fail("Sign-in service is missing from Bootstrap."); return; }
        orchestrator.CancelPending();
        orchestrator.Run(this, message =>
        {
            AppState.I?.SetStatus("Connecting to multiplayer...");
            if (PhotonNetwork.InRoom)
            {
                FindObjectOfType<Photon.VR.Player.PlayerSpawner>()?.RetrySpawn();
                AppState.I?.TryMarkReady();
                return;
            }
            if (RoomSwitchService.Instance != null) RoomSwitchService.Instance.JoinRandomPublicLobby();
            else if (!Photon.VR.PhotonVRManager.Connect()) AppState.I?.Fail("Could not start multiplayer.");
        }, error => AppState.I?.Fail(error));
    }

    private void OnDestroy() { if (I == this) I = null; }
}
