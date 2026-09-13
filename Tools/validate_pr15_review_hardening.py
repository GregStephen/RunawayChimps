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
    require(errors, visual, "ZombieVisualScaleMultiplier = 0.6f", visual_path)
    require(errors, visual, "navigation.modelForwardOffset = Vector3.zero", visual_path)
    require(errors, visual, "initialForward.y = 0f", visual_path)
    require(errors, visual, "visualAnchor.rotation = Quaternion.LookRotation(initialForward.normalized, Vector3.up)", visual_path)
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
        'MotionCompensationName = "CrawlerMotionCompensation"',
        'FindFirst(bones, "mixamorighips", "hips")',
        "motionCompensationRoot.InverseTransformPoint(animatedRootAnchor.position)",
        "float downwardRootSink = Mathf.Min(0f, totalLocalDrift.y)",
        "Vector3 compensatedLocalDrift = new Vector3(",
        "VentContainmentOffsetWorld => ventContainmentOffsetWorld",
        "SetVentContainmentOffsetWorld(Vector3 worldOffset, bool applyImmediately = false)",
        "visualAnchor.InverseTransformVector(ventContainmentOffsetWorld)",
        "calibratedMotionLocalPosition - compensatedLocalDrift + localContainmentOffset",
        "animatedRootFloorSinkWarning = 0.12f",
        "preserving upward crawl motion",
        "motionCompensationRoot.position += horizontalDelta",
        "motionCompensationRoot.localRotation = Quaternion.identity",
        "MaintainAnimatedRootInPlace();",
        "total horizontal internal root travel",
    ):
        require(errors, follower, token, follower_path)

    # Ownership is enforced semantically by forbidding every direct position/rotation write to
    # the Animator-owned Zombie root and core anchors. Do not rely on a comment string for this.
    for forbidden in (
        "animatedRootAnchor.position =",
        "frontAnchor.position =",
        "animatedRootAnchor.rotation =",
        "frontAnchor.rotation =",
        "visualRoot.position +=",
        "visualRoot.position =",
        "visualRoot.localPosition =",
        "visualRoot.rotation =",
        "visualRoot.localRotation =",
        "motionCompensationRoot.rotation =",
    ):
        if forbidden in follower:
            errors.append(
                f"{follower_path}: motion compensation must own rigid translation outside the Animator hierarchy; found {forbidden!r}"
            )

    heading_path = "Assets/Scripts/MonsterScripts/CrawlerVisualHeadingStabilizer.cs"
    heading = text(heading_path)
    for token in (
        "bodyPathFollower.VisualAnchor",
        "CrawlerMotionCompensation",
        "Quaternion.RotateTowards(",
        "No Zombie bone position/rotation is ever modified here.",
    ):
        require(errors, heading, token, heading_path)
    if "visualAnchor = visualRoot.parent" in heading:
        errors.append(
            f"{heading_path}: heading must use the explicit stable VisualAnchor, not whichever parent VisualRoot currently has"
        )

    cleaner_path = "Assets/Scripts/MonsterScripts/CrawlerLegacyVisualCleaner.cs"
    cleaner = text(cleaner_path)
    for token in (
        "FindLegacyTopLevelRoots",
        "ContainsLegacyBoneMarker",
        "DisableLegacyVisuals(GameObject gameplayRoot, Transform keepVisual)",
        "LegacyRootSupportTypeNames",
        '"RigBuilder"',
        '"BoneRenderer"',
        "DisableLegacyRootSupportComponents",
        "RemoveLegacyRootSupportComponentsImmediately",
        "DestroyObject(rootAnimator, true)",
    ):
        require(errors, cleaner, token, cleaner_path)
    for forbidden in (
        "gameplayRoot.GetComponentsInChildren<Renderer>",
        "gameplayRoot.GetComponentsInChildren<Animator>",
        "gameplayRoot.GetComponentsInChildren<SkinnedMeshRenderer>",
        "DestroyObject(rootAnimator, immediate)",
    ):
        if forbidden in cleaner:
            errors.append(f"{cleaner_path}: broad/non-ordered legacy cleanup returned via {forbidden!r}")

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
        "ResetAllLimbState();",
        "SolveArm(leftLeg);",
        "SolveArm(rightLeg);",
        "TryFindLastClearPoint(arm, arm.lower.position",
        "TryFindLastClearPoint(arm, arm.upper.position",
        "private readonly RaycastHit[] castHits = new RaycastHit[32]",
        "private readonly Collider[] overlapHits = new Collider[32]",
        '"mixamorigleftfoot"',
        '"mixamorigrightfoot"',
        "arm.upper.position",
        "arm.lastSafePosition = arm.hand.position",
    ):
        require(errors, ik, token, ik_path)

    containment_path = "Assets/Scripts/MonsterScripts/CrawlerVentContainment.cs"
    containment = text(containment_path)
    for token in (
        "DefaultExecutionOrder(325)",
        "Physics.ComputePenetration(",
        "Physics.SphereCastNonAlloc(",
        "maximumContainmentOffset = 0.32f",
        "head == null || chest == null || hips == null",
        "UpdateProbeHistory(preservePreviousSafePoints: offsetLimitedThisFrame)",
        "OverlapsStaticEnvironment(current, probe.radius)",
        "bodyFollower.SetVentContainmentOffsetWorld(desiredOffset, applyImmediately: true)",
        'AddProbe("head"',
        'AddProbe("chest"',
        'AddProbe("hips"',
        'AddProbe("left elbow"',
        'AddProbe("right elbow"',
        'AddProbe("left knee"',
        'AddProbe("right knee"',
        "collider.attachedRigidbody != null",
    ):
        require(errors, containment, token, containment_path)
    for forbidden in (
        "bone.position =",
        "bone.rotation =",
        "transform.position =",
        "transform.rotation =",
    ):
        if forbidden in containment:
            errors.append(f"{containment_path}: body containment must stay rigid/outside authored bones; found {forbidden!r}")

    if errors:
        for error in errors:
            print("ERROR:", error)
        print(f"FAILED: {len(errors)} PR #15 review-hardening contract issue(s).")
        return 1

    print(
        "PASS: PR #15 hardening protects marker-only legacy cleanup, serialization-proof Zombie forward setup, "
        "explicit scene migration, stable-anchor heading, non-animated Crawler motion compensation, horizontal travel "
        "cancellation with one-sided floor-height clamping, dependency-safe legacy-root cleanup, rigid head/core/joint "
        "vent containment, discontinuity-safe hand/foot contact, and solved-position IK caching."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
