# Test and build record template

Copy this into private notes or a sanitized issue/PR comment. Do not overwrite the template with a real run. Every field starts unrecorded, not passed. See [the branch-testing guide](../branch-testing-and-build-evidence.md).

## Identity

- Run ID and date/time including timezone: NOT RECORDED
- Related issue(s) and PR(s): NOT RECORDED
- Branch and full source commit SHA: NOT RECORDED
- Source tree: NOT RECORDED (clean / dirty; describe local code, scene, prefab and package changes)
- Actual Unity version and revision: NOT RECORDED
- Package manifest and lockfile hashes: NOT RECORDED
- Mode: NOT RECORDED (Editor / standalone Quest / Windows development client)
- Device and OS/runtime version: NOT RECORDED (Quest 3 owner / Quest 2 tester where applicable)
- Actor/headset counts, roles and sector arrangement: NOT RECORDED
- Environment differences versus previous result: NOT RECORDED

## Build details

Use NOT BUILT for Editor-only testing; do not invent an APK or install result.

- Build result: NOT RUN
- Build/version identifier and Android version code: NOT RECORDED
- Target/options: NOT RECORDED (Android/architecture, scripting backend, Development Build and profiling flags)
- Actual Android SDK/build-tools, NDK, JDK and Gradle versions: NOT RECORDED
- Artifact filename and SHA-256: NOT RECORDED
- Build log/evidence reference (private original, sanitized sharing copy): NOT RECORDED
- Install/update method and result: NOT RUN
- Build identity visible to tester: NOT RECORDED (absence is still an open #28 task)

## Source, import and automated evidence

Use one status per row: Not run / Blocked / Pass / Fail / Not applicable with reason.

| Check | Status | Result / evidence |
| --- | --- | --- |
| Exact-SHA Source Integrity, including branch-specific jobs | Not run | Link the actual run and SHA; no inference from an older green badge. |
| Unity import and compilation | Not run | Record errors/warnings and any resulting asset/package diff. |
| Required Editor validators | Not run | Name each validator and result; note any authoring/repair changes. |
| Required EditMode suite | Not run | Name expected suite, discovery and results, or justify non-applicability. |
| Required PlayMode suite | Not run | Name expected suite, discovery and results. |
| Managed non-Unity tests, if present | Not run | Separate evidence; not a Unity test substitute. |

For EACH required automated suite record: expected cases/scope; discovered; executed; passed; failed; skipped/inconclusive; runner exit/result; XML/log reference. Zero discovered/executed cases, missing XML, a required skipped case or a failed assertion cannot be reported as a pass. An exception or missing tool is Blocked/Fail, not Not applicable.

## Scenario record

Add one row per scenario and one record per revision. Keep actual counts even when stopping early.

| Scenario / expected result | Device and actor roles | Attempted / passed / failed | Actual observation | Status | Evidence / defect |
| --- | --- | --- | --- | --- | --- |
| TO FILL | NOT RECORDED | 0 / 0 / 0 | NOT RUN | Not run | NOT RECORDED |

Relevant scenarios may include cold-start poses, hand contact, card pickup/drop/scan/recovery, safe boundaries, Crawler pursuit/capture, travel/return, room change, pause/resume, network interruption, Coconut display/daily-state regression and later shop purchase/equip/persistence. Only include scenarios appropriate to the actual candidate and issue.

## Disposition

- Overall scope actually tested: NOT RECORDED
- New or recurring defect issue(s): NOT RECORDED
- Pre-existing defect versus candidate regression: NOT DETERMINED
- Stopped for safety/comfort: NOT RECORDED
- Remaining blocked or unrun checks: NOT RECORDED
- Decision: NOT REVIEWED (continue / fix and retest / acceptance met for named issue only)
- Greg's acceptance, where required: NOT RECORDED
- Required retest after merge/editor change: PENDING

A checked-in PR, source check or this completed form is not public-release approval. Preserve raw evidence privately and share only the minimum sanitized information. Two-headset results establish only the recorded actors/devices/conditions; they do not certify ten-player capacity.
