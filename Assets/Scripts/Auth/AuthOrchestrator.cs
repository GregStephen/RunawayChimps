using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.VR;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using PlayFab.EconomyModels;
using UnityEngine;

public class AuthOrchestrator : MonoBehaviour
{
    [Header("PlayFab")]
    [Tooltip("Optional: set TitleId here. If you already set it elsewhere, leave blank.")]
    [SerializeField] private string titleId = "";

    [Header("Auth")]
    [SerializeField] private bool enforceQuestAuth = false;

    [Header("Economy")]
    [Tooltip("Paste your coconut currency ItemId GUID here.")]
    [SerializeField] private string coconutCurrencyItemId;

    [Tooltip("CloudScript function name to grant login coconuts.")]
    [SerializeField] private string grantLoginCoconutsFunctionName = "GrantLoginCoconuts";

    [Tooltip("If true, will call GrantLoginCoconuts on login then refresh inventory.")]
    [SerializeField] private bool grantCoconutsOnLogin = true;

    private static AuthOrchestrator _instance;
    private static bool _hasRun;
    private bool _isRunning;

    private string _playFabId;
    private string _entityId;
    private string _entityType;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (!string.IsNullOrWhiteSpace(titleId))
            PlayFabSettings.staticSettings.TitleId = titleId;
    }

    public void Run(MonoBehaviour runner, Action<string> onReady, Action<string> onFatal)
    {
        if (_hasRun || _isRunning) return;
        _hasRun = true;
        _isRunning = true;

        IAuthProvider provider = SelectProvider();

        provider.Authenticate(
            runner,
            auth =>
            {
                // ONE stable PlayFab customId per provider/user
                var playFabCustomId = auth.Provider == "Meta"
                    ? $"meta_{auth.UserId}"
                    : $"dev_{auth.UserId}";

                LoginPlayFab(
                    playFabCustomId,
                    onSuccess: () =>
                    {
                        EnsureDisplayNameThenContinue(auth.Provider, onReady, onFatal);
                    },
                    onFatal: onFatal
                );
            },
            fail =>
            {
                if (enforceQuestAuth)
                {
                    onFatal(fail);
                    return;
                }

                // fallback to dev auth only if not enforcing quest auth
                new DevCustomIdAuthProvider().Authenticate(
                    runner,
                    auth =>
                    {
                        var playFabCustomId = $"dev_{auth.UserId}";

                        LoginPlayFab(
                            playFabCustomId,
                            onSuccess: () =>
                            {
                                EnsureDisplayNameThenContinue("Dev", onReady, onFatal);
                            },
                            onFatal: onFatal
                        );
                    },
                    onFatal
                );
            }
        );
    }

    private void LoginPlayFab(string customId, Action onSuccess, Action<string> onFatal)
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                ProfileConstraints = new PlayerProfileViewConstraints
                {
                    ShowDisplayName = true
                }
            }
        };

        PlayFabClientAPI.LoginWithCustomID(
            request,
            result =>
            {
                _playFabId = result.PlayFabId;
                _entityId = result.EntityToken?.Entity?.Id;
                _entityType = result.EntityToken?.Entity?.Type;

                if (string.IsNullOrWhiteSpace(_entityId) || string.IsNullOrWhiteSpace(_entityType))
                {
                    onFatal("PlayFab login succeeded but EntityToken was missing. Economy requires EntityToken.");
                    return;
                }

                // Cache profile display name if present (we’ll re-read from payload)
                _cachedProfileDisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName;

                Debug.Log($"PlayFab login OK. CustomId={customId} PlayFabId={_playFabId} Entity={_entityType}:{_entityId}");

                onSuccess?.Invoke();
            },
            error =>
            {
                onFatal("PlayFab login failed: " + error.GenerateErrorReport());
            }
        );
    }

    private string _cachedProfileDisplayName;

    private void EnsureDisplayNameThenContinue(string providerName, Action<string> onReady, Action<string> onFatal)
    {
        var displayName = _cachedProfileDisplayName;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = $"Chimp{UnityEngine.Random.Range(1000, 9999)}";
            PlayFabClientAPI.UpdateUserTitleDisplayName(
                new UpdateUserTitleDisplayNameRequest { DisplayName = displayName },
                _ =>
                {
                    Debug.Log($"Initial display name set to '{displayName}'");
                    ContinueAfterNameKnown(displayName, providerName, onReady, onFatal);
                },
                e =>
                {
                    Debug.LogError("UpdateUserTitleDisplayName FAILED: " + e.GenerateErrorReport());
                    // Continue anyway so the game proceeds
                    ContinueAfterNameKnown(displayName, providerName, onReady, onFatal);
                }
            );
            return;
        }

        ContinueAfterNameKnown(displayName, providerName, onReady, onFatal);
    }

    private void ContinueAfterNameKnown(string displayName, string providerName, Action<string> onReady, Action<string> onFatal)
    {
        // Set Photon names consistently
        PhotonVRManager.SetUsername(displayName);
        PhotonNetwork.NickName = displayName;

        onReady?.Invoke($"PlayFab OK. Provider={providerName}, PlayFabId={_playFabId}, NickName={displayName}");

        // Economy: grant then refresh
        if (!grantCoconutsOnLogin)
        {
            RefreshEconomyInventory();
            return;
        }

        GrantLoginCoconuts(
            onDone: RefreshEconomyInventory,
            onFail: RefreshEconomyInventory
        );
    }

    private void GrantLoginCoconuts(Action onDone, Action onFail)
    {
        if (string.IsNullOrWhiteSpace(grantLoginCoconutsFunctionName))
        {
            Debug.LogWarning("GrantLoginCoconuts skipped: function name is empty.");
            onDone?.Invoke();
            return;
        }

        PlayFabCloudScriptAPI.ExecuteFunction(
            new ExecuteFunctionRequest
            {
                FunctionName = grantLoginCoconutsFunctionName,
                FunctionParameter = new
                {
                    PlayFabId = _playFabId,
                    EntityId = _entityId,
                    EntityType = _entityType
                },
                GeneratePlayStreamEvent = true
            },
            r =>
            {
                Debug.Log($"GrantLoginCoconuts OK. FunctionResult: {r.FunctionResult}");
                onDone?.Invoke();
            },
            e =>
            {
                Debug.LogError("GrantLoginCoconuts FAILED: " + e.GenerateErrorReport());
                onFail?.Invoke();
            }
        );
    }

    private void RefreshEconomyInventory()
    {
        if (string.IsNullOrWhiteSpace(coconutCurrencyItemId))
        {
            Debug.LogError("coconutCurrencyItemId is empty. Paste the currency GUID in AuthOrchestrator inspector.");
            return;
        }

        PlayFabEconomyAPI.GetInventoryItems(
            new GetInventoryItemsRequest
            {
                Entity = new PlayFab.EconomyModels.EntityKey { Id = _entityId, Type = _entityType }
            },
            r =>
            {
                var items = r.Items ?? new List<InventoryItem>();

                // Currency amount
                var coconutItem = items.FirstOrDefault(i => i.Id == coconutCurrencyItemId);
                var coconuts = coconutItem?.Amount ?? 0;

                // Owned cosmetics (everything except currency)
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
            }
        );
    }

    private IAuthProvider SelectProvider()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (enforceQuestAuth)
            return new QuestMetaAuthProvider();
#endif
        return new DevCustomIdAuthProvider();
    }
}
