# Unity 2022.3.62f3 compatibility and adoption record

Prepared: 2026-09-22. Tracker: [#27](https://github.com/GregStephen/RunawayChimps/issues/27). Branch: `compat/unity-2022.3.62f3`. Status: **merged to main as the editor baseline** after clean import/playthrough and passing Reliability Regression Checks; Android/Quest/package acceptance remains pending.

## Exact comparison baseline

Fork: `9da4a1e46faa16484af9425edb41517a5a931a75`, the live main checked after Greg explicitly requested creation now. This lifts the earlier hold without claiming his unreported tests passed. The baseline declares `2022.3.55f1 (9f374180d209)`; the trial declares `2022.3.62f3 (96770f904ca7)`. Record the actual trial head with `git rev-parse HEAD` in every result; the fork SHA is not the candidate head.

Only ProjectVersion, its exact CI guard and documentation change. Packages, source gameplay, scenes/prefabs, authored vent landmarks, Photon PUN, built-in rendering and XR/OpenXR settings remain unchanged. Manifest blob: `79ca59345ce898b890eaaea0cfd56153008ed651`; lockfile blob: `c05494792ba89477f794e1f4315aead8fb48b239`. Preserve both, and review unexpected package/importer changes rather than accepting them automatically.

## Safe local setup

Close Unity and save/commit local work. Keep the original 55f1 checkout and editor intact. Use a new worktree with a separate Library/output directory, or a fresh clone; do not reuse the old Library or copy generated caches.

From the existing repository, when the example local branch and destination do not already exist:

```sh
git fetch origin
git worktree add --track -b trial/unity-62f3 ../RunawayChimps-62f3 origin/compat/unity-2022.3.62f3
```

If Git reports an existing destination/branch, choose another unused name; do not delete local work or force checkout to clear it. Add the new folder in Unity Hub and select 2022.3.62f3 explicitly.

The [official 62f3 release page](https://unity.com/releases/editor/whats-new/2022.3.62f3) confirms revision `96770f904ca7` and provides installers. Install Android Build Support with Android SDK & NDK Tools and OpenJDK. Use that editor's installed tools initially and check their actual locations/versions before building. Unity's [2022.3 dependency reference](https://docs.unity3d.com/2022.3/Documentation/Manual/android-supported-dependency-versions.html) lists NDK r23b (23.1.7779620) and JDK 11; do not substitute Unity 6's tools. Record actual SDK/build-tools, NDK, JDK and Gradle versions from the installation/build log. Module download or missing-tool failures are blockers to report, not reasons to silently alter packages/settings.

## Import, tests and build

1. Record clean/dirty state and exact source SHA. Import in 62f3; save the Editor log and all compile errors/warnings. Inspect `git status` and `git diff`, especially manifests, scenes, prefabs and XR settings. Do not blindly commit a full reserialization or allow importer changes to hide in the trial.
2. Confirm Source Integrity is green on the candidate SHA. Run the existing Reliability Regression and Sector Travel Editor checks from saved scenes. Record any intentional authoring changes. Run the complete `RunawayChimps.CardSystem.PlayModeTests` assembly, including card-system and hand-physics fixtures, and applicable fan tests. The baseline has 81 authored card cases, not 81 proved passes. Preserve XML/logs and actual discovered/executed/pass/fail/skip counts; missing required tests are not success.
3. Inspect installed Meta XR/OpenXR package metadata and actual project validation results. Do not auto-apply package upgrades, render-pipeline conversions or XR-setting fixes. The exact existing Meta XR 83.0.1/OpenXR 1.13.2 combination still needs evidence; declaration changes alone do not prove support.
4. Build an identifiable standalone Quest Android player using the existing project settings. Record build version, full source SHA, editor revision, package/toolchain identities and APK SHA-256. Keep signing material, user paths and raw personal/account logs private. This is a manual acceptance checklist, not completed #28 build automation.

## Headset acceptance still to run

| Check | Required observation | Current evidence |
| --- | --- | --- |
| Quest 3 cold start | Repeated standing/crouching/low-hand/recenter starts, stable floor/locomotion, quiet loading, readable terminal, one correctly placed avatar. Stop on unsafe physics. | Not run on 62f3 |
| Both hands and keycards | Close pickup/drop/re-grab/nudges, floor/wall contact, no direction-normalization assertion, personal 0/2 -> 1/2 -> 2/2 consumption and recovery. | Not run on 62f3 |
| Crawler and environment | Animation/materials, vent fit, safe rooms, capture/respawn, accepted fan visuals and authored landmarks retained. | Not run on 62f3 |
| Travel/session lifecycle | Hub-Level 1 round trip, capture, pause/resume, interruption/rejoin and no duplicate rig or unrecoverable loading. | Not run on 62f3 |
| Economy regression | Existing Coconut balance/display/daily behavior retained; do not reset accounts or grants for convenience. | Not run on 62f3 |
| Quest 2 follow-up | Safe build installs/starts and completes the same basic smoke with the available tester. Full multiplayer/performance/capacity stays #39/#40/#45. | Not run on 62f3 |

Compare a suspected regression to the same fork in 55f1, using a separate baseline checkout. Existing unresolved startup/quiet-console findings, #52 grip/impact polish and #56 inaudible fan audio remain recorded; an editor change does not fix them by declaration. The old Level 1-to-Level 2 route remains until #38 implements the approved return-to-Security ending. The shop remains #50. Preserve those distinctions.

## Evidence and rollback

Copy [the test/build record](templates/test-build-record.md) per tested revision. Record blocked/unrun checks honestly. The [security review](unity-runtime-security-review.md) retains dated advisory/support limitations; this trial is not a claim that 62f3 is the newest supported editor, vendor-certified for this configuration or a public-release certification. Recheck applicable requirements before distribution.

If a new regression is suspected, retain logs/diffs and compare against the preserved 55f1 pre-upgrade fork in a separate checkout rather than downgrading an upgraded Library in place. Because 62f3 is now on main, any rollback must be an explicit repository decision supported by evidence. #27 stays open for remaining Android/Quest/package acceptance; public distribution still requires the later release gates.
