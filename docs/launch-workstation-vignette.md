# Launch workstation vignette

Date: 2026-09-13. Branch: `feature/launch-presentation-polish`. Unity **2022.3.55f1**, Photon PUN.

## Confirmed direction

Greg approved taking the existing green facility security-system boot one step further by presenting it on a **physical security workstation monitor** instead of leaving the boot as a flat floating panel. The workstation is startup-only. Successful Hub/level travel remains black-only. The player/head camera must never be artificially translated or rotated.

This is a production-presentation refinement, not a new room, gameplay space, or loading mechanic. Startup still advances only from the existing real Photon/Hub/rig/avatar readiness state.

## Exact branch implementation

1. Keep the authored `Loading` canvas as the full-FOV black safety/transition layer. It remains responsible for opaque stereo coverage, errors, and the final fade into the Hub.
2. On cold startup only, create a local-only `SecurityWorkstationVignette` after the persistent XR camera becomes available. Place it once from the camera's initial horizontal heading, approximately two meters in front of the player, without parenting it to the head.
3. Build a deliberately small low-poly workstation from runtime primitives on the Loading/UI-only camera layer: desk surface/pedestals, monitor body/stand/base, keyboard, mug, badge/card prop, clipboard/paper, and a few dimly colored details. Disable/remove all primitive colliders so the vignette cannot affect Gorilla locomotion or physics.
4. Render the existing green security boot on a **world-space monitor canvas** mounted inside the physical monitor. Preserve the current real readiness rows, retry/error text, quiet relay ticks, CRT noise, scanlines, interference sweep, and `ACCESS GRANTED` dwell. Do not introduce a fake percentage or simulated boot stages.
5. Add a few readable optional flavor notes around the workstation. Initial text is deliberately non-progression-critical and may be revised later; it must not create a required puzzle solution or contradict confirmed level lore. First-pass notes: `CAM 04 / STILL DEAD`, `VENT B / AGAIN?`, and `IF THEY GET OUT / I QUIT.`
6. Keep the surrounding world black. The persistent startup camera culls to the Loading/UI layer only and clears black, so partially loaded Hub geometry never becomes visible behind the desk.
7. During the existing terminal fade, raise the authored black backdrop as the terminal fades out. Once the screen is fully covered, hide the workstation, activate the Hub, restore the tracked camera's original render settings, and fade black into the Hub. If readiness/session state fails during entry, restore the workstation and readable retry screen instead of leaving a half-faded desk.
8. Normal sector travel constructs **no workstation, monitor canvas, notes, CRT objects, or boot audio**. Existing travel remains black-only.

## Art / lore boundary

**Implemented presentation may use temporary low-poly primitive geometry.** This first pass is intentionally about composition, scale, readability, and mood rather than final prop art. A later Blender asset pass can replace the runtime geometry without changing startup logic.

The note text is **flavor/Easter-egg copy**, not confirmed progression lore. It should be treated as editable dressing until Greg separately approves exact wording. Do not make a note carry required credentials, codes, objective instructions, or a definitive claim about the Crawler's origin.

## Validation plan

### Source / repository

- Unity metadata/GUID integrity and C# syntax pass.
- Existing Level 1, PR #15, security-boot, threat-feedback, and launch-presentation source contracts still pass.
- Add workstation-specific guards: startup-only construction, world-space monitor canvas, no camera-transform writes, no RenderTexture, no Photon/gameplay writes, colliders disabled/removed, and black-travel behavior unchanged.

### Play Mode

- Start from `Assets/Scenes/Bootstrap.unity` in Unity 2022.3.55f1.
- Workstation appears in front of the initial player view without following head rotation.
- Physical monitor reads at comfortable distance/scale; green boot fits the screen without clipping.
- Desk/monitor silhouette is visible but surrounding environment remains black.
- Static/scanlines/interference remain subtle and text stays readable.
- Notes are discoverable when looking but do not compete with startup status.
- `ACCESS GRANTED` holds briefly, desk is covered by black before disappearing, then Hub reveals cleanly.
- Failure/retry restores the workstation and error text correctly.
- Hub/level travel remains black-only and never constructs the vignette.

### Headset / Quest later

- Both-eye/peripheral black coverage, head turning/recenter comfort, physical monitor distance, note legibility, no world-stuck discomfort, no compositor/world flash, native system-splash handoff, and Quest frame/memory cost remain pending until device setup is available.
