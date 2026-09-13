#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
design_path = ROOT / "docs/design-and-lore.md"
plan_path = ROOT / "docs/repository-improvement-plan.md"


def replace(path, replacements):
    text = path.read_text(encoding="utf-8-sig")
    before = text
    for old, new in replacements:
        if old in text:
            text = text.replace(old, new)
    if text != before:
        path.write_text(text, encoding="utf-8")
        return True
    return False


def main():
    design_changed = replace(design_path, [
        (
            "**Latest Crawler visual correction:** Greg's runtime test showed the previous body-path implementation can make Zombie Crawl's torso visibly squished/compressed while moving.",
            "**Superseded intermediate Crawler visual correction:** Greg's runtime test showed the previous body-path implementation can make Zombie Crawl's torso visibly squished/compressed while moving. This rotation-only follow-up is retained as history only; the later headset result and PR #15 merge-review hardening below are the active source direction."
        ),
        (
            "Pending validation: first prove `mixamo.com.anim` visibly deforms Zombie Crawl and that the persistent binding diagnostic does not fire.",
            "**Superseded validation target:** the rotation-only torso checks below belonged to the intermediate approach that later failed in headset. Do not use them as the active acceptance criteria. At that stage the next step was to prove `mixamo.com.anim` visibly deformed Zombie Crawl and that the persistent binding diagnostic did not fire."
        ),
        (
            "**Pending baseline validation:** open/import the branch in Unity 2022.3.55f1 and confirm the Level 1 hierarchy no longer shows the old MiniGamesKid armature beneath Crawler after the migration.",
            "**Pending baseline validation:** open/import the branch in Unity 2022.3.55f1. If the serialized old MiniGamesKid armature still exists, run **Tools > Runaway Chimps > Clean Legacy Level 1 Crawler Rig** explicitly on a clean Level 1 scene, or verify the runtime safety cleanup in Play Mode; import alone no longer mutates/saves the scene. Confirm the hierarchy then no longer shows the old MiniGamesKid armature beneath Crawler."
        ),
        (
            "| 2026-09-11 | The long Zombie Crawl visual must stay physically aligned with the Crawler gameplay path and remain inside the vents through corners; do not solve corners merely by shrinking the creature or rotating the entire long model as one rigid child. Preserve the authored crawl as limb motion while the torso bends along recent vent-path history. | Confirmed from Greg's runtime correction. Dedicated visual-anchor, body-path, seeded-trail, hand-containment and animation-diagnostic source implementation is on `codex/fix-level1-crawler-lighting` (`5f5ea84`, `97b20e6`); the September 12 correction supersedes the earlier core-bone position-warp method with proportion-preserving rotation-only torso correction on PR #15. Unity/Photon/headset validation pending. |",
            "| 2026-09-11 | The long Zombie Crawl visual must stay physically aligned with the Crawler gameplay path and remain inside the vents through corners; do not solve corners merely by shrinking the creature or rotating the entire long model as one rigid child. Preserve authored crawl limb motion. | Confirmed design requirement. The original seeded-trail position warp and the later rotation-only torso correction both failed headset validation and are superseded. PR #15 currently uses an Animator-owned rigid torso, whole-visual heading smoothing, and bounded arm-only surface-contact IK; a new rig-safe long-body corner solution remains pending. |"
        ),
    ])

    plan_changed = replace(plan_path, [
        (
            "Once `CrawlerVisualController` has attached Zombie Crawl, it overrides the complete visual anchor's old backwards heading without changing the imported skeleton.",
            "`CrawlerVisualController` now attaches Zombie Crawl at identity so the obsolete backwards yaw is absent at source. `CrawlerVisualHeadingStabilizer` then smooths the complete visual anchor toward actual gameplay travel without changing the imported skeleton."
        ),
        (
            "**Pending Unity/headset validation:** importing this branch in Unity must actually execute/save the scene migration, after which the Crawler hierarchy should no longer show the old MiniGamesKid armature.",
            "**Pending Unity/headset validation:** importing this branch no longer auto-mutates or saves Level 1. If the serialized MiniGamesKid armature remains, explicitly run **Tools > Runaway Chimps > Clean Legacy Level 1 Crawler Rig** on a clean scene, or verify the runtime marker-based safety cleanup in Play Mode; the Crawler hierarchy should then no longer show the old armature."
        ),
    ])

    print(f"design_changed={design_changed} plan_changed={plan_changed}")


if __name__ == "__main__":
    main()
