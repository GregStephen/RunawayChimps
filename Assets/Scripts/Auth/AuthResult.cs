public readonly struct AuthResult
{
    public readonly string Provider;     // "Meta" or "DevCustomId"
    public readonly string UserId;       // Meta user id or custom id
    public readonly string UserProof;    // Meta proof (optional)
    public readonly bool IsVerified;     // true for Meta/entitlement

    public AuthResult(string provider, string userId, string userProof, bool isVerified)
    {
        Provider = provider;
        UserId = userId;
        UserProof = userProof;
        IsVerified = isVerified;
    }
}
