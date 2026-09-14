using System.Collections;
using Photon.Pun;
using RunawayChimps.Loading;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class LoadingFlow : MonoBehaviour
{
    [SerializeField] private string hubSceneName = "Hub_Base";
    [SerializeField] private TMP_Text statusText;
    [Min(10f)] [SerializeField] private float startupTimeout = 90f;
    [Header("Security boot prototype - cold startup only")]
    [Tooltip("Small presentation floor, not a simulated loading time. Set to zero for immediate entry when ready.")]
    [Range(0f, 3f)] [SerializeField] private float minimumIntroSeconds = 1.5f;
    [Tooltip("Keeps ACCESS GRANTED visible briefly after real readiness is reached.")]
    [Range(0f, 1f)] [SerializeField] private float minimumReadyHoldSeconds = 0.35f;
    [SerializeField] private bool playBootSounds = true;
    private const float FadeDuration = 0.2f;
    private AsyncOperation hubLoad;
    private SecurityBootPresentation presentation;
    private bool startup;
    private bool entering;
    private bool triggerWasPressed;
    private float deadline;
    private float startedAt;
    private float readySince = -1f;
    private string loadError;

    private void Awake()
    {
        startup = !(RunawayChimps.Travel.SectorTravelService.I != null &&
            RunawayChimps.Travel.SectorTravelService.I.IsBusy);
    }

    private void Start()
    {
        presentation = SecurityBootPresentation.Install(gameObject.scene, statusText, startup, playBootSounds);
        if (!startup) return;
        startedAt = Time.realtimeSinceStartup;
        deadline = startedAt + Mathf.Max(10f, startupTimeout);
        BeginHubLoad();
    }

    private void BeginHubLoad()
    {
        if (SceneManager.GetSceneByName(hubSceneName).isLoaded || (hubLoad != null && !hubLoad.isDone)) return;
        try
        {
            loadError = null;
            if (!Application.CanStreamedLevelBeLoaded(hubSceneName))
                throw new System.InvalidOperationException("Hub scene is missing from Build Settings.");
            hubLoad = SceneManager.LoadSceneAsync(hubSceneName, LoadSceneMode.Additive);
            if (hubLoad == null) throw new System.InvalidOperationException("Could not start loading the hub.");
        }
        catch (System.Exception exception)
        {
            loadError = exception.Message;
            AppState.I?.Fail(loadError);
        }
    }

    private void Update()
    {
        if (!startup || entering) return;
        var state = AppState.I;
        bool hubReady = SceneManager.GetSceneByName(hubSceneName).isLoaded;
        if (state != null && hubReady)
        {
            state.MarkHubActive();
            state.TryMarkReady();
        }
        bool ready = CanEnterHub();
        if (ready)
        {
            if (readySince < 0f) readySince = Time.realtimeSinceStartup;
        }
        else
        {
            readySince = -1f;
        }
        if (!ready && Time.realtimeSinceStartup >= deadline && state != null && string.IsNullOrEmpty(state.LastError))
            state.Fail("Startup timed out. Check your connection and retry.");
        string error = state != null ? state.LastError : "Bootstrap state is missing. Start from Bootstrap.";
        if (string.IsNullOrEmpty(error)) error = loadError;
        bool reviewHeld = IsEditorReviewHeld();
        if (presentation != null)
            presentation.Present(state, hubReady, PhotonNetwork.InRoom, error, reviewHeld);
        else if (statusText != null)
            statusText.text = string.IsNullOrEmpty(error) ? (hubReady ? state?.Status : "Loading hub...") :
                error + "\nPress either trigger to retry. (Desktop: R)";

        bool introFloorMet = Time.realtimeSinceStartup - startedAt >= Mathf.Clamp(minimumIntroSeconds, 0f, 3f);
        bool readyHoldMet = readySince >= 0f &&
            Time.realtimeSinceStartup - readySince >= Mathf.Clamp(minimumReadyHoldSeconds, 0f, 1f);
        if (ready && !reviewHeld && introFloorMet && readyHoldMet)
        {
            entering = true;
            StartCoroutine(EnterHub());
            return;
        }
        bool pressed = TriggerPressed(XRNode.LeftHand) || TriggerPressed(XRNode.RightHand);
        bool retry = (pressed && !triggerWasPressed) || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);
        triggerWasPressed = pressed;
        if (!string.IsNullOrEmpty(error) && retry) Retry();
    }

    private bool CanEnterHub()
    {
        var state = AppState.I;
        return state != null && state.IsReady && state.HubActive && state.RigSnapped &&
            state.PhotonPlayerSpawned && state.PlayerVisualsReady && PhotonNetwork.InRoom &&
            SceneManager.GetSceneByName(hubSceneName).isLoaded &&
            string.IsNullOrEmpty(state.LastError) && string.IsNullOrEmpty(loadError);
    }

    private static bool IsEditorReviewHeld()
    {
#if UNITY_EDITOR
        return UnityEditor.SessionState.GetBool("RunawayChimps.SecurityBoot.HoldReview", false);
#else
        return false;
#endif
    }

    private static bool TriggerPressed(XRNode hand) => InputDevices.GetDeviceAtXRNode(hand)
        .TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool pressed) && pressed;

    public void Retry()
    {
        if (!startup || entering) return;
        deadline = Time.realtimeSinceStartup + Mathf.Max(10f, startupTimeout);
        readySince = -1f;
        loadError = null;
        AppState.I?.ClearFailure();
        presentation?.RestartAttempt();
        BeginHubLoad();
        if (GameBootstrap.I != null) GameBootstrap.I.RetryStartup();
        else AppState.I?.Fail("Bootstrap service is missing. Start from Bootstrap.");
    }

    private IEnumerator EnterHub()
    {
        yield return null;
        for (float elapsed = 0f; elapsed < FadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            if (!CanEnterHub()) { AbortEntry(); yield break; }
            presentation?.SetTerminalOpacity(1f - elapsed / FadeDuration);
            yield return null;
        }
        presentation?.SetTerminalOpacity(0f);
        yield return new WaitForFixedUpdate();
        if (!CanEnterHub()) { AbortEntry(); yield break; }
        var hub = SceneManager.GetSceneByName(hubSceneName);
        if (!SceneManager.SetActiveScene(hub))
        {
            AppState.I?.Fail("Could not activate the hub.");
            AbortEntry();
            yield break;
        }
        RunawayChimps.Travel.SectorTravelService.I?.NotifySceneReady(hub);
        presentation?.RestoreCameraForReveal();
        for (float elapsed = 0f; elapsed < FadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            if (!CanEnterHub()) { AbortEntry(); yield break; }
            presentation?.SetBackdropOpacity(1f - elapsed / FadeDuration);
            yield return null;
        }
        presentation?.SetBackdropOpacity(0f);
        yield return SceneManager.UnloadSceneAsync(gameObject.scene);
    }

    private void AbortEntry()
    {
        if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            SceneManager.SetActiveScene(gameObject.scene);
        readySince = -1f;
        presentation?.RestoreAfterInterruptedEntry();
        entering = false;
        if (AppState.I != null && string.IsNullOrEmpty(AppState.I.LastError))
            AppState.I.Fail("Startup interrupted. Check your connection and retry.");
    }
}
