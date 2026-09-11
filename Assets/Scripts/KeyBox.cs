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
    public bool IsComplete => currentKeys >= Mathf.Max(1, keysNeeded);

    private void Awake()
    {
        if (!travelToLevelTwoOnComplete) return;
        retryInteraction = GetComponent<XRSimpleInteractable>();
        if (retryInteraction == null) retryInteraction = gameObject.AddComponent<XRSimpleInteractable>();
        if (SectorTravelService.I != null)
            retryInteraction.interactionManager = SectorTravelService.I.InteractionManager;
        retryInteraction.selectEntered.AddListener(RetrySelected);
    }

    public bool TryAddKey(KeyCard card)
    {
        if (card == null || card.IsInserted || IsComplete || card.gameObject.scene != gameObject.scene)
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
        if (!travelToLevelTwoOnComplete && !IsComplete) AcceptKey();
    }

    private void AcceptKey()
    {
        currentKeys++;
        if (IsComplete)
        {
            // Keep the exit solid if loading fails; completion travels through a fade.
            if (travelToLevelTwoOnComplete) RetryCompletion();
            else if (door != null) door.OpenDoor();
        }
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
        if (IsComplete && travelToLevelTwoOnComplete && SectorTravelService.I != null)
            SectorTravelService.I.CompleteLevelOne(this);
    }

    private void OnDestroy()
    {
        if (retryInteraction != null) retryInteraction.selectEntered.RemoveListener(RetrySelected);
    }
}
