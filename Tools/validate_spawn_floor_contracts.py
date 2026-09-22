#!/usr/bin/env python3
"""Source contracts for cold-start Gorilla floor safety.

These checks protect the specific startup timing gap found after runtime regression:
the released rig must be proven floor-safe before RigSnapped, and the persistent guard
must remain active during the cold-start Loading presentation once that proof succeeds.
They do not replace Unity 2022.3.55f1, Play Mode, Photon, headset, or repeated cold-start validation.
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

    attempt_start = snapper.index("private IEnumerator CoSnapAndGround(")
    attempt_end = snapper.index("private IEnumerator WaitForTrackingOffsetStability(", attempt_start)
    attempt = snapper[attempt_start:attempt_end]
    try_index = attempt.index("\n        try\n")
    finally_index = attempt.index("\n        finally\n")
    execution = attempt[try_index:finally_index]
    cleanup = attempt[finally_index:]
    slot_index = execution.find("_spawnSlots.TryGetLocalSpawnPose(")
    freeze_index = execution.find("frozenBody.isKinematic = true;")
    restore_index = execution.find("RestoreFrozenRig();")
    verify_index = execution.find("_floorGuard.EnsureAboveSupportFloor(hubScene, out bool recovered)")
    snapped_index = execution.find("AppState.I?.MarkRigSnapped();")
    require(
        0 <= slot_index < freeze_index < restore_index < verify_index < snapped_index,
        "Current-room slot selection, frozen placement, release, live floor verification and RigSnapped must remain ordered.",
    )
    require(
        "RestoreFrozenRig();" in cleanup and
        cleanup.index("RestoreFrozenRig();") < cleanup.index("_snapping = false;") <
        cleanup.index("if (_retryRequested) BeginSnap();"),
        "Every attempt must restore its frozen rig before a queued retry can begin.",
    )
    require(
        "frozenBody.isKinematic = prevKinematic;" in attempt and
        "rigColliders[i].enabled = colliderStates[i]" in attempt and
        "locomotionPlayer.enabled = playerWasEnabled;" in attempt and
        "locomotionPlayer.ResetAfterTeleport();" in attempt[:try_index],
        "Cancellation cleanup must restore original collider/body/locomotion state and teleport caches.",
    )

    retry = snapper[snapper.index("public void RetrySnap()"):snapper.index("private void BeginSnap()")]
    require(
        "_snapGeneration++;" in retry and "_retryRequested = true;" in retry and
        "AppState.I?.ResetHubPlacementReady();" in retry and
        retry.index("_retryRequested = true;") < retry.index("if (!_snapping) BeginSnap();"),
        "New placement requests must invalidate stale readiness and remain queued behind an active attempt.",
    )
    ownership = snapper[snapper.index("private bool IsCurrentAttempt("):snapper.index("private bool HasLivePhysics(")]
    for token in ["generation != _snapGeneration", "IsTravelBusy()", "!hubScene.isLoaded",
                  "SceneManager.GetSceneByName(HubSceneName) != hubScene", "AppState.I.LastError",
                  "PhotonNetwork.InRoom", "ReferenceEquals(room, PhotonNetwork.CurrentRoom)",
                  "PhotonNetwork.LocalPlayer.ActorNumber == actorNumber"]:
        require(token in ownership, f"Snap ownership check is missing {token!r}.")
    require(
        "room = PhotonNetwork.CurrentRoom;" in execution[:slot_index] and
        "actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;" in execution[:slot_index],
        "The snap must bind its Photon room object and actor before reserving a slot.",
    )

    live = snapper[snapper.index("private bool HasLivePhysics("):attempt_start]
    for token in ["locomotionPlayer.isActiveAndEnabled", "!gorillaPlayerRigidbody.isKinematic",
                  "gorillaBodyCapsule.enabled"]:
        require(token in live, f"The live-physics proof must require {token!r}.")
    proof = execution[execution.index("while (postReleaseStableSteps < requiredPostReleaseSteps"):verify_index]
    require(
        "postReleaseAttempts < postReleaseAttemptLimit" in proof and "postReleaseAttempts++;" in proof,
        "Live floor verification must count attempts and stop at the configured limit.",
    )
    require(
        "if (!IsCurrentAttempt(generation, hubScene, room, actorNumber)) yield break;" in proof and
        "if (!HasLivePhysics(locomotionPlayer))" in proof,
        "Each resumed live-physics step must verify session ownership and a released rig before floor recovery.",
    )
    require(
        "if (!IsCurrentAttempt(generation, hubScene, room, actorNumber) || !HasLivePhysics(locomotionPlayer))" in
        execution[verify_index:snapped_index],
        "Readiness must remain guarded by current session ownership and live physics.",
    )
    disable = snapper[snapper.index("private void OnDisable()"):snapper.index("private void OnSceneLoaded(")]
    require(
        "_snapGeneration++;" in disable and "_retryRequested = false;" in disable and
        "StopCoroutine(_snapCoroutine);" in disable and "System.IDisposable)?.Dispose();" in disable,
        "Disabling the snapper must invalidate pending work and dispose its frozen-state cleanup.",
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
    print("PASS: source requires current-room placement and live floor checks before RigSnapped.")
    print("PASS: source protects queued retries, cancelled-rig cleanup and released-physics ownership.")
    print("PASS: source keeps floor recovery active through the cold-start Loading presentation.")
    print("PASS: Unity/Photon/XR/headset validation remains separate.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (AssertionError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
