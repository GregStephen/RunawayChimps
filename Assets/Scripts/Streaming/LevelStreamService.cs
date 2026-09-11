using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelStreamService : MonoBehaviour
{
    public static LevelStreamService I { get; private set; }

    public event Action<string> OnScenePreloaded; // reached 0.9, waiting activation
    public event Action<string> OnSceneActivated; // fully done

    private readonly Dictionary<string, AsyncOperation> _loading = new();
    private readonly HashSet<string> _activated = new();
    private readonly HashSet<string> _preloaded = new();
    private readonly HashSet<string> _abandoned = new();
    private readonly Dictionary<string, AsyncOperation> _unloading = new();

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded)
            {
                _activated.Add(s.name);
                _preloaded.Add(s.name);
            }
        }
    }

    public bool IsPreloaded(string sceneName) => _preloaded.Contains(sceneName) || IsActivated(sceneName);
    public bool IsActivated(string sceneName) => SceneManager.GetSceneByName(sceneName).isLoaded;

    public bool Preload(string sceneName)
    {
        if (RunawayChimps.Travel.SectorTravelService.I != null && RunawayChimps.Travel.SectorTravelService.I.IsBusy) return false;
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
        if (IsPreloaded(sceneName) || _loading.ContainsKey(sceneName)) return true;

        StartCoroutine(CoLoad(sceneName, allowActivation: false));
        return true;
    }

    public void Activate(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        if (IsActivated(sceneName)) return;

        if (_loading.TryGetValue(sceneName, out var op))
        {
            op.allowSceneActivation = true;
            return;
        }

        // If not preloading yet, just load + activate immediately
        StartCoroutine(CoLoad(sceneName, allowActivation: true));
    }

    private IEnumerator CoLoad(string sceneName, bool allowActivation)
    {
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null) throw new InvalidOperationException("Could not load scene: " + sceneName);
        op.allowSceneActivation = allowActivation;
        _loading[sceneName] = op;

        // Wait until preloaded (0.9)
        while (op.progress < 0.9f)
            yield return null;

        if (!_preloaded.Contains(sceneName))
        {
            _preloaded.Add(sceneName);
            Debug.Log($"[LevelStreamService] Preloaded '{sceneName}' (0.9).");
            OnScenePreloaded?.Invoke(sceneName);
        }

        // If activation allowed, op will complete; otherwise it sits here until Activate()
        while (!op.isDone)
            yield return null;

        _loading.Remove(sceneName);
        if (_abandoned.Remove(sceneName))
        {
            HideAndDiscard(sceneName);
            yield break;
        }
        _activated.Add(sceneName);
        _preloaded.Add(sceneName);

        Debug.Log($"[LevelStreamService] Activated '{sceneName}'.");
        OnSceneActivated?.Invoke(sceneName);
    }

    public IEnumerator LoadForTravel(string sceneName, float timeout)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
            throw new InvalidOperationException("Scene is not enabled in Build Settings: " + sceneName);

        float deadline = Time.realtimeSinceStartup + timeout;
        // A preload held at 0.9 stalls Unity's entire async operation queue.
        foreach (var pending in new List<string>(_loading.Keys))
        {
            if (pending == sceneName) _loading[pending].allowSceneActivation = true;
            else AbandonLoad(pending); // Drained preloads must not leave unrelated environments active.
        }
        while (_abandoned.Contains(sceneName) ||
               (_unloading.TryGetValue(sceneName, out var unloading) && !unloading.isDone))
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Previous scene load is still finishing.");
            yield return null;
        }
        _unloading.Remove(sceneName);
        Activate(sceneName);
        while (!IsActivated(sceneName) || _loading.ContainsKey(sceneName))
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Scene loading timed out: " + sceneName);
            yield return null;
        }
    }

    public IEnumerator UnloadForTravel(string sceneName, float timeout)
    {
        if (!IsActivated(sceneName)) yield break;
        if (SceneManager.GetActiveScene().name == sceneName)
            throw new InvalidOperationException("Refusing to unload the active environment.");
        if (!_unloading.TryGetValue(sceneName, out var op))
        {
            op = SceneManager.UnloadSceneAsync(sceneName);
            if (op == null) throw new InvalidOperationException("Could not unload scene: " + sceneName);
            _unloading[sceneName] = op;
        }
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!op.isDone)
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Scene cleanup is still finishing: " + sceneName);
            yield return null;
        }
    }

    public void AbandonLoad(string sceneName)
    {
        if (_loading.TryGetValue(sceneName, out var op))
        {
            _abandoned.Add(sceneName);
            op.allowSceneActivation = true; // Native loads cannot be cancelled; drain then discard.
        }
        else HideAndDiscard(sceneName);
    }

    private void HideAndDiscard(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded || SceneManager.GetActiveScene() == scene) return;
        foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
        if (!_unloading.ContainsKey(sceneName))
        {
            var op = SceneManager.UnloadSceneAsync(scene);
            if (op != null) _unloading[sceneName] = op;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_abandoned.Contains(scene.name))
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
        _activated.Add(scene.name);
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        _activated.Remove(scene.name);
        _preloaded.Remove(scene.name);
        _unloading.Remove(scene.name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        if (I == this) I = null;
    }
}
