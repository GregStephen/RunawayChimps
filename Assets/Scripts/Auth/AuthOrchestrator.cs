using System;
using Photon.Pun;          //
using Photon.Realtime;
using Photon.VR;     // optional
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class AuthOrchestrator
{
    private readonly PlayFabAuthService _playFab;
    private readonly bool _enforceQuestAuth;

    public AuthOrchestrator(PlayFabAuthService playFab, bool enforceQuestAuth)
    {
        _playFab = playFab;
        _enforceQuestAuth = enforceQuestAuth;
    }

    public void Run(MonoBehaviour runner, Action<string> onReady, Action<string> onFatal)
    {
        IAuthProvider provider = SelectProvider();

        provider.Authenticate(
            runner,
            auth =>
            {
                var playFabCustomId = auth.Provider == "Meta"
                    ? $"meta_{auth.UserId}"
                    : $"dev_{auth.UserId}";

                _playFab.LoginWithCustomId(
                    playFabCustomId,
                    createAccount: true,
                    onSuccess: result =>
                    {
                        //  Derive a display name
                        var profile = result.InfoResultPayload?.PlayerProfile;
                        var displayName = profile?.DisplayName;

                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            // fallback name if none is set in PlayFab yet
                            displayName = $"Chimp{UnityEngine.Random.Range(1000, 9999)}";
                            PlayFabClientAPI.UpdateUserTitleDisplayName(
                                new UpdateUserTitleDisplayNameRequest { DisplayName = displayName },
                                _ => Debug.Log("Initial display name set"),
                                e => Debug.LogError(e.GenerateErrorReport())
                                );
                        }

                        // THIS IS THE PLACE TO SET IT
                        PhotonVRManager.SetUsername(displayName);
                        onReady($"PlayFab OK. Provider={auth.Provider}, PlayFabId={result.PlayFabId}, NickName={displayName}");
                    },
                    onError: err =>
                    {
                        onFatal("PlayFab login failed: " + err.GenerateErrorReport());
                    }
                );
            },
            fail =>
            {
                if (_enforceQuestAuth)
                {
                    onFatal(fail);
                    return;
                }

                new DevCustomIdAuthProvider().Authenticate(
                    runner,
                    auth =>
                    {
                        var playFabCustomId = $"dev_{auth.UserId}";
                        _playFab.LoginWithCustomId(
                            playFabCustomId,
                            createAccount: true,
                            onSuccess: r =>
                            {
                                // Same logic for fallback path
                                var displayName =
                                    r.InfoResultPayload?.PlayerProfile?.DisplayName
                                    ?? $"Chimp{UnityEngine.Random.Range(1000, 9999)}";

                                PhotonNetwork.NickName = displayName;

                                onReady($"PlayFab OK (fallback). PlayFabId={r.PlayFabId}, NickName={PhotonNetwork.NickName}");
                            },
                            onError: e => onFatal("PlayFab login failed (fallback): " + e.GenerateErrorReport())
                        );
                    },
                    onFatal
                );
            }
        );
    }

    private IAuthProvider SelectProvider()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_enforceQuestAuth)
            return new QuestMetaAuthProvider();
#endif
        return new DevCustomIdAuthProvider();
    }
}
