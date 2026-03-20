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

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

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

    public bool IsPreloaded(string sceneName) => _preloaded.Contains(sceneName) || _activated.Contains(sceneName);
    public bool IsActivated(string sceneName) => _activated.Contains(sceneName);

    public void Preload(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        if (IsPreloaded(sceneName)) return;
        if (_loading.ContainsKey(sceneName)) return;

        StartCoroutine(CoLoad(sceneName, allowActivation: false));
    }

    public void Activate(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        if (_activated.Contains(sceneName)) return;

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
        _activated.Add(sceneName);
        _preloaded.Add(sceneName);

        Debug.Log($"[LevelStreamService] Activated '{sceneName}'.");
        OnSceneActivated?.Invoke(sceneName);
    }
}
