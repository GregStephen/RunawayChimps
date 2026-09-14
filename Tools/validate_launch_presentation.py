"""Source-only launch presentation checks. Unity/headset/build validation remains separate."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SPLASH = ROOT / "Assets/Branding/RunawayChimps_SystemSplash.png"
SPLASH_META = SPLASH.with_suffix(".png.meta")
SPLASH_GUID = "73af3b98a31d49a6a0b674b50ed8d20c"


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

    oculus = read("Assets/Oculus/OculusProjectConfig.asset")
    required_oculus = [
        f"systemSplashScreen: {{fileID: 2800000, guid: {SPLASH_GUID}, type: 3}}",
        "_systemLoadingScreenBackground: 0",
    ]
    for token in required_oculus:
        if token not in oculus:
            errors.append(f"OculusProjectConfig missing {token!r}.")

    setup = read("Assets/Scripts/Editor/LaunchPresentationSettings.cs")
    for token in [
        "MetaXRFeature Android",
        "systemSplashScreen",
        "BuildTarget.Android",
        "IPreprocessBuildWithReport",
        "RunawayChimps_SystemSplash.png",
        "PlayerSettings.SplashScreen.show",
        "_systemLoadingScreenBackground",
    ]:
        if token not in setup:
            errors.append(f"LaunchPresentationSettings missing {token!r}.")

    boot = read("Assets/Scripts/Loading/SecurityBootPresentation.cs")
    for token in [
        "Security Boot Terminal",
        "SecurityWorkstationVignette.Create",
        "MonitorCanvasRoot",
        "BuildCrtTreatment",
        "StaticRefreshInterval = 0.10f",
        "RefreshStaticTexture",
        "Low-level CRT static",
        "CRT scanline",
        "CRT interference sweep",
        "NextNoise01",
        "staticBurstUntil",
        "SetBackdropOpacity(1f - alpha)",
    ]:
        if token not in boot:
            errors.append(f"Security boot workstation/CRT treatment missing {token!r}.")

    workstation = read("Assets/Scripts/Loading/SecurityWorkstationVignette.cs")
    workstation_tokens = [
        "Security Workstation Vignette",
        "Security Monitor World Canvas",
        "RenderMode.WorldSpace",
        "MonitorCanvasScale = 0.00094f",
        "camera.transform.position + forward * 1.95f",
        "GameObject.CreatePrimitive",
        "collider.enabled = false",
        "Destroy(collider)",
        "Shader.Find(\"Unlit/Color\")",
        "Shader.Find(\"Standard\")",
        "CAM 04\\nSTILL DEAD",
        "VENT B\\nAGAIN?",
        "IF THEY GET OUT\\nI QUIT.",
    ]
    for token in workstation_tokens:
        if token not in workstation:
            errors.append(f"Security workstation vignette missing {token!r}.")

    for text, name in ((boot, "SecurityBootPresentation"), (workstation, "SecurityWorkstationVignette")):
        for forbidden in ["UnityEngine.Random.Range", "Random.Range(", "new RenderTexture",
                          "PhotonNetwork.Join", "PhotonNetwork.Instantiate", "MarkRigSnapped(", "TryMarkReady("]:
            if forbidden in text:
                errors.append(f"{name} must stay local, presentation-only and allocation-light: {forbidden!r} found.")
        if re.search(r"(?:camera|startupCamera|boundCamera)\.transform\.(?:position|rotation|localPosition|localRotation)\s*=", text):
            errors.append(f"{name} must not write the tracked camera transform.")

    if "0.065f" not in boot or "0.045f" not in boot:
        errors.append("Security boot CRT treatment no longer exposes the reviewed low-alpha interference/static caps.")

    flow = read("Assets/Scripts/Bootstrap/LoadingFlow.cs")
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

    workstation_doc = read("docs/launch-workstation-vignette.md")
    for token in [
        "Confirmed direction",
        "Exact branch implementation",
        "world-space monitor canvas",
        "CAM 04 / STILL DEAD",
        "black-only",
        "Validation plan",
    ]:
        if token not in workstation_doc:
            errors.append(f"Workstation vignette documentation missing {token!r}.")

    if errors:
        print("FAIL: launch presentation source contracts")
        for error in errors:
            print(" -", error)
        return 1

    print("PASS: launch presentation source contracts (OpenXR path, Meta system splash, physical workstation monitor, bounded CRT treatment, black-background ownership).")
    print("Unity import/compile, Play Mode appearance, APK build, compositor splash, headset handoff, Photon and Quest performance remain separate checks.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
