using UnityEditor;

/// <summary>Editor-only visual review; never supplies fake readiness or ships in a player build.</summary>
internal static class SecurityBootReviewMenu
{
    private const string MenuPath = "Tools/Runaway Chimps/Hold Security Boot For Review";
    private const string Key = "RunawayChimps.SecurityBoot.HoldReview";

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        SessionState.SetBool(Key, !SessionState.GetBool(Key, false));
    }

    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        Menu.SetChecked(MenuPath, SessionState.GetBool(Key, false));
        return true;
    }
}
