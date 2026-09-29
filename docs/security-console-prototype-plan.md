# Security console prototype plan

Recorded: 2026-09-29. Planning baseline: `main` at `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`. **Design direction confirmed; prototype work planned; no gameplay implementation or runtime validation supplied by this plan.**

[Issue #67](https://github.com/GregStephen/RunawayChimps/issues/67) owns the live work queue and acceptance status. This document records scope, decisions and test strategy, not a second completion checklist. Read [design and lore](design-and-lore.md), [repository improvement plan](repository-improvement-plan.md) and [AGENTS](../AGENTS.md) before implementation.

## Confirmed direction and corrections

Greg approved a **solo-first, co-op-enhanced** security-console prototype. Every required interaction must be completable by one player. Multiplayer may improve speed, optional division of work or situational awareness; a second operator, voice communication and simultaneous distant interactions are never required.

Use physical, in-world VR controls rather than a typing-heavy minigame or forced seat/view. Players can turn away and leave. Shared information and world consequences support concurrent use of different controls; ownership applies to an individually held control, not the entire station.

Do not assume Level 1 introduction or a terminal in every level. A rare set-piece is the working direction; exact production placement remains **Open**. This supersedes earlier brainstorming about teaching it in Level 1, using it every level, or requiring an operator to remain behind and relay information.

This optional prototype does not expand [the MVP contract](mvp-roadmap.md). The Chapter 1 ending and Coconut shop remain #38 and #50. Preserve Level 2's confirmed personal fuse/power objective. Do not assign the console to the Listener level automatically.

## Smallest useful prototype

The following are **proposed prototype defaults**, not final content or production progression decisions:

One saved, editable graybox station has two fixed same-sector cameras, one shared monitor, camera-selection buttons, a route-unlock button, one latching power lever, one route door, a small escape loop, a simple synchronized threat proxy and a personal endpoint beyond the door.

**Inspect route -> restore power -> unlock route -> judge a safe opening -> leave the station -> reach your own endpoint.**

A solo player acts sequentially. Co-op players can split actions or watch the route for one another, but can also finish independently without voice chat. The camera must inform a meaningful timing/route decision rather than decorate an enforced countdown. A player can leave, evade and return without losing required access.

Use a primitive threat proxy; do not build a new enemy framework, commission a monster, or move the vent-confined Crawler into this room. No cinematic, complex puzzle language, reward system or production scene integration is required.

## Shared versus personal state

| State | Prototype rule |
| --- | --- |
| Physical monitor | One shared selected camera per monitor. Viewers do not get different private selections on the same screen. |
| Door and power | Shared device state, observed consistently by occupants. |
| Held lever | One accepted owner, with bounded release/expiry; other controls stay usable. |
| Contact feedback | Local tactile/pending feedback is allowed; authoritative acceptance drives committed state indicators. |
| Completion | Each player traverses the route and activates their own endpoint. Another player cannot complete, reset or transport them. |
| Required access | Proposed default: restored power and route access latch for the shared sector generation. No teammate can re-lock the required passage or erase access. |
| Re-entry/reset | Proposed default: personal attempts reset on personal re-entry; shared state resets only after the sector is empty and a new generation starts. Late arrivals inherit device state, not completion. |

Latch/reset rules are testable defaults, not established production progression. A later reversible-power or timed-door proposal must independently prove it does not reintroduce compulsory co-op or trapping. Existing Level 1 personal-card/reset rules remain unchanged.

One-player support means one person using the existing session path, not a new offline-mode commitment. Two-headset results do not certify the intended ten-player room capacity.

## Work order

| Order | Issue | Deliverable | Dependencies |
| --- | --- | --- | --- |
| 1 | [#68: networked walking skeleton](https://github.com/GregStephen/RunawayChimps/issues/68) | Isolated scene, commands/current snapshot, power and route door; one-/two-actor smoke. | No prototype prerequisite; inherited hand wiring remains #64. |
| 2 | [#69: physical controls](https://github.com/GregStephen/RunawayChimps/issues/69) | Reachable buttons/lever, local-hand input, feedback, per-control ownership and early headset check. | #68. |
| 3 | [#70: shared cameras](https://github.com/GregStephen/RunawayChimps/issues/70) | Two views, one monitor, truthful status, bounded rendering and early profiling. | #68; #69 for physical selection acceptance. |
| 4 | [#71: solo encounter](https://github.com/GregStephen/RunawayChimps/issues/71) | Complete route, threat proxy, interruption recovery and personal endpoint. | #68, #69, #70. |
| 5 | [#72: network recovery](https://github.com/GregStephen/RunawayChimps/issues/72) | Contention, late arrival, controller loss, pause/rejoin and stale-state rejection. | #68, #69, #70, #71. |
| 6 | [#73: acceptance](https://github.com/GregStephen/RunawayChimps/issues/73) | Solo observation, optional two-headset co-op, Quest 2/3 measurements and keep/rework/defer decision. | One integrated candidate from #68-#72. |

Every work issue is assigned to GregStephen and includes a first action, bounded scope, acceptance checks and required evidence. Keep one implementation issue active. Add targeted tests with each slice; #73 is not permission to postpone all testing. Use `Refs #...` while required acceptance is pending, rather than automatically closing issues when code merges.

Medium size labels describe bounded work categories, not time promises. No release date, weekly capacity, extra team or purchased hardware is assumed. Split substantial unrelated discoveries into linked defects.

## Technical constraints

Use the adopted **Unity 2022.3.62f3 (96770f904ca7)**, Photon PUN and Built-in rendering. Older 55f1 evidence remains historical. Keep packages, XR settings, rendering and networking technology unchanged.

The console issues commands; devices own their state. Start with explicit desired-state actions, stable device IDs, room/sector generations and revisions. Reject duplicate/obsolete requests and keep a bounded current snapshot for arrivals and successor authority. The elected controller must have the sector loaded; the Photon room Master Client may be elsewhere. Reuse applicable session/election patterns without coupling to the Crawler. PUN client arbitration is not a trusted dedicated server or anti-cheat guarantee.

Adapt the existing PhysicalButton/local-hand path. Do not make the personal Hub keyboard/travel controls shared. #64 owns inherited missing-script/fingertip bindings: repair actual wiring, never weaken validators or add another rig to bypass it.

Render the selected same-sector camera locally from shared world state, not streamed pixels. Exact frame identity is not promised; matching gameplay information is required. Do not load remote sectors. Only the selected feed renders on a bounded schedule. #70's initial 512 x 384 and 10-15 feed updates/second are tuning candidates, not proven settings or a reduction in headset rendering rate.

Exclude recursive screens, personal objective props and first-person overlays. Ensure local-only headlamp differences do not hide essential shared information. Use large IDs and truthful unavailable/synchronizing feedback, no extra AudioListener, and explicit render-resource cleanup. Feed audio is silent initially; physical clicks are separate.

Keep this distinct from the launch terminal in PR #21 and recorded Hub surveillance in PR #16. Neither is a prerequisite or permission to change those features. Multiple monitors, pan/tilt, remote cameras, elaborate CRT effects, deceptive footage, live camera audio and final art remain deferred.

## Acceptance and decision

Prove a networked device change, then comfortable physical input, then useful affordable cameras, then the full solo encounter. Repair failed gates before adding art or scares.

Use Greg plus the available volunteer(s). Preserve one first uncoached solo observation before teaching controls. Test two actual headsets without voice communication and with optional coordination. Include concurrent inputs, authority outside the sector, pause/disconnect, late arrival and independent completion.

Record exact source/build/editor/device identity, actual test discovery/execution, actor roles, expected/actual outcomes and profiler measurements. Reuse [the evidence guide](branch-testing-and-build-evidence.md) and #40's budget method. Missing Quest 2 or second-headset evidence stays pending. Source checks, test doubles and local desktop actors are not headset or full-capacity evidence.

Softlocks, compulsory co-op, permanent control locks, progress-erasing teammate interactions, unusable VR controls or sustained performance failure block successful acceptance. Greg chooses **keep**, **rework** or **defer**. Keep justifies a separate production/placement proposal; it does not automatically add the feature to any level.

## Planning and documentation status

Current maintained documents, release issues and PhysicalButton source were reviewed before creating #67-#73. No prototype gameplay, Unity import/test, Photon session, Android build or headset/performance test was completed during planning.

**Overview synchronization remains pending.** The automated preparation write was blocked before creating a workflow. The companion `security-console-overview-updates.patch` prepares bounded changes for both maintained overviews and the design decision record against the inspected baseline. It is not an applied documentation update or a validation result. Review and apply it before merging the documentation PR; recheck contexts if main changes. No gameplay or release setting is changed by this plan.
