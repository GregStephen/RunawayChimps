#!/usr/bin/env python3
"""Source contracts for cold-start Gorilla floor safety.

These checks protect the specific startup timing gap found after runtime regression:
the released rig must be proven floor-safe before RigSnapped, and the persistent guard
must remain active during the cold-start Loading presentation once that proof succeeds.
They do not replace Unity 2022.3.55f1, Play Mode, Photon, or headset validation.
"""

from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8-sig")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def numeric_default(source: str, field: str) -> float:
    match = re.search(rf"\b{re.escape(field)}\s*=\s*(-?\d+(?:\.\d+)?)f?\s*;", source)
    require(match is not None, f"Could not read default value for {field}.")
    return float(match.group(1))


def main() -> int:
    snapper = read("Assets/Scripts/Bootstrap/RigSpawnSnapper.cs")
    guard = read("Assets/Scripts/Bootstrap/RigFloorPenetrationGuard.cs")

    require(
        "PostReleaseStableFixedSteps = 3" in snapper,
        "RigSpawnSnapper must require consecutive live-physics floor-safe steps before RigSnapped.",
    )
    require(
        "PostReleaseMaxAttempts = 12" in snapper,
        "RigSpawnSnapper must bound post-release stabilization attempts.",
    )
    require(
        "_floorGuard.ConfigureStartupRecoveryScene(HubSceneName);" in snapper,
        "RigSpawnSnapper must tell the persistent guard which loaded scene owns cold-start floor geometry.",
    )
    require(
        "_floorGuard.EnsureAboveSupportFloor(hubScene, out bool recovered)" in snapper,
        "RigSpawnSnapper must verify the released body against the actual Hub support floor.",
    )
    require(
        "postReleaseStableSteps = 0;" in snapper and "postReleaseStableSteps++;" in snapper,
        "A post-release recovery must restart the consecutive stability proof.",
    )

    restore_index = snapper.find("locomotionPlayer.enabled = playerWasEnabled;")
    verify_index = snapper.find("_floorGuard.EnsureAboveSupportFloor(hubScene, out bool recovered)")
    snapped_index = snapper.find("AppState.I?.MarkRigSnapped();")
    require(
        restore_index >= 0 and verify_index > restore_index and snapped_index > verify_index,
        "Live-physics floor verification must happen after locomotion/physics restore and before MarkRigSnapped.",
    )

    require(
        "public bool EnsureAboveSupportFloor(Scene scene, out bool recovered)" in guard,
        "RigFloorPenetrationGuard must expose an explicit-scene verification/recovery path for startup.",
    )
    require(
        "Scene ResolveRecoveryScene()" in guard,
        "RigFloorPenetrationGuard must resolve a world scene even while Loading is active.",
    )
    require(
        "SceneManager.GetSceneByName(startupRecoverySceneName)" in guard,
        "Cold-start recovery must target the loaded Hub scene while Loading remains active.",
    )
    require(
        "AppState.I != null && !AppState.I.RigSnapped" in guard,
        "The automatic guard should wait for RigSpawnSnapper ownership, not full application readiness.",
    )
    require(
        "AppState.I != null && !AppState.I.IsReady" not in guard,
        "The old IsReady gate recreated an unguarded post-release startup window.",
    )
    require(
        'active.name != "Loading"' in guard,
        "The guard must distinguish Loading from a real active world scene before using the startup fallback.",
    )
    require(
        "SectorTravelService.I != null && SectorTravelService.I.IsBusy" in guard,
        "Persistent floor recovery must not fight the travel coordinator while travel owns the rig.",
    )
    require(
        "origin.transform.position += Vector3.up * (penetration + recoverySkin);" in guard,
        "Floor recovery must remain upward-only and penetration-measured.",
    )
    require(
        "player.ResetAfterTeleport();" in guard,
        "Floor recovery must reset Gorilla locomotion caches after moving the rig.",
    )

    require(numeric_default(snapper, "PostReleaseStableFixedSteps") > 0,
            "PostReleaseStableFixedSteps must stay positive.")
    require(numeric_default(snapper, "PostReleaseMaxAttempts") >= numeric_default(snapper, "PostReleaseStableFixedSteps"),
            "PostReleaseMaxAttempts must allow the required stable steps.")
    require(numeric_default(guard, "maxRecoveryDepth") > numeric_default(guard, "allowedPenetration"),
            "maxRecoveryDepth must exceed allowedPenetration.")

    print("PASS: cold-start floor safety contracts are intact.")
    print("PASS: released rig is proven floor-safe before RigSnapped.")
    print("PASS: floor recovery remains active through the cold-start Loading presentation.")
    print("PASS: Unity/Photon/XR/headset validation remains separate.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
