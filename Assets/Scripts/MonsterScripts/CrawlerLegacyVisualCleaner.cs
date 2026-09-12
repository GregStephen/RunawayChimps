using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Removes the obsolete MiniGamesKid visual rig while leaving the Crawler gameplay root
/// and its navigation/capture/audio/Photon components intact. Zombie Crawl is passed as
/// keepVisual after it is attached to the gameplay root and is never touched.
/// </summary>
public static class CrawlerLegacyVisualCleaner
{
    // These names belong to the old MiniGamesKid skeleton. Zombie Crawl uses mixamorig:*
    // bones, so exact normalized-name matching keeps the two rigs unambiguous.
    private static readonly HashSet<string> LegacyBoneNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "waist",
        "shoulderl",
        "shoulderr",
        "elbowl",
        "elbowr",
        "wristl",
        "wristr",
        "weaponl",
        "weaponr",
    };

    /// <summary>
    /// Removes legacy render/animation components and any visual-only top-level skeleton
    /// branches underneath gameplayRoot. Returns the number of removed objects/components.
    /// </summary>
    public static int RemoveLegacyVisuals(GameObject gameplayRoot, Transform keepVisual, bool immediate)
    {
        if (gameplayRoot == null)
            return 0;

        Transform root = gameplayRoot.transform;
        var visualOnlyRoots = new HashSet<Transform>();
        var legacyRenderers = new List<Renderer>();
        var legacyAnimators = new List<Animator>();

        // The old model uses skinned renderers whose bones identify the armature much more
        // reliably than transform distance or scene position. Capture those branches first.
        foreach (SkinnedMeshRenderer renderer in gameplayRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null || IsKept(renderer.transform, keepVisual))
                continue;

            legacyRenderers.Add(renderer);
            AddTopLevelCandidate(root, renderer.transform, visualOnlyRoots);
            AddTopLevelCandidate(root, renderer.rootBone, visualOnlyRoots);

            Transform[] bones = renderer.bones;
            if (bones == null)
                continue;

            for (int i = 0; i < bones.Length; i++)
                AddTopLevelCandidate(root, bones[i], visualOnlyRoots);
        }

        // The current scene still contains the old shoulderL/shoulderR/etc. hierarchy even
        // when its renderer is disabled. Find it explicitly so the armature disappears from
        // the live hierarchy instead of remaining as misleading editor/runtime clutter.
        foreach (Transform child in root)
        {
            if (child == null || IsKept(child, keepVisual))
                continue;

            if (ContainsLegacyBoneMarker(child))
                visualOnlyRoots.Add(child);
        }

        foreach (Renderer renderer in gameplayRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || IsKept(renderer.transform, keepVisual))
                continue;

            if (!legacyRenderers.Contains(renderer))
                legacyRenderers.Add(renderer);
        }

        foreach (Animator animator in gameplayRoot.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || animator == GetAnimatorUnder(keepVisual) || IsKept(animator.transform, keepVisual))
                continue;

            legacyAnimators.Add(animator);
            if (animator.transform != root)
                AddTopLevelCandidate(root, animator.transform, visualOnlyRoots);
        }

        int removed = 0;

        // Delete whole legacy branches only when they are genuinely visual-only. A branch
        // containing a collider, AudioSource, NavMeshAgent, MonoBehaviour, etc. is preserved;
        // its obsolete visual components are removed individually below instead.
        foreach (Transform candidate in visualOnlyRoots)
        {
            if (candidate == null || candidate == root || IsKept(candidate, keepVisual))
                continue;

            if (!IsVisualOnlySubtree(candidate, keepVisual))
                continue;

            DestroyObject(candidate.gameObject, immediate);
            removed++;
        }

        // Remove any legacy renderers that were not already taken out with a safe subtree.
        for (int i = 0; i < legacyRenderers.Count; i++)
        {
            Renderer renderer = legacyRenderers[i];
            if (renderer == null || IsKept(renderer.transform, keepVisual))
                continue;

            GameObject owner = renderer.gameObject;
            DestroyObject(renderer, immediate);
            removed++;

            // A MeshFilter paired only with a removed MeshRenderer is visual baggage too.
            if (owner != null)
            {
                MeshFilter filter = owner.GetComponent<MeshFilter>();
                if (filter != null && !HasGameplayComponent(owner))
                {
                    DestroyObject(filter, immediate);
                    removed++;
                }
            }
        }

        // The old Animator can sit directly on the gameplay root, so remove the component,
        // never the root GameObject. CrawlerVisualController supplies Zombie's Animator to
        // MonsterActivationGate after initialization.
        for (int i = 0; i < legacyAnimators.Count; i++)
        {
            Animator animator = legacyAnimators[i];
            if (animator == null || IsKept(animator.transform, keepVisual))
                continue;

            DestroyObject(animator, immediate);
            removed++;
        }

        return removed;
    }

    public static bool ContainsLegacyVisualRig(GameObject gameplayRoot, Transform keepVisual = null)
    {
        if (gameplayRoot == null)
            return false;

        Transform root = gameplayRoot.transform;
        foreach (Transform child in root)
        {
            if (child != null && !IsKept(child, keepVisual) && ContainsLegacyBoneMarker(child))
                return true;
        }

        foreach (SkinnedMeshRenderer renderer in gameplayRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer != null && !IsKept(renderer.transform, keepVisual))
                return true;
        }

        foreach (Animator animator in gameplayRoot.GetComponentsInChildren<Animator>(true))
        {
            if (animator != null && !IsKept(animator.transform, keepVisual))
                return true;
        }

        return false;
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

    private static void AddTopLevelCandidate(Transform root, Transform candidate, HashSet<Transform> results)
    {
        if (root == null || candidate == null || candidate == root || !candidate.IsChildOf(root))
            return;

        Transform cursor = candidate;
        while (cursor.parent != null && cursor.parent != root)
            cursor = cursor.parent;

        if (cursor.parent == root)
            results.Add(cursor);
    }

    private static bool ContainsLegacyBoneMarker(Transform subtree)
    {
        if (subtree == null)
            return false;

        foreach (Transform candidate in subtree.GetComponentsInChildren<Transform>(true))
        {
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

            Transform owner = component.transform;
            if (IsKept(owner, keepVisual))
                return false;

            if (component is Transform ||
                component is Renderer ||
                component is MeshFilter ||
                component is Animator ||
                component is Animation ||
                component is LODGroup)
            {
                continue;
            }

            // Anything else is gameplay/physics/audio/state until proven otherwise.
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

        if (immediate)
            UnityEngine.Object.DestroyImmediate(target);
        else
            UnityEngine.Object.Destroy(target);
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
