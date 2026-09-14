#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def append_once(path: str, marker: str, text: str) -> None:
    target = ROOT / path
    current = target.read_text(encoding="utf-8-sig")
    if marker in current:
        return
    target.write_text(current.rstrip() + "\n\n" + text.strip() + "\n", encoding="utf-8")


append_once(
    "docs/design-and-lore.md",
    "## September 13 confirmed launch presentation and Quest system splash",
    '''## September 13 confirmed launch presentation and Quest system splash

**Confirmed design direction:** Greg approved the facility **security-system boot** as Runaway Chimps' launch presentation after reviewing the implemented prototype. The earlier floating/void startup idea is no longer an active alternative. Successful Hub/level travel remains **black-only** with no boot terminal, logo, tips or loading text; reconsider a minimal travel indicator only if measured transitions later approach roughly 10 seconds. Real travel failures still surface recovery feedback.

**Confirmed VR boundary:** launch presentation must not artificially translate or rotate the tracked player/head camera. Keep full stereo/peripheral coverage opaque and comfortable; do not add aggressive strobe, camera shake or forced camera flight.

**Implemented on `feature/launch-presentation-polish` / PR #21, pending runtime validation:** the approved in-app security boot remains unchanged as the authoritative custom startup scene. A new `Assets/Branding/RunawayChimps_SystemSplash.png` is wired to the Meta/Oculus project configuration with a black loading background. Android already uses the OpenXR loader and Meta XR Feature; `LaunchPresentationSettings` preserves that provider, assigns the same texture to `MetaXRFeature Android.systemSplashScreen` before Android builds, exposes apply/validate Editor commands, and fails an Android build early if the Quest system splash cannot be configured. The launch layer does not alter Photon readiness, room ownership, scene travel, rig motion or monster behavior.

**Platform boundary:** Meta's system splash is compositor-driven and is intended to cover startup before the first app frame, then hand off to the custom security boot. Unity's built-in splash remains serialized as enabled on this Unity 2022.3 project. The branch warns about that layer rather than blindly disabling it because Unity 2022 Personal licensing can require Unity branding; an actual Quest build must establish whether it can be removed under the active license.

**Pending validation:** Source Integrity for PR #21, Unity 2022.3.55f1 import/compile, Editor apply/validate commands, installed Quest APK system-splash display, first-frame handoff into the security boot, both-eye/peripheral coverage, recenter/pause/relaunch behavior, Photon startup/retry/two-client regression, black-only sector travel and Quest performance remain pending. Source inspection does not prove compositor or headset behavior. See `docs/launch-presentation.md`.'''
)

append_once(
    "docs/repository-improvement-plan.md",
    "## September 13 launch-presentation production pass",
    '''## September 13 launch-presentation production pass

**Confirmed product decision:** the facility security-system boot approved after PR #20 is the selected launch presentation. The floating/void concept is inactive. Normal Hub/level travel stays black-only unless measured transition duration later justifies a separate review; real errors retain recovery feedback.

**Implemented source on `feature/launch-presentation-polish` / PR #21:** add a lightweight branded Quest system-splash texture, serialize it into `OculusProjectConfig` with a black background, and add Editor/build-time tooling that assigns and validates the same texture on the active Android `MetaXRFeature` while preserving the existing OpenXR loader. The Android build preprocessor fails closed when the splash asset/feature assignment is unavailable. `Tools/validate_launch_presentation.py` protects the OpenXR loader contract, splash GUID/reference, black background, build guard and empty Unity VR splash-image slot. The existing security boot, Photon readiness gates and black-only travel are not replaced.

**Risk/ownership boundary:** this work owns presentation before and during app startup only. It must not create a second XR rig, move the tracked camera, invent progress, change Photon/session state, or expose partially initialized Hub geometry. Unity's built-in splash is deliberately not disabled in source until the active Unity 2022 license/build behavior is verified.

**Pending validation:** PR #21 Source Integrity; Unity 2022.3.55f1 import/compile; Quest Android build/install; compositor system splash -> first app frame -> security boot -> Hub handoff; both-eye/full-FOV readability; pause/resume/recenter/repeated cold launch; Photon startup/retry/two-client checks; black-only Hub/level transitions; and Quest frame-time/memory impact. Record device/build/commit before marking any runtime item validated. See `docs/launch-presentation.md`.'''
)
