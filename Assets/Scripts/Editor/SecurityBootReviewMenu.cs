using RunawayChimps.Loading;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only visual review; never supplies fake readiness or ships in a player build.</summary>
internal static class SecurityBootReviewMenu
{
    private const string MenuPath = "Tools/Runaway Chimps/Hold Security Boot For Review";
    private const string Key = "RunawayChimps.SecurityBoot.HoldReview";
    private const string SelectMenuPath = "Tools/Runaway Chimps/Select Active Security Boot Tuning";

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

    [MenuItem(SelectMenuPath)]
    private static void SelectActiveBoot()
    {
        var boot = Object.FindObjectOfType<SecurityBootPresentation>();
        if (boot == null)
        {
            Debug.LogWarning("No active SecurityBootPresentation. Start Play Mode from Bootstrap.unity first.");
            return;
        }

        Selection.activeGameObject = boot.gameObject;
        EditorGUIUtility.PingObject(boot.gameObject);
    }

    [MenuItem(SelectMenuPath, true)]
    private static bool ValidateSelectActiveBoot() => EditorApplication.isPlaying;
}
