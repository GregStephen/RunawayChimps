# Level 2 expanded layout v0.4

Recorded: 2026-09-11.

## Status

**Confirmed:** Level 2 must be at least twice the current v0.3 blockout's length and width. The current blockout is 18 x 18 m, so v0.4 targets a minimum 36 x 36 m footprint.

**Confirmed room sizing / implemented blockout:** Greg reviewed the fuse-power v0.4 blockout and approved the current room sizing/proportions below. Branch `design/level2-expanded-map-v04` builds that arrangement into the normal Level 2 Unity scene and reusable prefab. Obstacle placement and runtime Listener navigation may still be tuned. `main` still contains v0.3.

Do not implement the size change by scaling the v0.3 root transform to 2x. Build larger authored spaces so normal room scale, gate scale, colliders and traversal remain believable.

## Rules retained from the current design

- Keep Safe Entry and Completed Exit as the only confirmed safe spaces.
- Keep the confirmed four-fuse power-restoration objective, with a central Repair Lab island and two distinct escape routes from the room.
- Keep the optional reward room in the bottom-right area.
- Keep the current sci-fi frame language for open passages and the established full exit gate for the completed exit.
- Keep RETURN TO SECURITY in Safe Entry.
- Common routes must remain large enough for the Level 2 creature to traverse and turn.

## Confirmed v0.4 room sizing

Use Unity X to the right and Z north. Retain the current entry end where practical and expand mainly north/east: overall X = 0-36 m and Z = -4-32 m.

| Space | Confirmed blockout bounds | Approx. size |
| --- | --- | ---: |
| Safe Entry | X 0-6, Z -4-0 | 6 x 4 m |
| Test Hall A | X 0-18, Z 0-14 | 18 x 14 m |
| Lower Service Hall | X 18-30, Z 0-14 | 12 x 14 m |
| West Observation Wing | X 0-8, Z 14-22 | 8 x 8 m |
| Conditioning Hall B | X 8-30, Z 14-22 | 22 x 8 m |
| East Bypass | X 30-36, Z 2-22 | 6 x 20 m |
| North Gallery | X 8-20, Z 22-32 | 12 x 10 m |
| Repair Lab | X 20-36, Z 22-32 | 16 x 10 m |
| Completed Exit | X 0-8, Z 26-32 | 8 x 6 m |
| Optional Reward Room | X 30-36, Z -4-2 | 6 x 6 m |

These room bounds/sizes are now the confirmed v0.4 design target. Individual obstacle transforms, doorway fitting and navigation clearances may still move during Unity/runtime validation without reopening the overall room-sizing decision.

## Route concept

The larger level should use multiple connected loops rather than one oversized room. Test Hall A and Lower Service Hall form the lower loop. West Observation Wing and Conditioning Hall B form a second middle route. East Bypass reconnects the lower half to the repair side. North Gallery connects the upper route to the qualified exit and one side of the repair room.

The Repair Lab has one opening into North Gallery and a second toward the East Bypass/Conditioning side. Its central power island creates a full circulation ring instead of a wall-mounted dead end. A normal local loop can be Repair Lab -> North Gallery -> Conditioning Hall B -> East Bypass -> Repair Lab, with longer branches through West Observation, Test Hall A and Lower Service Hall. Four fuse-search locations deliberately pull the player into those larger branches before returning to charge each fuse.

## Player-scale objective correction — confirmed September 12

The approved room bounds remain unchanged. Runtime testing showed the initial power island, fuses, sockets and levers were scaled for the large environment rather than the gorilla player. Interactive hardware must use the active rig as the scale reference: roughly 10 cm hand-contact diameter, 0.36 m body width, 1.16 m body-capsule height and 1.5 m max arm length. The corrected prototype targets a ~20 cm fuse, ~0.58 m socket center, ~0.70 m lever center and ~0.95 m island height. These values are implemented for the next Unity ergonomic test and may receive small reach/comfort tuning without reopening the confirmed room sizes.

## Confirmed four-fuse power objective

Four personal hand-held cylindrical fuses are distributed in readable maintenance/search containers across major Level 2 spaces. Opening those drawers/cabinets/access panels is an audible transient event; dropping a fuse is also audible. The player transports each fuse to the Repair Lab's central four-slot distribution island. Two sockets are available on each long side so the player and Listener must move around the structure rather than treating it as a wall.

After inserting a fuse, the player holds a nearby charge lever while the island emits sustained electrical/mechanical noise. Releasing the lever allows an escape; charge duration is a tuning value, with 8–12 seconds as the initial test range. Installed/charged fuses persist through capture for that visit, while a carried fuse drops for its owner to recover. Leaving Level 2 resets the visit and returns all four fuses to their fixed containers. Completion remains personal per player.

A thick visible conduit runs from the qualified exit to the island. The fourth completed charge powers the exit and creates the final noisy run toward the door; there is no additional long master activation after the four charges.

## Geometry proposal

Use a few large readable partitions instead of many small props. Test Hall A gets two or three large blockers. Lower Service Hall gets equipment-sized wall masses with a cross-connection. Conditioning Hall B gets broad acoustic baffles. East Bypass gets offset wall stubs so it is not one straight 22 m sightline. These spaces are route-shaping geometry, not extra safe rooms.

Proposed starting clearance targets are 5-6 m for common chase routes, roughly 4 m actual clear width through creature-traversable framed passages after meshes/colliders are present, and a 4.8-5.2 m general ceiling for scale testing. Final values depend on the actual creature mesh and headset testing.

## Pacing goal

The expansion should create more uncertainty and route choice, not just more walking. Players should enter the dangerous area quickly from Safe Entry. Once they reach the repair side, local loops should let them disengage and return without running all the way back to spawn. The larger footprint must not make the repair objective trivial simply because the creature is farther away.

## Branch implementation status

1. **Implemented:** rebuild v0.4 from authored geometry instead of scaling the old root.
2. **Implemented:** synchronize `layout.json`, generator, reusable prefab and standalone scene.
3. **Implemented:** preserve existing travel components, `Level2EntrySpawn` and RETURN TO SECURITY behavior.
4. **Implemented / pending Unity measurement:** re-place linked gates using the confirmed asset families; actual mesh/collider clearance still needs Unity inspection.
5. **Pending:** build a Listener-specific navigation setup using the final creature footprint/agent radius so player-sized tight pockets are excluded. Project patrol and chase destinations onto that walkable area and test corners, doorway approaches, bypass offsets and the complete power-island loop for stuck cases.
6. **Source validated / runtime pending:** generated source and box-route checks pass; Runaway Chimps Editor validators, Unity 2022.3.55f1 compile/import, Play Mode travel, two-client Photon and headset checks remain pending.

## Pending decisions

The current room sizing/proportions are confirmed. Obstacle positions, ceiling height, exact repair-door fitting, detailed dressing and Listener navigation tuning remain open to runtime adjustment. The bottom-right reward-room rule and minimum 36 x 36 m footprint remain confirmed.
