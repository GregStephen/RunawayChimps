#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def append_once(path, marker, text):
    p = ROOT / path
    current = p.read_text(encoding="utf-8-sig")
    if marker not in current:
        p.write_text(current.rstrip() + "\n\n" + text.strip() + "\n", encoding="utf-8")

append_once(
    "docs/design-and-lore.md",
    "## September 13 PR #19 local-player threat feedback",
    '''## September 13 PR #19 local-player threat feedback

**Confirmed design:** threat/fear presentation is local-player-specific. A monster must explicitly be pursuing the local Photon ActorNumber before its source can affect that player's threat value; generic shared `IsChasing` is insufficient. Proximity remains generic, multiple threats use bounded `max` aggregation, and the first presentation is a VR-safe red peripheral vignette with a readable center. The Listener and future monsters use the same monster-agnostic pursuit contract; investigating a noise position alone is not player pursuit.

**Implemented on `feature/global-threat-feedback` / PR #19:** the Crawler adapter is scoped to authored `monsterId == 1`; `SectorMonsterSync` protocol v4 synchronizes pursued ActorNumber with authority epochs and per-authority revisions, reliable pursuit/retarget transitions, periodic recovery snapshots, stale-target expiry, sender validation, and fresh target reevaluation on controller handoff. Travel suppresses the local threat view immediately so the black Loading/fade presentation keeps priority. The obsolete unused `ScreenVignette` stub is removed.

**Merge reconciliation:** PR #19 now includes current `main` through the security-system boot prototype and VentRoom blower work. Those newer mainline changes and their validation/docs are preserved. Greg's explicit `UnityEngine.Random.Range` compile correction is preserved. The accidentally reintroduced orphan `Assets/StreamingAssets.meta` is intentionally not preserved because its GUID is now legitimately used by the VentBlower conduit material metadata on `main`.

**Confirmed multiplayer-development workflow:** use one Unity 2022.3.55f1 Editor client plus one Windows Development Build client on the same PC, both in the same explicit private Photon room, when a second headset is unavailable. The automated Client-B launcher, separate dev identities, desktop movement, test shortcuts, and read-only multiplayer/threat HUD remain **planned, not implemented**. See `docs/multiplayer-development-testing.md`.

**Pending validation:** Unity 2022.3.55f1 import/compile, both Editor validators, Play Mode pursuit/safe-room/capture/travel behavior, headset stereo/comfort, Quest performance, and two-client Photon targeting/retarget/handoff/reconnect remain pending.'''
)

append_once(
    "docs/repository-improvement-plan.md",
    "## September 13 PR #19 threat-feedback merge reconciliation",
    '''## September 13 PR #19 threat-feedback merge reconciliation

**Confirmed architecture:** personal threat feedback requires an explicit synchronized target ActorNumber match; shared `IsChasing` alone cannot drive it. `ProximityManager` stays generic, `ThreatFeedbackController` uses bounded `max` aggregation, and the first consumer is a local XR-camera peripheral vignette.

**Implemented source:** reusable `MonsterPursuitState`/provider contracts, fresh cached proximity samples, Crawler `monsterId == 1` composition, travel-priority suppression, and `SectorMonsterSync` protocol v4. Protocol v4 uses authority epochs plus per-authority revisions so same-frame changes and delayed packets from an earlier A -> B -> A authority term fail closed. The combined Source Integrity workflow runs both the security-boot and threat-feedback validators.

**Merge reconciliation completed:** current `main` security-system boot and VentRoom blower changes are preserved while PR #19's threat code is retained. The explicit `UnityEngine.Random.Range` correction is retained. The orphan `Assets/StreamingAssets.meta` reintroduced on the feature branch is dropped because current main already reuses that GUID for the VentBlower conduit material metadata.

**Multiplayer development workflow:** standardize one-PC checks on Unity Editor Client A plus Windows Development Build Client B in the same private Photon room. The convenience launcher/identity/desktop-control/HUD harness remains **planned, not implemented**; see `docs/multiplayer-development-testing.md`.

**Pending validation:** source checks do not replace Unity 2022.3.55f1 import/compile, Editor validators, Play Mode, headset/Quest, or two-client Photon runtime tests.'''
)
