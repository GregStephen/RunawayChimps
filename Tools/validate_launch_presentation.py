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
        "BuildCrtTreatment",
        "StaticRefreshInterval = 0.10f",
        "RefreshStaticTexture",
        "Low-level CRT static",
        "CRT scanline",
        "CRT interference sweep",
        "NextNoise01",
        "staticBurstUntil",
    ]:
        if token not in boot:
            errors.append(f"Security boot CRT treatment missing {token!r}.")
    for forbidden in ["UnityEngine.Random.Range", "Random.Range(", "new RenderTexture"]:
        if forbidden in boot:
            errors.append(f"Security boot CRT treatment must stay local, allocation-light and deterministic: {forbidden!r} found.")
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

    if errors:
        print("FAIL: launch presentation source contracts")
        for error in errors:
            print(" -", error)
        return 1

    print("PASS: launch presentation source contracts (OpenXR path, Meta system splash, build guard, CRT boot treatment, black-background ownership).")
    print("Unity import/compile, Play Mode appearance, APK build, compositor splash, headset handoff, Photon and Quest performance remain separate checks.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
