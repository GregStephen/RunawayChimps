# Level 2 expanded layout v0.4

Recorded: 2026-09-11.

## Status

**Confirmed:** Level 2 must be at least twice the current v0.3 blockout's length and width. The current blockout is 18 x 18 m, so v0.4 targets a minimum 36 x 36 m footprint.

**Proposed:** the room arrangement and dimensions below. The existing v0.3 Unity scene remains the implemented map until a larger scene/prefab is built and validated.

Do not implement the size change by scaling the v0.3 root transform to 2x. Build larger authored spaces so normal room scale, gate scale, colliders and traversal remain believable.

## Rules retained from the current design

- Keep Safe Entry and Completed Exit as the only confirmed safe spaces.
- Keep the noisy repair as the main objective and keep two distinct escape routes from the repair room.
- Keep the optional reward room in the bottom-right area.
- Keep the current sci-fi frame language for open passages and the established full exit gate for the completed exit.
- Keep RETURN TO SECURITY in Safe Entry.
- Common routes must remain large enough for the Level 2 creature to traverse and turn.

## Proposed 36 x 36 m planning grid

Use Unity X to the right and Z north. Retain the current entry end where practical and expand mainly north/east: overall X = 0-36 m and Z = -4-32 m.

| Space | Proposed bounds | Approx. size |
| --- | --- | ---: |
| Safe Entry | X 0-6, Z -4-0 | 6 x 4 m |
| Test Hall A | X 0-18, Z 0-14 | 18 x 14 m |
| Lower Service Hall | X 18-30, Z 0-14 | 12 x 14 m |
| West Observation Wing | X 0-8, Z 14-24 | 8 x 10 m |
| Conditioning Hall B | X 8-30, Z 14-24 | 22 x 10 m |
| East Bypass | X 30-36, Z 2-24 | 6 x 22 m |
| North Gallery | X 8-20, Z 24-32 | 12 x 8 m |
| Repair Lab | X 20-36, Z 24-32 | 16 x 8 m |
| Completed Exit | X 0-8, Z 26-32 | 8 x 6 m |
| Optional Reward Room | X 30-36, Z -4-2 | 6 x 6 m |

These are planning coordinates, not approved final wall transforms.

## Route concept

The larger level should use multiple connected loops rather than one oversized room. Test Hall A and Lower Service Hall form the lower loop. West Observation Wing and Conditioning Hall B form a second middle route. East Bypass reconnects the lower half to the repair side. North Gallery connects the upper route to the qualified exit and one side of the repair room.

The Repair Lab should have one opening into North Gallery and a second opening toward the East Bypass/Conditioning side. The objective mechanism should sit away from both openings so using it commits the player to the room. A normal local loop can be Repair Lab -> North Gallery -> Conditioning Hall B -> East Bypass -> Repair Lab, with longer branches through West Observation, Test Hall A and Lower Service Hall.

## Geometry proposal

Use a few large readable partitions instead of many small props. Test Hall A gets two or three large blockers. Lower Service Hall gets equipment-sized wall masses with a cross-connection. Conditioning Hall B gets broad acoustic baffles. East Bypass gets offset wall stubs so it is not one straight 22 m sightline. These spaces are route-shaping geometry, not extra safe rooms.

Proposed starting clearance targets are 5-6 m for common chase routes, roughly 4 m actual clear width through creature-traversable framed passages after meshes/colliders are present, and a 4.8-5.2 m general ceiling for scale testing. Final values depend on the actual creature mesh and headset testing.

## Pacing goal

The expansion should create more uncertainty and route choice, not just more walking. Players should enter the dangerous area quickly from Safe Entry. Once they reach the repair side, local loops should let them disengage and return without running all the way back to spawn. The larger footprint must not make the repair objective trivial simply because the creature is farther away.

## Implementation after layout approval

1. Rebuild the Level 2 blockout as v0.4 from authored geometry instead of scaling the old root.
2. Update `Tools/Level2Blockout/layout.json`, its generator/drawing tools, the reusable prefab and the standalone scene together.
3. Preserve existing travel components and RETURN TO SECURITY behavior; move markers only when the new geometry requires it.
4. Re-place linked gates using the confirmed standards and measure actual mesh/collider clearance in Unity.
5. Rebuild navigation after the physical layout is stable.
6. Rerun source checks, both Runaway Chimps Editor validators, Unity 2022.3.55f1 compile/import, Play Mode travel, two-client Photon tests and headset checks.

## Pending decisions

The exact wall positions, obstacle positions, ceiling height, repair-door locations and detailed dressing remain proposed until the enlarged blockout is reviewed. The bottom-right reward-room rule and minimum 36 x 36 m footprint are confirmed.
