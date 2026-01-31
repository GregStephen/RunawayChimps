using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingFlow : MonoBehaviour
{
    [SerializeField] private string hubSceneName = "Hub";
    [SerializeField] private TMP_Text statusText;

    private void OnEnable()
    {
        if (AppState.I != null)
            AppState.I.OnStatusChanged += HandleStatus;
    }

    private void OnDisable()
    {
        if (AppState.I != null)
            AppState.I.OnStatusChanged -= HandleStatus;
    }

    private void Start()
    {
        // Set initial text
        if (AppState.I != null)
            HandleStatus("Loading...");
        StartCoroutine(CoWaitAndLoad());
    }

    private void HandleStatus(string s)
    {
        if (statusText != null)
            statusText.text = s;
    }

    private IEnumerator CoWaitAndLoad()
    {
        // Wait until your networking/player spawn signals ready
        while (AppState.I != null && !AppState.I.IsReady)
            yield return null;

        SceneManager.LoadScene(hubSceneName, LoadSceneMode.Single);
    }
}
