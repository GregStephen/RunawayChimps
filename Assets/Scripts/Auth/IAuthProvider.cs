using System;
using UnityEngine;

public interface IAuthProvider
{
    void Authenticate(MonoBehaviour runner, Action<AuthResult> onSuccess, Action<string> onFailure);
}
