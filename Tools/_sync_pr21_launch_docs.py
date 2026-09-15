from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / "docs/design-and-lore.md"
PLAN = ROOT / "docs/repository-improvement-plan.md"

DESIGN_MARKER = "## September 14 launch presentation correction — restored distant green terminal"
PLAN_MARKER = "## September 14 PR #21 visual correction — restore distant green terminal"

DESIGN_SECTION = r'''

## September 14 launch presentation correction — restored distant green terminal

**Confirmed correction / supersedes the workstation direction above:** Greg rejected the physical security workstation/desk presentation and explicitly asked for the **old green security screen back**, positioned roughly **twice as far away**. The selected cold-start Unity presentation is again the flat green facility security terminal in black space. The desk, monitor shell, stand, keyboard, mug, badge/card prop, clipboard and flavor notes are no longer part of the active launch design.

**Confirmed placement:** keep the terminal centered around eye height on the initial horizontal viewing direction and place it at approximately **3.9 m**, exactly twice the former 1.95 m workstation-vignette distance. The tracked camera is never translated or rotated. The terminal may follow hidden `XROrigin` root relocation during startup so rig snapping does not leave it behind, but ordinary head look and room-scale motion must not head-lock the panel.

**Still confirmed:** the terminal reports only real Photon/Hub/rig/avatar readiness; successful Hub/Level 1/Level 2 travel remains black-only; failures retain recovery feedback; CRT noise/scanlines/interference remain restrained; the Meta/Quest system splash remains a separate pre-first-frame layer; and the Hub slot/session/reconnect/reveal hardening on PR #21 remains in scope. This visual correction does not undo those reliability changes.

**Implemented on `feature/launch-presentation-polish` / PR #21, pending runtime validation:** the legacy-named `SecurityWorkstationVignette` no longer constructs physical workstation primitives or prop materials. It now creates only the world-space terminal canvas, using the prior monitor canvas physical scale and a 3.90 m startup anchor. The obsolete workstation material resource was removed. Launch source validation was updated to reject reintroduction of the desk/prop presentation while preserving the existing render-isolation, readiness, final-reveal and multiplayer/session contracts.

**Pending validation:** Unity **2022.3.55f1** import/compile; Play Mode distance/readability/eye-height review; head-turn and hidden-origin-relocation behavior; retry/error restoration; `ACCESS GRANTED` -> full black -> terminal absent -> Hub reveal; two-client Photon slot/session/reconnect behavior; authored Hub-slot clearance; and Quest compositor splash, stereo/peripheral coverage, recenter/pause-resume, comfort and performance. Source checks do not establish those runtime/device results.
'''

PLAN_SECTION = r'''

## September 14 PR #21 visual correction — restore distant green terminal

**Confirmed correction:** the physical workstation/desk vignette previously implemented on PR #21 is no longer the selected launch presentation. Greg explicitly asked to restore the **old flat green security terminal** and move it roughly twice as far away. Treat the workstation-specific sections above as superseded design history, not the active target.

**Implemented source on `feature/launch-presentation-polish`:** `SecurityWorkstationVignette` remains the legacy class/file name but now owns only a world-space terminal anchor. It creates no primitive desk/monitor/keyboard/mug/badge/clipboard geometry and no flavor notes. The existing 1080 x 820 terminal canvas keeps its prior physical scale and is placed **3.90 m** from the initial horizontal camera heading, twice the former 1.95 m workstation distance. The obsolete `Resources/LaunchPresentation/WorkstationBase` material dependency was removed.

**Preserved implementation:** PR #21 still keeps the dedicated `LoadingPresentation` isolation layer, real startup readiness/error/retry behavior, bounded CRT treatment, final-reveal retirement guard, Meta/Quest system-splash validation, demand-driven 10-slot Photon Hub allocation, room-switch/reconnect resnap behavior, authored `HubSpawnSlots.prefab`, and the startup material-allocation cleanup. Successful sector travel remains black-only.

**Validation boundary:** rerun Source Integrity on the corrected head, then validate Unity **2022.3.55f1** import/compile and Play Mode presentation before merge. Runtime acceptance now focuses on the 3.9 m flat terminal's readability/comfort, no head-locking, clean black cover and reveal, retry recovery, plus the existing two-client Photon room/slot/reconnect and Quest/headset checks. Do not carry forward workstation-prop or note-readability tests; those presentation elements are intentionally removed.
'''


def update(path: Path, marker: str, section: str) -> bool:
    text = path.read_text(encoding="utf-8-sig")
    updated = text.replace("Last updated: 2026-09-13", "Last updated: 2026-09-14", 1)
    if marker not in updated:
        updated = updated.rstrip() + section + "\n"
    if updated == text:
        return False
    path.write_text(updated, encoding="utf-8", newline="\n")
    return True


changed = False
changed |= update(DESIGN, DESIGN_MARKER, DESIGN_SECTION)
changed |= update(PLAN, PLAN_MARKER, PLAN_SECTION)
print("Updated maintained PR21 launch docs." if changed else "Maintained PR21 launch docs already current.")
