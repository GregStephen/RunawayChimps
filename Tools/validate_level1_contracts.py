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

    runtime_landmark_path = ROOT / "Assets/Scripts/Lighting/VentLandmarkSetPiece.cs"
    if runtime_landmark_path.exists():
        errors.append(f"{rel(runtime_landmark_path)}: runtime vent landmark generation is superseded; landmarks must remain authored scene/prefab objects.")

    landmark_pulse_path = ROOT / "Assets/Scripts/Lighting/VentLandmarkPulse.cs"
    landmark_pulse = require(errors, landmark_pulse_path, [
        "public sealed class VentLandmarkPulse : MonoBehaviour",
        "[RequireComponent(typeof(Light))]",
        "baseIntensity * wave",
        "Configure(float newAmplitude, float newFrequency, float newPhase)",
    ])
    if re.search(r"^using\\s+Photon\\.", landmark_pulse, re.M):
        errors.append(f"{rel(landmark_pulse_path)}: decorative landmark pulse must not depend on Photon.")

    landmark_authoring_path = ROOT / "Assets/Scripts/Editor/VentLandmarkSceneAuthoring.cs"
    landmark_authoring = require(errors, landmark_authoring_path, [
        'LevelOneScenePath = "Assets/Scenes/Level1_Containment.unity"',
        'RootName = "Level1_VentLandmarks"',
        'VentRoomName = "VentRoom"',
        'PrefabFolder = "Assets/RunawayChimps/Environment/VentLandmarks/Prefabs"',
        "Author Vent Landmarks",
        "loadedScene.isDirty",
        "FindMonsterNavigation(scene)",
        "ExistingLandmarkExclusionRadius = 2.25f",
        "PrefabUtility.SaveAsPrefabAsset",
        "PrefabUtility.InstantiatePrefab",
        "EditorSceneManager.MarkSceneDirty(scene)",
        "EditorSceneManager.SaveScene(scene)",
        "Existing scene objects were preserved",
    ])
    if "RuntimeInitializeOnLoadMethod" in landmark_authoring:
        errors.append(f"{rel(landmark_authoring_path)}: vent landmark authoring must be an explicit editor action, never an automatic runtime/editor initializer.")
    if "DestroyImmediate(existingRoot" in landmark_authoring:
        errors.append(f"{rel(landmark_authoring_path)}: authoring must not overwrite Greg's existing manual landmark placement.")

    blower_path = ROOT / "Assets/Scripts/Lighting/VentBlowerSetPiece.cs"
    blower = require(errors, blower_path, [
        'LevelOneScene = "Level1_Containment"',
        'VentRoomName = "VentRoom"',
        'RuntimeRootName = "Level1_VentRoom_Blower"',
        'BlowerResourcePath = "RunawayChimps_VentBlower"',
        'RotorName = "FanRotor"',
        'RotorHubName = "FanHub"',
        'RotorPivotName = "FanRotor_CenteredPivot"',
        'HousingLeftLipName = "Housing_LeftLip"',
        'HousingRightLipName = "Housing_RightLip"',
        "HousingLipCenterX = 0.662f",
        "CorrectHousingLipOverlap(visualRoot.transform)",
        "position.x = -HousingLipCenterX",
        "position.x = HousingLipCenterX",
        "RotorSpinAxis = Vector3.up",
        "ConfigureRotor(importedRotor)",
        "FindDescendant(importedRotor, RotorHubName)",
        "importedRotor.InverseTransformPoint(hub.position)",
        "rotorPivot.SetParent(importedRotor.parent, false)",
        "rotorPivot.localPosition = importedRotor.localPosition",
        "importedRotor.localRotation * Vector3.Scale(importedRotor.localScale, hubLocalPosition)",
        "rotorPivot.localRotation = importedRotor.localRotation",
        "rotorPivot.localScale = importedRotor.localScale",
        "importedRotor.SetParent(rotorPivot, false)",
        "importedRotor.localPosition = -hubLocalPosition",
        "importedRotor.localRotation = Quaternion.identity",
        "importedRotor.localScale = Vector3.one",
        "rotorRestRotation = rotorPivot.localRotation",
        "rotorAngle = 0f",
        "AnimateRotor(Time.deltaTime)",
        "Mathf.Repeat(rotorAngle - FanDegreesPerSecond * deltaTime, 360f)",
        "rotorPivot.localRotation = rotorRestRotation * Quaternion.AngleAxis(rotorAngle, RotorSpinAxis)",
        'RedLensName = "RedLightLens"',
        "BlowerLocalPosition = new Vector3(-0.25f, 0.72f, 22.46f)",
        "Resources.Load<GameObject>(BlowerResourcePath)",
        "Instantiate(blowerPrefab, transform, false)",
        "OrientVisualIntoVentRoom(visualInstance.transform)",
        "GetComponentsInChildren<Collider>(true)",
        "collider.enabled = false;",
        "ShadowCastingMode.Off",
        "material.shader.isSupported",
        "LightType.Point",
        "LightShadows.None",
        "bounceIntensity = 0f",
        "spatialBlend = 1f",
        "MotorVolume = 0.72f",
        "MotorMinDistance = 1.5f",
        "MotorMaxDistance = 10f",
        "humSource.volume = MotorVolume",
        "humSource.minDistance = MotorMinDistance",
        "humSource.maxDistance = MotorMaxDistance",
        "Mathf.PI * 92f * t",
        "Mathf.PI * 640f * t",
        'AudioClip.Create("Vent_Blower_ProceduralLoop"',
    ])
    if re.search(r"^using\s+Photon\.", blower, re.M):
        errors.append(f"{rel(blower_path)}: decorative vent blower must not depend on Photon.")
    if "GameObject.CreatePrimitive(" in blower:
        errors.append(f"{rel(blower_path)}: approved Blender visual must not regress to generated primitive geometry.")
    if 'Shader.Find("Universal Render Pipeline/Lit")' in blower:
        errors.append(f"{rel(blower_path)}: blower must not dynamically assign URP/Lit while the project uses the built-in render pipeline.")
    if "CreateRuntimeMaterial(" in blower:
        errors.append(f"{rel(blower_path)}: blower materials must stay serialized/import-mapped rather than dynamically created.")
    if re.search(r"\.(?:Rotate|RotateAround)\s*\(", blower):
        errors.append(f"{rel(blower_path)}: fan must use its centered pivot and bounded rest-relative rotation, not incremental/orbital rotation.")
    positive_defaults(errors, blower_path, blower, ["FanDegreesPerSecond", "BaseRedLightIntensity"])

    graphics_path = ROOT / "ProjectSettings/GraphicsSettings.asset"
    require(errors, graphics_path, ["m_CustomRenderPipeline: {fileID: 0}"])
    quality_path = ROOT / "ProjectSettings/QualitySettings.asset"
    require(errors, quality_path, ["customRenderPipeline: {fileID: 0}"])

    blower_asset = ROOT / "Assets/Resources/RunawayChimps_VentBlower.fbx"
    blower_meta = ROOT / "Assets/Resources/RunawayChimps_VentBlower.fbx.meta"
    if not blower_asset.exists():
        errors.append(f"{rel(blower_asset)}: approved Blender FBX resource is missing.")
    elif blower_asset.stat().st_size < 10000:
        errors.append(f"{rel(blower_asset)}: approved Blender FBX resource is unexpectedly small.")
    meta = require(errors, blower_meta, ["ModelImporter:", "addColliders: 0", "importCameras: 0", "importLights: 0", "preserveHierarchy: 1", "bakeAxisConversion: 0"])
    if meta:
        guid(blower_meta, errors)

    blower_mat = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_DarkPaintedMetal.mat"
    blower_mat_meta = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_DarkPaintedMetal.mat.meta"
    require(errors, blower_mat, [
        "m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}",
        "_MainTex: {m_Texture: {fileID: 2800000, guid: 91adcb038ce34a8aa749fc2b00ba7e66, type: 3}",
    ])
    blower_mat_guid = guid(blower_mat_meta, errors)
    if blower_mat_guid and meta:
        if "name: M_DarkPaintedMetal" not in meta or blower_mat_guid not in meta:
            errors.append(f"{rel(blower_meta)}: M_DarkPaintedMetal is not mapped to VentBlower_DarkPaintedMetal.mat.")

    blower_mat = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_DullSteel.mat"
    blower_mat_meta = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_DullSteel.mat.meta"
    require(errors, blower_mat, [
        "m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}",
        "_MainTex: {m_Texture: {fileID: 2800000, guid: 5f19b7dd7c834e5ab83638f5c9f20e81, type: 3}",
    ])
    blower_mat_guid = guid(blower_mat_meta, errors)
    if blower_mat_guid and meta:
        if "name: M_DullSteel" not in meta or blower_mat_guid not in meta:
            errors.append(f"{rel(blower_meta)}: M_DullSteel is not mapped to VentBlower_DullSteel.mat.")

    blower_mat = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_FanBlade.mat"
    blower_mat_meta = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_FanBlade.mat.meta"
    require(errors, blower_mat, [
        "m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}",
        "_MainTex: {m_Texture: {fileID: 2800000, guid: 91adcb038ce34a8aa749fc2b00ba7e66, type: 3}",
    ])
    blower_mat_guid = guid(blower_mat_meta, errors)
    if blower_mat_guid and meta:
        if "name: M_FanBlade" not in meta or blower_mat_guid not in meta:
            errors.append(f"{rel(blower_meta)}: M_FanBlade is not mapped to VentBlower_FanBlade.mat.")

    blower_mat = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_Conduit.mat"
    blower_mat_meta = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_Conduit.mat.meta"
    require(errors, blower_mat, [
        "m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}",
        "_MainTex: {m_Texture: {fileID: 2800000, guid: 4d2d5d39b76547b291a6fe6c8e27b67f, type: 3}",
    ])
    blower_mat_guid = guid(blower_mat_meta, errors)
    if blower_mat_guid and meta:
        if "name: M_Conduit" not in meta or blower_mat_guid not in meta:
            errors.append(f"{rel(blower_meta)}: M_Conduit is not mapped to VentBlower_Conduit.mat.")

    blower_mat = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_RedMaintenanceLens.mat"
    blower_mat_meta = ROOT / "Assets/RunawayChimps/Shared/Models/Materials/VentBlower_RedMaintenanceLens.mat.meta"
    require(errors, blower_mat, ["m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}"])
    blower_mat_guid = guid(blower_mat_meta, errors)
    if blower_mat_guid and meta:
        if "name: M_RedMaintenanceLens" not in meta or blower_mat_guid not in meta:
            errors.append(f"{rel(blower_meta)}: M_RedMaintenanceLens is not mapped to VentBlower_RedMaintenanceLens.mat.")

    for texture_name in (
        "VentBlower_PaintedMetal_Albedo.tga",
        "VentBlower_BareSteel_Albedo.tga",
        "VentBlower_Conduit_Albedo.tga",
    ):
        texture_path = ROOT / "Assets/RunawayChimps/Shared/Models/Textures" / texture_name
        texture_meta = Path(str(texture_path) + ".meta")
        if not texture_path.exists():
            errors.append(f"{rel(texture_path)}: blower surface texture is missing.")
        elif texture_path.stat().st_size < 10000:
            errors.append(f"{rel(texture_path)}: blower surface texture is unexpectedly small.")
        require(errors, texture_meta, ["TextureImporter:", "enableMipMap: 1", "wrapU: 0", "wrapV: 0"])
        guid(texture_meta, errors)

    nav_path = ROOT / "Assets/Scripts/MonsterScripts/MonsterNavigation.cs"
    require(errors, nav_path, ["gameObject.AddComponent<CrawlerVisualController>();", "agent.updateRotation = false;"])

    visual_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerVisualController.cs"
    visual = require(errors, visual_path, [
        "preserveAuthoredScale = true",
        "ZombieVisualScaleMultiplier = 0.6f",
        'new GameObject("CrawlerVisualAnchor")',
        "navigation.modelForwardOffset = Vector3.zero",
        "visualAnchor.rotation = Quaternion.LookRotation(initialForward.normalized, Vector3.up)",
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
        "MaintainsAnimatedRootInPlace => true",
        "automatic pivot alignment",
        'MotionCompensationName = "CrawlerMotionCompensation"',
        "AlignVisualToLeader();",
        "motionCompensationRoot.InverseTransformPoint(animatedRootAnchor.position)",
        "float downwardRootSink = Mathf.Min(0f, totalLocalDrift.y)",
        "Vector3 compensatedLocalDrift = new Vector3(",
        "VentContainmentOffsetWorld => ventContainmentOffsetWorld",
        "SetVentContainmentOffsetWorld(Vector3 worldOffset, bool applyImmediately = false)",
        "visualAnchor.InverseTransformVector(ventContainmentOffsetWorld)",
        "calibratedMotionLocalPosition - compensatedLocalDrift + localContainmentOffset",
        "animatedRootFloorSinkWarning = 0.12f",
        "preserving upward crawl motion",
        "mixamorigspine2",
        "mixamorighips",
    ])
    if re.search(r"^using\s+Photon\.", follower, re.M):
        errors.append(f"{rel(follower_path)}: local visual alignment must not depend on Photon.")
    if re.search(r"\b(?:binding\.)?bone\.(?:position|rotation)\s*=", follower):
        errors.append(f"{rel(follower_path)}: failed runtime tests forbid post-Animator core-bone position/rotation writes.")
    for forbidden in (
        "visualRoot.position +=",
        "visualRoot.position =",
        "visualRoot.localPosition =",
        "visualRoot.rotation =",
        "visualRoot.localRotation =",
    ):
        if forbidden in follower:
            errors.append(f"{rel(follower_path)}: rigid correction must stay outside the Animator-owned visual root; found {forbidden!r}.")
    if "ApplyBodyPath();" in follower or "ConstrainHand(" in follower:
        errors.append(f"{rel(follower_path)}: experimental torso/leaf deformation must remain disabled; limb contact belongs in the dedicated IK component.")
    positive_defaults(errors, follower_path, follower, [
        "floorClearance", "animatedRootDriftWarning", "animatedRootFloorSinkWarning"
    ])

    heading_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerVisualHeadingStabilizer.cs"
    heading = require(errors, heading_path, [
        'LevelOneSceneName = "Level1_Containment"',
        "visualController.VisualRoot",
        "bodyPathFollower.VisualAnchor",
        "CrawlerMotionCompensation",
        "headingLookbackDistance = 0.9f",
        "maximumTurnDegreesPerSecond = 150f",
        "discontinuityDistance = 1.25f",
        "Quaternion.LookRotation(forward.normalized, Vector3.up)",
        "Quaternion.RotateTowards(",
        "ResetHeadingHistory(current, snapToGameplayRotation: true)",
        "No Zombie bone position/rotation is ever modified here.",
    ])
    if "visualAnchor = visualRoot.parent" in heading:
        errors.append(f"{rel(heading_path)}: heading must target the explicit CrawlerVisualAnchor, not VisualRoot.parent.")
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
        "hit.point + hit.normal * (radius + surfaceClearance)",
        "Quaternion.FromToRotation(",
        '"mixamorigleftarm"',
        '"mixamorigleftforearm"',
        '"mixamoriglefthand"',
        '"mixamorigrightarm"',
        '"mixamorigrightforearm"',
        '"mixamorigrighthand"',
        '"mixamorigleftupleg"',
        '"mixamorigleftleg"',
        '"mixamorigleftfoot"',
        '"mixamorigrightupleg"',
        '"mixamorigrightleg"',
        '"mixamorigrightfoot"',
        "SolveArm(leftLeg);",
        "SolveArm(rightLeg);",
        "collider.transform.IsChildOf(transform)",
        "collider.attachedRigidbody != null",
        "hand/foot surface-contact IK active",
    ])
    surface_defaults = positive_defaults(errors, surface_ik_path, surface_ik, [
        "handRadius", "footRadius", "surfaceClearance", "contactReleaseSpeed", "emergencyProbeRadiusScale"
    ])
    if surface_defaults.get("handRadius", 0) > 0.12 or surface_defaults.get("footRadius", 0) > 0.12:
        errors.append(f"{rel(surface_ik_path)}: hand/foot contact radius is unexpectedly large for vent contact IK.")
    if re.search(r"(?:spine|hips).*\.(?:position|rotation)\s*=", surface_ik, re.I):
        errors.append(f"{rel(surface_ik_path)}: surface-contact IK must never manipulate torso/core bones.")
    if re.search(r"\btransform\.position\s*=", surface_ik):
        errors.append(f"{rel(surface_ik_path)}: limb contact must not move the authoritative Crawler gameplay root.")

    containment_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerVentContainment.cs"
    containment = require(errors, containment_path, [
        'LevelOneSceneName = "Level1_Containment"',
        "DefaultExecutionOrder(325)",
        "bodyFollower.SetVentContainmentOffsetWorld(desiredOffset, applyImmediately: true)",
        "Physics.ComputePenetration(",
        "Physics.SphereCastNonAlloc(",
        "maximumContainmentOffset = 0.32f",
        "head == null || chest == null || hips == null",
        "UpdateProbeHistory(preservePreviousSafePoints: offsetLimitedThisFrame)",
        "OverlapsStaticEnvironment(current, probe.radius)",
        'AddProbe("head"',
        'AddProbe("chest"',
        'AddProbe("hips"',
        'AddProbe("left elbow"',
        'AddProbe("right elbow"',
        'AddProbe("left knee"',
        'AddProbe("right knee"',
        "collider.attachedRigidbody != null",
        "hands/feet remain limb-IK controlled",
    ])
    positive_defaults(errors, containment_path, containment, [
        "headRadius", "chestRadius", "hipsRadius", "jointRadius",
        "maximumContainmentOffset", "releaseSpeed", "discontinuityDistance"
    ])
    if re.search(r"(?:head|chest|hips|spine|elbow|knee).*\.(?:position|rotation)\s*=", containment, re.I):
        errors.append(f"{rel(containment_path)}: rigid vent containment must not directly write Zombie body/joint transforms.")
    if re.search(r"\btransform\.position\s*=", containment):
        errors.append(f"{rel(containment_path)}: containment must not move the authoritative gameplay root.")

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
        "WaitForTrackingOffsetStability(generation, hubScene, room, actorNumber)",
        "GroundCorrect(spawnPosition, hubScene, locomotionPlayer)",
        "TryGetLocalSpawnPose(spawnGo.transform, out spawnPosition, out spawnRotation)",
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
        print(f"FAILED: {len(errors)} Level 1/runtime contract issue(s).")
        return 1

    print("PASS: Level 1/runtime hand visual contact, Animator-owned Crawler torso, stable Crawler motion-wrapper/floor-clamp ownership, rigid Crawler vent-body containment, Crawler hand/foot surface-contact IK, legacy visual-rig cleanup, Crawler forward/turn continuity, keycard recovery, loading coverage, headlamp, vent blower, safe-zone, capture, and floor-recovery source contracts.")
    print("PASS: Runtime/Photon/XR/Quest validation remains separate.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
