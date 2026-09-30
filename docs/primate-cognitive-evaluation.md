# Primate Cognitive Evaluation - implementation draft

Date: 2026-09-30. Branch: `feature/primate-cognitive-test`, based on main `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`. Unity **2022.3.62f3 (96770f904ca7)**, Photon PUN and built-in rendering remain unchanged.

## Delivery status: source only, not a playable prefab

The game rules, local-contact gate, sector-aware network adapter, Editor inspector controls and source test harness are implemented on this branch. **The required serialized machine prefab, materials and audio assets are NOT delivered.** The asset-generation write was blocked by the authoring tool. Do not mark this feature complete, merge it as a playable toy, or infer an asset/import/runtime pass from source tests.

The Editor placement/validation commands exist but deliberately report a missing prefab rather than generate runtime placeholders or modify shared scenes. The planned prefab path is `Assets/RunawayChimps/Toys/PrimateCognitive/Prefabs/PrimateCognitiveEvaluation.prefab`; this is a target path, NOT an existing asset. No scenes, packages, project version, rendering or XR settings were changed. This work has no strength-tester or other toy branch dependency.

## Confirmed experience

Four distinct numbered positions and tones demonstrate a sequence. Either hand repeats it. Start at one step and append one step after each successful round. The first prototype defaults to eight rounds with a hard cap of sixteen. Display the round and explicit idle, demonstration, input, success, failure and completed states. Use dry facility assessments rather than voice acting. Failure and completion allow an immediate physical restart; no currency, cosmetics, persistent leaderboard, competitive mode, progression or general minigame framework.

## Source architecture

All runtime code is in `Assets/Scripts/Toys/PrimateCognitive/`.

- `CognitiveGame`: pure deterministic sequence/rules model, authority-owned state, session and phase tokens, bounded length, input/heartbeat/result deadlines, assessments and validated snapshot payloads.
- `CognitiveContactGate`: aggregates all colliders on a pad; requires all contacts to leave for 80 ms. Duplicate enter/stay, a second collider/hand on an already held pad, and state changes do not produce another press. `CognitiveReplica` validates current-controller epochs, revisions and fresh handshake nonces, including A -> B -> A and same-actor component re-enabling.
- `CognitivePad`: accepts only enabled `HandTag` colliders under the actual local Gorilla rig's `LocalRigMarker`, additionally rejecting non-owned Photon views. Trigger-stay recovers occupancy without inventing a press. Disabled/destroyed contacts are pruned. No shared `PhysicalButton` changes.
- `CognitiveMachine`: sector controller election via existing `SectorPresence`, explicit Photon event codes **188/189** and protocol tag `rc.cognitive.1`, reliable same-sector targeted snapshots and commands, actor-bound operator, heartbeat and inactivity recovery. No scene PhotonView or global scene synchronization. Room changes, current sector, travel/loading and component lifecycle gate participation.

The lowest active actor in the sector arbitrates one machine at a time. Operator identity is separate from authority identity. Spectators cannot restart or submit sequence steps for the operator; the authority uses Photon sender identity and rejects stale session/phase tokens and repeated command serials. Authority changes intentionally reset the test. Fresh nonce handshakes establish a new controller term, so earlier snapshots cannot revive an occupied session. Reconnection uses room-object identity, not only room name.

Only local eligible operators within the configured head-to-anchor radius emit heartbeats. Leaving the radius, pausing or disabling the component requests release; departure/sector change is independently observed by the authority. Missing heartbeats release after four seconds, input inactivity after twenty seconds even while presence continues, and unattended results after ten seconds. Live Photon delivery and the precise pause/disconnect timing remain pending acceptance. This is cooperative prototype networking, not server-authoritative anti-cheat; there are no rewards to secure.

Demonstration cues advance at most once per update rather than bursting several notes after a frame stall. Snapshots include cue serial/time; late snapshots do not replay a past sequence, and repeated snapshots do not double-play notes. Initial/resync snapshots establish state silently. Actual audio output and two-client timing are unverified without the missing audio assets and Unity execution.

## Intended asset wiring and reach (not yet authored)

The intended factory machine is a unit-scale movable prefab with a cabinet, world-space assessment display, two rows of two numbered pads, a separate START / RESTART pad, five trigger colliders and moving cap renderers, six assigned short spatial clips (four notes, one reserved signal, one failure sound), and an operator presence anchor. Use the existing LiberationSans SDF font and built-in materials; no new camera, listener, canvas overlay, XR configuration or runtime asset generator.

Reach must be measured against the actual unit-scale Bootstrap Gorilla rig, its 5 cm visual contact proxy and 8 cm direct-hand spheres. Proposed 18 x 16 cm main caps with approximately 34 cm horizontal center spacing and 27 cm vertical spacing keep the two-handed spread compact. Proposed control surface height is about 0.93 m with a 30-degree upward-facing tilt; final seated/standing placement and label sizing remain unvalidated. Treat these as authoring targets, not shipped dimensions.

## Editor commands and intended setup

**Not runnable to completion until the prefab is delivered.** Once authored and assigned:

1. Open with Unity 2022.3.62f3, outside Play Mode. Choose **Tools > Runaway Chimps > Toys > Place Cognitive Evaluation in Hub**. It opens Hub additively only on explicit invocation, selects an existing machine rather than replacing/moving it, supports Undo for a new placement, and never saves a scene. Inspect the proposed test position `(2.4, 0.15, -1.2)` for floor, computer, door and startup-slot clearance before keeping it.
2. Move/rotate the instance as desired, keeping unit scale for the documented contact dimensions. Save Hub manually. Manual duplicates require a unique `machineId` in every client's scene; the Inspector offers an Undoable ID assignment. Duplicate live IDs fail closed.
3. Run **Tools > Runaway Chimps > Toys > Validate Cognitive Evaluation Prefab**. This is an authored-reference check, not gameplay acceptance.
4. For desktop-only testing, enter Play Mode directly in Hub, select the machine and enable **Editor Controls**. Inspector HOLD toggles for pads 1-4 and START / RESTART simulate contact enter/exit through the same production gate and command path. Toggle off to release. Keep a toggle on through a phase change to exercise held-contact rejection. Selection loss releases virtual contacts. Editor controls and range bypass are compiled out of player builds; multiplayer Editor tests still obey sector/ownership rules.

## Inspector controls

| Control | Default | Meaning |
| --- | --- | --- |
| `maximumLength` | 8 | Maximum round; hard limit 16. |
| `demonstrationLead` | 0.7 s | Hands-clear delay before first cue. |
| `stepSeconds` | 0.65 s | Start-to-start cue spacing, clamped above flash duration. |
| `flashSeconds` | 0.32 s | Illuminated cue length. |
| `successSeconds` | 1.1 s | Successful-round assessment before appending/replaying. |
| `heartbeatTimeout` | 4 s | Missing-operator lease timeout. |
| `inputIdleTimeout` | 20 s | No sequence input, even with healthy heartbeat. |
| `resultHoldSeconds` | 10 s | Failure/completion screen before release to idle. |
| `operatorRadius` | 1.8 m | Actual local head to assigned anchor. |
| Pad `releaseSeconds` | 0.08 s | All-contact-clear rearm interval. |
| Pad `pressDepth` | 0.012 m | Cap movement in pad-local forward direction. |

Pacing/recovery values are captured when an authority term starts, not hot-edited into a live session. Move the control surface/anchor to tune reach; do not enlarge the Gorilla rig or global hand colliders. Tune clip volume and 3D attenuation on the assigned AudioSource after actual headset testing.

## Executable source checks

```sh
dotnet run --project Tools/PrimateCognitiveHarness/PrimateCognitiveHarness.csproj --configuration Release
python Tools/validate_source.py --syntax
python Tools/validate_repository_integrity.py
```

The cognitive harness compiles actual production rules/contact/replica files, not a port. It exercises 128 deterministic seeds through all sixteen rounds, prefix preservation, correct/wrong input, all states, wrong owner, invalid/stale tokens, restart and completion, finite bounds, heartbeat/presence/input/result expiry, contact debounce/duplicate/rest/state transitions, snapshot validation and authority handshakes. It does **not** compile the Unity adapter against Unity assemblies or execute native trigger events or Photon callbacks. The normal repository Source Integrity checks remain separate and unchanged. The PR records the exact executed results and head.

## Pending acceptance checklist

**Assets and Unity first:** deliver and inspect the serialized prefab/materials/audio; verify all references, font layout, no mesh overlap, trigger/cap separation and meaningful note differences. Run clean Unity 62f3 import/compile and the prefab validator. Verify one-instance placement, Undo, preservation of a moved/inactive instance, additive scene behavior and no forced save.

**Solo desktop:** start, watch one step, repeat, observe preserved prefix plus one step; deliberately fail and immediately restart. Hold any pad during demonstration and through input entry; release and press again to proceed. Hold START through multiple states. Verify max-length completion, no unbounded growth, result release, input abandonment, component disable/re-enable and missing-reference errors.

**Two clients:** simultaneous starts choose one operator; either client can own when free; non-operator touches do not add notes. Both see each cue/assessment and hear one note, including repeated same-number steps. Join/re-enter midway without replaying past notes. Test operator walking away, switching sectors, disconnecting, headset pause, and quitting while another actor is controller. Force controller changes and A -> B -> A, re-enable a controller component, and rejoin the same named room. Verify recovery to available state, no stale acceptance, and no other-sector audio or ownership changes.

**Headset:** seated and standing reach with both hands, clear 1-4 labels and display at comfortable distance, no reliance on color alone, clean releases with compound fingertip colliders, no accidental neighboring-pad hits, no resting chatter or locomotion shove, readable success/failure pacing, pleasant spatial volume, and no startup/door route obstruction. These are unexecuted checks, not passes.
