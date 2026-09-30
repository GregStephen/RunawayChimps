# Knock-Back Vent prototype

Recorded: 2026-09-30. **Confirmed prototype direction; implemented on `feature/knock-back-vent`, not merged.** Based on freshly read main `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`. Preserve Unity **2022.3.62f3 (96770f904ca7)**, Photon PUN, built-in rendering, packages and XR settings. The PR owns only this toy; it does not depend on another unmerged toy branch.

## Experience and limits

A metal panel reads **DO NOT COMMUNICATE WITH OCCUPANTS.** A local player taps with either tracked hand, pauses, and hears something behind the panel repeat the rhythm. The first accepted tapper owns that recording; another player cannot interleave a different rhythm into it. Either hand of the owning player remains usable. Two hands contacting in one render frame become one knock.

The default is an exact repeat in 90% of cycles, one additional knock in 6%, or one heavier final knock in 4%. The authority draws the variation once. These are configurable probabilities, not a claim about a short observed sample. Nothing emerges, attacks, awards currency/cosmetics or changes objectives, travel, monster state or progression. Only the inner visual panel moves, by a default 2 mm; the solid sensing collider stays fixed.

State is **Idle -> Recording -> Replying (including its initial delay) -> Cooldown -> Idle**. Default recording accepts at most six taps in 3.5 seconds and ends after 0.65 seconds of quiet. The answer begins another 0.75 seconds later. Extra input does not extend a full recording or create a second queued sequence. There is one recording, at most eight input taps and nine answer sounds, and a fixed set of 17 reusable audio sources. There are no coroutines, growing playback queues or retained event histories.

## Exact setup

Prefab: **`Assets/RunawayChimps/KnockBackVent/KnockBackVent.prefab`**.

Outside Play Mode, choose **Tools > Runaway Chimps > Toys > Place Knock-Back Vent in Hub**. The command opens the existing `Assets/Scenes/Hub_Base.unity` additively when needed, uses its arrival marker to find a nearby wall beside the terminal, and instantiates the real prefab. It supports one grouped Undo, selects an existing Hub vent rather than moving or duplicating it, and preserves other scene objects and overrides. A warning identifies a fallback placement when no wall is found. Inspect reach, wall fit and spawn/terminal clearance, then **save the Hub yourself**. The command never forces a scene save. Opening the project does not install anything.

The PR deliberately leaves `Hub_Base.unity`, Bootstrap, build scenes, packages and project/XR/render settings unchanged. There is no automatically installed vent until you explicitly place and save it. Both multiplayer test builds need the same saved placement.

Move and rotate the prefab root freely. Its **local +Z faces the player** and reply audio sits at local Z = -0.18 m, behind the panel. Keep a positive, uniform root scale, preferably `(1, 1, 1)`; nonuniform/mirrored scale fails reference validation. The root collider is 0.90 x 0.64 x 0.10 m. When moving it to another sector, update `sector` as well. A copied vent requires a unique `interactionId` within its sector; the Inspector has **Assign unique ID to this copy (Undo supported)**. Save that same ID on every client. Simultaneously active duplicate IDs fail closed rather than replying twice.

## Try a sample without a headset

Open Hub directly, make it the active scene, select the placed vent, and enter **offline Play Mode**. Do not start via Bootstrap for this diagnostic, because Bootstrap normally joins Photon. Enable **Scene view audio** and position the Scene view near the vent, or use an existing AudioListener. The toy does not create a camera, rig or extra listener.

Use **Tools > Runaway Chimps > Toys > Play Selected Knock-Back Vent Sample (Offline Play Mode)**, or the Inspector's **Play sample: tap, tap ... tap** button. It performs taps at offsets 0, 0.22 and 0.58 seconds, then uses the production recording/reply/cooldown path. With both variation chances set to zero before Play Mode, expect exactly those three answer timings. **Tools > Runaway Chimps > Toys > Cancel Selected Knock-Back Vent Sample** or **Cancel sample** stops all scheduled sounds and restores the panel. The sample is Editor-only and refuses connected rooms; joining a room cancels an offline sample. It is an audio/timing diagnostic, not a substitute for physical-hand or network testing.

## Inspector tuning

Rhythm settings are bounded and copied when the component enables. Exit and re-enter Play Mode after changing them. Presentation volume/movement can be adjusted while testing.

| Field | Default | Supported range / meaning |
| --- | --- | --- |
| `maxTaps` | 6 | 1-8; no overflow queue |
| `minimumInterval` | 0.12 s | 0.10-0.30 s between accepted taps |
| `quietInterval` | 0.65 s | 0.40-1.20 s; pause that ends recording |
| `maximumRecording` | 3.5 s | 1-5 s; hard recording deadline |
| `replyDelay` | 0.75 s | 0.40-2 s after recording closes |
| `cooldown` | 2 s | 1-8 s after final reply plus a 0.5 s audio tail |
| `extraKnockChance` / `heavyBangChance` | 0.06 / 0.04 | Each at most 0.15; combined at most 0.20. Set both to 0 for exact repeats. Heavy replaces only the final knock. |
| `extraKnockGap` | 0.28 s | 0.15-0.80 s after the repeated pattern |
| `tapVolume` / `replyVolume` / `bangVolume` | 0.40 / 0.55 / 0.65 | 0-1; start with conservative headset output volume |
| `movementMetres` | 0.002 | 0-0.006; set 0 to remove movement |
| `minimumTapSpeed` | 0.30 m/s | Inward rig-relative hand speed, 0.10-1 |
| `maximumHandSpeed` / `maximumFrameStep` | 7 m/s / 0.22 m | Discontinuity rejection, bounded to 2-10 / 0.08-0.30 |
| `releaseSeconds` | 0.07 s | 0.04-0.20 s continuously released before another contact |

The `AcceptedTapAudio` and `BehindPanelAudio` children contain the preassigned voice arrays. Their sources use full 3D spatial blend, linear attenuation from 0.6 to 8 m, no Doppler, no loop and no play-on-awake. Keep all voices' distance settings consistent when tuning. Clips must remain at most 0.5 seconds. All references and three built-in Standard materials are assigned in the prefab. The existing `BlockHandSurfaceAudio` marker suppresses the normal hand-impact path for this panel; do not remove it or add another impact source.

The three mono 22,050 Hz / 16-bit PCM WAVs are original procedural metal impacts: `Audio/Tap.wav` (0.09 s), `Audio/Reply.wav` (0.24 s), and `Audio/Bang.wav` (0.45 s). No downloaded recordings are used. The explicit offline authoring utility `python Tools/KnockBackVent/author_assets.py` can recreate the committed asset subtree with stable GUIDs; it overwrites this toy's generated prefab/materials/audio, so do not run it to install the toy or after hand-authoring asset changes without reviewing the diff. Runtime does not generate assets or audio clips.

## Input, networking and lifecycle

Input reads only the Bootstrap Gorilla player's tracked left/right controller transforms, offsets and `LocalRigMarker`. Controller motion supplies intent, but a tap also requires the corresponding collision-resolved `leftHandFollower`/`rightHandFollower` at the front of the enabled panel. A controller inside a wall or beyond arm reach cannot tap while the virtual hand is blocked elsewhere. Forward release clearance is 35 mm in world space, including uniformly scaled copies. A release is required per physical hand, not per collider. A slow contact consumes the arm state too. Resting contact, another collider on the same hand, remote-avatar collisions, head/body/prop collisions and reply audio cannot initiate another tap. Tracking validity, sample gaps, rig/head discontinuities, hand speed/displacement and reach are checked. Root yaw jumps and XR recenter notifications reset hand history. Rig-relative input speed preserves deliberate taps when Gorilla collision resolution translates the body.

The existing lowest-active-actor **sector election** supplies one authority per vent. Photon event 189 carries the namespaced `rc.knock.v1` payload, sector, saved interaction ID, room name, cycle, issue time and actual tapper. There is no scene PhotonView, new networking service or cached event. Requests go only to the authority; accepted tap timestamps and one complete chosen answer go only to a bounded snapshot of that sector's original participants. PUN `SendReliable` + `DoNotCache` is used throughout. Accepted tap audio uses the input timestamp plus a fixed 0.20 s transport allowance so packet-arrival jitter does not rewrite the rhythm; reply offsets use the same input timestamps and schedule against the local audio DSP clock.

The receiver rejects wrong sender/sector/identity, stale/future/duplicate/malformed messages, retired cycles and input sent during cooldown. Room object replacement, departure/travel, authority changes (including A -> B -> A), tapper departure, pause, disable, audio-device changes and long stalls cancel pending playback. A failed authority send cancels local presentation instead of knowingly playing an authority-only answer. There is no state replay or late-arrival synchronization: a late sector arrival waits for a new interaction. A plan arriving more than 80 ms after its start is dropped as a whole, not accelerated into a backlog; severe latency intentionally fails silently. Recording without a valid answer times out after at most 6.65 seconds from the cycle start rather than getting stuck forever. This bound includes the maximum five-second recording, permitted frame gap and message transit allowance; a legal plan constructed after a sub-750 ms hitch is not rejected merely for missing a tighter hard-coded deadline. Lifecycle resets reject equal-timestamp retired packets/input, while ordinary cooldown completion still accepts a fresh tap at its exact deadline. Received Cancel messages retire through the authority issue time, so their transit delay does not also erase a newer live cycle. Cancellation sends are restricted to the same live room object, and a callback already in flight cannot revive a disabled component or panel collider.

This uses the repository's client-authoritative PUN trust model, not server-side proof of remote physical contact. Real transport ordering, sector-property races, DSP behavior and headset sound localization still require the tests below. An enabled sector participant is expected to have the same saved prefab; removing/disabling the elected participant's toy while that actor stays in-sector fails closed, rather than inventing a separate authority service.

### September 30 self-review corrections — PR #79

**Reviewed baseline:** `8ee79c4d4d5db39ff3452134e21298bacb33c3aa` on `feature/knock-back-vent`. Greg requested a self-review and fixes, not a merge or broader toy work. The confirmed experience, prefab/setup command, audio, Unity 2022.3.62f3, Photon PUN, built-in rendering and XR/package/scene settings remain unchanged.

**Implemented on this branch, pending runtime validation:** cancellation now rejects equal-timestamp stale events and queued input, while retaining the inclusive normal cooldown deadline. A received cancellation uses its authority issue time so transport latency does not discard a newer valid rhythm. Reply validation and the missing-plan watchdog share bounded recording/frame-gap/transport budgets, fixing legal five-second recordings dropped after a sub-750 ms render hitch. Teardown sends only into the same live room object; disabled components/colliders cannot be revived by an already-dispatched callback. Physical taps additionally require the Gorilla collision-resolved hand at the panel's front, not merely a tracked controller passing through a wall or exceeding reach. Release clearance stays 35 mm in world space on uniformly scaled copies.

**Validated managed regressions, 2026-09-30:** **11 added adapter scenarios failed against the reviewed production code before the fixes**, then passed. The complete production adapter suite now passes **26 cases / 165 assertions**; the shared core/Unity-test checks pass **18 cases / 3,967 assertions** in the managed runner; the unchanged card-state harness passes **18,015 assertions**. These tests include same-timestamp Cancel/A-B-A, delayed Cancel followed by a fresh cycle, a 600 ms frame hitch at the maximum recording setting, a delayed valid reply, missing-plan timeout, disconnected/same-name-replaced rooms, disabled callbacks/colliders, blocked virtual hands, and both hands at 0.5/1/2 root scales. Existing initial-implementation counts above are historical. The local source export omits unrelated binary assets and the syntax-parser package; full-checkout syntax/asset/repository/CI evidence is recorded on PR #79, not inferred from that partial export.

**Still pending:** Unity import/compilation and Test Runner, native Gorilla collision/hand-follower behavior and Editor placement/Undo, listening and Quest 2/3, and real two-client PUN lifecycle/latency acceptance. Specifically retest a controller reaching through an obstruction, fresh release at each supported root scale, disabled/re-enabled panel, same-timestamp authority changes, and delayed cancellation immediately followed by a new rhythm. The review adds no automatic scene modification and closes no runtime/MVP gate.

## Executed diagnostics and evidence boundary

At the initial implementation revision on 2026-09-30, the shared production core passed **16 cases / 3,958 assertions**, including timing, quiet cutoff, bounded input, owner contention, deterministic variation branches, cooldown, cancellation in every phase, fresh release/resting/compound-contact rejection, tracking reset, stale delivery, malformed data and 500 stress cycles. The production runtime/hand adapter plus the actual `SectorPresence` implementation passed **14 managed cases / 90 assertions** using explicit Unity/Photon/XR/audio doubles. Coverage includes authority handoff, audience snapshots, owner departure, room replacement, pause/disable/audio cancellation, duplicate IDs, offline samples, late plans, failed sends, cooldown packet boundaries and translated/rotated hand-sampling math. The existing card-state harness also passed **18,015 assertions** unchanged.

The read-only feature validator checks prefab object IDs, local and external references, bounded spatial voices, fixed collider/relative visual, warning/material assignments, PCM headers/duration/non-silence and source/lifecycle/Editor-install boundaries. An independent YAML parse accepted all 84 serialized prefab objects. The source/CI workflow adds both harnesses and this validator without dropping any existing checks. Exact full-checkout source-validation results and published commit are recorded on the PR; a green check is not Unity validation.

Local C# diagnostics compiled the linked production files with .NET 8 Roslyn and reference assemblies; the adapter uses doubles, not native Unity or a running Photon session. **Unity 2022.3.62f3 import/compilation, Unity Test Runner, Editor command/Undo execution, audio listening, headset input, two-client Photon and Quest runtime acceptance are pending.** No source parser, managed assertion, prefab YAML check or PR merge state establishes those passes.

Useful commands from the repository root:

```sh
dotnet run --project Tools/KnockBackVentHarness/KnockBackVentHarness.csproj --configuration Release
dotnet run --project Tools/KnockBackVentAdapterHarness/KnockBackVentAdapterHarness.csproj --configuration Release
python Tools/validate_knock_back_vent.py
python Tools/validate_source.py --syntax
```

The Unity Test Runner fixture is `RunawayChimps.Tests.KnockBack.VentModelTests` (18 parameterized cases); its assertions share the same core checks as the managed harness.

## Pending headset / two-client acceptance checklist

Record exact commit, Unity version, device(s), number of clients, outcome and Console errors. Existing #63/#64 and other unverified release gates remain separate.

1. **Import and authoring:** open in 2022.3.62f3 with no missing scripts/materials/audio or new Console errors; run the Unity fixture. Place from the exact menu, Undo/redo, invoke again after moving/rotating, save/reopen and confirm the same one instance and overrides. Check readable outward-facing text, comfortable hand height, solid wall fit and unobstructed terminal/spawns. Try the offline sample and cancellation before/during its reply.
2. **Physical hands:** on Quest 3 and Quest 2, try single taps, uneven three-tap rhythms and the maximum-length pattern with each hand and alternating hands. With variations zero, count matching reply taps and compare rhythm. Rest, slide slowly, use overlapping hand/fingertip colliders, tap with props/head/body and observe a remote avatar: none should create repeated or unintended recordings. Require release before another tap; no second normal hand-impact sound.
3. **Tracking and comfort:** recenter, lose/regain controller tracking, teleport/respawn/travel, pause/resume and introduce a frame stall while near/resting on the panel. There must be no manufactured taps, old queued answers, stuck movement or altered locomotion. Start quietly and confirm the bang is not excessively loud. Check the tiny motion or set it to zero.
4. **Same-sector sharing:** use identical placements/builds in one Photon room. Swap elected controller and tapper roles; have both players tap simultaneously. Both should hear only the accepted owner's pattern and identical chosen answer, from behind the same panel with distance attenuation. A player in another sector hears nothing. Restore 6%/4% variation defaults and confirm variations never combine or independently diverge.
5. **Cancellation and arrivals:** leave the sector, disconnect/change rooms, remove the owning player, pause/disable the authority, or switch controller A -> B -> A during Recording, delayed Replying, audible Replying and Cooldown. Every remaining listener must stop stale playback and accept a later fresh cycle without duplicates. Join/return midway through a cycle: no old knocks or partial answer backlog. Simulate severe latency and verify dropped old plans do not burst on receipt.
6. **Repeated play:** complete repeated cycles/travel/room rejoins on both target devices. Check the Console and profiler for accumulating sources/events/objects, unintended objective/currency changes or frame/audio degradation. The prefab remains a small optional toy, not a release-validation shortcut.
