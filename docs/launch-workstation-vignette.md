# Launch workstation vignette

Date: 2026-09-13. Branch: `feature/launch-presentation-polish`, reconciled onto current `main` after PR #22 (`f9edea67af08ff7c35d043e05ad66a5615a9aeb2`). Unity **2022.3.55f1**, Photon PUN.

## Confirmed direction

Greg approved taking the existing green facility security-system boot one step further by presenting it on a **physical security workstation monitor** instead of leaving the boot as a flat floating panel. The workstation is startup-only. Successful Hub/level travel remains black-only. The player/head camera must never be artificially translated or rotated.

This is a production-presentation refinement, not a new room, gameplay space or loading mechanic. Startup still advances only from the existing real Photon/Hub/rig/avatar readiness state.

## Exact branch implementation

1. Keep the authored `Loading` canvas as the full-FOV black safety/transition layer. It remains responsible for opaque stereo coverage, errors and the final fade into the Hub.
2. Reserve project layer **`LoadingPresentation`** for cold-start presentation. On startup, move the Loading canvas hierarchy onto that layer, mask the persistent camera to that layer and clear to black. This prevents additively loaded Hub UI/world content from appearing behind the workstation.
3. Create one local-only `SecurityWorkstationVignette` after the persistent XR camera becomes available. Place it once from the camera's initial horizontal heading, approximately **1.95 m** in front of the player, without parenting it to the head.
4. Build a deliberately simple low-poly workstation from runtime primitives on `LoadingPresentation`: desk surface/pedestals, monitor body/stand/base, keyboard, mug, badge/card prop and clipboard/paper. Disable and remove all primitive colliders so the vignette cannot affect Gorilla locomotion or physics.
5. Render the existing green security boot on a **world-space monitor canvas** inside the physical monitor. The fitted canvas uses scale `0.00082`, making the 1080 × 820 UI approximately 0.886 × 0.672 m inside the 1.08 × 0.70 m monitor inset, leaving a deliberate dark margin instead of clipping the CRT face.
6. Preserve the current real readiness rows, retry/error text, quiet relay ticks, CRT noise, scanlines, interference sweep and `ACCESS GRANTED` dwell. Do not introduce a fake percentage or simulated boot stages.
7. Add readable optional flavor notes. First-pass text is `CAM 04 / STILL DEAD`, `VENT B / AGAIN?`, and `IF THEY GET OUT / I QUIT.` The first two are fitted to the physical bezel; the third lies on the desk/clipboard area. These are non-progression-critical Easter eggs and may be revised later.
8. During the existing terminal fade, raise the authored black backdrop as the monitor content fades. Once covered, hide the workstation. After Hub activation, temporarily render the saved Hub camera mask plus `LoadingPresentation`, fade the black overlay away, then restore the exact original camera culling mask, clear flags and background.
9. If readiness/session state fails during entry, reapply the isolated startup mask and restore the workstation/error presentation instead of leaving a half-faded desk or leaking Hub content.
10. Normal sector travel constructs **no workstation, monitor canvas, notes, CRT objects or boot audio**. Existing travel remains black-only.

## Implemented on this branch

- `Assets/Scripts/Loading/SecurityWorkstationVignette.cs` owns the local visual set only.
- `SecurityBootPresentation` creates the workstation only on cold startup, mounts the boot UI on its world-space monitor, owns CRT/static treatment and coordinates the black-cover handoff.
- `LoadingFlow` now asks the presentation to prepare a combined Hub + black-overlay camera mask for the final reveal, then restores the camera exactly after the fade.
- `ProjectSettings/TagManager.asset` reserves `LoadingPresentation` so startup rendering is isolated from the ordinary `UI` layer.
- `Tools/validate_security_boot.py` and `Tools/validate_launch_presentation.py` protect startup-only construction, dedicated-layer isolation, world-space monitor UI, fitted monitor scale, bezel note placement, collider removal, no tracked-camera writes, no Photon/readiness writes and unchanged black-only travel.

## Source validation

**Validated source only:** workstation implementation head `2b2bf1a41c637e0108c8b5b4ff8054070543f43f` passed Source Integrity run `34803476268` after reconciliation with current `main`. The full repository suite passed the Level 1, PR #15, security-boot, threat-feedback and launch/workstation contracts together. Subsequent changes on the branch are documentation-only cleanup/recording unless otherwise noted; the PR's latest Source Integrity run remains the final source gate before merge. This is source/tooling evidence only; it does not prove Unity import/compile or visual comfort.

## Art / lore boundary

**Implemented presentation uses temporary low-poly primitive geometry.** This pass is intentionally about composition, scale, readability, mood and technical ownership rather than final prop art. A later Blender asset pass can replace the runtime geometry without changing startup logic.

The note text is **flavor/Easter-egg copy**, not confirmed progression lore. Treat it as editable dressing until Greg separately approves exact wording. Do not make a note carry required credentials, codes, objective instructions or a definitive claim about the Crawler's origin.

## Validation plan

### Play Mode

- Start from `Assets/Scenes/Bootstrap.unity` in Unity 2022.3.55f1.
- Workstation appears in front of the initial player view without following head rotation.
- Physical monitor reads at comfortable distance/scale; green boot fits inside the screen with a dark margin and no clipping.
- Desk/monitor silhouette is visible but surrounding environment remains black; additively loaded Hub UI does not appear behind it.
- Static/scanlines/interference remain subtle and text stays readable.
- Notes stay attached to the bezel/desk, are discoverable when looking and do not compete with startup status.
- `ACCESS GRANTED` holds briefly, workstation becomes fully covered by black before hiding, then Hub reveals cleanly.
- Failure/retry restores the workstation, isolated camera mask and error text correctly.
- Hub/level travel remains black-only and never constructs the vignette.

### Headset / Quest later

- Both-eye/peripheral black coverage, head turning/recenter comfort, physical monitor distance, note legibility, no world-stuck discomfort, no compositor/world flash, native system-splash handoff, and Quest frame/memory cost remain pending until device setup is available.
