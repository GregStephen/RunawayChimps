#!/usr/bin/env python3
"""Focused source contracts for the active Level 1 runtime fixes.

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

    blower_path = ROOT / "Assets/Scripts/Lighting/VentBlowerSetPiece.cs"
    blower = require(errors, blower_path, [
        'LevelOneScene = "Level1_Containment"',
        'VentRoomName = "VentRoom"',
        'RuntimeRootName = "Level1_VentRoom_Blower"',
        'BlowerResourcePath = "RunawayChimps_VentBlower"',
        'RotorName = "FanRotor"',
        'RedLensName = "RedLightLens"',
        "BlowerLocalPosition = new Vector3(-0.25f, 0.72f, 22.46f)",
        "Resources.Load<GameObject>(BlowerResourcePath)",
        "Instantiate(blowerPrefab, transform, false)",
        "OrientVisualIntoVentRoom(visualInstance.transform)",
        "GetComponentsInChildren<Collider>(true)",
        "collider.enabled = false;",
        "ShadowCastingMode.Off",
        'Shader.Find("Universal Render Pipeline/Lit")',
        "LightType.Point",
        "LightShadows.None",
        "bounceIntensity = 0f",
        "spatialBlend = 1f",
        "maxDistance = 6.5f",
        'AudioClip.Create("Vent_Blower_ProceduralLoop"',
    ])
    if re.search(r"^using\s+Photon\.", blower, re.M):
        errors.append(f"{rel(blower_path)}: decorative vent blower must not depend on Photon.")
    if "GameObject.CreatePrimitive(" in blower:
        errors.append(f"{rel(blower_path)}: approved Blender visual must not regress to generated primitive geometry.")
    positive_defaults(errors, blower_path, blower, ["FanDegreesPerSecond", "BaseRedLightIntensity"])

    blower_asset = ROOT / "Assets/Resources/RunawayChimps_VentBlower.fbx"
    blower_meta = ROOT / "Assets/Resources/RunawayChimps_VentBlower.fbx.meta"
    if not blower_asset.exists():
        errors.append(f"{rel(blower_asset)}: approved Blender FBX resource is missing.")
    elif blower_asset.stat().st_size < 10000:
        errors.append(f"{rel(blower_asset)}: approved Blender FBX resource is unexpectedly small.")
    meta = require(errors, blower_meta, [
        "ModelImporter:",
        "addColliders: 0",
        "importCameras: 0",
        "importLights: 0",
        "preserveHierarchy: 1",
    ])
    if meta:
        guid(blower_meta, errors)

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
        "trail.Clear();",
        "Physics.RaycastNonAlloc(",
        "SamplePosition(binding.distanceBehind)",
        "SampleForward(binding.distanceBehind)",
    ])
    if re.search(r"^using\s+Photon\.", follower, re.M):
        errors.append(f"{rel(follower_path)}: local visual body reconstruction must not depend on Photon.")
    body = positive_defaults(errors, follower_path, follower, [
        "trailSampleSpacing", "retainedTrailLength", "teleportResetDistance", "bodyFollowWeight", "tangentSampleDistance"
    ])
    if body.get("bodyFollowWeight", 0) > 1:
        errors.append(f"{rel(follower_path)}: bodyFollowWeight must remain <= 1.")

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
        "if (!suppressLeftHand && IterativeCollisionSphereCast",
        "if (!suppressRightHand && IterativeCollisionSphereCast",
    ])
    positive_defaults(errors, player_path, player, ["teleportHandPenetrationTolerance"])

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
        if "\n  m_Name: VentRoom\n" not in scene:
            errors.append(f"{rel(scene_path)}: VentRoom anchor for the blower set piece is missing.")
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
        print(f"FAILED: {len(errors)} Level 1 contract issue(s).")
        return 1

    print("PASS: Level 1 headlamp, vent blower, Crawler visual/path, safe-zone, capture, floor-recovery, and spawn-safe hand source contracts.")
    print("PASS: Runtime/Photon/XR/Quest validation remains separate.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
