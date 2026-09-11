using System.Collections;
using Photon.Pun;
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
    private AsyncOperation hubLoad;
    private bool startup;
    private bool entering;
    private bool triggerWasPressed;
    private float deadline;
    private string loadError;

    private void Start()
    {
        if (RunawayChimps.Travel.SectorTravelService.I != null && RunawayChimps.Travel.SectorTravelService.I.IsBusy)
        {
            if (statusText != null) statusText.text = "Loading...";
            return;
        }
        startup = true;
        deadline = Time.realtimeSinceStartup + Mathf.Max(10f, startupTimeout);
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
        if (state != null && state.IsReady && hubReady && PhotonNetwork.InRoom)
        {
            entering = true;
            StartCoroutine(EnterHub());
            return;
        }
        if (Time.realtimeSinceStartup >= deadline && state != null && string.IsNullOrEmpty(state.LastError))
            state.Fail("Startup timed out. Check your connection and retry.");
        string error = state != null ? state.LastError : "Bootstrap state is missing. Start from Bootstrap.";
        if (string.IsNullOrEmpty(error)) error = loadError;
        if (statusText != null)
            statusText.text = string.IsNullOrEmpty(error) ? (hubReady ? state?.Status : "Loading hub...") :
                error + "\nPress either trigger to retry. (Desktop: R)";
        bool pressed = TriggerPressed(XRNode.LeftHand) || TriggerPressed(XRNode.RightHand);
        bool retry = (pressed && !triggerWasPressed) || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);
        triggerWasPressed = pressed;
        if (!string.IsNullOrEmpty(error) && retry) Retry();
    }

    private static bool TriggerPressed(XRNode hand) => InputDevices.GetDeviceAtXRNode(hand)
        .TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool pressed) && pressed;

    public void Retry()
    {
        if (!startup || entering) return;
        deadline = Time.realtimeSinceStartup + Mathf.Max(10f, startupTimeout);
        AppState.I?.ClearFailure();
        BeginHubLoad(); // Reuse an in-flight native scene load; never duplicate it.
        if (GameBootstrap.I != null) GameBootstrap.I.RetryStartup();
        else AppState.I?.Fail("Bootstrap service is missing. Start from Bootstrap.");
    }

    private IEnumerator EnterHub()
    {
        yield return null;
        yield return new WaitForFixedUpdate();
        if (!PhotonNetwork.InRoom || AppState.I == null || !AppState.I.IsReady)
        {
            entering = false;
            yield break;
        }
        var hub = SceneManager.GetSceneByName(hubSceneName);
        if (!SceneManager.SetActiveScene(hub))
        {
            AppState.I.Fail("Could not activate the hub.");
            entering = false;
            yield break;
        }
        RunawayChimps.Travel.SectorTravelService.I?.NotifySceneReady(hub);
        yield return SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}
