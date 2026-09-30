# Primate Cognitive Evaluation - repeated self-review

Date: 2026-09-30. PR #77, `feature/primate-cognitive-test`. Reviewed starting feature head: `9addd2d7115fdc509e8336600656a44de08964a8`; main remains `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`. The temporary read-only inspection commit does not change gameplay. Unity remains **2022.3.62f3 (96770f904ca7)**, Photon PUN, built-in rendering.

## Pass 1 - delayed synchronization

**Found and reproduced:** `CognitiveReplica.RequestSync` replaced the pending challenge every time the adapter retried. A first response arriving after the 0.5-second retry interval was rejected for echoing the previous challenge; steady latency above that interval could keep the machine offline indefinitely. The same flaw affected recovery after a controller component restarted under the same actor ID.

**Implemented:** retain a pending challenge until a valid acknowledgement consumes it. A new election or room reset still establishes a fresh challenge; a consumed reply cannot restore an older controller term.

**Before/after evidence:** the previously passing 57,013-assertion core suite failed after adding `DelayedHandshake`, at `delayed initial reply survives retries`. A new harness compiled the actual machine and pad adapters against managed doubles and independently failed at `delayed initial snapshot survives actual adapter retries`. Both pass after the production fix. The core suite now passes 57,020 assertions, including delayed first sync, delayed same-actor restart, unchanged-state acknowledgements and stale-controller responses.

## Pass 2 - upper-cap clearance

**Found and reproduced:** the actual parent rotations and dimensions placed both upper cap cubes partly inside `DisplayHousing`. An independent oriented-box intersection calculation found positive interior overlap, rather than merely overlapping world-axis bounding boxes. A new full-parent-transform clearance check failed against the shipped prefab.

**Implemented:** lower only `Controls_AdjustHeightHere` from 0.98 m to 0.95 m and regenerate the serialized prefab. Existing instance positions, component/asset identities, dimensions, labels, materials, audio and physics settings are preserved. Approximate cap-center heights become 0.87/1.10 m, and START is 0.72 m above the floor pivot. No shared scene is edited.

**Before/after evidence:** all five cap meshes are checked at rest and full configured press, with a 5 mm minimum vertical gap below the display case. Restoring the old deck height is a fifth rejected prefab mutation. Deterministic reproduction of all 34 feature asset/metadata files, the 214-object graph/reference checks, audio checks and four contact-filter mutations also pass.

## Pass 3 - post-fix review

**Result:** no further actionable findings in the reviewed source, managed execution and serialized-asset scope. This is not a claim that the feature is free of defects under unexecuted native runtime conditions.

Re-read the production rules, contact occupancy/release logic, snapshot validation, authority and operator separation, retry acknowledgement lifecycle, sender/session/phase/serial checks, scene/room/pause/disable cleanup, audio/visual dispatch, physical local-hand filter and Editor placement/Undo code. Reviewed the generator/prefab changes and tests. No changes to other toys, shared Hub/Bootstrap code, packages, rendering, engine or XR settings.

The production adapter diagnostic passes **63 assertions across six groups**:

| Group | Scope |
| --- | --- |
| DelayedSynchronization | Late initial reply; late same-actor controller restart; subsequent ownership acquisition |
| PhysicalAndEditorInputs | Held/duplicate contacts, either hand, sequence extension, maximum, failure audio, restart |
| ContactIdentityAndCleanup | Controller-root grab volumes, remote rig/Photon identity, stay events, disabled collider and re-enable recovery |
| LifecycleAndPresence | Real distance branch, walking away, pause/resume, room switch, callback removal and re-enable |
| NetworkOwnershipAndRecovery | Operator/spectator isolation, stale-session release, expired heartbeat, sector departure, authority handoff |
| MalformedAndUnrelatedEvents | Wrong protocol/machine/sector/event code, malformed snapshots and null payloads |

Both core and adapter diagnostics compile with C# 9 and warnings treated as errors. Locally they were executed with the .NET 8 runtime/Roslyn compiler from the review runner; CI uses the normal `dotnet run --project ... --configuration Release` commands. Final published SHA and fresh full-repository CI results are recorded on PR #77, not inferred from the older asset-delivery runs.

## Validation boundary and next acceptance

The adapter doubles simulate component lookup, translation-only transforms, clock values, identity and explicit message delivery. They do not implement Unity native physics, real callback ordering, TMP/rendering, audible output, Editor APIs, Photon transport or XR. The custom Editor is source-reviewed, not executed by this diagnostic. The asset graph check separately evaluates the actual authored rotations/scales and references, but does not import or render the prefab.

Still required in Unity 2022.3.62f3: clean import/compilation; placement/Undo with no forced save; solo physical and Editor inputs; actual upper-pad case clearance and seated/standing reach; readable display and spatial sound. With two real clients, include simultaneous START, 750 ms or greater round-trip latency, controller component restart, operator departure, pause/rejoin and authority handoff. Follow the full [feature acceptance checklist](primate-cognitive-evaluation.md). No merge or runtime acceptance is implied.
