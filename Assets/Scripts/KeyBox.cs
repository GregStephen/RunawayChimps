using System;
using System.Collections.Generic;
using RunawayChimps.Travel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public class KeyBox : MonoBehaviour
{
    [Header("Settings")]
    public int keysNeeded = 2;

    [Header("Assigned in Inspector")]
    public SlidingDoor door;
    [Tooltip("Enable only for the personal Level 1 completion keybox.")]
    public bool travelToLevelTwoOnComplete;

    private int currentKeys = 0;
    private readonly HashSet<int> acceptedCards = new HashSet<int>();
    private XRSimpleInteractable retryInteraction;
    private bool acceptingKey;

    public int CurrentKeys => currentKeys;
    public int RequiredKeys => Mathf.Max(1, keysNeeded);
    public bool IsComplete => currentKeys >= RequiredKeys;
    public event Action<int, int> ProgressChanged;

    private void Awake()
    {
        if (!travelToLevelTwoOnComplete) return;
        retryInteraction = GetComponent<XRSimpleInteractable>();
        if (retryInteraction == null) retryInteraction = gameObject.AddComponent<XRSimpleInteractable>();
        // This invisible target is for failed-travel retry, not ordinary
        // card pickup. Do not let it compete with cards while the lock is incomplete.
        retryInteraction.enabled = false;
        if (SectorTravelService.I != null)
            retryInteraction.interactionManager = SectorTravelService.I.InteractionManager;
        retryInteraction.selectEntered.AddListener(RetrySelected);
    }

    public bool TryAddKey(KeyCard card)
    {
        if (!isActiveAndEnabled || acceptingKey || card == null || !card.isActiveAndEnabled ||
            card.IsInserted || IsComplete || card.gameObject.scene != gameObject.scene)
            return false;
        if (travelToLevelTwoOnComplete && (!card.WasHeldByLocalPlayer ||
            gameObject.scene != SceneManager.GetActiveScene() || SectorTravelService.I == null ||
            SectorTravelService.I.IsBusy || SectorTravelService.I.CurrentSector != SectorId.Containment))
            return false;
        if (!acceptedCards.Add(card.GetInstanceID())) return false;
        AcceptKey();
        return true;
    }

    // Preserve older non-travel UnityEvent bindings; completion requires a local card.
    public void AddKey()
    {
        if (isActiveAndEnabled && !acceptingKey && !travelToLevelTwoOnComplete && !IsComplete) AcceptKey();
    }

    private void AcceptKey()
    {
        acceptingKey = true;
        currentKeys++;
        try
        {
            NotifyProgressChanged();
            if (this != null && isActiveAndEnabled && IsComplete)
            {
                // Progress is authoritative even if a presentation/travel callback
                // fails. Completed Level 1 progress remains available for retry.
                if (travelToLevelTwoOnComplete) RetryCompletion();
                else if (door != null) door.OpenDoor();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            acceptingKey = false;
        }
    }

    private void NotifyProgressChanged()
    {
        Action<int, int> listeners = ProgressChanged;
        if (listeners == null)
            return;

        int acceptedKeys = CurrentKeys;
        int requiredKeys = RequiredKeys;
        foreach (Action<int, int> callback in listeners.GetInvocationList())
        {
            try
            {
                callback(acceptedKeys, requiredKeys);
            }
            catch (Exception exception)
            {
                // A broken lamp listener must not prevent consumption, completion,
                // or delivery to the remaining progress listeners.
                Debug.LogException(exception, this);
            }
        }
    }

    private void Update()
    {
        UpdateRetryInteraction();
    }

    private void UpdateRetryInteraction()
    {
        if (retryInteraction == null)
            return;

        bool available = isActiveAndEnabled && travelToLevelTwoOnComplete && IsComplete &&
            gameObject.scene == SceneManager.GetActiveScene() && SectorTravelService.I != null &&
            !SectorTravelService.I.IsBusy && SectorTravelService.I.CurrentSector == SectorId.Containment;
        if (retryInteraction.enabled != available)
            retryInteraction.enabled = available;
    }

    private void OnDisable()
    {
        if (retryInteraction != null)
            retryInteraction.enabled = false;
    }

    private void RetrySelected(SelectEnterEventArgs args)
    {
        if (args.interactorObject.transform.GetComponentInParent<LocalRigMarker>() != null)
            RetryCompletion();
    }

    // If travel fails, completed progress stays in the source scene for a local retry.
    [ContextMenu("Travel/Retry completed Level 1")]
    public void RetryCompletion()
    {
        if (isActiveAndEnabled && IsComplete && travelToLevelTwoOnComplete && SectorTravelService.I != null)
            SectorTravelService.I.CompleteLevelOne(this);
    }

    private void OnDestroy()
    {
        if (retryInteraction != null) retryInteraction.selectEntered.RemoveListener(RetrySelected);
    }
}
