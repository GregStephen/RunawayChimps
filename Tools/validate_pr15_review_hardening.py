#!/usr/bin/env python3
"""Regression guards for issues found during the PR #15 merge review."""
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
    require(errors, visual, "visualLocalEuler = Vector3.zero", visual_path)
    require(errors, visual, "CrawlerLegacyVisualCleaner.DisableLegacyVisuals(gameObject, zombieVisual)", visual_path)
    if "new Vector3(0f, 180f, 0f)" in visual:
        errors.append(f"{visual_path}: obsolete 180-degree Zombie yaw was reintroduced")
    if "foreach (var renderer in GetComponentsInChildren<Renderer>" in visual:
        errors.append(f"{visual_path}: visual handoff must not disable every non-Zombie renderer")

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
    ):
        require(errors, ik, token, ik_path)

    if errors:
        for error in errors:
            print("ERROR:", error)
        print(f"FAILED: {len(errors)} PR #15 review-hardening contract issue(s).")
        return 1

    print("PASS: PR #15 review hardening protects narrow legacy cleanup, zero-yaw Zombie setup, explicit scene migration, and discontinuity-safe hand contact IK.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
