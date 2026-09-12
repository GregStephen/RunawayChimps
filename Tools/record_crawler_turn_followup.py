#!/usr/bin/env python3
"""Record the latest Crawler direction/turn runtime feedback and source follow-up."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / "docs/design-and-lore.md"
PLAN = ROOT / "docs/repository-improvement-plan.md"
VALIDATOR = ROOT / "Tools/validate_level1_contracts.py"


def insert_before(text: str, marker: str, block: str, label: str) -> str:
    first = block.strip().splitlines()[0]
    if first in text:
        return text
    if marker not in text:
        raise RuntimeError(f"{label}: marker not found: {marker}")
    return text.replace(marker, block.rstrip() + "\n\n" + marker, 1)


def update_design(text: str) -> str:
    status = """**September 12 Crawler direction/turn retest:** after the legacy visual-rig cleanup, Greg reports the first monster is working better, but the Zombie now clearly travels **backwards**, can be thrown/swing outside the vents on turns, and can sometimes look as though it jumps from one position to another. This is a failed runtime presentation result, not a resolved Crawler check. Source inspection matches the symptoms: `CrawlerVisualController` still creates the complete Zombie visual with the old 180-degree yaw, while the safe no-bone-writes baseline makes the long creature rotate as one rigid object around the gameplay path. Branch `fix/runtime-keycard-crawler-loading` now adds `CrawlerVisualHeadingStabilizer`. It corrects the complete visual anchor to the gameplay travel direction, derives heading from roughly 0.9 m of recent gameplay-root path, limits visual turning to 150 degrees/second, and resets the heading history rather than whipping the long model if the authoritative root moves more than 1.25 m in one frame. It does **not** write any Zombie bone position or rotation. This is implemented source behavior with Source validation passing; Unity/headset must still prove that the monster faces forward, no longer makes violent turn swings, and reveal whether any remaining jump is the gameplay/NavMesh/Photon root itself rather than only the visual heading. A rigid long model still cannot perfectly occupy both legs of a tight 90-degree duct, so true body bending remains pending a separate rig-safe solution."""
    text = insert_before(text, "## Decision and correction record", status, "design status")

    row_marker = "| 2026-09-12 | Level 1 Crawler should have one authoritative gameplay root plus the Zombie Crawl visual/Animator."
    new_row = "| 2026-09-12 | Zombie Crawl must face the same direction the Crawler actually travels, and sharp gameplay-root turns must not whip the complete long visual across the vent. Until a rig-safe bend exists, stabilize only the complete visual anchor from recent path heading; do not reintroduce direct bone transforms. | Confirmed from Greg's runtime retest after legacy-rig cleanup. PR #15 source-adds recent-path whole-visual heading, a bounded 150°/s turn rate and discontinuity reset. Source validation passes; headset straight/turn/jump diagnosis pending. |\n"
    if new_row.strip() not in text:
        if row_marker not in text:
            raise RuntimeError("design decision row marker not found")
        text = text.replace(row_marker, new_row + row_marker, 1)

    old_current = "| Confirmed | Level 1 Crawler uses one gameplay root for navigation/capture/audio/Photon and one Zombie Crawl visual/Animator rig. Do not keep the obsolete MiniGamesKid renderer/Animator/armature live in parallel, and do not write post-Animator Zombie bone positions or rotations. Long-body corner deformation is still desired, but both direct-transform attempts failed and are superseded; design a rig-safe solution only after the clean Animator-owned baseline passes. |"
    new_current = "| Confirmed | Level 1 Crawler uses one gameplay root for navigation/capture/audio/Photon and one Zombie Crawl visual/Animator rig. Do not keep the obsolete MiniGamesKid renderer/Animator/armature live in parallel, and do not write post-Animator Zombie bone positions or rotations. The complete Zombie visual must face gameplay travel; the current safe baseline may smooth whole-visual heading from recent root path to prevent violent turn whipping. Long-body corner deformation is still desired, but both direct bone-transform attempts failed and are superseded; design a rig-safe solution only after this Animator-owned baseline passes. |"
    if old_current in text:
        text = text.replace(old_current, new_current, 1)
    elif new_current not in text:
        raise RuntimeError("design current Crawler decision text not found")
    return text


def update_plan(text: str) -> str:
    block = """## September 12 Crawler direction and turn-continuity retest

**Confirmed runtime feedback:** after the legacy MiniGamesKid visual cleanup, Greg reports the Crawler is materially better, but Zombie Crawl moves backwards relative to its travel direction, can swing/be thrown outside the vent on turns, and can sometimes appear to jump from one spot to another. Treat all three as open runtime failures. The cleanup itself remains useful; do not restore the old armature to address them.

**Source finding:** the visual controller still initializes `CrawlerVisualAnchor` with a 180-degree yaw. Greg's retest provides direct evidence that this offset is wrong for the current Zombie import. Separately, the deliberately safe post-cleanup baseline keeps the whole long Zombie rigid so the Animator can own its skeleton. Rotating that long rigid visual directly with a sharp NavMesh-root turn can sweep most of the body across a wall even when the gameplay root remains on the vent NavMesh. A discontinuous gameplay-root/network correction can exaggerate the same effect and look like a visual teleport.

**Implemented on `fix/runtime-keycard-crawler-loading`:** new `CrawlerVisualHeadingStabilizer` is installed only for Level 1 Crawler gameplay roots. Once `CrawlerVisualController` has attached Zombie Crawl, it overrides the complete visual anchor's old backwards heading without changing the imported skeleton. It records recent gameplay-root X/Z motion, derives orientation from a 0.9 m lookback, and rotates the entire visual toward that heading at at most 150 degrees/second. A >=1.25 m single-frame gameplay-root displacement resets the history and snaps the visual heading to the authoritative root instead of sweeping the long body through the environment; it also logs the first such discontinuity so a remaining true root jump can be distinguished from visual rotation. No Zombie bone positions or rotations are written.

**Validation boundary:** this is a turn-continuity baseline, not the final corner-bending solution. A rigid long mesh cannot perfectly occupy two perpendicular narrow duct legs at once. In Unity/headset first verify forward orientation in a straight, then patrol and chase through the same 90-degree turn repeatedly. The body should rotate progressively rather than instantly whipping across the vent. Watch the Console for the gameplay-root discontinuity warning. If the warning coincides with the visible jump, inspect NavMesh/controller handoff/Photon root state next; if no warning occurs but the rigid tail still clips at the corner, that remaining issue belongs to the future rig-safe bend/IK solution. Repeat on the controlling client and then on a second Photon client before marking turn behavior resolved."""
    text = insert_before(text, "## September 12 integrated hand/Crawler retest and legacy hierarchy cleanup", block, "plan turn section")
    return text


def update_validator(text: str) -> str:
    marker = '    legacy_cleaner_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerLegacyVisualCleaner.cs"\n'
    block = '''    heading_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerVisualHeadingStabilizer.cs"
    heading = require(errors, heading_path, [
        'LevelOneSceneName = "Level1_Containment"',
        "visualController.VisualRoot",
        "headingLookbackDistance = 0.9f",
        "maximumTurnDegreesPerSecond = 150f",
        "discontinuityDistance = 1.25f",
        "Quaternion.LookRotation(forward.normalized, Vector3.up)",
        "Quaternion.RotateTowards(",
        "ResetHeadingHistory(current, snapToGameplayRotation: true)",
        "No Zombie bone position/rotation is ever modified here.",
    ])
    positive_defaults(errors, heading_path, heading, [
        "headingLookbackDistance", "maximumTurnDegreesPerSecond", "sampleSpacing", "historyDistance", "discontinuityDistance"
    ])

'''
    if 'heading_path = ROOT / "Assets/Scripts/MonsterScripts/CrawlerVisualHeadingStabilizer.cs"' not in text:
        if marker not in text:
            raise RuntimeError("validator insertion marker not found")
        text = text.replace(marker, block + marker, 1)

    old_pass = 'PASS: Level 1/runtime hand visual contact, Animator-owned Crawler skeleton, legacy visual-rig cleanup, keycard recovery, loading coverage, headlamp, safe-zone, capture, and floor-recovery source contracts.'
    new_pass = 'PASS: Level 1/runtime hand visual contact, Animator-owned Crawler skeleton, legacy visual-rig cleanup, Crawler forward/turn continuity, keycard recovery, loading coverage, headlamp, safe-zone, capture, and floor-recovery source contracts.'
    if old_pass in text:
        text = text.replace(old_pass, new_pass, 1)
    elif new_pass not in text:
        raise RuntimeError("validator pass message not found")
    return text


def main() -> None:
    design = DESIGN.read_text(encoding="utf-8")
    plan = PLAN.read_text(encoding="utf-8")
    validator = VALIDATOR.read_text(encoding="utf-8")

    DESIGN.write_text(update_design(design), encoding="utf-8")
    PLAN.write_text(update_plan(plan), encoding="utf-8")
    VALIDATOR.write_text(update_validator(validator), encoding="utf-8")


if __name__ == "__main__":
    main()
