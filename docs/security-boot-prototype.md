# Security-system boot prototype - historical implementation record

Date: 2026-09-13. Original branch: `codex/security-boot-prototype`; merged through PR #20 into `main` at merge commit `24a0d4e5feb89a663f8c0ed874800efd28c9af2d`.

The facility security-system boot prototype was visually approved and is now the confirmed launch direction for Runaway Chimps. This file is retained as the historical prototype record. Current launch-presentation work and validation belong in [Launch presentation](launch-presentation.md).

## Confirmed outcome

- The facility security-system boot is the selected launch presentation.
- The floating/void startup idea is no longer an active alternative.
- Normal Hub/level travel remains black-only unless measured load times later justify revisiting that rule.
- Unity/Photon/headset/Quest technical validation remains separate from design approval.

## Merged prototype behavior

PR #20 introduced the green/amber facility terminal, real startup prerequisite indicators, optional relay ticks, `ACCESS GRANTED` dwell, topmost Loading coverage, startup timing diagnostics, retry timing reset, and camera-state restoration safeguards. The presentation observes real Photon/Hub/rig/avatar readiness rather than inventing a progress percentage.

Source validation passed on the prototype work. Runtime/headset acceptance remains tracked in the current launch-presentation plan rather than being retroactively marked complete here.
