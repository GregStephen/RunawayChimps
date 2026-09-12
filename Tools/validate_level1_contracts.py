#!/usr/bin/env python3
"""Focused source contracts for the active Level 1/runtime fixes.

These checks catch accidental source/scene regressions. They do not prove Unity runtime,
Photon, XR/headset, animation deformation, lighting quality, or Quest performance.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def rel(path):
    return path.relative_to(ROOT).as_posix()


def read(path):
    return path.read_text(encoding="utf-8-sig")


def require(errors, path, tokens):
    if not path.exists():
        errors.append(f"{rel(path)}: required file is missing.")
        return ""
    text = read(path)
    for token in tokens:
        if token not in text:
            errors.append(f"{rel(path)}: missing contract token {token!r}.")
    return text


def positive_defaults(errors, path, text, fields):
    values = {}
    for field in fields:
        match = re.search(rf"\b{re.escape(field)}\s*=\s*(-?\d+(?:\.\d+)?)f?\s*;", text)
        if not match:
            errors.append(f"{rel(path)}: cannot read default {field}.")
            continue
        value = float(match.group(1))
        values[field] = value
        if value <= 0:
            errors.append(f"{rel(path)}: {field} must remain > 0 (found {value}).")
    return values


def guid(path, errors):
    match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", read(path), re.M) if path.exists() else None
    if not match:
        errors.append(f"{rel(path)}: required script GUID is missing.")
        return None
    return match.group(1).lower()


def mono_blocks(text):
    return re.findall(r"^--- !u!114 &-?\d+\s*$.*?(?=^--- !u!|\Z)", text, re.M | re.S)


def main():
    errors = []

    headlamp_path = ROOT / "Assets/Scripts/Lighting/VentHeadlampController.cs"
    headlamp = require(errors, headlamp_path, [
        'LevelOneScene = "Level1_Containment"',
        'RuntimeLightName = "Local_Vent_Headlamp"',
        "toolEquipped &&",
        "ZoneId.Level1_Vents",
        "trackedCamera.transform.Find(RuntimeLightName)",
        "LightType.Spot",
        "LightShadows.None",
        "SetToolEquipped(bool equipped)",
    ])
    if re.search(r"^using\s+Photon\.", headlamp, re.M):
        errors.append(f"{rel(headlamp_path)}: local headlamp must not depend on Photon.")
    lamp = positive_defaults(errors, headlamp_path, headlamp, [
        "beamRange", "beamIntensity", "outerSpotAngle", "innerSpotAngle"
    ])
    if lamp.get("innerSpotAngle", 0) >= lamp.get("outerSpotAngle", 999):
        errors.append(f"{rel(headlamp_path)}: innerSpotAngle must be smaller than outerSpotAngle.")

    nav_path = ROOT / "Assets/Scripts/MonsterScripts/MonsterNavigation.cs"
    require(errors, nav_path, ["gameObject.AddComponent<CrawlerVisualController>();", "agent.updateRotation = false;"])

    visual_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerVisualController.cs"
    visual = require(errors, visual_path, [
        "preserveAuthoredScale = true",
        'new GameObject("CrawlerVisualAnchor")',
        "gameObject.AddComponent<CrawlerBodyPathFollower>();",
        "zombieAnimator.applyRootMotion = false;",
        "AnimatorCullingMode.AlwaysAnimate",
        'Animator.StringToHash("mixamo_com")',
        "bodyPathFollower.ResetTrail();",
    ])
    positive_defaults(errors, visual_path, visual, [
        "detectionRange", "wanderSpeed", "chaseSpeed", "detectionInterval", "teleportDistance", "animationProbeWindow"
    ])

    follower_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerBodyPathFollower.cs"
    follower = require(errors, follower_path, [
        "public void ResetTrail()",
        "PreservesAnimatorSkeleton => true",
        "automatic pivot alignment",
        "never writes a bone position or rotation",
        "AlignVisualToLeader();",
        "mixamorigspine2",
        "mixamorighips",
    ])
    if re.search(r"^using\s+Photon\.", follower, re.M):
        errors.append(f"{rel(follower_path)}: local visual alignment must not depend on Photon.")
    if re.search(r"\b(?:binding\.)?bone\.(?:position|rotation)\s*=", follower):
        errors.append(f"{rel(follower_path)}: failed runtime tests forbid post-Animator core-bone position/rotation writes.")
    if "ApplyBodyPath();" in follower or "ConstrainHand(" in follower:
        errors.append(f"{rel(follower_path)}: experimental torso/leaf deformation must remain disabled; limb contact belongs in the dedicated IK component.")
    positive_defaults(errors, follower_path, follower, ["floorClearance"])

    heading_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerVisualHeadingStabilizer.cs"
    heading = require(errors, heading_path, [
        'LevelOneSceneName = "Level1_Containment"',
        "visualController.VisualRoot",
        "headingLookbackDistance = 0.9f",
        "maximumTurnDegreesPerSecond = 150f",
        "discontinuityDistance = 1.25f",
        "Quaternion.LookRotation(forward.normalized, Vector3.up)",
        "Quaternion.RotateTowards(",
        "ResetHeadingHistory(current, snapToGameplayRotation: true)",
        "No Zombie bone position/rotation is ever modified here.",
    ])
    positive_defaults(errors, heading_path, heading, [
        "headingLookbackDistance", "maximumTurnDegreesPerSecond", "sampleSpacing", "historyDistance", "discontinuityDistance"
    ])

    surface_ik_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerSurfaceContactIK.cs"
    surface_ik = require(errors, surface_ik_path, [
        'LevelOneSceneName = "Level1_Containment"',
        "DefaultExecutionOrder(350)",
        "visualController.VisualRoot",
        "visualController.VisualAnimator",
        "Physics.SphereCastNonAlloc(",
        "Physics.OverlapSphereNonAlloc(",
        "QueryTriggerInteraction.Ignore",
        "hit.point + hit.normal * (handRadius + surfaceClearance)",
        "Quaternion.FromToRotation(",
        '"mixamorigleftarm"',
        '"mixamorigleftforearm"',
        '"mixamoriglefthand"',
        '"mixamorigrightarm"',
        '"mixamorigrightforearm"',
        '"mixamorigrighthand"',
        "collider.transform.IsChildOf(transform)",
        "collider.attachedRigidbody != null",
        "Only upper-arm/forearm joints are corrected",
    ])
    surface_defaults = positive_defaults(errors, surface_ik_path, surface_ik, [
        "handRadius", "surfaceClearance", "contactReleaseSpeed", "emergencyProbeRadiusScale"
    ])
    if surface_defaults.get("handRadius", 0) > 0.12:
        errors.append(f"{rel(surface_ik_path)}: handRadius is unexpectedly large for vent contact IK.")
    if re.search(r"(?:spine|hips).*\.(?:position|rotation)\s*=", surface_ik, re.I):
        errors.append(f"{rel(surface_ik_path)}: surface-contact IK must never manipulate torso/core bones.")
    if re.search(r"\btransform\.position\s*=", surface_ik):
        errors.append(f"{rel(surface_ik_path)}: limb contact must not move the authoritative Crawler gameplay root.")

    legacy_cleaner_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerLegacyVisualCleaner.cs"
    legacy_cleaner = require(errors, legacy_cleaner_path, [
        "RemoveLegacyVisuals(GameObject gameplayRoot, Transform keepVisual, bool immediate)",
        '"shoulderl"',
        '"shoulderr"',
        "ContainsLegacyVisualRig",
        "IsVisualOnlySubtree",
        "Anything else is gameplay/physics/audio/state until proven otherwise.",
        "component is Renderer",
        "component is Animator",
    ])
    if "DestroyObject(gameplayRoot" in legacy_cleaner or "DestroyObject(root.gameObject" in legacy_cleaner:
        errors.append(f"{rel(legacy_cleaner_path)}: legacy cleanup must never destroy the Crawler gameplay root.")

    runtime_cleanup_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerLegacyVisualRuntimeCleanup.cs"
    require(errors, runtime_cleanup_path, [
        "RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)",
        "SceneManager.sceneLoaded += HandleSceneLoaded",
        "monster.gameObject.AddComponent<CrawlerLegacyVisualRuntimeCleanup>()",
        "visualController.VisualRoot",
        "CrawlerLegacyVisualCleaner.RemoveLegacyVisuals(",
        "Crawler gameplay root plus Zombie Crawl visual rig",
    ])

    editor_cleanup_path = ROOT / "Assets/Scripts/Editor/CrawlerLegacyVisualSceneCleanup.cs"
    editor_cleanup = require(errors, editor_cleanup_path, [
        'LevelOneScenePath = "Assets/Scenes/Level1_Containment.unity"',
        'LegacyModelPathFragment = "MiniGamesKidFirstRig.fbx"',
        "Clean Legacy Level 1 Crawler Rig",
        "PrefabUtility.UnpackPrefabInstance(",
        "CrawlerLegacyVisualCleaner.RemoveLegacyVisuals(",
        "EditorSceneManager.SaveScene(scene)",
    ])
    if "DestroyImmediate(monster.gameObject" in editor_cleanup:
        errors.append(f"{rel(editor_cleanup_path)}: editor migration must preserve the Crawler gameplay root.")

    zone_path = ROOT / "Assets/Scripts/Zones/ZoneTrigger.cs"
    require(errors, zone_path, [
        "IsLevelOneSafeBoundary",
        'CompareTag("MainCamera")',
        "ZoneId.Level1_Vents",
        "localCenter.x > box.center.x",
    ])

    capture_path = ROOT / "Assets/Scripts/TeleportGorillaPlayerPhotonVR.cs"
    require(errors, capture_path, [
        "OnTriggerStay(Collider other)",
        "GetComponentInParent<LocalRigMarker>()",
        "SectorId.Containment",
        "ZoneId.Level1_Vents",
        "travel.RespawnAt(TeleportLocation)",
        "DropAllKeyCards(capturePosition)",
        "player.transform.position",
    ])

    keycard_respawn_path = ROOT / "Assets/Scripts/Utils/RespawnToOriginalSpawn.cs"
    keycard_respawn = require(errors, keycard_respawn_path, [
        "maxFallBelowSpawn = 3f",
        "spawnPos.y - maxFallBelowSpawn",
        "rb.position = targetPosition",
        "Physics.SyncTransforms();",
    ])
    positive_defaults(errors, keycard_respawn_path, keycard_respawn, ["maxFallBelowSpawn", "respawnUpOffset"])

    loading_path = ROOT / "Assets/Scripts/Travel/LoadingCanvasOverscan.cs"
    loading = require(errors, loading_path, [
        'LoadingSceneName = "Loading"',
        'BackdropName = "XR_Loading_Backdrop"',
        "Overscan = 0.12f",
        "rect.anchorMin = new Vector2(-Overscan, -Overscan)",
        "rect.anchorMax = new Vector2(1f + Overscan, 1f + Overscan)",
        "rect.SetAsFirstSibling();",
        "image.color = Color.black;",
    ])
    if "UnityEngine.UI" not in loading:
        errors.append(f"{rel(loading_path)}: XR loading backdrop must use a screen-space UI Image.")

    spawn_path = ROOT / "Assets/Scripts/Bootstrap/RigSpawnSnapper.cs"
    spawn = require(errors, spawn_path, [
        "gameObject.AddComponent<RigFloorPenetrationGuard>();",
        "GetComponentsInChildren<Collider>(true)",
        "WaitForTrackingOffsetStability()",
        "GroundCorrect(spawnGo.transform.position, hubScene, locomotionPlayer)",
        "out blocker",
    ])
    positive_defaults(errors, spawn_path, spawn, [
        "FixedSettleSteps", "TrackingOffsetStableFrames", "TrackingOffsetMaxWaitFrames", "TrackingOffsetEpsilon", "GroundSkin"
    ])

    player_path = ROOT / "Assets/Scripts/NewGorillaLocomotionScripts/Player.cs"
    player = require(errors, player_path, [
        "ResolveHandPositionAfterTeleport",
        "RefreshBlockedHandAfterTeleport",
        "leftHandBlockedAfterTeleport",
        "rightHandBlockedAfterTeleport",
        "CollisionsSphereCast(",
        "minimumRaycastDistance + 0.005f",
        "MinimumVisualHandContactRadius = 0.05f",
        "minimumRaycastDistance = Mathf.Max(minimumRaycastDistance, MinimumVisualHandContactRadius)",
        "if (!suppressLeftHand && IterativeCollisionSphereCast",
        "if (!suppressRightHand && IterativeCollisionSphereCast",
    ])
    hand = positive_defaults(errors, player_path, player, [
        "teleportHandPenetrationTolerance", "MinimumVisualHandContactRadius"
    ])
    if hand.get("MinimumVisualHandContactRadius", 0) < 0.05:
        errors.append(f"{rel(player_path)}: visual hand contact radius must remain at least 0.05 m.")

    avatar_path = ROOT / "Assets/Resources/PhotonVR/Scripts/Player/PhotonVRPlayer.cs"
    avatar = require(errors, avatar_path, [
        "DefaultExecutionOrder(100)",
        "CopyTrackedHandPose(",
        "locomotion.rightHandFollower",
        "locomotion.leftHandFollower",
        "collisionSafePositionSource.position",
        "trackedRotationSource.rotation",
    ])
    if "CopyTrackedPose(RightHand, manager.RightHand)" in avatar or "CopyTrackedPose(LeftHand, manager.LeftHand)" in avatar:
        errors.append(f"{rel(avatar_path)}: local visible hands must not bypass Gorilla collision-safe follower positions.")

    guard_path = ROOT / "Assets/Scripts/Bootstrap/RigFloorPenetrationGuard.cs"
    guard = require(errors, guard_path, [
        "Physics.RaycastNonAlloc(",
        "SectorTravelService.I.IsBusy",
        "penetration > maxRecoveryDepth",
        "Vector3.up * (penetration + recoverySkin)",
        "player.ResetAfterTeleport();",
    ])
    floor = positive_defaults(errors, guard_path, guard, [
        "allowedPenetration", "recoverySkin", "maxRecoveryDepth", "recoveryCooldown"
    ])
    if floor.get("maxRecoveryDepth", 1) <= floor.get("allowedPenetration", 0):
        errors.append(f"{rel(guard_path)}: maxRecoveryDepth must exceed allowedPenetration.")

    scene_path = ROOT / "Assets/Scenes/Level1_Containment.unity"
    if not scene_path.exists():
        errors.append(f"{rel(scene_path)}: Level 1 scene is missing.")
    else:
        scene = read(scene_path)
        blocks = mono_blocks(scene)
        zone_guid = guid(ROOT / "Assets/Scripts/Zones/ZoneTrigger.cs.meta", errors)
        capture_guid = guid(ROOT / "Assets/Scripts/TeleportGorillaPlayerPhotonVR.cs.meta", errors)
        if zone_guid:
            zones = [block for block in blocks if zone_guid in block]
            safe = sum(bool(re.search(r"^\s*zone:\s*2\s*$", block, re.M)) for block in zones)
            vents = sum(bool(re.search(r"^\s*zone:\s*3\s*$", block, re.M)) for block in zones)
            if safe < 2:
                errors.append(f"{rel(scene_path)}: expected >=2 Level1_Antechamber ZoneTriggers, found {safe}.")
            if vents < 1:
                errors.append(f"{rel(scene_path)}: expected >=1 Level1_Vents ZoneTrigger, found {vents}.")
        if capture_guid and not any(capture_guid in block for block in blocks):
            errors.append(f"{rel(scene_path)}: serialized Crawler capture component is missing.")

    for error in errors:
        print("ERROR:", error)
    if errors:
        print(f"FAILED: {len(errors)} Level 1/runtime contract issue(s).")
        return 1

    print("PASS: Level 1/runtime hand visual contact, Animator-owned Crawler torso, Crawler hand surface-contact IK, legacy visual-rig cleanup, Crawler forward/turn continuity, keycard recovery, loading coverage, headlamp, safe-zone, capture, and floor-recovery source contracts.")
    print("PASS: Runtime/Photon/XR/Quest validation remains separate.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
