# Unity runtime security and baseline review

Reviewed: 2026-09-21. Tracker: [MVP-02, issue #27](https://github.com/GregStephen/RunawayChimps/issues/27). Status: **Unity 2022.3.62f3 compatibility candidate approved by Greg; implementation and runtime validation pending.**

Read [design and lore](design-and-lore.md), [repository improvement plan](repository-improvement-plan.md), and [MVP roadmap](mvp-roadmap.md) before changing project behavior. This is a technical review, not a new release-scope decision.

**September 22 documentation reconciliation:** current main is `d12b403fd17b1792c585d3612a7c77a12caf690f`, with PRs #21/#23/#24/#54/#58 already merged. The September 21 advisory/package investigation below is preserved as dated evidence, not a newly performed security or platform-certification review. The 62f3 trial still awaits Greg's confirmation of the finished tests/merges and exact starting baseline. This cleanup starts no upgrade. Current ProjectVersion and Source Integrity still declare 2022.3.55f1; no Unity/Android/headset validation is supplied here.

## Current project and authority boundary

Historical September 21 source reviewed on main `f9edea67af08ff7c35d043e05ad66a5615a9aeb2`; confirmed release decisions read from `docs/solo-mvp-roadmap` at `cf284ee6674a328fae75942f8a9530e6bc29001c` / PR #49 (historical documentation source). The target is standalone Quest 2 and Quest 3; the Windows second-client harness remains development-only.

`ProjectSettings/ProjectVersion.txt` still pins `2022.3.55f1 (9f374180d209)`. `.github/workflows/source-validation.yml` independently enforces `2022.3.55f1`. No editor pin, package, scene, renderer, networking implementation, or CI enforcement is changed by this review. Greg explicitly approved the **2022.3.62f3 (`96770f904ca7`)** isolated compatibility candidate on September 21. Actual compatibility evidence is still required before adopting it as the tested build baseline.

## Security applicability correction

The [Unity CVE-2025-59489 advisory](https://unity.com/security/sept-2025-01) identifies a native/managed loading vulnerability and lists `2022.3.62f2` as the first fixed ordinary 2022.3 release. The existing 55f1 pin predates that fix.

The more specific [Unity remediation FAQ](https://unity.com/security/sept-2025-01/remediation) explicitly includes **Quest** among platforms for which Unity has no findings suggesting this vulnerability is exploitable. This corrects the earlier implication that a Quest build is proven exploitable simply because it targets Android. It does not prove immunity or remove the benefit of rebuilding with a patched runtime. Windows development players require the applicable remediation too.

Unity's published [security index](https://unity.com/security) was reviewed. No newer applicable Editor advisory was identified there in this review; that is a bounded search result, not a guarantee that no other engine/package vulnerability exists. Recheck before final distribution.

## Proposed smallest practical candidate

**Approved candidate:** Unity **2022.3.62f3**, official changeset `96770f904ca7`, for an isolated compatibility test. This supersedes the earlier suggestion to test 62f2 first, not the current approved 55f1 project pin.

[62f2 release notes](https://unity.com/releases/editor/whats-new/2022.3.62f2) identify the security correction. [62f3 release notes](https://unity.com/releases/editor/whats-new/2022.3.62f3), dated October 28, 2025, describe the subsequent serialized-Inspector-field and 2D collision-callback memory-leak fixes. These are not a diagnosis of this project's 3D Gorilla floor/card defects.

This is a deliberately narrow 2022.3 candidate, **not** a claim that 62f3 is the newest Unity editor, the newest Enterprise 2022.3 build, or a currently fully supported long-term release. Unity's [official June 2026 Build Automation notice](https://discussions.unity.com/t/unity-devops-build-automation-2026-dependency-deprecation-cycle/1724029) records ordinary 2022.3 LTS expiry in May 2025 while retaining 62f3 as a Build Automation exception. Availability of an editor is not continuing security maintenance. Before public release, record the legacy-support risk and verify store/platform requirements; change to a maintained major version only by a separate approved decision if needed.

Unity also provides an application patcher for built artifacts in the remediation guide. Rebuilding from this available source is the preferred candidate path; adding a mandatory post-build binary-patching/signing pipeline is not selected here. Do not change Photon PUN or move to Unity 6/URP as an incidental part of this investigation. Greg separately approved **#51**, a bounded Unity 6 feasibility study after a known 2022.3 baseline exists; that ticket is evidence gathering only and does not authorize production migration.

## Package and platform review

The September 21 reviewed manifest pins Meta XR All-in-One **83.0.1**, XR Interaction Toolkit **2.6.4**, OpenXR **1.13.2**, XR Management **4.5.1**, Animation Rigging **1.2.1**, and AI Navigation **1.1.7**. The lockfile records the Meta Core/Platform/Interaction packages at 83.0.1. Source blob IDs: manifest `79ca59345ce898b890eaaea0cfd56153008ed651`; lockfile `c05494792ba89477f794e1f4315aead8fb48b239`. Preserve both files initially. The project contract remains the built-in renderer even though the URP package is installed.

Unity's versioned [XRI 2.6 documentation](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.6/manual/index.html) and [OpenXR 1.13 documentation](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.13/manual/index.html) list Unity 2021.3 or later compatibility. This supports trying the 2022.3 patch without upgrading either package; it does not prove the complete project imports or runs.

**Unresolved compatibility/support check:** Meta's [current setup guide](https://developers.meta.com/horizon/documentation/unity/unity-project-setup/), updated September 9, 2026, targets Unity 6 with newer OpenXR guidance. The retrieved [Meta Core v83 release notes](https://developers.meta.com/horizon/downloads/package/meta-xr-core-sdk/83.0/) do not establish support for this exact 83.0.1 / 2022.3 / OpenXR 1.13.2 combination. Inspect installed package metadata and the actual features in use, run Meta/OpenXR validation, and obtain build/headset evidence. Do not call the combination vendor-certified or infer a store rejection solely from a new-project setup guide. Do not automatically upgrade or downgrade packages to follow that guide.

**September 22 source identity:** the reconciled main manifest blob is `79ca59345ce898b890eaaea0cfd56153008ed651` and lockfile blob is `c05494792ba89477f794e1f4315aead8fb48b239`. This records source identity only; the exact installed Meta/XR configuration still requires the compatibility and runtime checks below.

## Controlled implementation plan after version approval

1. After Greg confirms his chosen tests/merges are complete and agrees the exact baseline, recheck main and create the isolated 62f3 trial from that SHA. Version approval is not public-release certification and does not lift the sequencing hold. PRs #21/#23/#24 are already merged; preserve their combined implementation and pending acceptance instead of merging them again.
2. Keep the original working checkout/editor installation intact. Use a separate checkout with its own Library/output directory. Save/commit local work first; do not hard-reset, clean away assets, or reuse an upgraded Library with the old editor.
3. Install 62f3 alongside 55f1, including Android Build Support, Android SDK & NDK Tools, and OpenJDK. Unity's [2022.3 dependency reference](https://docs.unity3d.com/2022.3/Documentation/Manual/android-supported-dependency-versions.html) lists NDK r23b (23.1.7779620) and JDK 11. Use the Hub-provided tools, and record actual SDK/build-tools/JDK/NDK/Gradle versions from the installed editor/build log. Do not infer Greg's installed toolchain from this reference or confuse SDK build-tools versions with the store target-API requirement.
4. Update both ProjectVersion lines coherently to `2022.3.62f3 (96770f904ca7)` and intentionally update the exact CI version guard. Update current AGENTS/README/build instructions and both maintained overviews while preserving historical test-version evidence. Inspect other validators/version references rather than globally rewriting history or accepting arbitrary editor versions.
5. Import in the selected editor. Preserve the package manifest/lock and renderer/input/network settings initially. Review any importer/lockfile changes; unexpected package upgrades are a blocker to investigate, not incidental cleanup. Run source checks and available Unity/Editor tests with logs. PR #23's card tests are now on main. Run the full `RunawayChimps.CardSystem.PlayModeTests` assembly, including card-state and hand-physics fixtures; absent or undiscovered required tests are blockers.
6. Build an identifiable standalone Quest player. On Quest 3, check cold startup, both controller hands, ordinary movement, existing card pickup/drop/scan, Crawler animation/materials, one Hub-Level 1 round trip, capture, pause/resume, and Coconut balance/display without changing grant rules. Compare against known defects on the same pre-upgrade code revision. The newly approved return-to-Security chapter ending is still separate implementation in #38; do not mistake the old code route for an editor regression.
7. Have the available Quest 2 tester run the same install/startup/basic-play smoke when a safe build is deliverable. Full two-headset/performance/capacity acceptance stays in #39/#40/#45; do not silently make unavailable early volunteer access a new circular prerequisite to #34.
8. Record commit, editor revision, package blobs, build version/hash, actual tests, errors, and rollback. Retire affected Windows/unreviewed artifacts from distribution only through an identified replacement; no old downloads have been removed by this review.

## Completion and pending evidence

Completed here: live repository/configuration inspection, official advisory/release/package-document review, candidate selection recommendation, and a documented implementation/rollback plan.

Not completed: installation/license verification, the editor/CI pin change, Unity compilation, package compatibility validation, Android build/signing, headset testing, performance measurement, artifact replacement, or any public submission. Issue #27 remains open.
