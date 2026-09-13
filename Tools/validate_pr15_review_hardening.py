#!/usr/bin/env python3
"""Regression guards for issues found during the PR #15 merge/runtime reviews."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def text(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def require(errors, value, token, label):
    if token not in value:
        errors.append(f"{label}: missing {token!r}")


def main():
    errors = []

    visual_path = "Assets/Scripts/MonsterScripts/CrawlerVisualController.cs"
    visual = text(visual_path)
    require(errors, visual, "visualAnchor.localRotation = Quaternion.identity", visual_path)
    require(errors, visual, "CrawlerLegacyVisualCleaner.DisableLegacyVisuals(gameObject, zombieVisual)", visual_path)
    if "visualLocalEuler" in visual or "new Vector3(0f, 180f, 0f)" in visual:
        errors.append(f"{visual_path}: Zombie forward orientation must not be controlled by a stale serialized yaw")
    if "foreach (var renderer in GetComponentsInChildren<Renderer>" in visual:
        errors.append(f"{visual_path}: visual handoff must not disable every non-Zombie renderer")
    require(errors, visual, '"mixamorigrightforearm"', visual_path)

    follower_path = "Assets/Scripts/MonsterScripts/CrawlerBodyPathFollower.cs"
    follower = text(follower_path)
    for token in (
        "DefaultExecutionOrder(300)",
        "MaintainsAnimatedRootInPlace => true",
        'FindFirst(bones, "mixamorighips", "hips")',
        "alignmentFrame.InverseTransformPoint(animatedRootAnchor.position)",
        "alignmentFrame.TransformPoint(animatedRootReferenceInParentSpace)",
        "MaintainAnimatedRootInPlace();",
        "visualRoot.position += correction",
        "complete Zombie visual is being counter-translated",
    ):
        require(errors, follower, token, follower_path)
    for forbidden in (
        "animatedRootAnchor.position =",
        "frontAnchor.position =",
        "animatedRootAnchor.rotation =",
        "frontAnchor.rotation =",
    ):
        if forbidden in follower:
            errors.append(f"{follower_path}: in-place correction must move the complete visual, not write skeleton transforms")

    cleaner_path = "Assets/Scripts/MonsterScripts/CrawlerLegacyVisualCleaner.cs"
    cleaner = text(cleaner_path)
    for token in (
        "FindLegacyTopLevelRoots",
        "ContainsLegacyBoneMarker",
        "DisableLegacyVisuals(GameObject gameplayRoot, Transform keepVisual)",
        "Never infer legacy ownership from \"not Zombie\" alone.",
    ):
        require(errors, cleaner, token, cleaner_path)
    for forbidden in (
        "gameplayRoot.GetComponentsInChildren<Renderer>",
        "gameplayRoot.GetComponentsInChildren<Animator>",
        "gameplayRoot.GetComponentsInChildren<SkinnedMeshRenderer>",
    ):
        if forbidden in cleaner:
            errors.append(f"{cleaner_path}: broad non-Zombie cleanup returned via {forbidden!r}")

    editor_path = "Assets/Scripts/Editor/CrawlerLegacyVisualSceneCleanup.cs"
    editor = text(editor_path)
    require(errors, editor, "loadedScene.isDirty", editor_path)
    require(errors, editor, "will not mutate/save a dirty scene automatically", editor_path)
    for forbidden in ("[InitializeOnLoad]", "EditorApplication.delayCall", "SessionState"):
        if forbidden in editor:
            errors.append(f"{editor_path}: cleanup must be explicit/manual; found {forbidden!r}")

    ik_path = "Assets/Scripts/MonsterScripts/CrawlerSurfaceContactIK.cs"
    ik = text(ik_path)
    for token in (
        "discontinuityDistance = 1.25f",
        "GameplayRootDiscontinued()",
        "ResetArmState(leftArm);",
        "ResetArmState(rightArm);",
        "TryFindLastClearPoint(arm.lower.position",
        "TryFindLastClearPoint(arm.upper.position",
        "private readonly RaycastHit[] castHits = new RaycastHit[32]",
        "private readonly Collider[] overlapHits = new Collider[32]",
        "arm.upper.position",
        "arm.lastSafePosition = arm.hand.position",
    ):
        require(errors, ik, token, ik_path)

    if errors:
        for error in errors:
            print("ERROR:", error)
        print(f"FAILED: {len(errors)} PR #15 review-hardening contract issue(s).")
        return 1

    print(
        "PASS: PR #15 hardening protects marker-only legacy cleanup, serialization-proof Zombie forward setup, "
        "explicit scene migration, gameplay-owned/in-place Crawler translation, discontinuity-safe hand contact, "
        "and solved-position IK caching."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
