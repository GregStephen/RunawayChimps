#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESIGN = ROOT / "docs/design-and-lore.md"
PLAN = ROOT / "docs/repository-improvement-plan.md"

DESIGN_HEADING = "## September 13 global local-player threat feedback architecture"
PLAN_HEADING = "## September 13 global player-specific threat feedback implementation"

DESIGN_SECTION = r'''## September 13 global local-player threat feedback architecture

**Confirmed architecture:** threat/fear presentation is a persistent **local-player-only** system, independent from a monster's generic shared chase flag. A local client may show pursuit feedback only when the authoritative monster state identifies that client's Photon ActorNumber as the pursued player, the source belongs to the same loaded sector, the local player is in an eligible danger zone, and travel/session state is valid. A generic `IsChasing=true` must never make every player see the same personal threat effect. This rule supersedes any earlier implication that the existing global proximity value or shared chase boolean alone is sufficient for personal threat presentation.

**Confirmed proximity boundary:** keep `ProximityManager` / `ProximityReactor` as the reusable generic local-distance layer. Threat feedback consumes its cached raw local-player distance and freshness rather than redefining proximity as fear or adding another hot-path scene search. Existing proximity-driven monster audio remains separate; a nearby player may still hear the shared monster even when that monster is pursuing somebody else.

**Confirmed presentation behavior:** the first consumer is a VR-safe red peripheral vignette. No eligible pursuit means no red effect. An actual pursuit begins with a faint warning, increases smoothly as the pursued monster approaches, and reaches its bounded maximum immediately before capture-range contact. The center of both eyes remains readable, maximum opacity is capped, transitions are smoothed, and the initial implementation does **not** flash or pulse the aperture. Travel, safe-room entry, target changes, stale/disconnected authority state, room changes, pause, and source removal clear desired threat and release the presentation smoothly. Black Loading/travel coverage retains visual priority.

**Confirmed aggregation and extensibility:** multiple eligible monsters use the strongest meaningful local threat (`max` aggregation), not additive opacity. `ThreatFeedbackController` exposes the aggregate local threat value/event so future local heartbeat/breathing, haptics, or audio filtering can consume the same signal without networking presentation state. Those additional effects remain future work; only the vignette is part of the first implementation.

**Confirmed monster contract:** Crawler, Listener, and future monsters expose the same monster-agnostic pursuit identity contract. Photon synchronization carries the pursued ActorNumber atomically with pursuit state. The Listener must not depend on Crawler navigation code: investigating a noise position does not, by itself, count as pursuing a known player. Listener threat feedback begins only if/when its own brain explicitly identifies and pursues an actor.

**Implemented on `feature/global-threat-feedback` / draft PR #19:** `MonsterPursuitState` and reusable pursuit-provider interfaces define the shared contract; `SectorMonsterSync` protocol v2 replicates pursued ActorNumber, sends reliable pursuit/retarget transitions in addition to periodic pose snapshots, recovers current state for arrivals, expires stale target identity, and invalidates the previous target during controller handoff. `ProximityReactor` exposes a fresh cached raw distance sample while preserving its existing UnityEvents. `ThreatFeedbackController` is installed on the persistent local XR rig, aggregates registered `MonsterThreatSource` values with `max`, and drives one local XR-camera `ThreatVignetteView`. A Level 1 composition adapter maps the existing Crawler root into the generic source contract without changing its closest-eligible-player targeting rules. The obsolete unused `ScreenVignette` stub is removed after migration. `Tools/validate_threat_feedback.py` protects these source contracts in Source Integrity.

**Pending validation:** Unity 2022.3.55f1 import/compile, both Runaway Chimps Editor validators, Play Mode pursuit/safe-room/travel behavior, stereo/headset comfort and center readability, Quest standalone performance, and two-client Photon validation remain pending. Two-client testing must prove Player A's chase never appears on Player B, A→B retargeting transfers feedback without a generic-chase gap, controller handoff/late arrival/reconnect cannot leave stale red, safe-zone/travel changes clear locally, and two simultaneous threats remain bounded. No heartbeat, breathing, haptic, or audio-filter consumer is implemented by this first pass.

'''

PLAN_SECTION = r'''## September 13 global player-specific threat feedback implementation

**Confirmed architecture:** Greg approved the audited design for one reusable persistent local threat-feedback layer. Personal feedback is keyed by the monster's explicit Photon target ActorNumber; generic shared `IsChasing` remains available for shared monster behavior/audio but is insufficient for a personal warning. `ProximityManager` stays generic and the threat layer consumes fresh cached distance samples. Multiple sources use bounded maximum aggregation. The first visible consumer is a smooth red peripheral vignette with a readable center and no aggressive flashing. Future heartbeat/breathing, haptics, and audio filtering remain architectural extension points only.

**Implemented source on `feature/global-threat-feedback` / draft PR #19:** new `MonsterPursuitState`, `IMonsterPursuitProvider`, and `IMonsterPursuitSyncTarget` contracts separate pursuit identity from Crawler-specific code. `MonsterNavigation` now publishes atomic pursuit identity while preserving `IsChasing` for existing shared audio. `SectorMonsterSync` protocol v2 sends position/rotation plus pursued ActorNumber, uses reliable pursuit/retarget transition snapshots alongside the existing periodic updates, validates the elected sector controller as sender, supplies complete current state to arrivals, expires stale remote pursuit identity, and clears the previous controller's target before a new controller makes a fresh decision. This changes network state only; vignette intensity/material state is never networked.

`ProximityReactor` now caches finite raw local-player distance plus unscaled sample time and invalidates it on clear/disable. `MonsterThreatSource` fails closed unless pursuit is explicitly aimed at `PhotonNetwork.LocalPlayer.ActorNumber`, sector and required local zone match, travel is not busy, and the proximity sample is fresh. Per-monster `ThreatProfile` supplies start/full-threat distances, response curve, pursuit baseline, and cap. `ThreatFeedbackController` lives on the persistent local XROrigin, uses allocation-free registered-source iteration and `max` aggregation, smooths acquisition/release with unscaled time, exposes the aggregate value/event for future local consumers, and resets presentation on pause/disable.

The Level 1 Crawler is connected through `CrawlerThreatSourceInstaller`, a one-time scene composition adapter rather than Crawler logic inside the global controller. It uses the Crawler's runtime-tuned detection range and requires `Level1_Vents`; safe-room entry therefore fails the source closed without changing shared Crawler simulation for another eligible player. The old unused `ScreenVignette` UI-image stub is removed. `ThreatVignetteView` renders only through the persistent local XR camera via `ScreenSpaceCamera`; the project-owned stereo-aware threat shader preserves a broad clear center and caps peripheral alpha at 0.32. No post-processing migration or networked presentation object is introduced.

**Source-regression protection implemented:** `Tools/validate_threat_feedback.py` verifies target normalization, local ActorNumber matching, fresh cached proximity, sector/zone/travel gates, max aggregation, generic pursuit contracts, protocol-v2 target synchronization and handoff invalidation, local-camera-only rendering, stereo shader macros, the bounded alpha contract, removal of `ScreenVignette`, and absence of network UI state or scene-wide searches in threat hot paths. Source Integrity now runs this checker in addition to the existing repository and Level 1 checks.

**Pending validation:** source/CI success does not establish runtime behavior. Unity 2022.3.55f1 must import/compile the branch and both Editor validators must pass. Play Mode/headset testing must prove no effect while not pursued; faint→strong distance response while pursued; readable center in both eyes; smooth safe-room/capture/travel release; no duplicate local view after repeated travel; and acceptable Quest frame time/memory. Two Photon clients must test A chased/B nearby, A→B retarget while shared chase stays true, either player's safe-room entry, controller departure/handoff, late arrival, room reconnect/pause, one threat per player from different monsters, and two simultaneous threats on one player using bounded max aggregation. Until those tests are actually run, Unity, headset, Quest, and two-client Photon remain **Pending validation**.

'''


def insert_once(text: str, marker: str, section: str, before: str) -> str:
    if marker in text:
        return text
    if before not in text:
        raise SystemExit(f"missing insertion anchor: {before}")
    return text.replace(before, section + before, 1)


def main():
    design = DESIGN.read_text(encoding="utf-8-sig")
    design = insert_once(
        design,
        DESIGN_HEADING,
        DESIGN_SECTION,
        "## September 12 PR #15 merge-review hardening",
    )

    decision_header = "| Recorded | Decision or correction | Status |\n| --- | --- | --- |\n"
    decision_row = (
        "| 2026-09-13 | Establish one persistent local-player-specific threat feedback architecture. "
        "Personal pursuit feedback requires an explicit synchronized target ActorNumber match; generic `IsChasing` alone cannot drive it. "
        "Keep proximity generic, aggregate multiple threats with bounded max, and use a VR-safe local peripheral vignette first. | "
        "Confirmed architecture; source-implemented on `feature/global-threat-feedback` / PR #19. Unity 2022.3.55f1, two-client Photon, headset, and Quest validation pending. |\n"
    )
    if decision_row not in design:
        if decision_header not in design:
            raise SystemExit("missing design decision table header")
        design = design.replace(decision_header, decision_header + decision_row, 1)

    current_header = "| Status | Decision |\n| --- | --- |\n"
    current_row = (
        "| Confirmed | Threat/fear presentation is local-player-specific. A monster must explicitly be pursuing the local Photon ActorNumber before that source can affect the local threat value; shared `IsChasing` is insufficient. Generic proximity remains separate, multiple threats use bounded max aggregation, and the first consumer is a VR-safe peripheral red vignette with future local audio/haptic consumers optional. |\n"
    )
    if current_row not in design:
        if current_header not in design:
            raise SystemExit("missing current decisions table header")
        design = design.replace(current_header, current_header + current_row, 1)

    DESIGN.write_text(design, encoding="utf-8")

    plan = PLAN.read_text(encoding="utf-8-sig")
    plan = insert_once(
        plan,
        PLAN_HEADING,
        PLAN_SECTION,
        "## September 13 Level 1 keycard-room environment and regeneration lore",
    )
    PLAN.write_text(plan, encoding="utf-8")


if __name__ == "__main__":
    main()
