using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Removes only the obsolete MiniGamesKid visual rig while preserving the Crawler gameplay
/// root and every unrelated visual/gameplay branch. Zombie Crawl is always passed as keepVisual.
/// </summary>
public static class CrawlerLegacyVisualCleaner
{
    private static readonly HashSet<string> LegacyBoneNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "waist", "shoulderl", "shoulderr", "elbowl", "elbowr",
        "wristl", "wristr", "weaponl", "weaponr",
    };

    // These components live on the old gameplay/model root rather than inside the marker-bone
    // branch. They are only treated as legacy after a MiniGamesKid marker branch is positively
    // identified. RigBuilder depends on Animator, so runtime cleanup disables both instead of
    // asking Unity to destroy Animator while that dependency is still alive.
    private static readonly HashSet<string> LegacyRootSupportTypeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "RigBuilder", "BoneRenderer",
    };

    /// <summary>
    /// Disables only branches positively identified by MiniGamesKid bone markers. This is the
    /// zero-frame visual handoff used before deferred runtime deletion; unrelated renderers and
    /// animators below the Crawler root are intentionally untouched.
    /// </summary>
    public static int DisableLegacyVisuals(GameObject gameplayRoot, Transform keepVisual)
    {
        if (gameplayRoot == null)
            return 0;

        List<Transform> legacyRoots = FindLegacyTopLevelRoots(gameplayRoot.transform, keepVisual);
        if (legacyRoots.Count == 0)
            return 0;

        int changed = 0;
        for (int i = 0; i < legacyRoots.Count; i++)
        {
            Transform legacyRoot = legacyRoots[i];
            foreach (Renderer renderer in legacyRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsKept(renderer.transform, keepVisual) || !renderer.enabled)
                    continue;
                renderer.enabled = false;
                changed++;
            }

            foreach (Animator animator in legacyRoot.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || IsKept(animator.transform, keepVisual) || !animator.enabled)
                    continue;
                animator.enabled = false;
                changed++;
            }
        }

        changed += DisableLegacyRootSupportComponents(gameplayRoot, keepVisual);
        return changed;
    }

    /// <summary>
    /// Removes explicit MiniGamesKid marker branches. A visual-only legacy branch may be
    /// removed wholesale; if it also owns gameplay/physics/audio/state, only visual components
    /// inside that verified legacy branch are removed and the branch itself is preserved.
    /// </summary>
    public static int RemoveLegacyVisuals(GameObject gameplayRoot, Transform keepVisual, bool immediate)
    {
        if (gameplayRoot == null)
            return 0;

        List<Transform> legacyRoots = FindLegacyTopLevelRoots(gameplayRoot.transform, keepVisual);
        if (legacyRoots.Count == 0)
            return 0;

        int removed = 0;
        for (int i = 0; i < legacyRoots.Count; i++)
        {
            Transform legacyRoot = legacyRoots[i];
            if (legacyRoot == null || IsKept(legacyRoot, keepVisual))
                continue;

            if (IsVisualOnlySubtree(legacyRoot, keepVisual))
            {
                DestroyObject(legacyRoot.gameObject, immediate);
                removed++;
                continue;
            }

            // Anything else is gameplay/physics/audio/state until proven otherwise. Keep the
            // hierarchy and remove only render/animation baggage inside this verified branch.
            Renderer[] renderers = legacyRoot.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null || IsKept(renderer.transform, keepVisual))
                    continue;

                GameObject owner = renderer.gameObject;
                DestroyObject(renderer, immediate);
                removed++;

                MeshFilter filter = owner != null ? owner.GetComponent<MeshFilter>() : null;
                if (filter != null && !HasGameplayComponent(owner))
                {
                    DestroyObject(filter, immediate);
                    removed++;
                }
            }

            Animator[] animators = legacyRoot.GetComponentsInChildren<Animator>(true);
            for (int a = 0; a < animators.Length; a++)
            {
                Animator animator = animators[a];
                if (animator == null || IsKept(animator.transform, keepVisual))
                    continue;
                DestroyObject(animator, immediate);
                removed++;
            }
        }

        // Runtime Destroy is deferred and Unity refuses to destroy Animator while the old
        // RigBuilder still depends on it. Keep Play Mode deterministic by disabling those root
        // support components. The explicit Editor migration can remove dependencies immediately
        // in the correct order and then remove the obsolete root Animator.
        removed += immediate
            ? RemoveLegacyRootSupportComponentsImmediately(gameplayRoot, keepVisual)
            : DisableLegacyRootSupportComponents(gameplayRoot, keepVisual);

        return removed;
    }

    public static bool ContainsLegacyVisualRig(GameObject gameplayRoot, Transform keepVisual = null)
    {
        return gameplayRoot != null && FindLegacyTopLevelRoots(gameplayRoot.transform, keepVisual).Count > 0;
    }

    private static List<Transform> FindLegacyTopLevelRoots(Transform root, Transform keepVisual)
    {
        var results = new List<Transform>();
        if (root == null)
            return results;

        foreach (Transform child in root)
        {
            if (child == null || IsKept(child, keepVisual))
                continue;

            if (ContainsLegacyBoneMarker(child))
                results.Add(child);
        }

        return results;
    }

    private static int DisableLegacyRootSupportComponents(GameObject gameplayRoot, Transform keepVisual)
    {
        if (gameplayRoot == null)
            return 0;

        int changed = 0;
        Animator keptAnimator = GetAnimatorUnder(keepVisual);
        Component[] components = gameplayRoot.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            if (component is Animator animator)
            {
                if (animator != keptAnimator && animator.enabled)
                {
                    animator.enabled = false;
                    changed++;
                }
                continue;
            }

            if (!LegacyRootSupportTypeNames.Contains(component.GetType().Name))
                continue;

            if (component is Behaviour behaviour && behaviour.enabled)
            {
                behaviour.enabled = false;
                changed++;
            }
        }
        return changed;
    }

    private static int RemoveLegacyRootSupportComponentsImmediately(GameObject gameplayRoot, Transform keepVisual)
    {
        if (gameplayRoot == null)
            return 0;

        int removed = 0;
        Component[] components = gameplayRoot.GetComponents<Component>();

        // Remove every legacy component that may RequireComponent(Animator) first.
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || !LegacyRootSupportTypeNames.Contains(component.GetType().Name))
                continue;
            DestroyObject(component, true);
            removed++;
        }

        Animator rootAnimator = gameplayRoot.GetComponent<Animator>();
        Animator keptAnimator = GetAnimatorUnder(keepVisual);
        if (rootAnimator != null && rootAnimator != keptAnimator)
        {
            DestroyObject(rootAnimator, true);
            removed++;
        }

        return removed;
    }

    private static Animator GetAnimatorUnder(Transform keepVisual)
    {
        return keepVisual != null ? keepVisual.GetComponentInChildren<Animator>(true) : null;
    }

    private static bool IsKept(Transform candidate, Transform keepVisual)
    {
        if (candidate == null || keepVisual == null)
            return false;
        return candidate == keepVisual || candidate.IsChildOf(keepVisual);
    }

    private static bool ContainsLegacyBoneMarker(Transform subtree)
    {
        if (subtree == null)
            return false;

        Transform[] transforms = subtree.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && LegacyBoneNames.Contains(Normalize(candidate.name)))
                return true;
        }
        return false;
    }

    private static bool IsVisualOnlySubtree(Transform subtree, Transform keepVisual)
    {
        if (subtree == null || IsKept(subtree, keepVisual))
            return false;

        Component[] components = subtree.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            if (IsKept(component.transform, keepVisual))
                return false;

            if (component is Transform || component is Renderer || component is MeshFilter ||
                component is Animator || component is Animation || component is LODGroup)
                continue;

            return false;
        }

        return true;
    }

    private static bool HasGameplayComponent(GameObject owner)
    {
        if (owner == null)
            return false;

        Component[] components = owner.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component is Transform || component is Renderer || component is MeshFilter)
                continue;
            return true;
        }
        return false;
    }

    private static void DestroyObject(UnityEngine.Object target, bool immediate)
    {
        if (target == null)
            return;
        if (immediate) UnityEngine.Object.DestroyImmediate(target);
        else UnityEngine.Object.Destroy(target);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int length = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (!char.IsLetterOrDigit(ch))
                continue;
            buffer[length++] = char.ToLowerInvariant(ch);
        }
        return new string(buffer, 0, length);
    }
}
