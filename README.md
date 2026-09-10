# Runaway Chimps

A Photon PUN VR horror game about escaping a laboratory. Open this project with **Unity 2022.3.55f1**. Cheeky Chimps is a separate Unity 6 project.

## Project documentation

- [Design and lore](docs/design-and-lore.md): maintained game rules, level concepts, images, proposals, and decision corrections.
- [Repository improvement plan](docs/repository-improvement-plan.md): review evidence, implementation status, priorities, and acceptance checks.
- [AGENTS.md](AGENTS.md): instructions to read and update the relevant documents when decisions, implementation, or test results meaningfully change.

The repository documents are the maintained versions; earlier Word documents are downloadable snapshots. Confirmed design, implemented code, and completed testing are separate statuses. The current reliability fixes remain pending Unity and headset validation.

## Current project layout

Start from `Assets/Scenes/Bootstrap.unity`. On `level1split`, the enabled build scenes are Bootstrap (0), Loading (1), Hub_Base (2), and Level1_Containment (3). The new containment scene and its metadata are committed at `ac69718`; Greg reports the extraction and doors are added. Geometry, references, and runtime behavior still need validation.

Level1, Level1_2_Hall, and the other disabled scene iterations are experiments. Use `Assets/Scenes/Level1_Containment.unity` for the new destination; the earlier planned name `Level01_Containment` is superseded. The prepared `level1split-travel.patch` wires the computer and both entrance doors to the travel service. Door returns use `HubDoorReturnSpawn` in the hallway; terminal returns use `HubReturnSpawn` in front of the computer. Both entrances to Level 1 use `Level1EntrySpawn` in its safe room. Each transition fades through Loading while keeping the same Photon room and XR rig. Unity and headset validation remain pending.

To add a scene through Unity 2022.3, drag its `.unity` asset from the Project window into **File > Build Settings > Scenes In Build**, or open it and click **Add Open Scenes**. Keep the intended scene checked and Bootstrap first. Commit `ProjectSettings/EditorBuildSettings.asset` after changing this list.

MiniGamesKidFirstRig is the current level monster. The inactive Zombie Crawl object is the chosen visual replacement. Sector monster synchronization is implemented on `level1split` and awaits multi-client validation. Zombie Crawl integration and the broader personal keycard/Level 2 exit lifecycle remain separate work.

## Level 2 map blockout

The editable Level 2 layout is in
`Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity`,
with a reusable prefab in the adjacent `Prefabs` folder. Open the scene, select
`Level2_Blockout_v03`, and press **F** over Scene view. Toggle the ceiling group
for an overhead view. See the [map setup and validation notes](Assets/RunawayChimps/Level2Blockout/README.md).

The map includes the repair-room escape loop, reused sci-fi gates and a locked
reward-room blockout at the bottom right off the bypass. It references the
existing MASH door assets. Scanner/reward shapes are placeholders. The Listener, repair progression and
cross-level bonus-card saving/rewards remain unwired. Level 2 is registered at
enabled build index 4 and uses Bootstrap's existing rig. Hub selection and Level 1
completion now lead to its safe entry; future-terminal actions return to Level 1
or the Hub computer. Start from Bootstrap for runtime travel tests.
Source/box-layout checks pass; Unity import, gate mesh fit and headset checks
remain pending.

## Audio and proximity fixes

- `AudioScaler` stops its source when the vent filter, patrol/chase filter, or automatic distance threshold excludes playback. Disabling the scaler also stops its source. Mute-change logging respects `debugLogs`.
- `PlayerVentState.LocalPlayerInVent` uses `ZoneStateService.LocalZone` when the service exists and the client is in a Photon room. Hub_Base already uses ZoneTrigger and LocalRigMarker; the local rig does not need a parent PhotonView for this query. Older scenes without the zone service retain the Vent-tagged-volume fallback.
- `ProximityManager` resets effects when a reactor belongs to another zone. `ProximityReactor` sends one exit for an active interaction and a zero proximity value, allowing materials and audio to reset. Disabling a reactor clears its state, so returning can trigger a fresh entry.
- `MaterialSwapper` tolerates a renderer being destroyed during unload and reports an invalid material slot instead of throwing an index error. The correct renderer and material slot still need assigning when integrating Zombie Crawl.
- Proximity checks use the existing typed `ZoneId` field and reuse their snapshot collection instead of allocating an array and looking up the field by reflection every tick.
- `PhotonVRManager.DefaultRoomLimit` is ten. The current manager prefab and Bootstrap instance have no serialized override. New rooms use this default; existing sixteen-player rooms are not resized, and public matching filters by the requested capacity.

The earlier reliability change did not change component names, public serialized fields, scene files, prefab files, or asset GUIDs. The subsequent `level1split` work changes build registration, scene travel wiring, loading and rig recovery, and sector presence/monster messages. It fixes the missing entrance-button target and moves XR interaction management into the persistent service. Keycard count and closest-player switching remain unchanged; monster targets now require the same sector and vent zone.

## Checks before merging

The branch was reviewed at source level and checked with `git diff --check`. Unity compilation, Play Mode, audio playback, Photon sessions, and headset behavior could not be run in the editing environment. Perform these checks in Unity 2022.3.55f1:

1. Open Bootstrap and confirm compilation succeeds. Enter Play Mode and join a new room. Check that the room's maximum player count is ten. Test both public joining and a new private code; if a local Inspector override exists, reconcile it with the ten-player setting.
2. Enter the vents and confirm the local zone becomes Level1_Vents. With the monster patrolling, approach until the patrol loop is audible. Start a chase: the patrol loop must stop and the chase loop must play. When chase ends, only the patrol loop should play.
3. While near the monster, enter the safe room. Check that the zone changes, the proximity material returns to its normal state, and vent audio stops. Repeat at both keycard-room entrances and the containment room. If a room does not set the intended safe zone, inspect its existing ZoneTrigger placement; this branch does not move trigger volumes.
4. Move beyond the audio threshold without changing zones. Confirm automatic audio stops. Return, and confirm it starts again. Repeat for both audio sources.
5. While in proximity range, disable and re-enable the relevant ProximityReactor in Play Mode. Effects must reset on disable and react again on the next proximity update. Disable an AudioScaler while its source is playing: the source must stop. Restore the components after the check.
6. Use two clients: one remains in the hub while the other enters the vents. Only the vent client's local vent audio should be eligible. This checks local filtering; also run the separate monster synchronization checks below.
7. Confirm unrelated hub and shop proximity interactions still enter and exit correctly, including reactors with ZoneId.None. Those reactors retain their existing behavior across zones.

Run headset audio checks with the existing patrol and chase clips. The in-game TEST Play Now context menu changes spatial audio settings for debugging and is not a substitute for these checks.

## Scene travel checks

Scene registration is published at `6443fd0`; the travel and additional cleanup are merged through PR #3. Start from Bootstrap. Select **Tools > Runaway Chimps > Validate Sector Travel** outside Play Mode to audit enabled scenes, required references, distinct Hub markers, door/button wiring, the persistent rig arrangement, and baked navigation data. The validator leaves your open scenes unchanged; it does not replace runtime testing.

| Route | How to try it | Expected arrival |
| --- | --- | --- |
| Computer → Level 1 | Use Up/Down to select Level 1, then Enter | Level 1 safe cage room |
| Hallway → Level 1 | Press the existing entrance button, or select the entrance door with the local XR controller | Same safe cage room |
| Level 1 → Hub door | Select the matching entrance door from inside Level 1 | Hallway on the Hub side of the door, facing into the hallway |
| Terminal → Hub | Future terminal calls `SectorTravelService.ReturnToHub()`; for an Editor test use the service component's Return to Hub context menu during Play Mode | In front of the Hub computer, facing it |

The future level terminal UI is not built; adding it is deferred until more levels exist. Its return API and computer spawn are ready; the final door-versus-button interaction choice remains open. Walking into a door should not start travel.

- Confirm fade → Loading → destination works in both headset eyes. Travel must preserve the room code, actor number, and one local rig. Check clear floor space and comfortable orientation at all three arrival markers; missing floor or obstructed head/body must reject arrival and restore the source.
- Repeat each direction at least ten times, including repeated button presses and travelling while holding a card. End with one active environment, no old level objects/cards, no stale hand impulse, no stuck movement/fade, and working grab/button interactions. Re-entry starts a fresh local Level 1 scene; personal card consumption and Level 2 completion still need their separate implementation.
- With two clients in one room, leave the room master in Hub while the other enters Level 1. Avatars, name meshes, collision, and voice playback must be absent across sectors and return when reunited. Existing Photon Voice traffic is still received; this change filters playback, not network bandwidth.
- Bring both clients into Level 1 and compare monster pose/chase state. Move one into a safe room, return the controlling player to Hub, disconnect them, suspend/resume their headset, and join during a chase. The remaining eligible level player must take control; an empty level has no active controller. Verify the baked NavMesh supports patrol/chase after each handover.
- Force a missing destination/arrival marker in a temporary local test, interrupt the connection during travel, and test a load timeout. Before arrival commits, restore the original environment and all previous movement/physics flags; no stuck black screen or duplicate queued arrival. Restore test edits afterward.
- Test capture with a held card and safe-room recovery. Confirm other players and their personal objectives remain unaffected. Profile scene-transition peak memory and frame time on Quest.

Source syntax and serialized-reference checks passed in the editing environment. **Unity compilation/import, the Editor validator, Play Mode, Photon sessions, and headset tests have not been run.**

## Additional cleanup checks

The combined travel patch also fixes the Hub computer's Color action and name-save handling, room-specific player spawn cancellation, local-only visual readiness, stale physical-button presses, verbose trigger/proximity logs, unnecessary material cloning, hand-impact audio, and held-item collision-layer restoration. The Color page now includes a preview label and hex value. Existing component class names and asset GUIDs are preserved; the held-item script and its metadata are renamed from `DisableCollisionWhileHeld` to `HeldItemCollisionMode` to match the class.

1. At the computer, adjust Color and press Enter. Confirm the avatar changes, the value survives returning to Hub and restarting, and another player sees the update.
2. Leave/disconnect while the player spawner is waiting, then rejoin. Reconnect while Level 1 is active and Hub is unloaded. Confirm one local avatar in the current room, no late spawn from an old attempt, and no waiting forever for an unloaded Hub. Force a missing environment to check the 30-second timeout diagnostic; full startup retry UI is still pending.
3. Confirm only the local avatar's settle check marks local visuals ready. A remote avatar or a cancelled old-room coroutine must not do so.
4. Press a button, disable the pressing collider or the button, restore it, and press again. It should return to its resting position and accept one new press.
5. Verify zone/audio transitions with diagnostics off. Repeated proximity swaps should preserve other material-slot references and should not create material copies merely to replace one binding.
6. On the Name page, try blank input, repeated Enter while a save is pending, a failed request, and saving while signed out. Keep the existing name until success, retain the typed edit after failure, and allow retry. Travel during a pending save and confirm a successful result persists without touching destroyed terminal UI.
7. Adjust the Color preview before pressing Enter; only the preview should change until Apply. Check reset, dark colors, and monitor text fit in VR.
8. With hand-audio fallback casts temporarily off, approach a wall and ceiling and then move away. Sound should require an inward impact. Check a surface-specific audio profile without a default, an empty override with a valid default, and travel/capture without a false hand slap. Restore the test settings.
9. Confirm the renamed HeldItemCollisionMode component imports on the existing Level 1 item. Test an item with different root/child layers, a second hand joining and releasing, final release, and disable/re-enable while held. Restore each original layer after final release/disable; travelling with the item must release it correctly. This does not complete personal keycard ownership or consumption.

Source-level validation covers syntax, serialized references, whitespace, and applying the complete patch back to its base. Runtime behavior and performance still need Unity and headset validation.

## Level 2 travel checks

On this branch, the Hub computer has a **Level 2** menu option. Insert both
personal Level 1 cards to travel automatically to the same Level 2 safe entry.
Incomplete keyboxes do not qualify; a failed transition retains completed state
for a local retry by selecting the keybox. Each new Level 1 visit starts fresh.

Level 2's west safe-entry wall now has one **RETURN TO SECURITY** button.
It calls `LevelTerminalActions.ReturnToHub()` and arrives at the Hub computer.
The plate and cap are editable scene/prefab geometry; its plain text label appears
at runtime. Only the local hand/fingertip can press it. There is no screen or
Level 1 selection UI; `ReturnToLevelOne()` remains available as a code/context-menu
hook. Check label readability, hand reach, held-contact suppression, rejection of
remote hands/head/body/props, and release/retry after failed travel in Play Mode.

Run the expanded Editor travel validator, then test all four routes, repeated
inputs, rollback/retry, fresh Level 1 visits, two-client independent completion
and sector visibility/voice, and headset floor/body/head clearance. C# syntax and
serialized wiring checks passed; Unity compilation and runtime tests are pending.
The card scripts' filenames now match their existing classes with GUIDs retained.
