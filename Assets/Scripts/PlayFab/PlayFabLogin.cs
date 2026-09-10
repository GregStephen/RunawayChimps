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
        if (result.EntityToken?.Entity == null)
        {
            Debug.LogError("Sign-in returned no economy identity.");
            return;
        }
        var entityId = result.EntityToken.Entity.Id;
        var entityType = result.EntityToken.Entity.Type;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"PlayFab login OK. PlayFabId={playFabId} EntityId={entityId} EntityType={entityType}");
#endif

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
            if (r.Error != null) Debug.LogWarning("Login reward failed: " + r.Error.Message);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"GrantLoginCoconuts OK. FunctionResult: {r.FunctionResult}");
#endif

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

        EconomyInventoryLoader.Refresh(entityId, entityType, coconutCurrencyItemId);
    }

    private void OnLoginFailure(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
    }
}
