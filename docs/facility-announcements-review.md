# Facility announcements: repeated review record

Date: September 30, 2026. Branch: `feature/facility-announcements`. PR: [#80](https://github.com/GregStephen/RunawayChimps/pull/80), **open and unmerged**. Review baseline: `b3e508c7f19b93adb9cfd807bbea933cdbe3ba8a`. The temporary snapshot-only commits did not change that runtime. Final publication revisions and full-repository CI links are recorded on the PR.

## Request and scope

Greg requested code review, fixes, and repeated re-review until no further findings. Read `AGENTS.md`, the relevant maintained design/plan sections, the setup guide, all feature runtime/Editor code, relevant local-rig/sector lifecycle and PUN source, and the serialized references. Preserve Unity **2022.3.62f3 (96770f904ca7)**, Photon PUN and built-in rendering. This review does not add a gameplay requirement, canonical lore, another rig/camera, runtime speech service or dependency on another feature branch.

## Findings and implemented corrections

| Finding | Correction and regression evidence |
| --- | --- |
| A stalled cue could advance to speech after the reserved network slot had expired. A new event could overwrite the output reference while its old AudioSource was still playing. | Capture the accepted slot deadline; expire/cancel before ticking or receiving; reject replacement while a stage is active. Regressions cover delayed cue, occupied slot and a different nearest output at replacement. |
| With expiry enforced, a shorter stall could still start a sentence too late to finish. | Check the remaining cue/speech/end-cue duration before beginning each stage; prefer silence to a deliberately truncated sentence. This additional second-pass finding has a failing-before/passing-after case. |
| A failed `RaiseEvent` consumed room repeat history, leaving a one-line collection silent despite never sending that line. | Record history only after successful enqueue; retain a full quiet retry interval. Test both failure and successful recovery/receive. Enqueue success is not claimed to prove remote delivery. |
| The schedule could emit using an authority cached before a lower eligible actor arrived. | Force a bounded membership/election refresh immediately before a due send and recheck ownership/deadline/occupancy. Tests cover the between-polls arrival and A-B-A handover. |
| Raw Photon timestamps wrap back to zero, leaving prior deadlines unreachable. | Detect the large backwards wrap, cancel audio, reset local gates/deadlines, withdraw the old readiness ticket and rearm quietly with a new ticket. Preserve same-room repeat history. Test idle and active-cue rollover; ordinary small backwards adjustment is not mistaken for rollover. |
| An active cached camera could keep a disabled/replaced Gorilla rig eligible. | Require the current active Gorilla player in readiness; associate the camera cache with that rig and bind replacements through the existing local marker/ownership/XROrigin checks. Test disable/withdrawal and replacement. |
| The content-availability path accepted streaming or zero-weight entries that did not meet the documented playback/selection contract. | Require loaded non-streaming speech/cues and positive speech weight consistently. Test streaming and zero-weight rejection without normal caption-only announcements. |
| NaN/infinite audio settings and local subtitle preferences could propagate into source distances/volume or caption geometry. | Use finite, bounded defaults for these feature-local controls. Add adapter and pure-policy edge-value regressions; do not modify shared audio/UI settings. |
| Disabling the caption component cleared it once, but the director's next direct `Show` could repopulate it. | Refuse presentation on an inactive/disabled view, gate canvas visibility, and clear the old caption before deferred replacement destruction. Test disable during speech and stale-text-free re-enable. |

No recordings, materials, prefabs, collection wording, engine/packages/settings, existing scenes or unrelated features are changed. Four synthetic spoken WAVs and two original nonverbal cues remain available; subjective voice/intelligibility acceptance is still pending.

## Review rounds and actual results

1. **First pass:** the original runtime failed nine of the initial 16 managed adapter cases. Applied the first corrections; all 16 then passed.
2. **Second pass:** added tests for a late sentence that could not finish and for disabled-caption resurrection. Both failed against the first-pass code; fixed them and reran successfully. Added explicit director handover and successful send-recovery coverage.
3. **Final pass:** reviewed the corrected source, Editor tools, asset contracts and tests again. No further actionable issue was found within this reviewed scope. The finished suite passes **19 cases / 60 assertions**. As a negative control, the same finished suite compiled against the original published runtime and reproduced **11 failing cases** (47 assertions reached before those per-case failures), confirming that the tests actually distinguish the repairs.

| Executed check | Result and boundary |
| --- | --- |
| `dotnet run --project Tools/FacilityAnnouncementsAdapterHarness --configuration Release` | **19 cases / 60 assertions passed.** Links the five actual runtime feature files and existing `SectorPresence.cs` against instrumented managed doubles. |
| `dotnet run --project Tools/FacilityAnnouncementsHarness --configuration Release` | **20,064 assertions passed**, including the original repeat-prevention loop plus new timestamp/presentation-bound cases. Pure production policy, not native integration. |
| `python Tools/FacilityAnnouncements/validate_assets.py` | **974 assertions passed.** Serialized references, metadata, WAV format/content/checksums and source boundaries; not Unity import/rendering/listening. |
| Full repository source validation | Run by the existing PR workflow on the published revision; see exact run and SHA on PR #80. Do not substitute a partial source snapshot for the full repository check. |

Local managed execution used .NET SDK 8.0.425 with the 8.0.31 reference/runtime packs, no added NuGet dependency. The permanent feature CI runs the new adapter harness as a read-only check. Temporary review transport/preparation workflows are not part of the delivered feature.

## Evidence limitations and pending acceptance

The doubles manually advance clocks, provide AudioSource state, apply custom properties synchronously, deliver messages and invoke lifecycle methods. They do **not** reproduce Unity destroyed-object semantics, native audio start/end scheduling, asynchronous Photon acknowledgement/order, real rig transforms or XR rendering. The production Editor code is reviewed but is not executed or compiled against Unity by this harness. A managed pass is not an approval to merge without reviewing the existing acceptance checklist.

**Not performed:** Unity 2022.3.62f3 import/compilation, actual Editor validation/placement/preview execution, native AudioSource listening, live two-client Photon sessions, headset caption/audio acceptance, Android/Quest builds or performance profiling. The original spoken recordings are unchanged; no new human listening approval is claimed.

Retain the solo/two-client/headset checklist in [the setup guide](facility-announcements.md). Additional focused native checks are stalled-frame cue/speech cancellation, disabled caption re-enable, cached-camera/rig retirement, and authority changes around a scheduled send. A Photon time-boundary test can be instrumented in a diagnostic build without waiting for uptime; the managed simulation alone does not verify the live callback order. No claim is made that repeated review proves the absence of every possible defect.
