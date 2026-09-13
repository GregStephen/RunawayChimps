#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / "docs/design-and-lore.md"
PLAN = ROOT / "docs/repository-improvement-plan.md"


def update(path, marker, section, replacements=()):
    text = path.read_text(encoding="utf-8-sig")
    before = text
    for old, new in replacements:
        if old in text:
            text = text.replace(old, new)
    if marker not in text:
        insert_before = "## Decision and correction record\n" if path == DESIGN else "## September 12 automated source validation\n"
        if insert_before in text:
            text = text.replace(insert_before, section + "\n" + insert_before, 1)
        else:
            text += "\n\n" + section
    if text != before:
        path.write_text(text, encoding="utf-8")
        return True
    return False


def main():
    design_section = """## September 12 Crawler animation-root drift correction

**Confirmed failed runtime result from `monsterOff.mp4`:** Zombie Crawl still does not remain physically colocated with the Crawler gameplay parent. In the recorded test, the gameplay/visual parent remains on the vent route while the skinned Zombie travels far away inside its own hierarchy, periodically returning/snapping near the parent before drifting again. Treat this as a failed Crawler presentation test; it is not ordinary rigid-tail corner clipping.

**Source diagnosis / implemented correction on PR #15:** `CrawlerBodyPathFollower` previously aligned the imported Zombie to the gameplay root only once during `Configure()`. Zombie Crawl is a Generic Mixamo rig and the standalone crawl clip can animate translation on `mixamorig:Hips`; `Animator.applyRootMotion = false` prevents Animator root-motion application to the GameObject but does not guarantee that Generic bone-position curves become an in-place clip. The active ownership rule is now explicit: **the Crawler gameplay/NavMesh/Photon root owns all monster travel translation. The crawl animation owns pose/limb motion, but no translational authority.** After the authored Animator and whole-visual heading evaluate, `CrawlerBodyPathFollower` at execution order 300 compares `mixamorig:Hips` with a calibrated point stored in `CrawlerVisualAnchor` space and counter-translates the complete Zombie hierarchy so the animated Hips stays at that parent-relative point. It never writes Hips, spine, chest, or other bone transforms. Hand contact IK still runs afterward at order 350.

**Pending validation:** Unity 2022.3.55f1/headset must prove the Zombie remains colocated with the selected Crawler parent through several complete crawl cycles while stationary, patrolling, chasing, turning, and after Photon/controller handoff. The body may still have the separately documented rigid long-body corner-fit limitation, but it must no longer walk/crawl meters away from the gameplay root or snap back on animation-loop boundaries. A one-time warning that the crawl animation attempted substantial internal root translation is diagnostic evidence that the new in-place compensation is doing work; repeated visible separation remains a failed test.
"""
    design_replacements = [
        (
            "| Confirmed | Level 1 Crawler uses one gameplay root for navigation/capture/audio/Photon and one Zombie Crawl visual/Animator rig. Do not keep the obsolete MiniGamesKid renderer/Animator/armature live in parallel. Core torso/spine/hips remain Animator-owned with no post-Animator path deformation; bounded arm-only environmental IK is the explicit exception for hand-to-vent contact. The complete Zombie visual must face gameplay travel; the current safe baseline may smooth whole-visual heading from recent root path to prevent violent turn whipping. Long-body corner deformation is still desired, but both direct bone-transform attempts failed and are superseded; design a rig-safe solution only after this Animator-owned baseline passes. |",
            "| Confirmed | Level 1 Crawler uses one gameplay root for navigation/capture/audio/Photon and one Zombie Crawl visual/Animator rig. Do not keep the obsolete MiniGamesKid renderer/Animator/armature live in parallel. The gameplay/NavMesh/Photon root owns all monster travel translation; the Generic crawl animation is presentation-only and must be kept in-place by rigid whole-visual compensation if `mixamorig:Hips` carries translation. Core torso/spine/hips remain Animator-owned with no post-Animator path deformation; bounded arm-only environmental IK is the explicit exception for hand-to-vent contact. The complete Zombie visual must face gameplay travel. Long-body corner deformation is still desired, but both direct bone-transform attempts failed and are superseded; design a rig-safe solution only after this baseline passes. |"
        )
    ]

    plan_section = """## September 12 Crawler visual-parent drift video follow-up

**Confirmed runtime failure:** Greg's `monsterOff.mp4` shows a different failure from the known rigid-body corner clipping. The selected Crawler gameplay/visual parent remains on the vent route while the Zombie skinned presentation moves far away from that parent and periodically snaps back near it. This makes the monster appear outside the vents even when the authoritative navigation root is still in the correct area.

**Source finding:** `CrawlerBodyPathFollower` had become a one-time rigid alignment utility after the failed torso-deformation experiments. It corrected the FBX/chest pivot and floor during `Configure()` but performed no continuous alignment afterward. The Zombie FBX imports as a Generic rig, while the separate `mixamo.com.anim` clip drives the Zombie Animator. `applyRootMotion=false` is insufficient as an ownership boundary when translation exists as Generic skeleton/bone curves: an animated `mixamorig:Hips` position can move the skinned hierarchy relative to `CrawlerVisualAnchor` even though the NavMesh root itself never left the vent. The periodic return seen in the video is consistent with the animated translation looping/resetting rather than with normal NavMesh movement alone.

**Implemented on `fix/runtime-keycard-crawler-loading`:** `CrawlerBodyPathFollower` now runs at execution order 300, after whole-visual heading (250) and before hand contact IK (350). After initial pivot/floor calibration it records `mixamorig:Hips` in the parent `CrawlerVisualAnchor` coordinate space. Every rendered frame it computes the expected Hips world point from that stable parent-space reference and translates the **complete Zombie visual root** by the difference. This makes the crawl presentation in-place while preserving every authored bone transform and segment length. It never sets Hips/chest/spine position or rotation. `Tools/validate_pr15_review_hardening.py` now protects this gameplay-owned translation contract.

**Validation boundary:** this source correction should eliminate animation-driven visual-parent separation, but it is not yet runtime-proven. Test multiple full animation loops in a straight vent before judging turns. Then repeat patrol, chase, the same 90-degree corner, capture/respawn, and two-client Photon controller handoff. If the parent itself jumps, continue using the existing `Crawler gameplay root moved ... m in one frame` diagnostic; if the parent stays put but the mesh again drifts away, the in-place compensation has failed and the remaining animation hierarchy must be inspected directly. Long-body corner fitting remains a separate unresolved rig-safe problem.
"""

    d = update(DESIGN, "## September 12 Crawler animation-root drift correction", design_section, design_replacements)
    p = update(PLAN, "## September 12 Crawler visual-parent drift video follow-up", plan_section)
    print(f"design_changed={d} plan_changed={p}")


if __name__ == "__main__":
    main()
