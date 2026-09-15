# Launch presentation

Last updated: 2026-09-14. Branch: `feature/launch-presentation-polish`. Runaway Chimps uses Unity **2022.3.55f1**, Photon PUN, Meta XR SDK 83.0.1 and OpenXR 1.13.2.

## Confirmed direction

**Confirmed:** the game launch presentation uses the approved **facility security-system boot**. Normal successful Hub/level travel remains **black-only** with no boot terminal, logo, tips or loading text unless measured transition times later justify revisiting that rule. Real travel failures still surface recovery feedback.

**Superseding September 14 correction:** Greg rejected the physical security workstation/desk presentation and explicitly asked for the **old green screen back**, positioned roughly **twice as far away**. The workstation/desk version is no longer the selected visual direction.

The selected Unity-rendered launch sequence is now:

1. Black startup isolation.
2. One flat green facility security terminal in black space.
3. The terminal sits about **3.9 m** ahead of the initial horizontal player view, twice the previous 1.95 m workstation distance.
4. Real readiness rows update from Photon/Hub/rig/avatar state; no fake percentage or simulated progress.
5. `ACCESS GRANTED` holds briefly.
6. Black fully covers and retires the terminal before the Hub is revealed.
7. Successful Hub/Level 1/Level 2 travel remains black-only and does not replay the terminal.

The player/head camera is never artificially translated or rotated. The terminal may follow hidden `XROrigin` root relocation during startup so rig snapping does not leave it behind, but ordinary head look and room-scale motion do not head-lock it.

## Implemented on this branch

**Implemented visual correction, pending Unity validation:** `SecurityWorkstationVignette` remains the legacy source-file/class name, but it no longer builds a workstation. It now creates only the world-space green terminal canvas. The previous runtime desk, monitor shell, keyboard, mug, badge, clipboard and note props have been removed from the active presentation.

The 1080 × 820 terminal keeps the prior monitor-canvas physical scale (`0.00082`) and is placed **3.90 m** from the initial horizontal camera heading at eye height. This preserves the approved terminal design while making it feel substantially farther away and less intrusive.

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
- Play Mode: confirm the flat green terminal appears about **3.9 m** ahead and is centered at a comfortable eye-height presentation.
- Verify the increased distance produces the intended smaller/farther appearance while keeping all startup text readable.
- Verify ordinary head turning does not move/head-lock the terminal and hidden XR-origin relocation does not leave it behind.
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
