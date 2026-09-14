# Launch presentation

Last updated: 2026-09-13. Branch: `feature/launch-presentation-polish`, rebased onto current `main` after PR #19 (`68eaeab375d94c787b69ce8726d22df161008772`). Runaway Chimps uses Unity **2022.3.55f1**, Photon PUN, Meta XR SDK 83.0.1 and OpenXR 1.13.2.

## Confirmed direction

**Confirmed:** the game launch presentation uses the approved **facility security-system boot**. The earlier floating/void startup idea is not an active alternative. Normal Hub/level travel remains **black-only** with no boot terminal, logo, tips or loading text unless measured transition times later justify revisiting that rule. Real travel failures still surface recovery feedback.

**Confirmed VR boundary:** the player/head camera is never artificially moved for launch presentation. Stereo/peripheral coverage must remain opaque and comfortable. No aggressive strobe, screen shake or forced camera flight is part of the launch design.

**Confirmed presentation correction, September 13:** the approved green facility terminal should not remain visually flat. It should read as an aging facility/security display with restrained CRT/static character that can be judged directly in Unity Play Mode. Keep the treatment subtle enough for VR: no full-screen flicker and no effect outside the terminal panel.

## Implemented on this branch

**Implemented, pending runtime validation:** a new `Assets/Branding/RunawayChimps_SystemSplash.png` provides a simple branded facility-security panel that visually leads into the existing in-app security boot. `Assets/Oculus/OculusProjectConfig.asset` now references that image with a black system-loading background.

Because Android currently uses the OpenXR loader, `LaunchPresentationSettings` also assigns the same texture to the serialized **MetaXRFeature Android** `systemSplashScreen` property before Android builds and exposes Editor menu items to apply/validate the setting explicitly. This preserves the current OpenXR provider rather than switching XR loaders.

**Implemented security-boot polish, pending Play Mode/headset validation:** `SecurityBootPresentation` now adds low-contrast procedural CRT noise behind the terminal text, faint fixed scanlines, and an occasional soft horizontal interference sweep. A short, bounded static increase accompanies real startup milestones coming online. The treatment uses a small 64 × 48 reusable texture, deterministic local noise, no RenderTexture, no `UnityEngine.Random`, no tracked-camera movement and no full-FOV brightness flash. Prototype-only runtime/Inspector wording has also been removed.

**Play Mode vs. Quest build boundary:** the green security boot and its CRT/static treatment are ordinary Unity UI and **are visible in Play Mode** when starting from `Bootstrap.unity`. The Meta/Horizon **system splash is not visible in Editor Play Mode** because it is compositor-driven before Unity's first application frame; that layer requires an Android/Quest build to validate.

**Implemented guardrail:** Android builds fail early if the Quest system splash cannot be found or assigned to the Meta OpenXR/Oculus project config. The setup logs a warning when Unity's built-in splash is still enabled because Meta's current guidance prefers system splash + custom startup scene. The branch does not blindly disable Unity's splash: Unity 2022 Personal licensing can enforce Unity branding, so that remains a license/build-specific validation step.

**Implemented source validation:** `Tools/validate_launch_presentation.py` checks the splash asset/GUID, active OpenXR loader contract, Meta/Oculus splash references, black system background, build preprocessor, absence of a Unity VR splash image, production boot naming, and the bounded CRT/static implementation. It deliberately does not claim that a Quest compositor displayed the image or that the CRT treatment is visually comfortable until it is actually reviewed.

## Expected launch stack

For Quest builds after this branch is applied, the intended sequence is:

1. Horizon OS launches Runaway Chimps and shows the Meta **system splash** against black while the app initializes.
2. The compositor removes the system splash on the first rendered app frame.
3. The custom green security boot appears on black with subtle CRT noise/scanlines/interference, observes real startup milestones, and keeps `ACCESS GRANTED` visible briefly before entry.
4. The boot fades into the Hub.
5. Later Hub/level travel stays black-only.

Meta documents the system splash as compositor-driven and removed on the first app frame; this branch therefore keeps the first application presentation stationary and black-backed so the handoff does not expose partially initialized world geometry.

## Source validation record

**Validated source only, 2026-09-13:** Source Integrity run `34798289632` passed on commit `75104bd7051a772385636cab61f75e4e0b4e86ac`. The run passed first-party C# syntax/reference checks, Unity metadata/GUID integrity, Level 1 contracts, PR #15 hardening, the merged security-boot contracts, PR #19 local threat-feedback contracts, the launch-presentation contracts, Python compilation, merge-marker checks, and human-authored whitespace. This predates the CRT/static visual-polish commit; the final branch head must pass Source Integrity again before the new treatment is considered source-validated. This is source/tooling evidence only, not Unity compilation, a Quest build, compositor behavior, Photon runtime, or headset validation.

## Not implemented / still open

- **Not implemented:** floating startup environment, surveillance-video reveal, CRT geometry/warping, final logo artwork, forced camera movement or a fake progress percentage.
- **Open polish:** whether the small native splash panel should later be replaced with final approved logo artwork while keeping the same system-splash role.
- **Open license/build detail:** whether the active Unity 2022 license permits disabling the built-in Unity splash. Do not claim that layer is gone until an actual Quest build confirms it.

## Pending validation

- **Pending validation:** open `Bootstrap.unity` in Unity 2022.3.55f1 and press Play. Confirm the green boot now shows subtle noise, scanlines and occasional interference without reducing text readability or producing uncomfortable flicker.
- **Pending validation:** Unity 2022.3.55f1 import and compile of `LaunchPresentationSettings`, the CRT/static changes, and all existing startup scripts.
- **Pending validation:** run **Tools > Runaway Chimps > Launch Presentation > Apply Quest System Splash**, then validate the serialized OpenXR Android feature in the Inspector.
- **Pending validation:** build/install a Quest APK and confirm the native Meta splash appears before the first app frame, stays centered/readable under head movement, and hands off cleanly to the security boot without an unintended gray/Unity splash step.
- **Pending validation:** Quest 2/3/3S as available, both-eye/peripheral coverage, recenter, pause/resume, cold launch, repeated launch, and audio comfort.
- **Pending validation:** existing Photon startup/retry paths, two-client room behavior, Hub entry and black-only Hub/level travel remain unchanged.
- **Pending validation:** Quest frame timing and memory impact of both the splash asset and the small procedural static texture are negligible.

Source validation is not runtime proof. Record the device, build/commit, observed launch layers and timing before marking any compositor/headset item validated.
