# Launch vignette — restored flat security terminal

Date: 2026-09-14. Branch: `feature/launch-presentation-polish`. Runaway Chimps uses Unity **2022.3.55f1** and Photon PUN.

## Superseding correction

**Confirmed by Greg on September 14:** the physical security workstation/desk vignette is no longer the selected launch presentation. Restore the earlier **flat green security terminal** and place it substantially farther from the player. Greg asked for roughly **twice the distance** of the workstation version.

This correction supersedes the earlier physical-desk direction in this document and elsewhere on PR #21. The desk, monitor shell, keyboard, mug, badge, clipboard and sticky-note flavor props are no longer part of the active startup presentation.

The multiplayer Hub-slot, reconnect/session, Quest splash, render-isolation, readiness, error/retry and final-reveal hardening from PR #21 remain in scope. Only the startup visual composition is reverted.

## Implemented on this branch

`SecurityWorkstationVignette` remains the legacy class/file name for the moment, but its implementation is now only a local presentation anchor for the green terminal. It creates no primitive workstation geometry and no prop materials.

The terminal canvas keeps the same physical scale used by the previous monitor (`0.00082` for the 1080 × 820 UI), but its anchor is now **3.9 m** from the initial horizontal camera heading. That is exactly twice the former **1.95 m** workstation distance. The panel is centered at eye height.

The anchor is parented to the persistent `XROrigin`, so hidden startup rig relocation carries it while ordinary head look and room-scale head motion do not head-lock the panel. The tracked camera transform is never written.

The green terminal still shows real Photon/Hub/rig/avatar readiness only. Existing retry/error behavior, quiet relay ticks, CRT noise/scanlines/interference and the brief `ACCESS GRANTED` state remain unchanged. Normal successful Hub/Level 1/Level 2 travel remains **black-only**.

## Removed from the active presentation

- Physical desk and pedestals.
- Monitor shell, stand and base.
- Keyboard, mug, badge/card and clipboard.
- `CAM 04 / STILL DEAD`, `VENT B / AGAIN?`, and `IF THEY GET OUT / I QUIT.` presentation notes.
- Runtime primitive construction and the workstation material resource as a visual dependency.

The old workstation implementation remains historical context in earlier commits, not the selected design.

## Reveal and failure behavior

The full-FOV black Loading cover still owns startup isolation. The camera sees only `LoadingPresentation` against black until the Hub reveal is prepared. When startup completes, the green terminal fades under black and is retired before the Hub becomes visible. An interrupted entry/retry may rebuild the local terminal presentation.

Successful sector travel continues to construct no launch terminal or boot audio. Real travel failures keep recovery feedback.

## Pending validation

- Unity **2022.3.55f1** import/compile.
- Play Mode: confirm the flat green terminal appears about **3.9 m** ahead, is centered comfortably, and the new angular size/readability feels right.
- Confirm head turning does not move/head-lock the panel and hidden `XROrigin` relocation does not leave it behind.
- Confirm CRT treatment remains subtle and text is still readable at the increased distance.
- Confirm `ACCESS GRANTED` -> full black -> terminal absent -> Hub reveal has no pop, flash or Hub leakage.
- Confirm failure/retry restores the terminal and readable error state.
- Confirm ordinary Hub/Level 1/Level 2 travel remains black-only.
- Two-client Photon slot/session/reconnect tests remain pending.
- Quest compositor splash, stereo/peripheral coverage, recenter/pause-resume, performance and comfort remain pending.

Source validation is not Unity/headset proof.

**Validated source only — restored distant green terminal:** clean head `cdf6b87a55d429351acf0d6b96a632fda4847893` passed Source Integrity run `34917902460` on 2026-09-14. The run passed Unity 2022.3.55f1 version enforcement, first-party C# syntax/reference and enabled-scene checks, Unity metadata/GUID integrity, Level 1 and PR #15 contracts, the restored distant security-boot contracts, local threat-feedback contracts, launch/Quest/Hub-slot/session contracts, Python compilation, merge-marker rejection and human-authored whitespace. This validates source/tooling only; Unity import/compile, Play Mode, Photon multi-client behavior and Quest/headset behavior remain pending.
