# Defect report template

Copy one report per independently reproducible problem into a GitHub issue. Link the parent roadmap ticket and affected PR; do not paste raw private logs, tokens, player/account IDs, room codes, local usernames/paths or signing credentials. See [the testing guide](../branch-testing-and-build-evidence.md).

## Summary

Title: [BUG] <observable problem and location>

Related roadmap issue/PR: TO FILL

## Tested version and context

- Full source commit SHA and branch: TO FILL
- Clean or locally modified tree; list relevant differences: TO FILL
- Build/version and APK SHA-256, or Editor-only: TO FILL
- Actual Unity version and revision: TO FILL
- Device/runtime: TO FILL (Quest 2, Quest 3, Editor or Windows development client; include OS/runtime)
- Actor count, real headset count, relevant controller/target roles and sectors: TO FILL
- Frequency: TO FILL (failed attempts / total attempts, sessions and poses)
- First observed revision and last known passing revision: UNKNOWN until supported by evidence

## Reproduction

Starting scene/state: TO FILL

1. TO FILL
2. TO FILL
3. TO FILL

Expected result: TO FILL

Actual result: TO FILL

Recovery/workaround: NOT ESTABLISHED

Safety/comfort impact and reason testing stopped, when applicable: TO FILL

## Impact for triage

Suggested severity: UNTRIAGED. Greg/review confirms the classification.

| Severity | Meaning / handling |
| --- | --- |
| Critical / blocker | Unsafe movement or severe discomfort, security exposure, currency/ownership loss, impossible startup or other serious loss of safe access. Stop dependent testing; no release. |
| High | Required objective cannot be completed/recovered, broken personal completion, persistent loading/network failure or repeated severe gameplay malfunction. No release until resolved. |
| Minor | Non-blocking presentation or usability issue with safe continued play, no lost objective/ownership, and a known limited impact. Acceptance is never automatic. |

Reproduction frequency does not make an unsafe or data-loss defect minor. Split independent defects, but link related symptoms rather than filing duplicates.

## Evidence and status

- Sanitized log excerpt / screenshot / video reference: NOT RECORDED
- Does evidence identify the exact build and scenario? NOT RECORDED
- Pre-existing defect versus branch/editor regression: UNKNOWN
- Proposed explanation (optional, not a confirmed cause): NOT ESTABLISHED
- Fix branch/commit/PR: NOT IMPLEMENTED
- Source/managed checks after fix: NOT RUN
- Unity checks after fix: NOT RUN
- Headset/Photon retest and repetitions: NOT RUN
- Integrated-build retest: NOT RUN
- Resolution: OPEN

A proposed cause is not a diagnosis. An implementation or merge is not a successful retest. Keep acceptance issues open until their required evidence exists; link fixes with Refs rather than automatic closure while runtime work remains.
