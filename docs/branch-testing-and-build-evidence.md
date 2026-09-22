# Branch testing and build evidence

Prepared: 2026-09-21; reconciled: 2026-09-22. Tracker: [#28](https://github.com/GregStephen/RunawayChimps/issues/28). Status: documentation/templates prepared; end-to-end local build workflow and runtime acceptance pending.

Read [design and lore](design-and-lore.md), [repository improvement plan](repository-improvement-plan.md), [roadmap](mvp-roadmap.md), [source validation](ci-validation.md), and the candidate PR before testing. This guide organizes existing acceptance requirements; it neither changes gameplay nor replaces individual ticket gates.

## Current merged baseline and sequencing

Main `d12b403fd17b1792c585d3612a7c77a12caf690f` includes PRs #21/#23/#24/#54/#58. Test their combined implementation; do not wait for or repeat those merges. Use the exact candidate's 2022.3.55f1 editor declaration and current Source Integrity workflow, including the merged startup-floor and card/interaction checks.

The separate 2022.3.62f3 trial remains on hold until Greg confirms his chosen tests/merges are finished and agrees the starting baseline. This docs replacement does not create an upgrade branch or change editor/package pins. #28 preparation may proceed; Unity 6 feasibility remains separate #51, not a prerequisite to the first safe tester build.

## One record per tested revision

Copy [the test/build record](templates/test-build-record.md) into a local notes file or the relevant issue/PR comment. Record full commit SHA, branch/PR, actual editor, local modifications, device/runtime, artifact version/checksum when applicable, actor count, scenario and outcome. A branch name is not a stable build identifier. Record authored scene/prefab changes made during import/validation; do not silently test an uncommitted tree and attribute it to the clean commit.

Use **Not run**, **Blocked**, **Pass**, **Fail**, or **Not applicable with reason** per check. Empty fields and undiscovered tests are not passes. Record expected test discovery separately from executed/passed/failed/skipped counts. No single green summary may imply Unity or headset acceptance from source-only results.

A short pre-merge result is useful even on 55f1. It is historical branch evidence, not acceptance of the later merged/62f3 candidate. A PR merge also does not close #29-#33 automatically.

## Before a headset session

1. Save local work and identify the exact candidate. Use its own appropriate checkout/Library when changing editors. Do not discard uncommitted work or downgrade an upgraded working copy.
2. Read that revision's `.github/workflows/source-validation.yml`. Verify Source Integrity on the candidate SHA, including all branch-specific checks. The older `docs/ci-validation.md` overview is not a substitute for the actual workflow. Current main includes PR #23's managed/card/interaction checks and PR #24's startup-floor contracts; all remain required where applicable.
3. Import/compile in the declared Unity version. Record errors and run **Tools > Runaway Chimps > Run Reliability Regression Checks** and **Validate Sector Travel** as applicable. Start from saved scenes and inspect any resulting asset diffs; do not assume every Editor validator is read-only.
4. Use **Window > General > Test Runner > PlayMode** and run the complete `RunawayChimps.CardSystem.PlayModeTests` assembly in its supported empty test context, including both `CardSystemPlayModeTests` and `KeycardHandPhysicsPlayModeTests`. Do not filter only the first class and omit hand physics. The merged PR #23 records 81 authored cases (58 + 23); verify actual discovery/execution counts on the tested revision rather than assuming that count forever. Preserve XML and the Editor log. Missing required tests, skipped required cases, absent XML or zero-test discovery are blockers, not passes.
5. Start gameplay from `Assets/Scenes/Bootstrap.unity`. If import, compilation, required tests or safety checks fail, fix/report the failure before spending a headset session on dependent scenarios.

## Focused checks on the merged candidate

These are starting checklists, not replacements for each PR's full acceptance criteria. Numerical samples come from the roadmap tickets and do not guarantee absence of defects.

| Merged implementation / acceptance owner | First focused session | Failures to record immediately |
| --- | --- | --- |
| Startup floor, PR #24 / #29 | Initial 20 cold starts over at least two sessions: standing, crouched, hands low, recenter and ordinary idle. Then one Hub-Level 1 round trip and capture respawn. | Body-floor penetration, pinned hands/rig, unsafe launch, repeated floor-guard recovery, stabilization failure or unusable retry. |
| Card framework, PR #23 / #30 | Compile/discover/run the actual card suite and required Editor/source checks. Read the current PR's declared coverage; record observed counts, not a permanently hard-coded case count. | Missing tests, failed assertions, broken references, undiscovered assembly or exceptions. |
| Physical cards, PR #23 / #31 | After prerequisite safety/test checks, perform the ticket's 20 pickup/drop/re-grab cycles per hand; flat/rotated, hand switching, floor/wall/body proximity. Check 0/2 -> 1/2 -> 2/2, rejection, loss recovery, capture and re-entry. | Invisible barrier, shove/sticky grip, duplicate credit, wrong-lock credit, lost required card or a consumed card becoming usable again. |
| Launch/Hub slots, PR #21 / #32 | Use the current PR checklist for flat terminal readability, full black handoff, safe Hub reveal, room changes/rejoin and one avatar. Record concurrent slot/role tests separately when two actors are available. | Old terminal resurfacing, scene leakage, stale placement, duplicate avatar or unrecoverable loading. |

**Additional merged follow-ups:** preserve Greg's accepted fan rotation/trim/textures from #54, record blower audio failure in #56, and keep #52 card-grip/impact polish separate. Check the #58 vent landmarks on the actual headset revision; a merge is not visual/comfort acceptance. Run the authored `VentBlowerPlayModeTests` when validating the fan, separately from the card assembly; execution is not established by source inspection.

**Stop on unsafe physics or severe discomfort.** Do not keep repeating a hazardous reproduction to satisfy a numeric sample. Record the observed failure count and use [the defect template](templates/defect-report.md). An Editor-only run is not a headset run; two desktop actors are not two Quest devices.

## Remaining baseline and integrated acceptance

#27 validates the editor change against the agreed merged baseline. #28 then completes a repeatable local import/test/build procedure and real build-identification support. #29/#30/#31 collect or repeat applicable startup/card evidence; #32 owns regression on the exact combined candidate even when the underlying PRs are already merged. Do not integrate them a second time or erase useful isolated results.

#33 checks safe travel, capture and interruptions. Its starting matrix includes ten Hub-Level 1-Hub round trips and ten capture cycles, plus pause/network failure cases, stopping on safety failures. #34 then delivers an identifiable private build to the Quest 2 tester after its safe-build prerequisites. Do not wait for final shop art or Unity 6. #34 can prepare distribution instructions now, but delivery is not authorized by this document.

The approved new Chapter 1 ending belongs to #38. Until that change is actually present, the older completion route to Level 2 is an implementation gap, not automatically an editor-upgrade regression. Likewise, color changing is user-reported non-working; Coconut balances/daily updates are user-reported working and must not be reset to make tests convenient. The completion-earned cosmetic is desired future work, not a requirement for these checks.

## Build record and remaining #28 work

For every shared artifact, retain build/version, source SHA and dirty state, actual Unity revision, package manifest/lock hashes, Android toolchain versions, build target/options, artifact checksum and dated test results. Keep raw logs and signing/account details private; public reports use sanitized excerpts. Never commit tokens, player identifiers, private room codes, signing material, local usernames/paths or raw personal recordings. The template is a checklist, not automatic collection or an in-game diagnostics feature.

Still pending: executable local orchestration or an end-to-end proven menu procedure; a diagnostics-accessible build identifier; review of current README implementation/merge claims; actual Unity result/XML handling; a failing-run demonstration; and two complete fresh-output runs on the approved baseline. #28 remains open until those checks are performed. No Unity import, test, build, headset session or gameplay acceptance was executed when this packet was prepared.

Track only explicitly accepted low-impact defects in [the minor-issue register](templates/accepted-minor-issues.md). A safety, objective-loss, currency/ownership-loss, security or unrecoverable-session failure must not be relabeled minor to meet a release date.
