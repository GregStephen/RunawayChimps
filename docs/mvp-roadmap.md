# Runaway Chimps solo-developer MVP roadmap

Last updated: 2026-09-21. Execution tracker: [GitHub issue #25](https://github.com/GregStephen/RunawayChimps/issues/25).

Read [design and lore](design-and-lore.md), the [repository improvement plan](repository-improvement-plan.md) and [AGENTS](../AGENTS.md) before implementation. Those documents retain the detailed game rules and historical evidence; this document indexes the release work rather than creating a second independent design.

## Confirmed constraints and decision boundaries

Greg confirmed on September 21 that he is the sole developer and will be the only person implementing the game. He can most likely recruit one or two volunteer testers after he can deliver a build. He requested GitHub tickets and a double-check of every created ticket. There is no assumed separate QA, art, engineering or release team, no confirmed weekly hours, and no committed release date.

The earlier management assessment's large-beta/20-tester and weekly-capacity suggestions are not staffing commitments or prerequisites to private testing. Use Greg's local checks plus the available volunteers in repeated small sessions. Missing hardware, extra testers and unfamiliar first-time users remain evidence limitations, not presumed resources.

**Confirmed product-scope correction, September 21:** the first release includes one complete Hub + Level 1 Chapter 1 **plus a small Coconut cosmetic shop**. Greg confirmed that individual Coconut balances and the daily Coconut update/grant behavior are already working in his current runtime and wants the earned currency to have a launch use. Track the bounded shop in #50. Defer playable Level 2/Listener, the larger utility inventory, weekly/seasonal shop rotation and real-money purchases while preserving existing assets/design. Player-color changing is **not** currently a working release feature per Greg's runtime correction; source code for it exists, but do not treat it as validated or make it a launch dependency. Pricing for the game itself, exact device/platform support, audience, public/private release mode, permanent chapter progression and the chapter-ending rule remain explicit decisions in #26.

**Current technology and rules:** Unity 2022.3.55f1, Photon PUN and built-in rendering remain the recorded baseline. #27 investigates approved security remediation; this planning change does not update the editor or authorize Unity 6, networking or rendering migration. Keep two personal Amber cards and visit-local consumption rules. The intended ten-player room cap is unchanged; #45 must resolve full-capacity evidence or an explicitly approved alternative before public claims.

## Working method

One implementation item in progress; at most one other waiting on volunteer testing. Follow prerequisites rather than treating every P0 as work to start today. P0 identifies a foundation or release gate; P1 identifies planned MVP work. These are title priorities, not a statement that every ticket is a newly discovered critical defect.

Each issue contains a first action, dependencies, scope, acceptance criteria and evidence required to close it. If investigation reveals a large independent defect, create a linked bounded issue rather than expanding the original indefinitely. #35's local testing harness can be explicitly deferred if volunteers and the existing procedure are sufficient.

Use the project statuses precisely: Proposed, Confirmed, Planned, Open, Implemented, Pending validation and Validated. A merged PR is implementation, not acceptance. Use `Refs #...` while a validation issue still needs tests; do not automatically close it just because code merges. Record build/commit, editor, runtime/device, actor count, scenario, expected/actual outcome and sanitized supporting evidence. Update both maintained overview documents after confirmed decisions, meaningful implementation and actual test results.

## Ordered work index

The phase groups below are planning groups, not a claim that GitHub milestone or Project-board objects have been created. The original 23 work issues were created open and assigned to GregStephen; the September 21 confirmed shop-scope correction added #50 as a 24th work item. Live issue state is authoritative.

| Phase | Work item | Ticket |
| --- | --- | --- |
| A - baseline | 01. Confirm release scope and open product decisions | [#26](https://github.com/GregStephen/RunawayChimps/issues/26) |
| A - baseline | 02. Resolve Unity runtime security and approved baseline | [#27](https://github.com/GregStephen/RunawayChimps/issues/27) |
| A - baseline | 03. Repeatable local builds, Unity tests and evidence | [#28](https://github.com/GregStephen/RunawayChimps/issues/28) |
| B - safe tester build | 04. Validate startup floor correction, PR #24 | [#29](https://github.com/GregStephen/RunawayChimps/issues/29) |
| B - safe tester build | 05. Execute actual card Unity tests, PR #23 | [#30](https://github.com/GregStephen/RunawayChimps/issues/30) |
| B - safe tester build | 06. Physical card pickup/scan/recovery acceptance | [#31](https://github.com/GregStephen/RunawayChimps/issues/31) |
| B - safe tester build | 07. Integrate startup, Hub slots and cards | [#32](https://github.com/GregStephen/RunawayChimps/issues/32) |
| B - safe tester build | 08. Safe travel, capture and interruption recovery | [#33](https://github.com/GregStephen/RunawayChimps/issues/33) |
| B - safe tester build | 09. Deliver a private build to volunteers | [#34](https://github.com/GregStephen/RunawayChimps/issues/34) |
| C - complete chapter | 10. Minimal optional local two-client harness | [#35](https://github.com/GregStephen/RunawayChimps/issues/35) |
| C - complete chapter | 11. Crawler visual alignment and vent fit | [#36](https://github.com/GregStephen/RunawayChimps/issues/36) |
| C - complete chapter | 12. Pursuit, both safe routes and fair capture | [#37](https://github.com/GregStephen/RunawayChimps/issues/37) |
| C - complete chapter | 13. Approved ending and unfinished-route gating | [#38](https://github.com/GregStephen/RunawayChimps/issues/38) |
| D - player quality | 14. Two-headset multiplayer and session failures | [#39](https://github.com/GregStephen/RunawayChimps/issues/39) |
| D - player quality | 15. Target-device performance baseline | [#40](https://github.com/GregStephen/RunawayChimps/issues/40) |
| D - player quality | 16. Onboarding, comfort and readable controls | [#41](https://github.com/GregStephen/RunawayChimps/issues/41) |
| D - player quality | 17. Bounded environment/audio polish | [#42](https://github.com/GregStephen/RunawayChimps/issues/42) |
| D - player quality | Shop. Small fixed Coconut cosmetic shop | [#50](https://github.com/GregStephen/RunawayChimps/issues/50) |
| E - release readiness | 18. Minimum public-session social safety | [#43](https://github.com/GregStephen/RunawayChimps/issues/43) |
| E - release readiness | 19. Settings, account recovery and diagnostics | [#44](https://github.com/GregStephen/RunawayChimps/issues/44) |
| E - release readiness | 20. Rights, privacy and store readiness | [#46](https://github.com/GregStephen/RunawayChimps/issues/46) |
| E - release readiness | 21. Capacity claim versus available evidence | [#45](https://github.com/GregStephen/RunawayChimps/issues/45) |
| F - release | 22. Small closed beta and exact-candidate approval | [#47](https://github.com/GregStephen/RunawayChimps/issues/47) |
| F - release | 23. Release/hotfix procedure and approved publication | [#48](https://github.com/GregStephen/RunawayChimps/issues/48) |

Start with #26, then #27 and #28. After the baseline, #29 and #30 can be worked sequentially in either practical order; both precede #31, then #32, #33 and safe external delivery #34. The small launch shop in #50 can be developed after the repeatable build/test baseline is available and must pass account/ownership/release checks before #47. Distribution setup and account/asset-rights checks can begin early without waiting for final art. Each later ticket states its actual prerequisites. #35 is not a prerequisite to sending a headset build.

## Testing adapted to available people

1. **Solo source and Unity checks:** use the existing source contracts and execute the actual Unity tests. A source/managed-state pass is not a Unity or headset pass.
2. **Solo headset:** varied startup poses, both hands, card lifecycle, both routes, safe boundaries, capture, repeated travel and failure recovery. Record repeat counts and conditions rather than relying on one successful run.
3. **Local two-client development:** use the existing [multiplayer testing plan](multiplayer-development-testing.md). Distinct actual Photon actors in an Editor plus Development Build can assist protocol/state diagnosis; do not assume the planned desktop driver is already implemented.
4. **Greg plus one or two volunteers:** deliver an identifiable private build early, assign a small scenario per session, swap target/controller roles and repeat after fixes. Capture each person's first uncoached exposure during early testing; later revisits are not fresh first impressions. No separate new tester group is assumed at each milestone.
5. **Public release evidence:** validate the actual supported device list and claimed player capacity. Two/three-person sessions are useful but cannot certify ten-headset operation. #45 requires fuller evidence, an explicit tested lower-cap decision, or remaining private.

Initial numeric samples in individual tickets are practical first-pass targets, not a proof of zero bugs or replacements for final regression. Later integration owns its own retest: isolated startup/card acceptance can close before #32, and performance baseline #40 can close before the art recheck #42. Reopen the original defect if it regresses. The final candidate must pass affected checks again after risky changes.

## Release gate and scope control

No known critical/high-severity defects, all applicable acceptance checks passed on an identified candidate, and only explicitly accepted minor issues. A stuck/buried rig, unsafe shove, lost required card, broken personal completion, unrecoverable loading or serious service/security failure blocks release. Preserve true difficulty while removing technical unfairness.

#47 approves the exact build and evidence, not publication. #48 requires Greg's explicit publication approval and checks the actual release, with a rehearsed hotfix/update path and practical support ownership. Do not promise instant store rollback, new paid infrastructure, automatic monitoring or an unstaffed support team.

## September 21 evidence and backlog review

Repository baseline read: main `f9edea67af08ff7c35d043e05ad66a5615a9aeb2`. At review, PR #24 (`52223e9`) and PR #23 (`d12d8b6`) remained open/draft; PR #21 (`cf6f7f6`) remained open. Their implementation is reused by the tickets rather than duplicated. Recheck live PR state/head before implementation; these are dated observations.

The issue review checked scope, priorities, dependencies, existing-PR references, sole ownership, acceptance/evidence boundaries and the distinction between small human samples and full-capacity proof. It clarified isolated-versus-integrated closure in #29/#31, performance baseline versus later regression in #40, the volunteer dependency in #37 and reuse of first-exposure observations in #41. Later on September 21, Greg corrected the launch scope to include a small Coconut cosmetic shop and reported player-color changing as non-working; #50 and the affected release tickets were updated accordingly.

**Planning evidence only:** tickets and documentation do not implement gameplay or establish any new Unity, Photon, headset, performance or release pass. The maintained docs and current issue comments must carry actual results when those checks are executed.
