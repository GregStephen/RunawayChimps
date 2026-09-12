#!/usr/bin/env python3
"""Record the September 12 runtime retest and legacy Crawler hierarchy cleanup.

This is an idempotent repository-document migration used by the temporary branch workflow.
It deliberately records source implementation separately from Unity/headset validation.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / "docs/design-and-lore.md"
PLAN = ROOT / "docs/repository-improvement-plan.md"


def insert_before(text: str, marker: str, block: str, label: str) -> str:
    if block.splitlines()[0] in text:
        return text
    if marker not in text:
        raise RuntimeError(f"{label}: marker not found: {marker}")
    return text.replace(marker, block.rstrip() + "\n\n" + marker, 1)


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if new in text:
        return text
    if old not in text:
        raise RuntimeError(f"{label}: expected text not found")
    return text.replace(old, new, 1)


def update_design(text: str) -> str:
    status = """**September 12 integrated hand/Crawler retest + legacy-rig cleanup:** Greg's latest runtime screenshots supersede any implication that the hand-contact or Crawler visual defects are resolved. The PR #14 spawn reset is a partial success: the hands no longer begin buried, but the first-person fingertips can still penetrate the floor. PR #15 now enforces at least a 0.05 m Gorilla hand-contact radius and drives the visible/network avatar hand position from the collision-safe Gorilla follower while preserving tracked controller rotation; headset retest is pending. The Crawler also still rendered severely crushed/folded and could appear outside the vent. The old `MiniGamesKidFirstRig` armature (`shoulderL`/`shoulderR` etc.) is still serialized beneath the Crawler and was visibly far from the new model. Source inspection confirms that stale armature is **not** the Zombie clip target—Zombie Crawl uses its own Generic `mixamorig:*` skeleton—but Greg confirmed it should no longer remain as a parallel live hierarchy. On `fix/runtime-keycard-crawler-loading`, the failed position-warp and rotation-only torso-warp approaches are superseded: `CrawlerBodyPathFollower` performs no post-Animator bone transforms, a new legacy-rig cleaner removes only obsolete MiniGamesKid render/Animator/armature branches while preserving navigation/capture/audio/Photon/gameplay components, a Level 1 Editor migration unpacks the old model instance when necessary and saves the cleaned scene, and a runtime safety net performs the same cleanup after Zombie Crawl attaches. The intended live hierarchy is now **one Crawler gameplay root + Zombie Crawl visual/Animator**. Source validation protects that contract; Unity 2022.3.55f1/headset must still prove the cleaned hierarchy, Zombie alignment inside the vent, authored crawl deformation, and hand-floor contact before any defect is marked resolved."""
    text = insert_before(text, "## Decision and correction record", status, "design status")

    decision_marker = "| 2026-09-12 | A Level 1 objective keycard that falls through the floor"
    cleanup_row = "| 2026-09-12 | Level 1 Crawler should have one authoritative gameplay root plus the Zombie Crawl visual/Animator. The obsolete MiniGamesKid renderer, Animator and armature must not remain as a second live visual/skeleton hierarchy. | Confirmed by Greg after the latest Scene/runtime screenshots. PR #15 source-implements editor migration plus runtime safe cleanup while preserving navigation, capture, audio, patrol and Photon components. Serialized scene cleanup occurs when Unity runs the migration; source validation passes independently, and Unity/headset alignment/chase/capture validation remains pending. |\n"
    if cleanup_row.strip() not in text:
        if decision_marker not in text:
            raise RuntimeError("design decision marker not found")
        text = text.replace(decision_marker, cleanup_row + decision_marker, 1)

    old_current = "| Confirmed | The Crawler's visible long body must remain aligned to the vent route and bend around corners rather than behaving like one rigid rotating child. The crawl clip supplies limb motion; path correction may rotate torso bones locally but must preserve authored skeleton segment lengths and must not squash/stretch the torso by rewriting hierarchical core-bone positions. |"
    new_current = "| Confirmed | Level 1 Crawler uses one gameplay root for navigation/capture/audio/Photon and one Zombie Crawl visual/Animator rig. Do not keep the obsolete MiniGamesKid renderer/Animator/armature live in parallel, and do not write post-Animator Zombie bone positions or rotations. Long-body corner deformation is still desired, but both direct-transform attempts failed and are superseded; design a rig-safe solution only after the clean Animator-owned baseline passes. |"
    text = replace_once(text, old_current, new_current, "design current Crawler decision")

    old_monster_intro = "`Zombie Crawl` is now the selected and integrated visible model on the existing Level 1 gameplay root; the old MiniGamesKid render is hidden while navigation, capture, patrol/audio and Photon state remain on that root."
    new_monster_intro = "`Zombie Crawl` is the selected visible model on the existing Level 1 gameplay root. Greg's latest correction removes the obsolete MiniGamesKid visual rig rather than merely hiding it: navigation, capture, patrol/audio and Photon state remain on the gameplay root, while the runtime hierarchy should contain only the new Zombie visual/Animator for presentation."
    text = replace_once(text, old_monster_intro, new_monster_intro, "design monster intro")

    superseding = """**Superseding September 12 Crawler hierarchy correction:** the rotation-only follow-up also failed in headset and produced a severely folded/crushed Zombie, so it must not be treated as the active solution. The old `shoulderL` armature visible in Greg's Scene view belongs to MiniGamesKid and is not the Zombie `mixamorig:*` animation target, but it is unnecessary and confusing and is now removed by the new editor/runtime cleanup path. `CrawlerBodyPathFollower` is reduced to rigid whole-visual pivot/floor alignment and never writes Zombie bone positions or rotations. The Zombie Animator owns the complete skeleton. Corner bending is intentionally disabled for the baseline test. The new Editor migration safely unpacks the old MiniGamesKid model instance only when needed and deletes visual-only branches; any branch containing gameplay/physics/audio/state components is preserved. A runtime cleanup repeats that safety rule after Zombie Crawl attaches, so gameplay systems cannot be accidentally deleted.

**Pending baseline validation:** open/import the branch in Unity 2022.3.55f1 and confirm the Level 1 hierarchy no longer shows the old MiniGamesKid armature beneath Crawler after the migration. In Play Mode, confirm one Crawler gameplay root follows its NavMesh path and Zombie Crawl remains rigidly attached/aligned to it inside a straight vent. The Zombie must retain authored proportions and `mixamo_com` must visibly deform its `mixamorig:*` limbs. Record corner clipping separately—corner deformation is intentionally pending. Then repeat chase, safe-room return, capture, Photon handover and headset tests. If Zombie still separates from the gameplay root with the legacy rig gone, diagnose the visual-anchor/pivot/Animator binding directly rather than adding another old-rig transform correction."""
    text = insert_before(text, "### The missing subject in Level 1", superseding, "design superseding crawler correction")

    old_future = "| Crawler and difficulty | Validate the latest proportion-preserving Zombie Crawl body-path implementation for visual/gameplay-root alignment, stable authored body lengths, real limb animation binding, seeded body extension, 90-degree/junction bending, hand containment, 12 m prototype detection, contact/held-overlap capture and both safe-room boundaries. Start with one card; add a second only if playtests justify a distinct route and story purpose. |"
    new_future = "| Crawler and difficulty | First validate the cleaned single-root hierarchy and Animator-owned Zombie baseline: no legacy MiniGamesKid armature, no body crushing, correct visual/gameplay-root alignment inside a straight vent, real `mixamo_com` limb deformation, 12 m prototype detection, contact/held-overlap capture and both safe-room boundaries. Corner deformation is pending a new rig-safe implementation after this baseline passes. |"
    text = replace_once(text, old_future, new_future, "design future Crawler row")

    old_test = "1. Validate PR #15's Crawler correction: in a straight vent at patrol and chase speed, the torso must retain the Zombie Crawl model's authored proportions rather than compressing/squashing. Then verify the front/chest remains aligned to the gameplay path and the rotation-only torso bending follows a 90-degree turn/junction without stretching/compressing the core skeleton or leaving the vent. Confirm `mixamo_com` visibly moves limb bones and inspect bounded hand containment separately."
    new_test = "1. Validate PR #15's cleaned Crawler baseline: after Unity runs the legacy-rig scene migration, Crawler should no longer expose the old MiniGamesKid armature. In Play Mode the Zombie must stay attached to the single gameplay root, remain inside/aligned in a straight vent, retain authored proportions, and visibly animate `mixamo_com` limbs at patrol/chase speed. At a 90-degree turn, record rigid clipping separately; direct torso-bone bending is intentionally disabled until a new rig-safe solution is designed."
    text = replace_once(text, old_test, new_test, "design next Crawler test")
    return text


def update_plan(text: str) -> str:
    block = """## September 12 integrated hand/Crawler retest and legacy hierarchy cleanup

**Confirmed runtime failures:** Greg's latest screenshots show two remaining defects. The PR #14 hand reset stopped the hands from initially spawning buried, but visible fingertips can still pass through the floor and surface contact still feels wrong. Zombie Crawl remains visibly crushed/folded and was observed outside the vent; the old MiniGamesKid armature is still present under the Crawler hierarchy and can be spatially far away from the Zombie. These are failed runtime results, not resolved checks.

**Hand source follow-up implemented on PR #15:** the active Bootstrap rig had serialized a 0.02 m Gorilla hand contact distance while the authored rig/default uses 0.05 m, and the visible local Photon avatar was copying raw XR controller positions rather than Gorilla's collision-safe hand followers. `Player` now enforces at least 0.05 m contact radius; `PhotonVRPlayer` uses the collision-safe follower for hand position and raw tracking only for rotation. Headset fingertip/palm contact, locomotion push, travel/respawn and two-client presentation are still pending.

**Crawler source finding:** the legacy `shoulderL`/`shoulderR` hierarchy is MiniGamesKid's armature. Zombie Crawl is a separate Generic rig and `mixamo.com.anim` targets `mixamorig:*` paths, so the old armature is not the Zombie animation target. It is nevertheless obsolete live hierarchy and makes debugging/alignment much harder. The previous post-Animator world-position deformation failed by squashing the skeleton; the rotation-only replacement also failed by folding the hierarchical torso. Both are superseded.

**Confirmed cleanup / implemented source:** Greg approved removing unnecessary first-monster elements. `fix/runtime-keycard-crawler-loading` now defines one intended live presentation: the existing Crawler gameplay root retains `MonsterNavigation`, NavMesh movement, capture trigger, patrol/audio, proximity/activation and Photon state, while Zombie Crawl is the only visual/Animator rig. `CrawlerLegacyVisualCleaner` removes old renderers/Animator/visual-only armature branches but refuses to delete branches containing gameplay, physics, audio or state components. `CrawlerLegacyVisualSceneCleanup` is a Unity Editor migration that opens `Level1_Containment`, safely unpacks the obsolete `MiniGamesKidFirstRig.fbx` instance when necessary, removes the visual-only legacy rig and saves the scene; it skips an already-loaded dirty scene unless invoked explicitly from **Tools > Runaway Chimps > Clean Legacy Level 1 Crawler Rig**. `CrawlerLegacyVisualRuntimeCleanup` is the safety net for unsaved/older scene copies: after Zombie Crawl attaches, it removes any remaining old visual rig in Play Mode. `CrawlerBodyPathFollower` remains an Animator-owned rigid alignment baseline with no post-Animator bone writes. Source validation now protects all of these contracts.

**Pending Unity/headset validation:** importing this branch in Unity must actually execute/save the scene migration, after which the Crawler hierarchy should no longer show the old MiniGamesKid armature. In Play Mode verify the single gameplay root and Zombie visual remain colocated and inside a straight vent while stationary, patrolling and chasing; `mixamo_com` must visibly deform the Zombie's own limbs without crushing. Corner deformation is intentionally pending and rigid corner clipping should be recorded separately. Recheck capture, safe rooms, audio, Photon controller handover and Quest performance. If the Zombie is still outside the vent after the old rig is gone, investigate NavMesh root position, visual anchor/pivot and Animator binding directly rather than restoring legacy transforms."""
    text = insert_before(text, "## September 12 spawn-hand initialization follow-up", block, "plan integrated cleanup")

    # Make the old Crawler section unambiguously historical.
    historical_marker = "## September 11 Crawler visual-path and animation correction\n\n"
    note = "**Superseded implementation note, September 12:** the direct position-warp and later rotation-only post-Animator torso corrections both failed runtime validation. The active PR #15 baseline now performs no Zombie bone writes and removes the obsolete MiniGamesKid visual rig; see the September 12 integrated cleanup section above.\n\n"
    if note.strip() not in text:
        if historical_marker not in text:
            raise RuntimeError("plan historical Crawler marker not found")
        text = text.replace(historical_marker, historical_marker + note, 1)

    # Acceptance row if present.
    text, _ = re.subn(
        r"^\| Crawler presentation \|.*$",
        "| Crawler presentation | Level 1 has one Crawler gameplay root plus Zombie Crawl as the only live visual/Animator rig; no MiniGamesKid armature remains after scene migration/runtime cleanup. Zombie remains aligned to the gameplay root inside a straight vent, retains authored proportions, and `mixamo_com` visibly deforms its own skeleton with no post-Animator bone writes. Corner-body deformation is pending a separate rig-safe implementation. |",
        text,
        count=1,
        flags=re.M,
    )
    return text


def main() -> None:
    design = DESIGN.read_text(encoding="utf-8")
    plan = PLAN.read_text(encoding="utf-8")
    DESIGN.write_text(update_design(design), encoding="utf-8")
    PLAN.write_text(update_plan(plan), encoding="utf-8")


if __name__ == "__main__":
    main()
