using System;
using UnityEngine;

public class DevCustomIdAuthProvider : IAuthProvider
{
    private const string Key = "PF_DEV_CUSTOM_ID";

    public void Authenticate(MonoBehaviour runner, Action<AuthResult> onSuccess, Action<string> onFailure)
    {
        var id = GetOrCreateId();
        onSuccess(new AuthResult("DevCustomId", id, userProof: null, isVerified: false));
    }

    private static string GetOrCreateId()
    {
        if (PlayerPrefs.HasKey(Key))
            return PlayerPrefs.GetString(Key);

        var id = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(Key, id);
        PlayerPrefs.Save();
        return id;
    }
}
