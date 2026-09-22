"""Source-only launch presentation checks. Unity/headset/build validation remains separate."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SPLASH = ROOT / "Assets/Branding/RunawayChimps_SystemSplash.png"
SPLASH_META = SPLASH.with_suffix(".png.meta")
SPLASH_GUID = "73af3b98a31d49a6a0b674b50ed8d20c"
SLOT_PREFAB = ROOT / "Assets/Resources/HubSpawn/HubSpawnSlots.prefab"


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8-sig")


def main() -> int:
    errors: list[str] = []

    if not SPLASH.exists() or SPLASH.stat().st_size < 500:
        errors.append("Quest system splash PNG is missing or unexpectedly small.")
    if not SPLASH_META.exists() or SPLASH_GUID not in SPLASH_META.read_text(encoding="utf-8-sig"):
        errors.append("Quest system splash metadata/GUID is missing or changed.")

    xr_general = read("Assets/XR/XRGeneralSettingsPerBuildTarget.asset")
    openxr_loader_meta = read("Assets/XR/Loaders/OpenXRLoader.asset.meta")
    loader_match = re.search(r"^guid:\s*(\w+)", openxr_loader_meta, re.MULTILINE)
    loader_guid = loader_match.group(1) if loader_match else None
    if not loader_guid or xr_general.count(f"guid: {loader_guid}") < 2:
        errors.append("Standalone/Android XR loader contract no longer points at OpenXRLoader.")

    openxr_settings = read("Assets/XR/Settings/OpenXR Package Settings.asset")
    if f"systemSplashScreen: {{fileID: 2800000, guid: {SPLASH_GUID}, type: 3}}" not in openxr_settings:
        errors.append("MetaXRFeature Android system splash is not committed to the splash asset.")

    oculus = read("Assets/Oculus/OculusProjectConfig.asset")
    required_oculus = [
        f"systemSplashScreen: {{fileID: 2800000, guid: {SPLASH_GUID}, type: 3}}",
        "_systemLoadingScreenBackground: 0",
    ]
    for token in required_oculus:
        if token not in oculus:
            errors.append(f"OculusProjectConfig missing {token!r}.")

    review_menu = read("Assets/Scripts/Editor/SecurityBootReviewMenu.cs")
    for token in ["Hold Security Boot For Review", "Select Active Security Boot Tuning",
                  "FindObjectOfType<SecurityBootPresentation>", "Selection.activeGameObject"]:
        if token not in review_menu:
            errors.append(f"Security boot live-tuning menu missing {token!r}.")

    setup = read("Assets/Scripts/Editor/LaunchPresentationSettings.cs")
    preprocess = setup.split("internal sealed class LaunchPresentationBuildPreprocessor", 1)[-1]
    if "Apply(saveAssets:" in preprocess:
        errors.append("Android build preprocessing must validate committed launch settings, not mutate/save project assets.")
    for token in [
        "MetaXRFeature Android",
        "systemSplashScreen",
        "BuildTarget.Android",
        "IPreprocessBuildWithReport",
        "RunawayChimps_SystemSplash.png",
        "PlayerSettings.SplashScreen.show",
        "_systemLoadingScreenBackground",
        "IsBoolValue",
        "IsIntValue",
        "IsAndroidOpenXRLoaderConfigured",
        "Android Providers",
        "OpenXRLoader.asset",
    ]:
        if token not in setup:
            errors.append(f"LaunchPresentationSettings missing {token!r}.")

    boot = read("Assets/Scripts/Loading/SecurityBootPresentation.cs")
    for token in [
        "Security Boot Terminal",
        "BuildTerminal(hostCanvas.transform)",
        "RenderMode.ScreenSpaceCamera",
        "hostCanvas.planeDistance = Mathf.Max(1.5f",
        "BaseTerminalViewWidthFraction = 0.72f",
        "BaseTerminalViewHeightFraction = 0.84f",
        "[SerializeField, Range(0.40f, 1.00f)] private float terminalScale = 0.75f",
        "size.x * BaseTerminalViewWidthFraction * terminalScale / DesignWidth",
        "size.y * BaseTerminalViewHeightFraction * terminalScale / DesignHeight",
        "PresentationLayerName = \"LoadingPresentation\"",
        "LayerMask.NameToLayer(PresentationLayerName)",
        "SetLayerRecursively(hostCanvas.gameObject, presentationLayer)",
        "boundCamera.cullingMask = 1 << presentationLayer",
        "boundCamera.cullingMask = savedCullingMask | (1 << presentationLayer)",
        "BuildCrtTreatment",
        "StaticRefreshInterval = 0.10f",
        "RefreshStaticTexture",
        "Low-level CRT static",
        "CRT scanline",
        "CRT interference sweep",
        "NextNoise01",
        "staticBurstUntil",
        "staticCrackle",
        "PlayOneShot(staticCrackle",
        "SetBackdropOpacity(1f)",
    ]:
        if token not in boot:
            errors.append(f"Security boot terminal/CRT treatment missing {token!r}.")

    panel_path = ROOT / "Assets/Scripts/Loading/SecurityWorkstationVignette.cs"
    if panel_path.exists():
        errors.append("Superseded world-space launch panel helper must remain removed.")

    for forbidden in [
        "SecurityWorkstationVignette",
        "RenderMode.WorldSpace",
        "TerminalDistance",
        "MonitorCanvasRoot",
    ]:
        if forbidden in boot:
            errors.append(f"Security boot must remain on the restored PR #20 screen-space path: {forbidden!r} found.")

    tag_manager = read("ProjectSettings/TagManager.asset")
    if "- LoadingPresentation" not in tag_manager:
        errors.append("LoadingPresentation layer is not reserved in TagManager.asset.")

    for forbidden in ["UnityEngine.Random.Range", "Random.Range(", "new RenderTexture",
                      "PhotonNetwork.Join", "PhotonNetwork.Instantiate", "MarkRigSnapped(", "TryMarkReady("]:
        if forbidden in boot:
            errors.append(f"SecurityBootPresentation must stay local, presentation-only and allocation-light: {forbidden!r} found.")
    if re.search(r"(?:camera|startupCamera|boundCamera)\.transform\.(?:position|rotation|localPosition|localRotation)\s*=", boot):
        errors.append("SecurityBootPresentation must not write the tracked camera transform.")

    if "0.065f" not in boot or "0.045f" not in boot:
        errors.append("Security boot CRT treatment no longer exposes the reviewed low-alpha interference/static caps.")

    allocator = read("Assets/Scripts/Bootstrap/HubSpawnSlotAllocator.cs")
    for token in ["SlotCount = 10", "SetCustomProperties(desired, expected)", "OnPlayerLeftRoom",
                  "OnMasterClientSwitched", "TryGetLocalSpawnPose", "LayoutResourcePath",
                  "Resources.Load<GameObject>", "TryFindOwnedSlot", "ResetHubPlacementReady"]:
        if token not in allocator:
            errors.append(f"Hub spawn allocator missing {token!r}.")
    if "private void Update()" in allocator or "SlotOffsets" in allocator:
        errors.append("Hub spawn claims/layout must stay demand-driven and authored outside C#.")
    if not SLOT_PREFAB.exists():
        errors.append("Authored Hub spawn-slot prefab is missing.")
    else:
        slot_prefab = SLOT_PREFAB.read_text(encoding="utf-8-sig")
        marker_names = re.findall(r"m_Name: HubSpawnSlot_\d{2}", slot_prefab)
        if len(marker_names) != 10 or len(set(marker_names)) != 10:
            errors.append("HubSpawnSlots.prefab must contain exactly ten uniquely named authored markers.")
        document_ids = re.findall(r"^--- !u!\d+ &(-?\d+)\s*$", slot_prefab, re.MULTILINE)
        if "100100000" in document_ids:
            errors.append("HubSpawnSlots.prefab must not serialize a GameObject/component with Unity's reserved prefab fileID 100100000.")
        if len(document_ids) != len(set(document_ids)):
            errors.append("HubSpawnSlots.prefab contains duplicate serialized YAML object fileIDs.")

    rig_snapper = read("Assets/Scripts/Bootstrap/RigSpawnSnapper.cs")
    for token in ["HubSpawnSlotAllocator", "TryGetLocalSpawnPose", "SpawnSlotWaitSeconds"]:
        if token not in rig_snapper:
            errors.append(f"RigSpawnSnapper multiplayer slot integration missing {token!r}.")

    manager = read("Assets/Resources/PhotonVR/Scripts/PhotonVRManager.cs")
    if "HubSpawnSlotAllocator.AddInitialRoomProperties" not in manager or "matchmakingProps" not in manager:
        errors.append("Photon room creation must initialize Hub slot properties without filtering matchmaking on slot occupancy.")

    player_spawner = read("Assets/Resources/PhotonVR/Scripts/Player/PlayerSpawner.cs")
    if "!AppState.I.RigSnapped" not in player_spawner:
        errors.append("Photon avatar spawning must wait for the local Hub rig slot snap.")

    app_state = read("Assets/Scripts/Bootstrap/AppState.cs")
    if "ResetHubPlacementReady" not in app_state or "RigSnapped = false" not in app_state:
        errors.append("AppState must support invalidating Hub placement on a new Photon-room session.")

    visuals = read("Assets/Scripts/PlayerScripts/PlayerVisualReadyReporter.cs")
    if "GetSharedMaterials(materialScratch)" not in visuals or ".sharedMaterials" in visuals:
        errors.append("Player visual readiness must reuse a shared-material list instead of allocating arrays each frame.")

    flow = read("Assets/Scripts/Bootstrap/LoadingFlow.cs")
    hand_audio = read("Assets/Scripts/PlayerScripts/HandImpactAudio.cs")
    for token in ["PrepareCameraForHubReveal()", "SetBackdropOpacity(0f)", "RestoreCameraForReveal()",
                  "IsColdStartupPresentationActive"]:
        if token not in flow:
            errors.append(f"LoadingFlow safe terminal-to-Hub reveal/startup-audio gate missing {token!r}.")
    if "LoadingFlow.IsColdStartupPresentationActive" not in hand_audio:
        errors.append("HandImpactAudio must suppress synthetic hand impacts while cold-start presentation is active.")

    if "Security boot prototype" in flow or "Security Boot Prototype" in boot:
        errors.append("Production launch presentation still contains prototype-only runtime/Inspector naming.")

    player = read("ProjectSettings/ProjectSettings.asset")
    if "m_VirtualRealitySplashScreen: {fileID: 0}" not in player:
        errors.append("Unity Virtual Reality Splash Image must remain empty when using Meta system splash + custom startup scene.")
    if "m_ShowUnitySplashScreen: 1" in player:
        print("NOTE: Unity built-in splash is still serialized as enabled. Device/build validation must determine whether the active Unity 2022 license permits disabling it.")

    launch_doc = read("docs/launch-presentation.md")
    for token in [
        "Confirmed",
        "Implemented",
        "Pending validation",
        "feature/launch-presentation-polish",
        "system splash",
        "black-only",
    ]:
        if token not in launch_doc:
            errors.append(f"Launch presentation documentation missing {token!r}.")

    vignette_doc = read("docs/launch-workstation-vignette.md")
    for token in [
        "screen-space",
        "original PR #20",
        "flat green security terminal",
        "black-only",
        "Pending validation",
    ]:
        if token not in vignette_doc:
            errors.append(f"Launch vignette documentation missing {token!r}.")

    if errors:
        print("FAIL: launch presentation source contracts")
        for error in errors:
            print(" -", error)
        return 1

    print("PASS: launch presentation source contracts (OpenXR path, Meta system splash, restored PR #20 screen-space green terminal, bounded CRT/audio treatment, safe Hub reveal).")
    print("Unity import/compile, Play Mode appearance, APK build, compositor splash, headset handoff, Photon and Quest performance remain separate checks.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
