# Security-system boot prototype - Option A

Date: 2026-09-13. Branch: `codex/security-boot-prototype`, based on `main` commit `05ea14f4af038296eec23f69de12aa9b26985b13`. Draft PR **#20**. Unity **2022.3.55f1**, Photon PUN. This is a testable visual direction, not final art approval or a merged feature.

## Decision and correction record

**Confirmed for prototyping:** Greg requested a test branch for Option A, the facility security-system boot. The floating idea is **Proposed, not selected or implemented**; do not combine it with this prototype or treat the earlier assistant preference as Greg's approval.

**Confirmed travel correction:** normal Hub/level travel should stay completely black, without terminal, logo, tips or loading text. This supersedes the earlier visible-Loading-UI travel presentation. Reconsider a minimal indicator only after measured transitions approach roughly **10 seconds**, not as an implemented timer. Real error/recovery feedback must remain visible.

**Implemented on this branch, pending runtime validation:** a green/amber framed facility terminal, Runaway Chimps title treatment, five real prerequisite indicators, a slow activity cursor, optional quiet relay ticks and a short black handoff into the Hub. No camera motion, strobe, animated scanline shader, fake percent complete, surveillance feed or new Photon behavior is introduced.

| Terminal row | Actual source |
| --- | --- |
| FACILITY LINK | `PhotonNetwork.InRoom` |
| SECURITY SECTOR | The configured Hub scene is loaded |
| ARRIVAL ALIGNMENT | `AppState.RigSnapped` |
| AVATAR LINK | `AppState.PhotonPlayerSpawned` |
| VISUAL SYSTEMS | `AppState.PlayerVisualsReady` |

The raw startup/error message remains beneath the diagnostics. Retry uses either controller trigger or desktop **R**. Decorative text does not claim that containment or monsters have been simulated. A surveillance-feed finish remains **Proposed, not implemented**.

## Try it

1. Check out `codex/security-boot-prototype` and open the project in Unity **2022.3.55f1**. Allow scripts to import and resolve Console compiler errors before Play.
2. Open **Assets/Scenes/Bootstrap.unity** and press Play. No prefab hookup, Editor migration or new Build Settings entry is required. LoadingFlow installs the presentation on its existing serialized Loading canvas at runtime.
3. For a longer desktop look, enable **Tools > Runaway Chimps > Hold Security Boot For Review** before Play. Start from Bootstrap; once ready, the terminal explicitly says it is held for review. Toggle the menu off to enter the Hub. This hold never bypasses networking/readiness and is compiled out of player builds. It persists only for the current Editor session. Prefer desktop inspection while held; this is not a new playable room or locomotion area.
4. Turn review hold off and repeat ordinary Hub/Level 1/Level 2 travel. Successful transfers should remain black and never show the startup terminal or old tips. Existing travel errors and recovery remain intact.

For tuning, open **Assets/Scenes/Loading.unity**, select **LoadingFlow**, and edit `Minimum Intro Seconds` (default **1.5**, range **0-3**) or `Play Boot Sounds` (default on, low volume). Save the scene only when intentionally changing prototype settings, then test from **Bootstrap**, not by starting Loading alone. Zero minimum still retains two short 0.2-second fades. The presentation cannot finish before actual readiness. Slow startup retains the existing 90-second timeout and retry flow, not a simulated progress timer.

## Ownership and implementation limits

`SecurityBootPresentation` is local and owned by the existing Loading scene. It reuses the authored TMP font, canvas, background and 12% XR overscan. It does not create a second XR rig, alter head/controller transforms, network the intro, or change room selection. Startup temporarily uses the persistent camera's UI-only culling mask and black clear to hide partially initialized world geometry. Its original culling mask, clear flags and background are restored for Hub reveal and on presentation destruction. Interrupted entry reopens the terminal rather than hiding an error behind a half-faded screen. Installation and scene-root discovery run in Start, after scene Awake/OnEnable initialization.

The native **Quest/Meta system splash is not implemented or configured by this branch**. Unity splash/logo/license settings and XR package versions are unchanged. This prototype starts after Unity renders the Loading scene. Final branded splash artwork and platform/build validation remain separate planned work. The title is a prototype treatment, not an approved new logo asset.

The old serialized text/UI remain in Loading for fallback/source compatibility; their TMP renderers and LoadingDebugText are disabled at runtime when the presentation installs. The view and generated sound clip are destroyed when Loading unloads. During ordinary travel the installer builds no terminal or sound source and retains the black background/overscan. The existing SectorTravelService, scene load, rig freeze/settle/recovery, fade sequence and Photon lifecycle are unchanged.

## Validation record

**Validated source only, 2026-09-13:** both Source integrity push run `34792159108` and PR run `34792189121` passed for code commit `74d42b7eb409c8c12b9751443cf9b77195fb35da`. These include the Unity-version declaration, 125-script/5-scene source and reference checks, C# syntax parsing, repository metadata/GUID integrity, Level 1 contracts, PR15 hardening, new security-boot source contracts, Python syntax, merge markers and human-authored whitespace.

**Earlier failed source check and correction:** run `34791809971` on `f9b7616` passed C# syntax/source references but found the pre-existing orphan `Assets/StreamingAssets.meta`. Commit `74d42b7` removes only that orphaned metadata, defers scene-root discovery to Start and adds the boot-contract check to the existing read-only workflow. No write-enabled preparation workflow is present on this branch.

**Pending validation:** Unity 2022.3.55f1 import/compile, Play Mode, Photon and headset testing. No Unity runtime result is claimed by this document.

### Runtime acceptance

- **Startup:** Bootstrap to terminal to Hub; zero/default minimum; no missing-font/pink UI, old tips, premature world flash, extra camera/audio listener, stale UI-only culling mask or exception. Repeat with Editor domain reload enabled and disabled.
- **Failure:** slow/no connection, missing Hub Build Settings entry, trigger/R retry, repeated retry while a scene load is in flight, disconnect during either fade. No false-ready entry, duplicate Hub load, permanent black screen or readiness bypass. Errors must remain legible and must not be parsed as rich text.
- **Headset:** cold Quest build plus Link/desktop as applicable; both-eye and peripheral coverage; head turning/recenter/IPD; panel readability, optional sound level, pause/resume and real-load frame timing. Native system-splash coverage is a separate test, not evidence for this scene prototype.
- **Regression:** Hub to Level 1, both Hub returns, Level 1 completion to Level 2, RETURN TO SECURITY, capture respawn, failed travel and two-client Photon in different sectors. No intro replay, successful-travel text, changed room selection, missing avatar, stuck fade or changed monster/interaction behavior.

Record device, build/commit, observed duration and result with screenshots/logs. Green source checks do not close runtime items.

## Maintained-overview synchronization: prepared, not applied

The two maintained overview files, `docs/design-and-lore.md` and `docs/repository-improvement-plan.md`, have **not yet been modified by this prototype branch**. Their synchronization remains a pre-merge item, rather than a completed documentation update. The latest decisions and actual tests are recorded above.

`docs/security-boot-maintained-docs.patch` prepares an additive decision/correction section for both files, preserving all existing text and explicitly superseding the older visible-Loading-UI travel direction. Its insertion context was checked against the retrieved opening sections; it has not been applied or tested against a complete local repository checkout. After reconciling any newer documentation edits, apply and review it as a documentation-only change. It is not required to run the prototype.
