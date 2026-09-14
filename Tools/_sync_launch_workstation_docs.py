#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

RECORDS = {
    "docs/design-and-lore.md": '''## September 13 physical launch workstation refinement

**Confirmed launch presentation:** Greg approved presenting the existing facility security-system boot on a **physical security workstation monitor and desk vignette** rather than leaving it as a flat floating panel. This is startup-only presentation, not a new explorable room. The player/head camera remains fully player-controlled and is never artificially translated or rotated. Successful Hub/level travel remains black-only and does not replay the workstation.

**Implemented on `feature/launch-presentation-polish` / PR #21, pending Unity visual validation:** cold startup now creates a local-only `SecurityWorkstationVignette` approximately 1.95 m ahead of the initial horizontal head direction. The temporary low-poly set includes a desk, monitor/stand, keyboard, mug, security badge/card prop and clipboard; primitive colliders are disabled and removed. The real green boot is mounted on a fitted world-space monitor canvas and retains its real Photon/Hub/rig/avatar readiness rows, retry/error handling, relay ticks, CRT static/scanlines/interference and `ACCESS GRANTED` dwell. The current optional flavor notes are `CAM 04 / STILL DEAD`, `VENT B / AGAIN?`, and `IF THEY GET OUT / I QUIT.`; those notes are editable Easter-egg dressing, not puzzle requirements or locked progression lore.

**Startup isolation correction:** the workstation and black Loading cover use the dedicated `LoadingPresentation` layer instead of the ordinary project `UI` layer, preventing additively loaded Hub UI from leaking behind the vignette. During Hub entry, black fully covers and hides the workstation, the camera temporarily renders its saved Hub mask plus `LoadingPresentation` while black fades away, then its exact original culling mask, clear flags and background are restored. An interrupted reveal reapplies the isolated startup mask and restores the workstation/error presentation.

**Validated source only:** implementation commit `2b2bf1a41c637e0108c8b5b4ff8054070543f43f` passed Source Integrity run `34803476268` after reconciliation with current `main`, including Unity metadata/GUID integrity, C# syntax/references, Level 1 and PR #15 contracts, security-boot contracts, PR #19 threat-feedback contracts, and the new workstation/launch-presentation guards. **Pending validation:** Unity 2022.3.55f1 import/compile; Play Mode composition, scale, text/readability, black-cover handoff and retry; headset stereo/comfort; Photon regression; Quest compositor splash handoff and performance. Source validation does not establish visual or device behavior.''',
    "docs/repository-improvement-plan.md": '''## September 13 physical launch workstation production pass

**Confirmed UX direction:** PR #21 now treats the green security boot as a physical, startup-only security workstation vignette. The workstation is not gameplay geometry, is not an explorable room, never moves the tracked camera, and must not appear during ordinary Hub/level travel. The first-pass notes are optional flavor only and must not become hidden progression requirements without a separate design decision.

**Implemented source:** `SecurityWorkstationVignette` builds visual-only low-poly desk/monitor props with primitive colliders disabled/removed, places the set once from the initial horizontal XR-camera heading, and exposes a world-space monitor canvas to `SecurityBootPresentation`. The monitor UI is fitted inside its physical inset with a dark border, while two small notes are fitted to the bezel and one rests on the desk. `SecurityBootPresentation` preserves the existing real readiness/error/retry/CRT behavior and coordinates an opaque-black handoff before the workstation is hidden.

**Render-ownership hardening:** `ProjectSettings/TagManager.asset` reserves `LoadingPresentation`. Cold startup moves the Loading canvas hierarchy and workstation to that isolated layer and masks the persistent camera to it, so additively loaded Hub UI cannot bleed through. Hub reveal temporarily combines the saved camera mask with `LoadingPresentation` while the black overlay fades, then restores the camera exactly. Travel still returns before workstation/boot construction and remains black-only.

**Validated source only:** commit `2b2bf1a41c637e0108c8b5b4ff8054070543f43f` passed Source Integrity run `34803476268` on the current-main reconciliation. The security-boot and launch validators now protect startup-only construction, dedicated-layer isolation, world-space monitor ownership, fitted canvas/bezel composition, collider removal, no tracked-camera writes, no Photon/readiness writes, and unchanged black travel. **Still pending:** Unity 2022.3.55f1 import/compile and Play Mode visual review; failure/retry/reveal behavior; headset stereo/comfort; Photon startup/travel regression; native Quest system-splash handoff and Quest performance.'''
}

for relative, block in RECORDS.items():
    path = ROOT / relative
    current = path.read_text(encoding="utf-8-sig")
    heading = block.splitlines()[0]
    if heading not in current:
        path.write_text(current.rstrip() + "\n\n" + block.strip() + "\n", encoding="utf-8")
