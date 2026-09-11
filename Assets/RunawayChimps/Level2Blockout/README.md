# Runaway Chimps Level 2 blockout v0.4

Expanded editable greybox for Unity 2022.3.55f1 and Photon PUN. This branch prototype supersedes the v0.3 18 × 18 m layout as the proposed Level 2 target while preserving its travel contract, noisy-repair objective, bottom-right reward-room direction, and existing gate asset language.

**Implementation status, September 11, 2026:** v0.4 is implemented on branch `design/level2-expanded-map-v04`, not merged to `main`. `main` still contains the v0.3 Level 2 blockout. The v0.4 scene/prefab are generated from `Tools/Level2Blockout/build_level2.py`; repair gameplay, Listener AI, reward persistence and final exit qualification remain unwired. Unity, Photon and headset validation remain pending.

## Open the map

1. Check out `design/level2-expanded-map-v04`.
2. Open `Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity`.
3. Select `Level2_Blockout_v04`, hover over Scene view and press **F**.
4. Disable `03_Ceilings__Toggle_for_Top_View` for an overhead inspection.

The reusable `Prefabs/Level2_Blockout.prefab` contains the same authored environment. Use the scene or prefab, not both together. The scene is an unpacked generated copy; editing it does not automatically update the prefab or generator.

## Layout

The authored envelope is X=0–36 m, Z=-4–32 m: 36 m by 36 m overall, four times the floor area of v0.3. The map is enlarged by adding connected spaces rather than scaling the old root transform.

| Space | Bounds / purpose |
| --- | --- |
| Safe Entry | X 0–6, Z -4–0. Existing arrival and RETURN TO SECURITY control remain here. |
| Test Hall A | X 0–18, Z 0–14. Large blockers form the first chase area. |
| Lower Service Hall | X 18–30, Z 0–14. Machinery islands create alternate movement lines. |
| West Observation Wing | X 0–8, Z 14–24. Longer western branch and route variation. |
| Conditioning Hall B | X 8–30, Z 14–24. Staggered acoustic baffles break sightlines. |
| East Bypass | X 30–36, Z 2–24. Offset wall masses create a zig-zag without choking its entrances. |
| Exit Approach | X 0–8, Z 24–26. Dangerous approach to the qualified exit. |
| Completed Exit | X 0–8, Z 26–32. Intended safe completed area behind the temporary qualification blocker. |
| North Gallery | X 8–20, Z 24–32. Connects the upper loop, exit approach and west repair doorway. |
| Repair Lab | X 20–36, Z 24–32. Noisy objective sits on the north wall away from both escape openings. |
| Optional Reward Room | X 30–36, Z -4–2. Bottom-right locked room; 6 × 6 m is a blockout proposal. |

The main local repair loop is Repair Lab → North Gallery → Conditioning Hall B → East Bypass → Repair Lab. Longer escape branches run through West Observation, Test Hall A and Lower Service Hall. The two repair openings are intentionally separated so the Listener can approach from one side without deleting the other route.

## Scale and gate dependencies

- Ceiling underside is approximately 5.0 m for this scale prototype; walls are 0.20 m thick and floor slabs 0.25 m.
- Common creature-traversable authored openings start at 4.0 m wide × 4.2 m high.
- Entry wall gap remains 3.2 × 3.8 m.
- Full exit wall gap remains 2.35 × 3.0 m and uses the complete Level 1 objective-exit gate at scale `(1, 1.1564301, 0.8)`.
- Reward wall gap remains 2.2 × 3.1 m and uses the smaller Level 1 entrance gate at scale `(1, 1.3197935, 1.2965604)`.
- Entry and both repair openings use frame-only linked copies of the existing sci-fi gate. The repair frames are provisionally widened for the giant; inspect actual bevel/collider clearance in Unity before treating the numeric wall gap as usable mesh clearance.

Existing dependencies are unchanged:

- `Assets/MASH Virtual/Sci Fi Doors/Prefab/Sci Fi Gates.prefab`
- `Assets/MASH Virtual/Sci Fi Doors/Prefab/Gate_Small.prefab`
- Their existing FBX/material/texture dependencies.

The source prefabs are not modified.

## Objective and replay placeholders

The noisy repair remains the main objective. `05_Repair_Station__Visual_Only` contains the bench, jammed release, strike plate and hammer placeholders at the north side of the Repair Lab. No repair counter, sound event, Listener investigation, qualification, animation or hammer interaction is wired by this blockout.

`12_Optional_Reward_Room__Visual_Only` keeps the scanner, cyan three-bar placeholder identity, collectible plinth and optional currency-cache shape. The identifying color/symbol is illustrative. Cross-level card ownership, persistence, scanning, door animation and personal rewards remain planned. The room is not designated safe.

## Travel wiring preserved

| Action | Arrival |
| --- | --- |
| Hub computer → Level 2 | `Level2EntrySpawn` in Safe Entry |
| Personal Level 1 completion → Level 2 | Same Level 2 entry |
| RETURN TO SECURITY physical button | `HubReturnSpawn` in front of the Hub computer |
| Deferred Level 1 action | `Level1EntrySpawn`; code/context-menu only |

`Level2EntrySpawn` remains at `(3, 0.05, -2.4)`. `HubReturnControlMarker`, the safe-entry trigger and `Return_To_Security_Button` remain in the same entry area. The branch therefore changes the environment layout without intentionally changing the Level 2 travel contract.

## Source validation

`Tools/Level2Blockout/validate_level2.py` performs source/geometry checks only. It verifies unique generated Unity object IDs, local serialized references, five linked gate instances, required travel bindings, box-layout connectivity and temporary barriers. It uses both a human proxy and a conservative 2.75 m-wide / 3.2 m-tall Listener proxy.

The intended acceptance for the greybox is that both proxies can reach the Repair Lab and that blocking either repair doorway still leaves the other route connected. The East Bypass offsets are positioned away from its entrances specifically to avoid an accidental Listener bottleneck. The completed exit and reward room remain isolated by their temporary blockers; removing the reward blocker should connect the player to the reward room.

These are source-level box checks. They deliberately exclude linked gate mesh bevels, animated body shape, NavMesh behavior and Gorilla locomotion contacts.

## Pending validation

Before merging, open the branch in Unity 2022.3.55f1 and run both Runaway Chimps Editor validators. Then verify actual linked gate/frame thresholds, hand/body collision, the 5 m vertical scale, Listener turning/reach, NavMesh, Safe Entry/Completed Exit safety rules, repair interruption pacing, RETURN TO SECURITY reach/filtering, all travel routes, two-client Photon sector behavior, voice/presentation, repeated transitions and Quest performance.

Start from Bootstrap for runtime travel tests. Opening the Level 2 scene directly and pressing Play does not create the persistent rig or Photon session.
