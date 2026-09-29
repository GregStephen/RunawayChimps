# Security console prototype plan

Recorded: 2026-09-29. Planning baseline: `main` at `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`. **Design direction confirmed; prototype work planned; no gameplay implementation or runtime validation supplied by this plan.**

[Issue #67](https://github.com/GregStephen/RunawayChimps/issues/67) owns the live work queue and acceptance status. This document records scope, decisions and the test strategy, not a second completion checklist. Read [design and lore](design-and-lore.md), [repository improvement plan](repository-improvement-plan.md) and [AGENTS](../AGENTS.md) before implementation.

## Confirmed direction and corrections

Greg approved the following direction after reviewing the initial security-terminal concept:

- **Solo-first, co-op-enhanced:** every required interaction must be completable by one player. Multiplayer may improve speed, optional division of work or situational awareness; a second operator and voice communication are not requirements.
- Physical, in-world VR controls rather than a typing-heavy minigame or forced camera/seat interaction. A player can turn away and leave the console.
- Shared information and world consequences, with concurrent use of nonconflicting controls. Ownership applies to an individually held control, not the whole station.
- Do not assume Level 1 introduction or a terminal in every level. A rare set-piece is the working direction; exact production placement remains open.

These corrections supersede the earlier brainstorming that suggested teaching the terminal in Level 1, extending it through every level, or requiring an operator to remain behind and relay information. The prototype is not a new Chapter 1 release dependency and does not change [the MVP contract](mvp-roadmap.md). The approved Chapter 1 ending and Coconut shop remain owned by #38 and #50.

## Bounded prototype proposal

Build one saved, editable graybox test scene with one station, two fixed same-sector camera positions, one shared monitor, camera-selection buttons, a route-unlock button, one latching power lever, one route door, a small escape loop and a simple synchronized moving threat proxy. Provide a personal endpoint beyond the door.

Proposed flow:

**Inspect route -> restore power -> unlock route -> judge a safe opening -> leave the station -> reach your own endpoint.**

A solo player performs the actions sequentially. Co-op players may do different actions simultaneously or watch the route for one another, but they can also finish independently with voice chat unused. The camera should support a meaningful decision, not merely decorate an enforced countdown.

No final monster model, bespoke animation, cinematic, complex puzzle language, new economy/reward, full facility framework or production-level scene integration is required. Use a primitive threat proxy; do not move the vent-confined Crawler into the room or replace the Listener's separate noisy-repair objective.

## Shared versus personal state

| State | Prototype rule |
| --- | --- |
| Physical monitor selection | One shared selected camera per physical monitor. Two viewers do not see private camera choices on the same screen. |
| Door and power | Shared device state, observed consistently by occupants. |
| Lever manipulation | One accepted owner per held control; other controls stay available. Ownership has bounded release/expiry. |
| Tactile/pending feedback | Local presentation can acknowledge contact, but authoritative acceptance drives committed world indicators. |
| Completion | Each player traverses the route and activates their own endpoint. Another player cannot complete, reset or transport them. |
| Required access | Proposed prototype default: restored power and unlocked route latch for the shared sector generation; no teammate can re-lock the required passage or erase that access. |
| Re-entry/reset | Proposed prototype default: personal attempt resets on that player's new visit; shared state resets only after the sector is empty and a new generation begins. Joining an already-unlocked sector does not grant completion. |

The latch/reset rules are prototype defaults for testing, not established production-level progression. If a later encounter needs reversible power, timed access or personal authorization, propose it separately and prove it does not reintroduce compulsory co-op or trapping. No existing Level 1 personal-card/reset rule is changed here.

One-player support means one person in the existing session path. It does not silently add offline mode. Likewise, two-headset prototype results do not certify the intended ten-player room capacity.

## Implementation sequence

| Order | Issue | Deliverable | Dependency |
| --- | --- | --- | --- |
| 1 | [#68: networked walking skeleton](https://github.com/GregStephen/RunawayChimps/issues/68) | Isolated scene, device commands/current-state snapshot, power and route door; basic one-/two-actor smoke. | No prototype prerequisite; inherited hand wiring remains #64. |
| 2 | [#69: physical controls](https://github.com/GregStephen/RunawayChimps/issues/69) | Reachable buttons and lever, local-hand input, feedback and per-control ownership; early headset comfort check. | #68. |
| 3 | [#70: shared camera feeds](https://github.com/GregStephen/RunawayChimps/issues/70) | Two fixed views, one shared monitor, truthful status and bounded rendering; early profiling. | #68; #69 for physical selection acceptance. |
| 4 | [#71: complete encounter](https://github.com/GregStephen/RunawayChimps/issues/71) | Solo route, simple threat proxy, interruption recovery and independent personal endpoint. | #68, #69, #70. |
| 5 | [#72: network recovery](https://github.com/GregStephen/RunawayChimps/issues/72) | Contention, late arrival, controller loss, pause/rejoin and stale-state rejection tested on real actors. | #68, #69, #70, #71. |
| 6 | [#73: prototype acceptance](https://github.com/GregStephen/RunawayChimps/issues/73) | Uncoached solo observation, optional two-headset co-op, Quest 2/3 measurements and keep/rework/defer decision. | One integrated candidate from #68-#72. |

Each issue includes a first action, scope, acceptance checks and evidence requirements. Keep one implementation issue active. Targeted tests belong with each slice; #73 is not permission to defer all validation until the end. Use `Refs #...` while an issue's required acceptance is pending; a merged implementation is not an executed headset test.

The medium size labels describe bounded work categories, not time promises. Do not assume a release date, weekly hours, a separate QA team or purchased hardware. Split a substantial unrelated discovery into a linked defect rather than expanding one task indefinitely.

## Architecture constraints

Use the adopted **Unity 2022.3.62f3 (96770f904ca7)**, Photon PUN and Built-in rendering. Older 55f1 records remain historical. Do not change packages, render pipeline, XR settings or networking technology for this prototype.

The console issues device commands; it is not the independent source of truth for every door, power state or personal objective. Start with explicit desired-state actions rather than blind toggles. Identify devices, actors, room sessions, sector generations and state revisions; reject duplicates and obsolete requests. Keep a bounded current snapshot for arrivals and successor authority rather than depending on replaying an unlimited history.

The elected controller must have the prototype sector loaded. The Photon room Master Client may be elsewhere. Reuse existing applicable session/election patterns, but do not couple the feature to the Crawler controller. Client-side PUN arbitration is not a trusted dedicated server or an anti-cheat guarantee.

Inspect and adapt the existing `PhysicalButton` and local-hand filters. Do not convert the personal Hub keyboard/travel controls into shared inputs. #64 owns inherited missing-script/fingertip binding defects; repair them rather than weakening validation or creating another rig.

## Camera and VR cost boundaries

Each client renders the selected same-sector camera locally from shared game state. Do not stream video pixels or load remote sectors. Matching gameplay information matters; exact pixel/frame identity across clients is not promised.

Start with one selected feed rendered on a bounded schedule, with adjustable resolution/rate. #70 proposes 512 x 384 and 10-15 feed updates/second only as initial tuning candidates; headset rendering remains independent and measurements choose the final settings. Exclude recursive monitor surfaces, personal objective props, first-person overlays and misleading local-only illumination. No additional AudioListener is allowed.

Use large camera IDs and truthful unavailable/synchronizing feedback. Silent feeds are the prototype default; physical clicks are separate. Defer elaborate CRT effects, deceptive feeds, live camera audio, multiple monitors and pan/tilt controls until the core interaction and device budget are proven.

This system is distinct from the local cold-start terminal in PR #21 and recorded Hub surveillance work in PR #16. Neither is a dependency or permission to change those features.

## Acceptance and stop points

First prove a networked device change; then comfortable physical input; then useful, affordable camera information; then the full solo encounter. Stop to repair a failed gate rather than compensating with extra art or scares.

The final test uses Greg plus the available volunteer(s), not an assumed large QA group. Preserve the first uncoached solo observation before teaching the controls. Test two headsets both without voice communication and with optional coordination. Include authority outside the sector, concurrent inputs, pause/disconnect, late arrival and independent completion.

Record exact source/build/editor/device identity, actual discovered/executed tests, actor roles, expected/actual outcomes and profiler measurements. Reuse [the evidence guide](branch-testing-and-build-evidence.md) and #40's budget method. Missing Quest 2 or additional-headset evidence stays pending. Source checks, test doubles and local desktop clients do not establish headset comfort or full-capacity behavior.

A softlock, compulsory second operator, permanent control lock, progress-erasing teammate interaction, unusable VR controls or sustained performance failure blocks successful acceptance. Greg then chooses **keep**, **rework**, or **defer**. A keep result justifies a separate production/placement proposal; it does not automatically introduce the mechanic in Level 1, Level 2 or every level.

## Planning evidence

The current maintained documents, release issues and existing physical-button code were reviewed before creating #67-#73. All six work issues are assigned to GregStephen. This record adds planning and confirmed corrections only. No Unity import, test suite, Photon session, Android build, headset test or prototype gameplay implementation was completed during planning.
