using System;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using Oculus.Platform;
using Oculus.Platform.Models;
#endif

public class QuestMetaAuthProvider : IAuthProvider
{
    public void Authenticate(MonoBehaviour runner, Action<AuthResult> onSuccess, Action<string> onFailure)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Core.AsyncInitialize().OnComplete(initMsg =>
            {
                if (initMsg.IsError)
                {
                    onFailure("Meta Platform init failed: " + initMsg.GetError().Message);
                    return;
                }

                Entitlements.IsUserEntitledToApplication().OnComplete(entMsg =>
                {
                    if (entMsg.IsError)
                    {
                        onFailure("Entitlement failed: " + entMsg.GetError().Message);
                        return;
                    }

                    Users.GetLoggedInUser().OnComplete(userMsg =>
                    {
                        if (userMsg.IsError)
                        {
                            onFailure("GetLoggedInUser failed: " + userMsg.GetError().Message);
                            return;
                        }

                        var user = userMsg.Data;

                        Users.GetUserProof().OnComplete(proofMsg =>
                        {
                            if (proofMsg.IsError)
                            {
                                onFailure("GetUserProof failed: " + proofMsg.GetError().Message);
                                return;
                            }

                            var proof = proofMsg.Data.Value;

                            // user.ID is a ulong - convert to string
                            onSuccess(new AuthResult(
                                provider: "Meta",
                                userId: user.ID.ToString(),
                                userProof: proof,
                                isVerified: true
                            ));
                        });
                    });
                });
            });
        }
        catch (Exception ex)
        {
            onFailure("Meta Platform exception: " + ex);
        }
#else
        onFailure("QuestMetaAuthProvider called on non-Quest build.");
#endif
    }
}
