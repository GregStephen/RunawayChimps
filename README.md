# Runaway Chimps

A Photon PUN VR horror game about escaping a laboratory. Open this project with **Unity 2022.3.55f1**. Cheeky Chimps is a separate Unity 6 project.

## Current project layout

Start from `Assets/Scenes/Bootstrap.unity`. The enabled build scenes are Bootstrap, Loading, and Hub_Base. Hub_Base currently contains the starting area, shop, and Primate Containment gameplay under Level1Root.

Level1, Level1_2_Hall, and the other disabled scene iterations are experiments. The planned map split extracts containment from Hub_Base into a new Level01_Containment scene at the Security Gate. The split and runtime travel are not implemented by this branch.

MiniGamesKidFirstRig is the current level monster. The inactive Zombie Crawl object is the chosen visual replacement. Monster synchronization, rig integration, and the personal keycard lifecycle remain separate work.

## Audio and proximity fixes

- `AudioScaler` stops its source when the vent filter, patrol/chase filter, or automatic distance threshold excludes playback. Disabling the scaler also stops its source. Mute-change logging respects `debugLogs`.
- `PlayerVentState.LocalPlayerInVent` uses `ZoneStateService.LocalZone` when the service exists and the client is in a Photon room. Hub_Base already uses ZoneTrigger and LocalRigMarker; the local rig does not need a parent PhotonView for this query. Older scenes without the zone service retain the Vent-tagged-volume fallback.
- `ProximityManager` resets effects when a reactor belongs to another zone. `ProximityReactor` sends one exit for an active interaction and a zero proximity value, allowing materials and audio to reset. Disabling a reactor clears its state, so returning can trigger a fresh entry.
- `MaterialSwapper` tolerates a renderer being destroyed during unload and reports an invalid material slot instead of throwing an index error. The correct renderer and material slot still need assigning when integrating Zombie Crawl.
- Proximity checks use the existing typed `ZoneId` field and reuse their snapshot collection instead of allocating an array and looking up the field by reflection every tick.
- `PhotonVRManager.DefaultRoomLimit` is ten. The current manager prefab and Bootstrap instance have no serialized override. New rooms use this default; existing sixteen-player rooms are not resized, and public matching filters by the requested capacity.

No component names, public serialized fields, scene files, prefab files, or asset GUIDs were changed. Existing Inspector event bindings remain applicable. The current keycard count and monster target-selection rules are unchanged.

## Checks before merging

The branch was reviewed at source level and checked with `git diff --check`. Unity compilation, Play Mode, audio playback, Photon sessions, and headset behavior could not be run in the editing environment. Perform these checks in Unity 2022.3.55f1:

1. Open Bootstrap and confirm compilation succeeds. Enter Play Mode and join a new room. Check that the room's maximum player count is ten. Test both public joining and a new private code; if a local Inspector override exists, reconcile it with the ten-player setting.
2. Enter the vents and confirm the local zone becomes Level1_Vents. With the monster patrolling, approach until the patrol loop is audible. Start a chase: the patrol loop must stop and the chase loop must play. When chase ends, only the patrol loop should play.
3. While near the monster, enter the safe room. Check that the zone changes, the proximity material returns to its normal state, and vent audio stops. Repeat at both keycard-room entrances and the containment room. If a room does not set the intended safe zone, inspect its existing ZoneTrigger placement; this branch does not move trigger volumes.
4. Move beyond the audio threshold without changing zones. Confirm automatic audio stops. Return, and confirm it starts again. Repeat for both audio sources.
5. While in proximity range, disable and re-enable the relevant ProximityReactor in Play Mode. Effects must reset on disable and react again on the next proximity update. Disable an AudioScaler while its source is playing: the source must stop. Restore the components after the check.
6. Use two clients: one remains in the hub while the other enters the vents. Only the vent client's local vent audio should be eligible. This checks local filtering; it does not prove monster synchronization, which remains unfinished.
7. Confirm unrelated hub and shop proximity interactions still enter and exit correctly, including reactors with ZoneId.None. Those reactors retain their existing behavior across zones.

Run headset audio checks with the existing patrol and chase clips. The in-game TEST Play Now context menu changes spatial audio settings for debugging and is not a substitute for these checks.
