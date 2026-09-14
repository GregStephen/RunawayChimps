# Security-system boot prototype - Option A

Date: 2026-09-13. Branch: `codex/security-boot-prototype`, based on `main` commit `05ea14f4af038296eec23f69de12aa9b26985b13`. Draft PR **#20**. Unity **2022.3.55f1**, Photon PUN. This is a testable visual direction, not final art approval or a merged feature.

## Decision and correction record

**Confirmed for prototyping:** Greg requested a test branch for Option A, the facility security-system boot. The floating idea is **Proposed, not selected or implemented**.

**Confirmed travel correction:** normal Hub/level travel should stay completely black, without terminal, logo, tips or loading text. Reconsider a minimal indicator only after measured transitions approach roughly **10 seconds**. Real error/recovery feedback remains visible.

**Implemented on this branch, pending runtime validation:** a green/amber framed facility terminal, Runaway Chimps title treatment, five real prerequisite indicators, a slow activity cursor, optional quiet relay ticks and a short black handoff into the Hub. No camera motion, strobe, fake percent complete, surveillance feed or new Photon behavior is introduced.

**September 13 pre-test polish implemented in `80bb561`:** the Loading canvas now forces a topmost sorting order so already-loaded Hub UI cannot appear above the launch/travel cover. Once real readiness is reached, `ACCESS GRANTED` remains visible for a configurable **0.35-second** minimum before the terminal fade, instead of potentially appearing for only one rendered frame after a slow startup. Editor/development builds emit one `[SecurityBoot] Ready in ...` timing summary with the first-observed time for each real prerequisite. Retry starts a fresh timing attempt. Disabling or destroying the presentation restores the persistent camera settings as an additional safety net.

| Terminal row | Actual source |
| --- | --- |
| FACILITY LINK | `PhotonNetwork.InRoom` |
| SECURITY SECTOR | The configured Hub scene is loaded |
| ARRIVAL ALIGNMENT | `AppState.RigSnapped` |
| AVATAR LINK | `AppState.PhotonPlayerSpawned` |
| VISUAL SYSTEMS | `AppState.PlayerVisualsReady` |

The raw startup/error message remains beneath the diagnostics. Retry uses either controller trigger or desktop **R**. A surveillance-feed finish remains **Proposed, not implemented**.

## Try it

1. Check out `codex/security-boot-prototype` and open the project in Unity **2022.3.55f1**.
2. Open **Assets/Scenes/Bootstrap.unity** and press Play. No prefab hookup, Editor migration or new Build Settings entry is required.
3. For a longer desktop look, enable **Tools > Runaway Chimps > Hold Security Boot For Review** before Play. Toggle the menu off to enter the Hub once real startup prerequisites are ready.
4. Turn review hold off and repeat ordinary Hub/Level 1/Level 2 travel. Successful transfers should remain black and never show the startup terminal or old tips.
5. Capture the one-line `[SecurityBoot] Ready in ...` Console timing summary from a normal startup. It is diagnostic evidence only and is compiled out of non-development player builds.

For tuning, open **Assets/Scenes/Loading.unity**, select **LoadingFlow**, and edit `Minimum Intro Seconds` (default **1.5**, range **0-3**), `Minimum Ready Hold Seconds` (default **0.35**, range **0-1**), or `Play Boot Sounds` (default on, low volume). Save the scene only when intentionally changing prototype settings, then test from **Bootstrap**.

## Ownership and implementation limits

`SecurityBootPresentation` is local and owned by the existing Loading scene. It reuses the authored TMP font, canvas, background and 12% XR overscan. It does not create a second XR rig, alter head/controller transforms, network the intro, or change room selection. The Loading canvas is topmost while present. Startup temporarily uses the persistent camera's UI-only culling mask and black clear; its original culling mask, clear flags and background are restored for Hub reveal, component disable and presentation destruction.

The native **Quest/Meta system splash is not implemented or configured by this branch**. Unity splash/logo/license settings and XR package versions are unchanged. Final branded splash artwork and platform/build validation remain separate planned work.

During ordinary travel the installer builds no terminal or sound source and retains the black background/overscan. The existing SectorTravelService, scene load, rig freeze/settle/recovery, fade sequence and Photon lifecycle are unchanged.

## Validation record

**Validated source only, 2026-09-13:** Source integrity previously passed for `74d42b7` and later docs-only head `46eae76`.

**Current polish status:** `80bb561` implements the topmost cover, minimum ready dwell, timing diagnostics, retry timing reset and disable-time camera restoration. Source integrity and all Unity/runtime checks are pending on this head until the current workflow completes.

**Pending validation:** Unity 2022.3.55f1 import/compile, Play Mode, Photon and headset testing. No Unity runtime result is claimed by this document.

### Runtime acceptance

- **Startup:** Bootstrap to terminal to Hub; zero/default intro and ready-hold values; `ACCESS GRANTED` remains legible; no missing-font/pink UI, old tips, partially loaded Hub UI over the terminal, premature world flash, extra camera/audio listener, stale camera mask or exception. Capture the timing summary.
- **Failure:** slow/no connection, missing Hub Build Settings entry, trigger/R retry, repeated retry while a scene load is in flight, disconnect during either fade. No false-ready entry, duplicate Hub load, permanent black screen or readiness bypass. Retry timing should restart cleanly.
- **Headset:** cold Quest build plus Link/desktop as applicable; both-eye and peripheral coverage; head turning/recenter/IPD; panel readability, ready dwell, optional sound level, pause/resume and real-load frame timing.
- **Regression:** Hub to Level 1, both Hub returns, Level 1 completion to Level 2, RETURN TO SECURITY, capture respawn, failed travel and two-client Photon in different sectors. No intro replay, successful-travel text, Hub UI bleed-through, changed room selection, missing avatar, stuck fade or changed monster/interaction behavior.

Record device, build/commit, observed duration and result with screenshots/logs. Green source checks do not close runtime items.

## Maintained-overview synchronization

`docs/security-boot-maintained-docs.patch` remains the prepared update for `docs/design-and-lore.md` and `docs/repository-improvement-plan.md`. It still needs reconciliation/application before merge. The current polish details above must be carried into that maintained-document update; do not treat the older visible-Loading-UI travel wording as active.
