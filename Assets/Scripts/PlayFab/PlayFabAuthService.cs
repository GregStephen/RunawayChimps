using System;
using PlayFab;
using PlayFab.ClientModels;

public class PlayFabAuthService
{
    public void Initialize(string titleId)
    {
        if (!string.IsNullOrEmpty(titleId))
            PlayFabSettings.staticSettings.TitleId = titleId;
    }

    public void LoginWithCustomId(
        string customId,
        bool createAccount,
        Action<LoginResult> onSuccess,
        Action<PlayFabError> onError)
    {
        var req = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = createAccount,

            //This makes result.InfoResultPayload.PlayerProfile available
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                ProfileConstraints = new PlayerProfileViewConstraints
                {
                    ShowDisplayName = true
                }
            }
        };

        PlayFabClientAPI.LoginWithCustomID(req, onSuccess, onError);
    }
}
