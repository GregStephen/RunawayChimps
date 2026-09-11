using System;
using UnityEngine;

public class AppState : MonoBehaviour
{
    public static AppState I { get; private set; }

    public bool IsReady { get; private set; }
    public string Status { get; private set; } = "Starting...";
    public string LastError { get; private set; }

    // Stage flags
    public bool HubActive { get; private set; }
    public bool RigSnapped { get; private set; }
    public bool PhotonPlayerSpawned { get; private set; }
    public bool PlayerVisualsReady { get; private set; }

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

        ResetReady();
    }

    public void ResetReady()
    {
        LastError = null;
        IsReady = false;
        HubActive = false;
        RigSnapped = false;
        PhotonPlayerSpawned = false;
        PlayerVisualsReady = false;
        SetStatus("Starting...");
    }

    public void SetStatus(string status)
    {
        Status = status;
        OnStatusChanged?.Invoke(status);
    }

    public void Fail(string message)
    {
        LastError = message;
        SetStatus(message);
    }

    public void ClearFailure() => LastError = null;

    public void ResetPlayerReady()
    {
        IsReady = PhotonPlayerSpawned = PlayerVisualsReady = false;
    }

    // Stage markers
    public void MarkHubActive() => HubActive = true;
    public void MarkRigSnapped() => RigSnapped = true;
    public void MarkPhotonPlayerSpawned() => PhotonPlayerSpawned = true;
    public void MarkPlayerVisualsReady() => PlayerVisualsReady = true;
    public void TryMarkReady()
    {
        if (!IsReady && string.IsNullOrEmpty(LastError) && HubActive && RigSnapped && PhotonPlayerSpawned && PlayerVisualsReady)
        {
            IsReady = true;
            RunawayChimps.Travel.SectorTravelService.I?.NotifySceneReady(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }

    private void OnDestroy() { if (I == this) I = null; }
}
