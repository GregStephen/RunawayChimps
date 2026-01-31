using System.Collections.Generic;
using System.Linq;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using PlayFab.EconomyModels;
using UnityEngine;

public class PlayFabLogin : MonoBehaviour
{
    [Header("PlayFab")]
    [SerializeField] private string titleId = ""; // set in Inspector

    // IMPORTANT: paste your FULL coconut currency ItemId GUID here
    [Header("Economy")]
    [SerializeField] private string coconutCurrencyItemId = "af2a94dc-6fd9-40e8-b289-c35e2565875f"; // e.g. "af2a94dc-6fd9-40e8-b289-..."

    private const string DeviceIdKey = "PF_CUSTOM_ID";

    private void Awake()
    {
        if (!string.IsNullOrEmpty(titleId))
            PlayFabSettings.staticSettings.TitleId = titleId;
    }

    private void Start()
    {
        var customId = GetOrCreateCustomId();

        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
    }

    private static string GetOrCreateCustomId()
    {
        if (PlayerPrefs.HasKey(DeviceIdKey))
            return PlayerPrefs.GetString(DeviceIdKey);

        var id = System.Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(DeviceIdKey, id);
        PlayerPrefs.Save();
        return id;
    }

    private void OnLoginSuccess(LoginResult result)
    {
        var playFabId = result.PlayFabId;
        var entityId = result.EntityToken.Entity.Id;
        var entityType = result.EntityToken.Entity.Type;

        Debug.Log($"PlayFab login OK. PlayFabId={playFabId} EntityId={entityId} EntityType={entityType}");

        PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest
        {
            FunctionName = "GrantLoginCoconuts",
            FunctionParameter = new
            {
                PlayFabId = playFabId,
                EntityId = entityId,
                EntityType = entityType
            },
            GeneratePlayStreamEvent = true
        },
        r =>
        {
            Debug.Log($"GrantLoginCoconuts OK. FunctionResult: {r.FunctionResult}");

            // Always refresh inventory after grant
            RefreshEconomyInventory(entityId, entityType);
        },
        e =>
        {
            Debug.LogError("GrantLoginCoconuts FAILED: " + e.GenerateErrorReport());

            // Still refresh inventory so the game can proceed
            RefreshEconomyInventory(entityId, entityType);
        });
    }


    private void RefreshEconomyInventory(string entityId, string entityType)
    {
        if (string.IsNullOrWhiteSpace(coconutCurrencyItemId))
        {
            Debug.LogError("coconutCurrencyItemId is empty (paste the currency GUID in the inspector).");
            return;
        }

        PlayFabEconomyAPI.GetInventoryItems(new GetInventoryItemsRequest
        {
            Entity = new PlayFab.EconomyModels.EntityKey { Id = entityId, Type = entityType }
        },
        r =>
        {
            var items = r.Items ?? new List<InventoryItem>();

            // 1) Coconuts
            var coconutItem = items.FirstOrDefault(i => i.Id == coconutCurrencyItemId);
            var coconuts = coconutItem?.Amount ?? 0;

            // 2) Owned cosmetics (everything except the currency item)
            // If your catalog “cosmetics” are normal items, they’ll be in Items with their Id.
            var owned = items
                .Where(i => i.Id != coconutCurrencyItemId)
                .Select(i => i.Id)
                .ToHashSet();

            EconomyState.Set(coconuts, owned);

            Debug.Log($"EconomyState updated. Coconuts={coconuts} OwnedCount={owned.Count}");
        },
        e =>
        {
            Debug.LogError("GetInventoryItems FAILED: " + e.GenerateErrorReport());
        });
    }

    private void OnLoginFailure(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
    }
}
