#!/usr/bin/env python3
"""One-time idempotent PR #15 documentation sync after merge-review hardening."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / "docs/design-and-lore.md"
PLAN = ROOT / "docs/repository-improvement-plan.md"

DESIGN_MARKER = "## September 12 PR #15 merge-review hardening"
PLAN_MARKER = "## September 12 PR #15 merge-review hardening"


def replace_if_present(text: str, old: str, new: str) -> str:
    return text.replace(old, new) if old in text else text


def update_design() -> bool:
    text = DESIGN.read_text(encoding="utf-8-sig")
    original = text

    # Preserve the historical sequence while making the active/superseding state unambiguous.
    text = replace_if_present(
        text,
        "PR #15 (`fix/runtime-keycard-crawler-loading`) source-implements targeted corrections: keycard recovery now uses a spawn-relative fall fail-safe as well as the absolute kill height and resets the Rigidbody pose coherently; Crawler path correction no longer writes world positions into hierarchical chest/spine/hips bones, preserving authored torso segment lengths while bending by rotation and rigidly aligning the visual front anchor; and Loading canvases receive a black 12% overscanned camera-space backdrop behind the existing UI.",
        "PR #15 (`fix/runtime-keycard-crawler-loading`) source-implements targeted corrections: keycard recovery now uses a spawn-relative fall fail-safe as well as the absolute kill height and resets the Rigidbody pose coherently; the first Crawler follow-up tried rotation-only torso correction, but the later headset result documented below supersedes that approach with an Animator-owned rigid torso baseline; and Loading canvases receive a black 12% overscanned camera-space backdrop behind the existing UI."
    )
    text = replace_if_present(
        text,
        "a Level 1 Editor migration unpacks the old model instance when necessary and saves the cleaned scene, and a runtime safety net performs the same cleanup after Zombie Crawl attaches.",
        "an explicit menu-driven Level 1 Editor migration can unpack the old model instance when requested and refuses to mutate/save an already-dirty scene, while a runtime safety net performs the marker-based cleanup after Zombie Crawl attaches."
    )
    text = replace_if_present(
        text,
        "Source inspection matches the symptoms: `CrawlerVisualController` still creates the complete Zombie visual with the old 180-degree yaw, while the safe no-bone-writes baseline makes the long creature rotate as one rigid object around the gameplay path.",
        "Source inspection identified the old 180-degree visual yaw as the backwards-facing cause. The reviewed PR #15 source now hard-codes the Zombie visual anchor to identity so a stale serialized yaw cannot restore that failure; the safe no-core-bone-writes baseline still makes the long creature rotate as one rigid object around the gameplay path."
    )
    text = replace_if_present(
        text,
        "| 2026-09-12 | The Crawler's path-following presentation must preserve the Zombie Crawl skeleton's authored body proportions. Do not reposition hierarchical chest/spine/hips bones along the trail because that can compress/stretch their segment lengths; use rotation-only torso bending plus rigid visual alignment. | Confirmed correction from Greg's squished-body runtime report. PR #15 source-implements rotation-only core-torso path correction and rigid front-anchor translation; Source validation rejects future core-bone position writes. Unity/headset straight/corner/junction validation pending. |",
        "| 2026-09-12 | The Crawler's path-following presentation must preserve the Zombie Crawl skeleton's authored body proportions. Do not reposition or independently rotate hierarchical chest/spine/hips bones after Animator evaluation; both attempted torso-deformation approaches failed in headset. Keep the core torso Animator-owned and use only bounded arm-contact IK as the approved post-Animator exception. | Confirmed correction from Greg's squished/folded-body runtime reports. PR #15 now uses rigid whole-visual alignment/heading plus dedicated arm-only surface-contact IK; long-body corner bending remains pending a separate rig-safe solution. Unity/headset validation pending. |"
    )
    text = replace_if_present(
        text,
        "Serialized scene cleanup occurs when Unity runs the migration; source validation passes independently, and Unity/headset alignment/chase/capture validation remains pending.",
        "Serialized scene cleanup is now an explicit manual menu action rather than an automatic Editor startup mutation; it refuses dirty scenes. Runtime marker-based cleanup remains the safety net, and Unity/headset alignment/chase/capture validation remains pending."
    )
    text = replace_if_present(
        text,
        "Do not keep the obsolete MiniGamesKid renderer/Animator/armature live in parallel, and do not write post-Animator Zombie bone positions or rotations.",
        "Do not keep the obsolete MiniGamesKid renderer/Animator/armature live in parallel. Core torso/spine/hips remain Animator-owned with no post-Animator path deformation; bounded arm-only environmental IK is the explicit exception for hand-to-vent contact."
    )
    text = replace_if_present(
        text,
        "after Unity runs the legacy-rig scene migration, Crawler should no longer expose the old MiniGamesKid armature.",
        "after running the explicit legacy-rig cleanup menu when needed (or allowing the Play Mode runtime safety cleanup), Crawler should no longer expose the old MiniGamesKid armature."
    )

    if DESIGN_MARKER not in text:
        section = """
## September 12 PR #15 merge-review hardening

**Confirmed implementation correction:** the PR #15 merge review found two source-level blockers in the first cleanup/contact pass. Legacy Crawler cleanup was too broad because it could infer that any non-Zombie renderer/Animator belonged to MiniGamesKid, and Crawler hand contact retained world-space contact history across a discontinuous gameplay-root move. Both are superseded. Legacy cleanup is now marker-based only: a branch must contain verified MiniGamesKid bones such as `shoulderL`/`elbowL` before it may be disabled or removed, so unrelated current/future visuals beneath the gameplay root are preserved. The Editor cleanup is manual-only through **Tools > Runaway Chimps > Clean Legacy Level 1 Crawler Rig** and refuses to mutate/save an already-dirty Level 1 scene; opening Unity no longer performs an automatic scene migration.

**Crawler orientation/contact hardening:** the current Zombie import is now created with an identity visual rotation in code, with no serialized yaw field, so the superseded 180-degree orientation cannot return through a stale scene override. `CrawlerSurfaceContactIK` resets both arm-contact caches when the authoritative gameplay root moves at least 1.25 m in one frame, preventing a Photon/controller/NavMesh correction from sweeping a hand from an old vent location. Already-overlapping first poses probe from both forearm and upper arm and fall back to a bounded last-clear-point search. After a two-bone solve, the next collision cast is seeded from the hand's **actual solved world position**, not an unreachable requested wall target.

**Active rule / pending validation:** this section supersedes any earlier wording that presents rotation-only torso bending as the current solution or says no Zombie bone may ever be touched after Animator evaluation. The active core-body rule is: no post-Animator path deformation of hips/spine/chest; the Animator owns the torso. Bounded upper-arm/forearm IK is allowed only for static vent-surface hand contact. Long-body 90-degree corner deformation remains unresolved. `Tools/validate_pr15_review_hardening.py` now protects marker-only cleanup, manual-only scene migration, serialization-proof forward orientation, discontinuity-safe contact, and solved-position IK caching. These are implemented/source-validated safeguards only; Unity 2022.3.55f1, headset, two-client Photon, and Quest validation remain pending.

"""
        marker = "## Decision and correction record\n"
        if marker in text:
            text = text.replace(marker, section + marker, 1)
        else:
            text += "\n" + section

    if text != original:
        DESIGN.write_text(text, encoding="utf-8")
        return True
    return False


def update_plan() -> bool:
    text = PLAN.read_text(encoding="utf-8-sig")
    original = text

    text = replace_if_present(
        text,
        "the visual controller still initializes `CrawlerVisualAnchor` with a 180-degree yaw. Greg's retest provides direct evidence that this offset is wrong for the current Zombie import.",
        "the runtime failure traced back to the visual controller's old 180-degree `CrawlerVisualAnchor` yaw. The reviewed PR #15 source now removes that serialized yaw entirely and initializes the current Zombie import at identity, so the obsolete offset cannot return from a stale scene override."
    )
    text = replace_if_present(
        text,
        "`CrawlerLegacyVisualSceneCleanup` is a Unity Editor migration that opens `Level1_Containment`, safely unpacks the obsolete `MiniGamesKidFirstRig.fbx` instance when necessary, removes the visual-only legacy rig and saves the scene; it skips an already-loaded dirty scene unless invoked explicitly from **Tools > Runaway Chimps > Clean Legacy Level 1 Crawler Rig**.",
        "`CrawlerLegacyVisualSceneCleanup` is now an explicit menu-only Unity Editor migration. When requested from **Tools > Runaway Chimps > Clean Legacy Level 1 Crawler Rig**, it can open `Level1_Containment`, safely unpack the verified obsolete `MiniGamesKidFirstRig.fbx` instance when necessary, and save the cleanup; it refuses to mutate/save an already-loaded dirty scene and never runs automatically on Editor startup."
    )
    text = replace_if_present(
        text,
        "The latest visual controller preserves authored world scale, creates a separate visual anchor, calibrates visible chest/spine alignment and floor contact, explicitly drives/probes the crawl state, and delegates long-body corner deformation to `CrawlerBodyPathFollower`. That follower derives a local seeded motion trail from the synchronized gameplay root and places torso sections progressively farther behind it so the model can bend around corners without networking bones. Hand containment is a prototype post-Animator correction and may still be replaced by two-bone IK after visual testing.",
        "The active visual controller preserves authored world scale, creates a separate visual anchor, calibrates rigid visual/floor alignment, explicitly drives/probes the crawl state, and leaves hips/spine/chest Animator-owned. `CrawlerVisualHeadingStabilizer` smooths only the complete visual heading from recent gameplay-root motion. Dedicated `CrawlerSurfaceContactIK` performs bounded two-bone correction for hands that would penetrate static vent geometry; it does not deform the core torso or network bones. Long-body corner deformation remains a separate unresolved rig-safe task."
    )
    text = replace_if_present(
        text,
        "animation binding, calibrated body alignment, seeded recent-path corner bending, hand containment/IK if needed, local headlamp presentation/performance, zone eligibility, navigation/audio, one network controller and state handover.",
        "animation binding, calibrated rigid body alignment, whole-visual heading continuity, dedicated hand surface-contact IK, local headlamp presentation/performance, zone eligibility, navigation/audio, one network controller and state handover; keep long-body corner bending separate until a rig-safe design is proven."
    )

    if PLAN_MARKER not in text:
        section = """## September 12 PR #15 merge-review hardening

**Confirmed review findings:** the PR #15 merge review found that the first legacy-rig cleaner was broader than its stated contract and that the first Crawler hand-contact implementation could carry stale world-space hand history across a discontinuous gameplay-root move. It also found that the old 180-degree Zombie yaw remained serialized as a configurable default, first-frame hand overlap recovery was too dependent on a forearm-only sphere cast, and the Editor cleanup could mutate/save Level 1 automatically at startup. These are source-review findings, not new gameplay rules.

**Implemented corrections on `fix/runtime-keycard-crawler-loading`:** legacy cleanup now considers only top-level branches that contain explicit MiniGamesKid marker bones (`shoulderL`, `elbowL`, etc.); unrelated non-Zombie renderers and Animators are preserved. `CrawlerVisualController` delegates its immediate visual handoff to that marker-based cleaner. The one-time Editor migration is menu-only, refuses an already-dirty Level 1 scene, and no longer uses `InitializeOnLoad`/delayed automatic saving. The current Zombie visual anchor is hard-coded to identity rather than exposing the superseded 180-degree yaw as serialized state.

`CrawlerSurfaceContactIK` now resets both arm contact caches on a >=1.25 m single-frame gameplay-root discontinuity, probes already-overlapping hands from both forearm and upper arm, falls back to a bounded last-clear-point search, and seeds the next sweep from the **actual solved hand position** after reach clamping. Core torso/spine/hips remain Animator-owned; arm-only environmental IK is the bounded post-Animator exception. `Tools/validate_pr15_review_hardening.py` runs in Source validation and fails if broad non-Zombie cleanup, automatic scene migration, serialized Zombie yaw, discontinuity-unsafe hand history, or requested-target rather than solved-position caching returns.

**Validation boundary:** Source validation passing proves only these source contracts. Unity 2022.3.55f1 still must compile/import the branch and headset testing must verify forward orientation, straight/turn motion, hand wall/floor/ceiling contact, elbow stability, release from contact, keycard recovery, Loading coverage, player-hand contact, capture/safe-room behavior, and two-client Photon controller handover. Long rigid-body 90-degree corner fitting remains separately unresolved and must not be hidden by reintroducing the failed torso transforms.

"""
        marker = "## September 12 automated source validation\n"
        if marker in text:
            text = text.replace(marker, section + marker, 1)
        else:
            text += "\n" + section

    if text != original:
        PLAN.write_text(text, encoding="utf-8")
        return True
    return False


def main():
    changed = update_design() | update_plan()
    print("Updated PR #15 maintained docs." if changed else "PR #15 maintained docs already synchronized.")


if __name__ == "__main__":
    main()
