using System;
using UnityEngine;

public class AppState : MonoBehaviour
{
    public static AppState I { get; private set; }

    public bool IsReady { get; private set; }

    public event Action<string> OnStatusChanged;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ResetReady()
    {
        IsReady = false;
        SetStatus("Starting...");
    }

    public void SetStatus(string status)
    {
        OnStatusChanged?.Invoke(status);
    }

    public void MarkReady()
    {
        IsReady = true;
    }
}
