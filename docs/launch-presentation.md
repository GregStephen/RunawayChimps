# Launch presentation

Last updated: 2026-09-21. Branch: `feature/launch-presentation-polish`. Runaway Chimps uses Unity **2022.3.55f1**, Photon PUN, Meta XR SDK 83.0.1 and OpenXR 1.13.2.

## Confirmed direction

**Confirmed:** the game launch presentation uses the approved **facility security-system boot**. Normal successful Hub/level travel remains **black-only** with no boot terminal, logo, tips or loading text unless measured transition times later justify revisiting that rule. Real travel failures still surface recovery feedback.

**Superseding September 14 correction:** Greg rejected the physical security workstation/desk presentation and explicitly asked for the **old green screen back**. The workstation/desk version is no longer the selected visual direction.

**September 21 runtime correction:** the first world-space tuning at **3.9 m** was visually rejected as **way too far away**, and the terminal rendered reversed/mirrored. The 3.9 m target is superseded. The mirrored presentation was a source bug caused by a 180-degree local Y rotation on the world-space canvas.

The selected Unity-rendered launch sequence is now:

1. Black startup isolation.
2. One flat green facility security terminal in black space.
3. The current retune places the terminal about **2.5 m** ahead of the initial horizontal player view. This is an implemented tuning candidate, not yet visually approved.
4. Real readiness rows update from Photon/Hub/rig/avatar state; no fake percentage or simulated progress.
5. `ACCESS GRANTED` holds briefly.
6. Black fully covers and retires the terminal before the Hub is revealed.
7. Successful Hub/Level 1/Level 2 travel remains black-only and does not replay the terminal.

The player/head camera is never artificially translated or rotated. The terminal may follow hidden `XROrigin` root relocation during startup so rig snapping does not leave it behind, but ordinary head look and room-scale motion do not head-lock it.

## Implemented on this branch

**Implemented visual correction, pending Unity validation:** `SecurityWorkstationVignette` remains the legacy source-file/class name, but it no longer builds a workstation. It now creates only the world-space green terminal canvas. The previous runtime desk, monitor shell, keyboard, mug, badge, clipboard and note props have been removed from the active presentation.

The 1080 × 820 terminal keeps the prior monitor-canvas physical scale (`0.00082`) and is now placed **2.50 m** from the initial horizontal camera heading at eye height. The previous 3.90 m test is rejected. The canvas now uses an identity local rotation so the player sees the front face rather than the mirrored back face.

**Implemented render isolation:** the startup terminal and authored black Loading cover use the dedicated `LoadingPresentation` layer. During startup the persistent camera sees only that layer against a black clear, preventing additively loaded Hub UI/world content from leaking through. During the final reveal it temporarily renders the saved Hub mask plus `LoadingPresentation`, fades black away, then restores the exact original camera culling mask, clear flags and background. Interrupted entry reapplies startup isolation and may rebuild the terminal/error presentation.

**Implemented terminal behavior:** `SecurityBootPresentation` retains real Photon/Hub/rig/avatar milestone rows, retry/error text, quiet relay ticks, CRT noise, faint scanlines, a soft interference sweep and the brief `ACCESS GRANTED` dwell. No RenderTexture, shared `UnityEngine.Random`, fake percentage or camera motion is introduced.

**Implemented native Quest layer, pending device validation:** `Assets/Branding/RunawayChimps_SystemSplash.png` remains assigned to the Meta/Oculus system-splash path with a black loading background. Android remains on the existing OpenXR loader with the Meta Android feature enabled. Build preprocessing validates committed settings rather than mutating/saving them.

**Implemented multiplayer/session hardening:** ten room-owned Hub spawn slots, authored in `Assets/Resources/HubSpawn/HubSpawnSlots.prefab`, remain part of PR #21. Slot claims are demand-driven by Hub placement, recover existing ownership, release/reconcile through Photon lifecycle events, and require a fresh Hub snap after a new room session before the local avatar may spawn. The visual rollback does not remove this reliability work.

## Removed / superseded presentation work

The following PR #21 presentation elements are now historical rather than selected behavior:

- Physical security desk/pedestals.
- Monitor shell/stand/base surrounding the terminal.
- Keyboard, mug, badge/card prop and clipboard.
- `CAM 04 / STILL DEAD`, `VENT B / AGAIN?`, and `IF THEY GET OUT / I QUIT.` startup notes.
- The idea that a final authored Blender workstation should replace the primitive desk.

The native Quest splash, CRT treatment, startup render isolation, Hub-slot/session fixes, reveal-state hardening and allocation cleanup remain active.

## Platform boundary

The Meta/Horizon **system splash** is compositor-driven before Unity's first application frame and therefore cannot be validated from ordinary Editor Play Mode. The green security terminal is normal Unity presentation and should be visible when starting from `Assets/Scenes/Bootstrap.unity`.

Unity 2022's built-in splash remains a pending license/build detail. Do not claim it is absent until an actual Quest build confirms the active license/build behavior.

## Pending validation

- Unity **2022.3.55f1** import/compile.
- Play Mode: confirm the flat green terminal appears about **2.5 m** ahead and is centered at a comfortable eye-height presentation.
- Verify the 2.5 m retune feels modestly farther than the original without becoming distant, and that all startup text remains readable.
- Verify text is no longer mirrored/reversed. Verify ordinary head turning does not move/head-lock the terminal and hidden XR-origin relocation does not leave it behind.
- Verify the subtle CRT/static treatment is still comfortable/readable at the longer distance.
- Verify `ACCESS GRANTED` -> full black -> terminal absent -> Hub reveal has no terminal reconstruction, gray flash, world/UI leakage or permanent black.
- Force a startup interruption/retry and confirm the terminal/error presentation restores correctly.
- Repeat successful Hub/Level 1/Level 2 travel and confirm it remains black-only.
- Two-client Photon: simultaneous distinct Hub slot claims, room switch/rejoin/reconnect placement, slot reuse and Master Client handoff.
- Authored Hub-slot floor/wall/prop clearance and multiplayer comfort.
- Quest Android build/install: compositor system splash -> green security terminal handoff, both-eye/peripheral coverage, recenter, pause/resume, repeated cold launch, performance and memory.

## Validation record

Earlier PR #21 source runs validated the workstation-era source state only and do **not** validate this superseding visual correction. Run Source Integrity again on the corrected branch head before treating source validation as current. Unity/runtime/headset validation remains separate even after source checks pass.

See [Launch vignette — restored flat security terminal](launch-workstation-vignette.md) for the focused implementation and acceptance checklist.

**Validated source only — restored distant green terminal:** clean head `cdf6b87a55d429351acf0d6b96a632fda4847893` passed Source Integrity run `34917902460` on 2026-09-14. The run passed Unity 2022.3.55f1 version enforcement, first-party C# syntax/reference and enabled-scene checks, Unity metadata/GUID integrity, Level 1 and PR #15 contracts, the restored distant security-boot contracts, local threat-feedback contracts, launch/Quest/Hub-slot/session contracts, Python compilation, merge-marker rejection and human-authored whitespace. This validates source/tooling only; Unity import/compile, Play Mode, Photon multi-client behavior and Quest/headset behavior remain pending.

## September 21 runtime retune — distance and orientation

**Failed visual validation:** the 3.90 m world-space terminal was far too distant in Play Mode and the text appeared reversed/mirrored.

**Implemented correction, pending retest:** terminal distance is reduced to **2.50 m** and the world-space canvas local rotation is now identity instead of a 180-degree Y flip. Source validators explicitly reject the mirrored rotation and constrain the current distance to a conservative mid-distance range. The exact 2.50 m value remains tuning until Greg visually approves it.

**Validated source only — September 21 terminal distance/orientation retune:** code/docs head `cdfdfe16e3fb9809339bfad507e18e749a022393` passed Source Integrity run `35676148648`. The run passed Unity 2022.3.55f1 version enforcement, C# syntax/references, repository integrity, Level 1 and PR #15 contracts, front-facing security-boot contracts, local threat-feedback contracts, launch/Quest/Hub-slot/session contracts, Python compilation, merge-marker rejection and whitespace checks. This validates source/tooling only; Unity Play Mode/headset visual approval of the 2.50 m placement remains pending.

## September 21 final visual correction — original screen-space boot

The 2.50 m world-space retune also failed runtime review: the boot/perceived view moved with XR-rig settling, hidden hand-floor impacts were audible, and the effect no longer felt like the earlier approved loading screen. The active direction is now the **original PR #20 screen-space green terminal**, not a world-space panel at any distance.

**Implemented:** build the 1080 × 820 terminal directly on the Loading canvas; bind the canvas with `RenderMode.ScreenSpaceCamera`; restore the original view-relative 72% width / 84% height scaling and camera-plane placement; remove the world-space panel helper; suppress `HandImpactAudio` for the duration of cold-start Loading; retain visual CRT noise/scanlines/interference; and add a very soft initial/periodic static crackle. All Photon Hub-slot/session, retry/reveal, black-travel and Quest-splash hardening remains in place.

**Pending validation:** confirm in Unity 2022.3.55f1 that the boot once again looks like the earlier approved green screen, does not visibly fall with the rig, startup hand impacts are silent, CRT/static reads clearly but comfortably, and Hub reveal remains clean.

## September 21 screen-space size retune

**Runtime feedback:** the restored screen-space green terminal is stable but still too large.

**Implemented tuning candidate:** use the approved `terminalScale = 0.50`, producing roughly **36% width / 42% height** from the 72% / 84% PR #20 baseline. The screen remains `ScreenSpaceCamera`; no world-space distance is used. All CRT/static, startup-audio suppression, readiness, retry/reveal, Photon Hub-slot/session and Quest-splash behavior remains unchanged.

**Pending validation:** visual approval of size/readability in Unity 2022.3.55f1 Play Mode/headset.

## September 21 live Play Mode tuning workflow

**Implemented for visual tuning:** the screen-space terminal now exposes a serialized `terminalScale` slider from **0.40 to 1.00**. `1.00` is the original PR #20 footprint; the visually approved default is **0.50**, which renders the terminal at half the original PR #20 linear scale. The aspect ratio is preserved because one multiplier controls both dimensions.

**Editor workflow:** before Play, enable **Tools > Runaway Chimps > Hold Security Boot For Review**. Start `Bootstrap.unity`; once the boot is visible/ready it stays on screen. Then choose **Tools > Runaway Chimps > Select Active Security Boot Tuning**. Unity selects the runtime Loading canvas object containing `SecurityBootPresentation`; adjust **Terminal Scale** in the Inspector and the terminal resizes live every frame. Play Mode changes do not persist after stopping, so record the preferred value and make it the serialized default afterward.

This tuning workflow is Editor-only and does not bypass real startup readiness or ship in a player build.

## September 21 terminal scale approval

**Validated visual tuning:** Greg used the live Play Mode tuning workflow and reported that **Terminal Scale = 0.50 looked awesome**. Record **0.50** as the approved default for the screen-space green security boot. This validates the terminal's visual size in that Play Mode review only; it does not by itself validate the remaining startup audio/static, Photon multi-client, Hub reveal, or Quest/headset checks.
