using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingFlow : MonoBehaviour
{
    [SerializeField] private string hubSceneName = "Hub_Base";
    [SerializeField] private TMP_Text statusText;

    private void Start()
    {
        AppState.I?.ResetReady();
        SetStatus("Loading hub...");
        StartCoroutine(CoPreloadHubThenEnter());
    }

    private void SetStatus(string s)
    {
        if (statusText != null) statusText.text = s;
        AppState.I?.SetStatus(s);
    }

    private IEnumerator CoPreloadHubThenEnter()
    {
        // 1) Preload hub additively (but DO NOT activate it yet)
        var op = SceneManager.LoadSceneAsync(hubSceneName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;
        yield return null;

        // Mark hub loaded/available
        AppState.I?.MarkHubActive();
        AppState.I?.TryMarkReady();
        SetStatus("Connecting...");

        // 2) Wait until everything is truly ready (spawn + visuals + rig snap)
        while (AppState.I != null && !AppState.I.IsReady)
            yield return null;

        // 3) Final settle (optional but helps)
        yield return null;
        yield return new WaitForFixedUpdate();

        SetStatus("Entering...");

        // 4) NOW activate hub
        var hubScene = SceneManager.GetSceneByName(hubSceneName);
        SceneManager.SetActiveScene(hubScene);

        // 5) Unload Loading
        yield return SceneManager.UnloadSceneAsync("Loading");
    }
}
