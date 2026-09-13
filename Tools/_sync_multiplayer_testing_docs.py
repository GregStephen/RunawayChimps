#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / "docs/design-and-lore.md"
PLAN = ROOT / "docs/repository-improvement-plan.md"

DESIGN_MARKER = "### Multiplayer development validation workflow"
PLAN_MARKER = "## September 13 local multiplayer development testing workflow"

DESIGN_SECTION = '''### Multiplayer development validation workflow

**Confirmed development process, tooling partly planned:** when a second headset or development machine is unavailable, use one Unity 2022.3.55f1 Editor client plus one Windows Development Build client on the same PC, and force both into the same explicit private Photon room code. This is the standard development topology for proving player-specific state such as threat targeting, personal objectives, sector separation, and controller handoff. It does not replace final two-headset/Quest validation.

The existing private-room path is already present, but the no-second-headset convenience layer is **planned, not implemented**: a development-only launcher should give each local instance a distinct development identity, auto-join a named test room, add desktop controls for Client B, and show a read-only multiplayer/threat HUD. The harness must not fake Photon ActorNumbers, authority, target selection, zone state, travel, or production authentication. See [Multiplayer development testing](multiplayer-development-testing.md) for the maintained procedure and test matrix.

'''

PLAN_SECTION = '''## September 13 local multiplayer development testing workflow

**Confirmed development workflow:** standardize one-PC multiplayer testing on **Unity Editor Client A + Windows Development Build Client B**, both running the same commit and explicitly joining the same private Photon room code. The current Hub computer and `RoomSwitchService.JoinPrivateRoom(...)` already provide the same-room path. This workflow is intended to make routine two-actor regression testing possible without requiring two headsets for every code pass.

**Planned tooling, not implemented:** add a development-only local multiplayer harness that builds/launches Client B, assigns separate throwaway development identities, can auto-join a named private test room, provides keyboard/mouse movement and repeatable test setup for the non-headset client, and exposes a read-only debug HUD for room/ActorNumber/sector/zone/monster-controller/target/threat state. Keep the helper behind Editor/Development Build guards and never fake the production Photon/gameplay state being tested.

**Validation boundary:** one-PC Editor + Development Build evidence may be recorded as local two-client validation for the exact behavior exercised. It does not validate headset tracking/input, Quest performance, platform authentication, or two-headset comfort/interaction. The detailed maintained procedure and the threat-feedback A/B matrix live in [Multiplayer development testing](multiplayer-development-testing.md).

'''


def insert_before(text, marker, section, anchor):
    if marker in text:
        return text
    if anchor not in text:
        raise SystemExit(f"missing anchor: {anchor}")
    return text.replace(anchor, section + anchor, 1)


def sync_protocol(text):
    text = text.replace("SectorMonsterSync protocol v2", "SectorMonsterSync protocol v4")
    text = text.replace("SectorMonsterSync protocol v3", "SectorMonsterSync protocol v4")
    return text


def main():
    design = DESIGN.read_text(encoding="utf-8-sig")
    design = insert_before(
        design,
        DESIGN_MARKER,
        DESIGN_SECTION,
        "## September 12 PR #15 merge-review hardening",
    )
    design = sync_protocol(design)
    record_header = "| Recorded | Decision or correction | Status |\n| --- | --- | --- |\n"
    record_row = (
        "| 2026-09-13 | Standardize local multiplayer development checks on one Unity Editor client plus one Windows Development Build client using the same explicit private Photon room. Plan a dev-only second-client launcher/identity/desktop-control/HUD layer so routine two-player testing does not require two headsets. | Confirmed development workflow; detailed reference in `docs/multiplayer-development-testing.md`. Convenience harness remains planned/not implemented; final two-headset/Quest validation still required. |\n"
    )
    if record_row not in design:
        if record_header not in design:
            raise SystemExit("missing decision table")
        design = design.replace(record_header, record_header + record_row, 1)
    DESIGN.write_text(design, encoding="utf-8")

    plan = PLAN.read_text(encoding="utf-8-sig")
    plan = insert_before(
        plan,
        PLAN_MARKER,
        PLAN_SECTION,
        "## September 13 global player-specific threat feedback implementation",
    )
    plan = sync_protocol(plan)
    PLAN.write_text(plan, encoding="utf-8")


if __name__ == "__main__":
    main()
