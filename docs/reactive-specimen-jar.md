# Reactive Specimen Jar — optional prototype

September 30, 2026. Implemented on `feature/reactive-specimen-jar`, based on freshly read main `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`; not merged by this work. Unity remains **2022.3.62f3 (96770f904ca7)**, Photon PUN and built-in rendering. No package, XR, rendering setting, Hub scene or Bootstrap scene changes.

## Confirmed scope and multiplayer rule

A fixed, non-grabbable jar contains one small original eyeball-like biological growth. It idles, notices the nearest eligible local hand, approaches the glass as that hand gets closer, recoils from a genuine tap, and occasionally watches the local player's face after a look-away. It never escapes, attacks, blocks an objective, rewards a player, or establishes a named subject or experiment history. This is optional atmosphere, not new canon or a release requirement.

**LOCAL PER VIEWER:** every client runs its own cosmetic state and motion. A sees A's hand/head reactions; B sees B's. The jar has no PhotonView, RPC, ownership transfer, shared target, network animator, or saved progression. Different eye poses on two clients are intentional. Remote avatars cannot supply hand input. Photon room/actor identity is read only to invalidate an old local session.

## Setup — no automatic scene edits

Prefab: `Assets/RunawayChimps/Toys/ReactiveSpecimenJar/ReactiveSpecimenJar.prefab`.

Open `Assets/Scenes/Hub_Base.unity` outside Play Mode, then choose:

**Tools > Runaway Chimps > Toys > Place Reactive Specimen Jar in Hub**

The command places one prefab beside `HubReturnSpawn`, with its stand resting at the marker's floor height. Inspect clearance from the terminal, spawn slots and walking routes; reposition/rotate the entire root as needed. Its local +Z is the front and its root is the jar's center. **Save the scene manually** when satisfied. Placement supports Undo/Redo. Running the command again selects an existing jar, including an inactive one, without moving it or replacing its overrides. It will not open, save, overwrite, or install anything automatically. Deleting/undoing it leaves no installer to recreate it.

The command's initial offset is a test convenience, not an approved final Hub location. Only the jar body has a solid BoxCollider; the decorative stand is not a locomotion obstacle. Keep the jar out of required paths. Start the normal game from Bootstrap for headset/Photon testing; the prefab does not create an extra rig, camera or AudioListener.

## Desktop preview

On a **scene instance**, use **Create desktop preview handles (Undoable)** in its Inspector. This creates EditorOnly head/left/right transforms and enables Editor Preview. Enter Play Mode and select the jar. Use its Scene-view position/rotation handles, or the child transforms, to move the hands and turn the head (+Z is gaze). Existing preview handles are preserved. Creation is disabled for prefab assets and Prefab Mode/loaded prefab contents; Scene-view handles also reject prefab asset/contents transforms assigned manually. The corrected Undo operation records both the jar settings and the complete handle hierarchy, including Redo poses. Native Undo/Redo acceptance remains pending.

The same sampled-hand and state logic runs. Ease a hand toward a pane, leave it at least 0.12 seconds fully withdrawn, then move inward briskly for a real tap test. A large one-frame Scene-view drag is deliberately rejected as a tracking jump. **Preview recoil** is explicitly a visual diagnostic that bypasses tap detection; it does not prove tapping works. The Inspector shows state, target, accepted-tap count and watch count since reset. **Reset preview state** restores the authored pose and disarms contact.

For the surprise, move both hands beyond the interest radius while leaving them active, set Watch Chance to 1 and Watch Cooldown to 2 temporarily, face the jar, then turn the preview head in small increments until it points away for more than Away Delay. Look back within Watch Seconds. Restore the shipped defaults afterward. Disabling both preview hands or the head resets the toy. Preview inputs are compiled out of player builds; EditorOnly handles are stripped. Turn Editor Preview off before real-rig testing. Never apply test handles or test tuning to the shipping prefab accidentally.

## Inspector tuning

Distances below are world metres except Motion Extents and Idle Drift, which are jar-root local units. Keep **uniform positive root scale** and the authored specimen size. Moving/rotating the whole prefab is supported; resizing its individual glass/specimen children requires revalidating containment.

| Control | Default | Meaning |
| --- | --- | --- |
| Interest Radius | 0.55 m | Distance from the hand contact sphere to the glass; closest eligible hand wins, ties go left. |
| Response Speed / Turn Speed | 6 / 8 | Exponential position/rotation smoothing. Target switching never snaps. |
| Motion Extents | 0.095 / 0.14 / 0.095 | Ellipsoid for the specimen center, reserving full rotation and breathing clearance. |
| Idle Drift | 0.008 | Small suspended motion. |
| Viewer Distance | 3 m | Beyond this, reset rather than retain an unattended reaction. |
| Hand Radius / Contact Skin | 0.05 / 0.008 m | Contact envelope around the tracked controller transform. Tune with both real hands. |
| Minimum Tap Speed | 0.30 m/s | Inward speed, not merely tangential motion along glass. |
| Release Margin / Rearm Seconds | 0.025 m / 0.12 s | Full withdrawal needed before another tap. |
| Reaction Cooldown / Recoil Seconds | 0.55 / 0.40 s | One global reaction window; simultaneous hands cannot double it. |
| Maximum Hand Step / Speed | 0.28 m / 10 m/s | Reject discontinuous samples. Long frames over 0.1 s also reset. |
| Watch Chance / Cooldown | 0.35 / 18 s | One roll per eligible look-away episode; cooldown also starts on reset and after a failed roll. |
| Away Delay / Watch Seconds | 0.8 / 4 s | Sustained look-away requirement and watch duration. Hands take priority over watching. |
| Visible / Away Half Angle | 75 / 105 degrees | Conservative head-forward cones with a neutral band; no eye-tracking hardware. |

The runtime binds only the current Gorilla Player singleton's left/right tracking transforms under its marked local XROrigin. It requires valid XR head and controller tracking flags, rejects a non-owned ancestral PhotonView, and samples each anatomical hand once per frame. Hand colliders, fingertip duplicates and remote tags are not discovered as inputs. Losing an individual hand clears its history; loss of the head/both hands, distance, travel, inactive scene, room-object/actor changes, pause/focus loss or disable resets the whole toy. Reacquisition has a 0.2-second quiet period and contact must rearm; spawning/recentering inside the glass is not a tap.

The surprise only starts after the jar was in the head-facing cone and then well outside it. It always **moves smoothly**, even if the player turns back early. It does not use renderer visibility (which could include editor/remote cameras), eye tracking, occlusion rays or a teleport. Head-forward orientation is a conservative proxy for attention, not measured gaze. Real headset/controller tracking during large head turns still needs acceptance.

## Asset and performance boundaries

All geometry is original, reproducibly authored by `python Tools/build_specimen_jar.py`. Runtime uses the committed native Mesh assets and prefab, not a generator. The explicit generator overwrites this feature's defaults only; commit deliberate Inspector changes before regenerating. It does not open/edit scenes.

There are **569 vertices, 712 triangles, three renderers and five material slots**: lumpy specimen body/iris/pupil, four simple tinted panes, and a combined frame/stand. Assigned built-in Standard materials use a restrained emission tint for readability. No texture downloads, skeletal rig, fluid simulation, refraction, realtime light, reflection probe, shader package or new audio pipeline. Shadows/probe usage are off on these renderers. Actual draw calls, stereo transparency, readability and GPU cost still depend on Unity/Quest and must be measured; five material slots are not a measured draw-call claim.

## Automated checks and evidence

`dotnet run --project Tools/SpecimenJarHarness/SpecimenJarHarness.csproj --configuration Release` executes the actual production `SpecimenJarState.cs`: target selection, 5,000 bounds samples, one-shot/rearm gates, cooldowns, recoil priority, gaze episodes, chance and resets. It does not substitute a port of the algorithm.

`python Tools/validate_specimen_jar.py` checks serialized references, mesh/index data, normals/winding, whole-mesh containment at any rotation/breathing scale, material budgets and the local/tracking/lifecycle/authoring source boundaries. Twelve negative source mutations must be rejected, including the preview creation/Undo ordering and prefab-contents/handle isolation guards added during review. The Source validation workflow runs both new checks alongside the unchanged existing suite and card harness.

`Assets/Tests/SpecimenJar/SpecimenJarPlayModeTests.cs` contains **12 authored Unity cases** (including parameterized cases) using the actual prefab and native collider methods. They cover import/wiring/materials, full specimen bounds, both hands, compound-collider chatter, slow contact, discontinuities, resets, preview tracking and unrelated-hand hierarchy exclusion. Run the `RunawayChimps.SpecimenJar.PlayModeTests` assembly in the Editor's Play Mode Test Runner. It is not a live Photon two-client test.

`Assets/Tests/SpecimenJar/Editor/SpecimenJarAuthoringTests.cs` adds **six authored native Editor cases** in `RunawayChimps.SpecimenJar.EditModeTests`. Run this assembly in the **Edit Mode** Test Runner. It checks Undo/Redo restoration of prior preview settings, references and all poses under a moved/rotated/scaled root; creation after Undo; preservation of existing handles; and prefab asset/contents rejection (including manually assigned handle transforms). It uses an additive temporary scene, never saves the prefab/Hub, and does not clear the user's pre-existing Undo history. These cases are authored, not executed by the Python checker or managed policy harness.

Executed evidence is recorded in the maintained design/improvement documents and the PR. **Unity import/compilation, these Unity tests, real Photon behavior and headset performance are pending unless an explicit run is recorded.**

## Headset / two-client acceptance — not yet executed

Use the published branch in Unity 2022.3.62f3; first check clean import/compile and run both specimen Unity test assemblies (12 Play Mode and six Edit Mode cases). Then test Quest 3 and Quest 2 with Editor Preview disabled:

- Each hand independently: approach from several sides/heights, verify smooth nearest-hand switching and full containment, withdraw beyond radius, then step beyond viewer distance. Jar stays fixed and grip does nothing.
- Tap: deliberate gentle/firm inward taps each react once. Rest, slide, wiggle in contact, use both hands together and test duplicate fingertip colliders. Withdraw to rearm. Recenter, resume the headset, lose/reacquire controller tracking and begin with a hand intersecting glass; none should manufacture a tap.
- Look-away/back: withdraw both hands from interest, look at the jar, turn away, look back shortly afterward. Confirm the occasional face stare, cooldown, smooth return and no visible teleport. Test near the cone boundary and verify missing tracking does not leave a stale target. Temporarily deterministic preview settings may establish the visual, but restore production chance/cooldown for acceptance.
- Lifecycle: Hub → Level 1 → Hub, capture/return, public/private room switch, leave/rejoin the same room, disable/re-enable, pause/resume and repeated placement Undo/Redo. No stale recoil, phantom tap, duplicate jar, exception, forced save or progression change.
- **Two clients:** A keeps local hands outside radius while B reaches/taps the jar; A's target and accepted-tap count must not change. Reverse roles. Then each holds a different hand near the glass simultaneously: each client must see its own target. Move/respawn B's remote avatar through A's jar, change rooms and repeat. Record each client's view; unrelated-hierarchy unit coverage is not proof that this live check passed.
- Presentation/performance: inspect the eye through each pane at grazing angles, close distance and both eyes; verify no clipping, pink shader or unreadable pupil. Profile on Quest 2/3 and inspect Hub placement clearance. Existing unrelated main defects (#63/#64) remain separate and are not fixed by this toy.
