using System;
using Photon.VR;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using UnityEngine;

public class AuthOrchestrator : MonoBehaviour
{
    [Header("PlayFab")] [SerializeField] private string titleId = "";
    [Header("Auth")] [SerializeField] private bool enforceQuestAuth;
    [Min(5f)] [SerializeField] private float authenticationTimeout = 30f;
    [Header("Economy")] [SerializeField] private string coconutCurrencyItemId;
    [SerializeField] private string grantLoginCoconutsFunctionName = "GrantLoginCoconuts";
    [SerializeField] private bool grantCoconutsOnLogin = true;

    private static AuthOrchestrator _instance;
    private bool _hasRun;
    private bool _isRunning;
    private int _attempt;
    private float _deadline;
    private Action<string> _onReady;
    private Action<string> _onFatal;
    private string _readyMessage;
    private string _playFabId;
    private string _entityId;
    private string _entityType;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        if (!string.IsNullOrWhiteSpace(titleId)) PlayFabSettings.staticSettings.TitleId = titleId;
    }

    public void Run(MonoBehaviour runner, Action<string> onReady, Action<string> onFatal)
    {
        if (_isRunning) return;
        if (_hasRun && PlayFabClientAPI.IsClientLoggedIn()) { onReady?.Invoke(_readyMessage); return; }
        _hasRun = false;
        _isRunning = true;
        int attempt = ++_attempt;
        _onReady = onReady;
        _onFatal = onFatal;
        _deadline = Time.realtimeSinceStartup + Mathf.Max(5f, authenticationTimeout);
        Guard(attempt, () => SelectProvider().Authenticate(runner,
            auth => Guard(attempt, () => LoginPlayFab(attempt, auth)),
            error => Guard(attempt, () =>
            {
                if (enforceQuestAuth) { Fail(attempt, error); return; }
                new DevCustomIdAuthProvider().Authenticate(runner,
                    auth => Guard(attempt, () => LoginPlayFab(attempt, auth)),
                    failure => Fail(attempt, failure));
            })));
    }

    private bool IsCurrent(int attempt) => this != null && isActiveAndEnabled && _isRunning && attempt == _attempt;

    private void Guard(int attempt, Action action)
    {
        if (!IsCurrent(attempt)) return;
        try { action(); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Fail(attempt, "Sign-in could not finish. Please retry.");
        }
    }

    private void Fail(int attempt, string error)
    {
        if (!IsCurrent(attempt)) return;
        _isRunning = _hasRun = false;
        var callback = _onFatal;
        _onReady = _onFatal = null;
        callback?.Invoke(error);
    }

    public void CancelPending()
    {
        ++_attempt; // Native callbacks may still arrive; discard their continuation.
        _isRunning = false;
        _onReady = _onFatal = null;
    }

    private void Update()
    {
        if (_isRunning && Time.realtimeSinceStartup >= _deadline)
            Fail(_attempt, "Sign-in timed out. Check your connection and retry.");
    }

    private void LoginPlayFab(int attempt, AuthResult auth)
    {
        if (string.IsNullOrWhiteSpace(auth.UserId)) { Fail(attempt, "Sign-in returned no player identity."); return; }
        var customId = (auth.Provider == "Meta" ? "meta_" : "dev_") + auth.UserId;
        // Existing development identity is preserved. Server-side Meta proof
        // validation/account migration remains a separate release requirement.
        PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
            }
        }, result => Guard(attempt, () =>
        {
            _playFabId = result.PlayFabId;
            _entityId = result.EntityToken?.Entity?.Id;
            _entityType = result.EntityToken?.Entity?.Type;
            if (string.IsNullOrWhiteSpace(_entityId) || string.IsNullOrWhiteSpace(_entityType))
            {
                Fail(attempt, "Sign-in returned no economy identity. Please retry.");
                return;
            }
            var displayName = result.InfoResultPayload?.PlayerProfile?.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayName)) { Complete(attempt, displayName); return; }
            displayName = "Chimp" + UnityEngine.Random.Range(1000, 10000);
            PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = displayName },
                saved => Guard(attempt, () => Complete(attempt, saved.DisplayName)),
                error => Guard(attempt, () =>
                {
                    Debug.LogWarning("Initial display name was not saved: " + error.ErrorMessage, this);
                    Complete(attempt, displayName);
                }));
        }), error => Fail(attempt, "Sign-in failed: " + error.ErrorMessage));
    }

    private void Complete(int attempt, string displayName)
    {
        PhotonVRManager.SetUsername(displayName);
        _readyMessage = "Signed in.";
        _hasRun = true;
        _isRunning = false;
        var callback = _onReady;
        _onReady = _onFatal = null;
        callback?.Invoke(_readyMessage);
        // Reconnecting to Photon must not grant the login reward again.
        RefreshLoginEconomy();
    }

    private void RefreshLoginEconomy()
    {
        string entityId = _entityId, entityType = _entityType;
        Action refresh = () => EconomyInventoryLoader.Refresh(entityId, entityType, coconutCurrencyItemId);
        if (!grantCoconutsOnLogin || string.IsNullOrWhiteSpace(grantLoginCoconutsFunctionName)) { refresh(); return; }
        try
        {
            PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest
            {
                FunctionName = grantLoginCoconutsFunctionName,
                FunctionParameter = new { PlayFabId = _playFabId, EntityId = entityId, EntityType = entityType },
                GeneratePlayStreamEvent = true
            }, result =>
            {
                if (result.Error != null) Debug.LogWarning("Login reward failed: " + result.Error.Message);
                refresh();
            }, error => { Debug.LogWarning("Login reward failed: " + error.ErrorMessage); refresh(); });
        }
        catch (Exception exception) { Debug.LogException(exception, this); refresh(); }
    }

    private IAuthProvider SelectProvider()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (enforceQuestAuth) return new QuestMetaAuthProvider();
#endif
        return new DevCustomIdAuthProvider();
    }

    private void OnDisable() => CancelPending();
    private void OnDestroy() { if (_instance == this) _instance = null; }
}
