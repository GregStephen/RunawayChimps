# Runaway Chimps solo-developer MVP roadmap

Last updated: 2026-09-22. Execution tracker: [GitHub issue #25](https://github.com/GregStephen/RunawayChimps/issues/25).

Read [design and lore](design-and-lore.md), the [repository improvement plan](repository-improvement-plan.md) and [AGENTS](../AGENTS.md) before implementation. Those documents retain the detailed game rules and historical evidence; this document indexes the release work rather than creating a second independent design.

**Authority:** issue #25 and its linked work issues own live ticket status, acceptance and dependencies. This document keeps the confirmed release contract and high-level phase navigation; it is not a second completion checklist.

## Confirmed constraints and decision boundaries

Greg confirmed on September 21 that he is the sole developer and will be the only person implementing the game. He can most likely recruit one or two volunteer testers after he can deliver a build. He requested GitHub tickets and a double-check of every created ticket. There is no assumed separate QA, art, engineering or release team, no confirmed weekly hours, and no committed release date.

The earlier management assessment's large-beta/20-tester and weekly-capacity suggestions are not staffing commitments or prerequisites to private testing. Use Greg's local checks plus the available volunteers in repeated small sessions. Missing hardware, extra testers and unfamiliar first-time users remain evidence limitations, not presumed resources.

**Confirmed product-scope correction, September 21:** the first release includes one complete Hub + Level 1 Chapter 1 **plus a small Coconut cosmetic shop**. Greg confirmed that individual Coconut balances and the daily Coconut update/grant behavior are already working in his current runtime and wants the earned currency to have a launch use. Track the bounded shop in #50. Defer playable Level 2/Listener, the larger utility inventory, weekly/seasonal shop rotation and real-money purchases while preserving existing assets/design. Player-color changing is **not** currently a working release feature per Greg's runtime correction; source code for it exists, but do not treat it as validated or make it a launch dependency. The first release is confirmed **free** and **public**, with private pre-release testing followed by a public launch that supports both public matchmaking and private rooms. The initial supported platform is **standalone Quest**, targeting **Quest 2 and Quest 3**; Greg has a Quest 3 and a likely tester has a Quest 2, so Quest 2 is the lower-performance acceptance device. PC VR/Link is not an initial-release support requirement. Preserve the current teen/adult horror direction rather than toning down the Crawler, blood/damage or regeneration-lab content preemptively.


**Confirmed Chapter 1 ending and persistence:** after the second required Amber card is accepted, give a brief completion acknowledgment, fade, and return only that player to Security. Other players continue their own runs. A new Level 1 visit begins fresh visit-local card/objective state. Coconuts, purchased/equipped cosmetics, player name and supported settings may persist. No permanent Chapter 1 completion/progression record is required for MVP. Greg would like Level 1 completion to eventually award a cosmetic, but the exact reward is undecided and is **proposed future work**, not a release blocker.

**Current technology and rules:** this isolated branch declares Unity 2022.3.62f3 (96770f904ca7), with Photon PUN and built-in rendering unchanged. Main at the trial fork `9da4a1e` remains 55f1. This is not a validated baseline or a Unity 6 migration. Preserve the two personal Amber cards, visit-local consumption and ten-player target; #45 still owns capacity evidence.

**#27 investigation, September 21:** Greg approved **Unity 2022.3.62f3 (`96770f904ca7`)** as the isolated compatibility candidate, replacing the earlier 62f2 suggestion. The [Unity runtime security review](unity-runtime-security-review.md) corrects the overbroad Quest vulnerability assumption, records the ordinary 2022.3 support-lifecycle limitation and unresolved exact Meta/XR compatibility, and provides installation, validation and rollback steps. Main at the fork remains pinned to 2022.3.55f1; the isolated branch now declares 62f3 in both ProjectVersion and CI. Unity/build/headset evidence is still pending in #27.

## Current merged baseline and sequencing

Reviewed main: `d12b403fd17b1792c585d3612a7c77a12caf690f`, September 22. PRs #21 (launch/Hub slots), #23 (keycards), #24 (startup floor/reconnect), #54 (fan rotation/materials) and #58 (vent landmarks) are merged. Preserve these implementations; no repeat integration is needed. See the maintained overviews for their dated evidence and pending runtime checks. Greg accepted basic card handling and the fan visuals; the final card-query quiet-console retest and broader startup/card/Photon/headset acceptance are not automatically passed. Fan audio remains #56 and card grip/impact polish remains #52.

**Engine hold lifted, September 22:** Greg explicitly requested the compatibility branch now. `compat/unity-2022.3.62f3` starts from live main `9da4a1e46faa16484af9425edb41517a5a931a75`, with the editor/CI declaration updated and runtime acceptance pending. This supersedes the earlier wait instruction, not the missing test evidence. Use [the trial guide](unity-2022.3.62f3-compatibility.md); no merge or public distribution is authorized by branch creation.

**Preparation available for #28:** the [testing guide](branch-testing-and-build-evidence.md), [test/build record](templates/test-build-record.md), [defect report](templates/defect-report.md) and [minor-issue register](templates/accepted-minor-issues.md) are maintained here after reconciliation of PR #49. These are copyable, unfilled documentation, not an executable build pipeline, diagnostics feature or executed test results.

With the isolated trial authorized from `9da4a1e`, #27 owns editor compatibility acceptance and #28 the proven local procedure/build identity. Gather remaining startup/card evidence in #29/#30/#31; #32 owns exact-combined-build regression, not re-merging existing PRs. #33 covers travel/capture/interruption acceptance before safe private distribution in #34. Shop art and Unity 6 are not prerequisites to the first safe tester build. The approved Chapter 1-to-Security ending is still implementation work in #38.

## Working method

One implementation item in progress; at most one other waiting on volunteer testing. Follow prerequisites rather than treating every P0 as work to start today. P0 identifies a foundation or release gate; P1 identifies planned MVP work. These are title priorities, not a statement that every ticket is a newly discovered critical defect.

Each issue contains a first action, dependencies, scope, acceptance criteria and evidence required to close it. If investigation reveals a large independent defect, create a linked bounded issue rather than expanding the original indefinitely. #35's local testing harness can be explicitly deferred if volunteers and the existing procedure are sufficient.

Use the project statuses precisely: Proposed, Confirmed, Planned, Open, Implemented, Pending validation and Validated. A merged PR is implementation, not acceptance. Use `Refs #...` while a validation issue still needs tests; do not automatically close it just because code merges. Record build/commit, editor, runtime/device, actor count, scenario, expected/actual outcome and sanitized supporting evidence. Update both maintained overview documents after confirmed decisions, meaningful implementation and actual test results.

## Phase navigation

These are release-planning groups, not GitHub milestone/Project objects or duplicated live status. The original 23 work issues plus #50 and #51 form the 25-item roadmap; consult issue #25 and each linked ticket for current state and full acceptance criteria.

| Phase | Intended outcome | Work tickets |
| --- | --- | --- |
| A - baseline | Confirm scope; approve the isolated editor baseline; make local builds/tests repeatable. | [#26](https://github.com/GregStephen/RunawayChimps/issues/26), [#27](https://github.com/GregStephen/RunawayChimps/issues/27), [#28](https://github.com/GregStephen/RunawayChimps/issues/28) |
| B - safe tester build | Validate merged startup/cards, combined behavior, travel and safe volunteer delivery. | [#29](https://github.com/GregStephen/RunawayChimps/issues/29), [#30](https://github.com/GregStephen/RunawayChimps/issues/30), [#31](https://github.com/GregStephen/RunawayChimps/issues/31), [#32](https://github.com/GregStephen/RunawayChimps/issues/32), [#33](https://github.com/GregStephen/RunawayChimps/issues/33), [#34](https://github.com/GregStephen/RunawayChimps/issues/34) |
| C - complete chapter | Optional local-client harness; Crawler/vent/pursuit acceptance; approved chapter ending. | [#35](https://github.com/GregStephen/RunawayChimps/issues/35), [#36](https://github.com/GregStephen/RunawayChimps/issues/36), [#37](https://github.com/GregStephen/RunawayChimps/issues/37), [#38](https://github.com/GregStephen/RunawayChimps/issues/38) |
| D - player quality | Multiplayer, target-device performance, onboarding, bounded art/audio and the small Coconut shop. | [#39](https://github.com/GregStephen/RunawayChimps/issues/39), [#40](https://github.com/GregStephen/RunawayChimps/issues/40), [#41](https://github.com/GregStephen/RunawayChimps/issues/41), [#42](https://github.com/GregStephen/RunawayChimps/issues/42), [#50](https://github.com/GregStephen/RunawayChimps/issues/50) |
| E - release readiness | Public-session safety, settings/accounts, rights/privacy/store readiness and supported capacity evidence. | [#43](https://github.com/GregStephen/RunawayChimps/issues/43), [#44](https://github.com/GregStephen/RunawayChimps/issues/44), [#46](https://github.com/GregStephen/RunawayChimps/issues/46), [#45](https://github.com/GregStephen/RunawayChimps/issues/45) |
| F - release | Small closed beta, exact-candidate acceptance and explicitly approved publication/hotfix procedure. | [#47](https://github.com/GregStephen/RunawayChimps/issues/47), [#48](https://github.com/GregStephen/RunawayChimps/issues/48) |
| Separate investigation | Bounded Unity 6 feasibility after the known 2022.3 baseline; not a production migration commitment. | [#51](https://github.com/GregStephen/RunawayChimps/issues/51) |

#29 and #30 can run sequentially in either practical order; both precede #31. #32 retains combined-candidate regression responsibility. #35 does not block #34. Prepare distribution and rights/account work early, without sharing an unsafe build. #50 follows the repeatable build/test baseline and must pass ownership/release checks before #47. #51 is non-blocking unless #27 identifies a concrete release blocker.

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

Historical September 21 baseline: main `f9edea67af08ff7c35d043e05ad66a5615a9aeb2`. The September 22 merged snapshot above supersedes these PR-state observations, not the underlying test evidence. At review, PR #24 (`52223e9`) and PR #23 (`d12d8b6`) remained open/draft; PR #21 (`cf6f7f6`) remained open. Their implementation is reused by the tickets rather than duplicated. Recheck live PR state/head before implementation; these are dated observations.

The issue review checked scope, priorities, dependencies, existing-PR references, sole ownership, acceptance/evidence boundaries and the distinction between small human samples and full-capacity proof. It clarified isolated-versus-integrated closure in #29/#31, performance baseline versus later regression in #40, the volunteer dependency in #37 and reuse of first-exposure observations in #41. Later on September 21, Greg corrected the launch scope to include a small Coconut cosmetic shop and reported player-color changing as non-working; #50 and the affected release tickets were updated accordingly.

**Planning evidence only:** tickets and documentation do not implement gameplay or establish any new Unity, Photon, headset, performance or release pass. The maintained docs and current issue comments must carry actual results when those checks are executed.
