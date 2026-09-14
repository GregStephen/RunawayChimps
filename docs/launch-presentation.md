# Launch presentation

Last updated: 2026-09-13. Branch: `feature/launch-presentation-polish`, reconciled onto current `main` after PR #22 (`f9edea67af08ff7c35d043e05ad66a5615a9aeb2`). Runaway Chimps uses Unity **2022.3.55f1**, Photon PUN, Meta XR SDK 83.0.1 and OpenXR 1.13.2.

## Confirmed direction

**Confirmed:** the game launch presentation uses the approved **facility security-system boot**. The earlier floating/void startup idea is not an active alternative. Normal Hub/level travel remains **black-only** with no boot terminal, logo, tips or loading text unless measured transition times later justify revisiting that rule. Real travel failures still surface recovery feedback.

**Confirmed workstation refinement:** the green boot now belongs on a **physical security workstation monitor** in a small desk vignette rather than remaining a flat floating panel. This is startup-only presentation, not a room the player explores. The player/head camera is never artificially moved. The workstation is placed once from the initial horizontal camera heading and then remains world-stationary.

**Confirmed CRT/static correction:** the facility display should feel old and imperfect without becoming uncomfortable in VR. The monitor may use low-contrast static, faint scanlines, occasional soft interference and small milestone-linked static changes, but no full-screen flicker, forced camera motion, aggressive strobe or fake progress percentage.

## Implemented on this branch

**Implemented physical startup vignette, pending Play Mode/headset validation:** `SecurityWorkstationVignette` builds a deliberately simple low-poly security desk around the existing boot. It includes a monitor shell/stand, desk and pedestals, keyboard, mug, badge/card prop and clipboard. Runtime primitive colliders are disabled and removed; the set owns no locomotion, physics, Photon or readiness behavior.

The existing green boot is now mounted on a **world-space canvas inside the physical monitor**. The monitor canvas was tightened to fit within the screen opening with a deliberate dark border. The two monitor notes were pulled onto the actual bezel instead of floating outside it. The current optional flavor notes are `CAM 04 / STILL DEAD`, `VENT B / AGAIN?`, and `IF THEY GET OUT / I QUIT.` These are editable Easter-egg dressing, not required puzzle clues or confirmed progression lore.

**Implemented render isolation:** the startup workstation and authored black Loading cover use a dedicated `LoadingPresentation` layer. During startup the persistent camera sees only that layer against a black clear, preventing additively loaded Hub UI/world content from leaking behind the desk. During the final reveal it temporarily renders the saved Hub mask plus `LoadingPresentation`, fades the black cover away, then restores the exact original culling mask, clear flags and background. Interrupted entry re-applies the isolated startup mask and restores the workstation/error presentation.

**Implemented security-screen polish:** `SecurityBootPresentation` retains the real Photon/Hub/rig/avatar milestone rows, retry/error text, quiet relay ticks and `ACCESS GRANTED` dwell. It adds low-contrast procedural CRT noise, faint fixed scanlines and an occasional soft horizontal interference sweep. A short bounded static increase accompanies actual startup milestones coming online. The treatment uses one small reusable 64 × 48 texture/buffer, deterministic local noise, no RenderTexture and no shared `UnityEngine.Random` state.

**Implemented native Quest layer, pending device validation:** `Assets/Branding/RunawayChimps_SystemSplash.png` is assigned to the Meta/Oculus system-splash path with a black loading background. `LaunchPresentationSettings` applies/validates the Meta OpenXR setting before Android builds and fails early if the required splash cannot be configured. The Android XR loader remains OpenXR; this branch does not switch XR providers.

**Play Mode vs. Quest build boundary:** the physical desk, monitor, notes, green boot and CRT/static treatment are ordinary Unity presentation and **are visible in Play Mode** when starting from `Bootstrap.unity`. The Meta/Horizon **system splash is not visible in Editor Play Mode** because it is compositor-driven before Unity's first application frame; that layer requires an Android/Quest build to validate.

## Expected launch stack

1. On Quest later: Horizon OS shows the Meta **system splash** against black while the app initializes.
2. First Unity frame: a dark, isolated physical security workstation appears in black space.
3. The green facility boot runs on the workstation monitor and observes real readiness milestones.
4. `ACCESS GRANTED` remains visible briefly.
5. The workstation is covered by the full-FOV black Loading layer before being hidden.
6. Hub + black overlay render together; black fades into the Hub; the camera then restores its exact original render state.
7. Later Hub/level travel remains black-only and constructs no workstation, notes, CRT UI or boot audio.

Unity 2022's built-in splash remains a **pending license/build detail**. The branch does not blindly disable it because Unity 2022 Personal licensing can enforce Unity branding. The build tooling warns while that layer remains enabled so an actual Quest build can establish whether it can be removed under the active license.

## Source validation record

The pre-workstation launch/CRT implementation passed Source Integrity before this refinement. The workstation implementation has its own source contracts covering startup-only construction, dedicated render-layer isolation, world-space monitor UI, collider removal, no tracked-camera writes, no Photon/readiness writes, fitted monitor composition and unchanged black-only travel. Record the latest clean-head run here only after it completes successfully; source validation remains separate from Unity visual/runtime validation.

## Not implemented / still open

- **Not implemented:** a final authored Blender workstation/prop set, surveillance-video reveal, CRT geometry/warping, final logo artwork, forced camera movement or fake progress.
- **Open art polish:** the current desk is runtime primitive geometry intended to prove composition/scale. It can later be replaced by authored Blender assets without changing startup logic.
- **Open Easter-egg copy:** the three current notes are first-pass flavor. Exact note wording is not locked as canon unless Greg approves it separately.
- **Open native splash art:** the current Quest splash can later be replaced with final approved logo artwork while keeping the same compositor role.
- **Open license/build detail:** whether the active Unity 2022 license permits disabling the built-in Unity splash. Do not claim that layer is gone until an actual Quest build confirms it.

## Pending validation

- **Pending validation now:** open `Assets/Scenes/Bootstrap.unity` in Unity 2022.3.55f1 and press Play. Confirm the workstation appears approximately two meters ahead, remains world-stationary while looking around, and does not expose loaded Hub UI behind it.
- **Pending validation:** confirm the physical monitor/desk scale feels right, the green boot fits the inset without clipping, CRT effects remain subtle/readable, and the three flavor notes are discoverable without competing with status text.
- **Pending validation:** verify `ACCESS GRANTED` -> black cover -> Hub reveal has no workstation pop, gray flash or premature Hub UI; force a startup interruption/retry and confirm the workstation/error presentation is restored.
- **Pending validation:** repeat ordinary Hub/Level 1/Level 2 travel and verify it remains black-only with no workstation replay.
- **Pending validation:** Unity 2022.3.55f1 import/compile of all launch/workstation scripts and the new `LoadingPresentation` layer.
- **Pending validation for Quest setup later:** run **Tools > Runaway Chimps > Launch Presentation > Apply Quest System Splash**, then **Validate Quest System Splash**; build/install an APK and verify compositor splash -> physical security boot handoff.
- **Pending validation:** Quest stereo/peripheral coverage, recenter, pause/resume, cold/repeated launch, note readability, comfort, frame timing and memory.
- **Pending validation:** Photon startup/retry/two-client behavior remains unchanged.

See [Launch workstation vignette](launch-workstation-vignette.md) for the exact implementation and focused acceptance checklist. Source validation is not runtime proof.
