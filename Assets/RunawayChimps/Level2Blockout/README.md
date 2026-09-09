# Runaway Chimps Level 2 blockout v0.3

Editable layout for Unity 2022.3.55f1, based on the noisy-repair plan. The optional
reward room is now at the bottom right beneath the bypass, as requested. This is
an environment blockout with Level 1 completion, Hub selection and safe-entry
travel actions wired in code. Repair, reward interactions and AI remain unwired.

## Open the map

1. Check out the complete `codex/level2-blockout` branch in the Runaway Chimps
   project. The earlier downloadable map package predates this travel update;
   importing that package alone does not install the new runtime code.
2. Open `Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity`.
3. Select `Level2_Blockout_v03`, hover over Scene view and press **F**.
4. Toggle `03_Ceilings__Toggle_for_Top_View` for an overhead look.

The reusable `Prefabs/Level2_Blockout.prefab` contains the same environment.
Use the scene or the prefab, not both together. The scene is an unpacked copy;
editing it does not automatically update the reusable prefab.

## Existing gate dependencies

The package references these assets already in your project:

- `Assets/MASH Virtual/Sci Fi Doors/Prefab/Sci Fi Gates.prefab`
- `Assets/MASH Virtual/Sci Fi Doors/Prefab/Gate_Small.prefab`
- Their existing FBX, material and texture dependencies.

The originals are not modified or bundled again. Importing into an empty project
without those assets will leave missing references.

`11_Existing_SciFi_Gates` contains three frame-only copies for the entry and repair
passages, the complete exit gate at the original Level 1 exit scale
`(1, 1.1564301, 0.8)`, and the small reward-room gate at the Level 1 entrance scale
`(1, 1.3197935, 1.2965604)`. Only placement/orientation changes for the full gates.
Frame-only copies disable the door panels and are scaled provisionally to fit.
The gate copies have static MeshColliders; there is no opening animation or logic.

Check their actual mesh alignment, bevelled clearance and floor thresholds in
Unity. The wall openings are not a measurement of clear space inside the frames.

## Reward room and replay idea

The 4 × 4 m room occupies X=14–18, Z=-4–0, with a north-facing door off the bypass.
`12_Optional_Reward_Room__Visual_Only` contains the scanner, a cyan sign with three
white bars, a collectible plinth and an optional currency-cache shape. The color
and symbol are illustrative. A future card should carry the same identifying
marking, with readable text in final art. The source card is on another level;
Level 4 is an example, not a committed source. There is no Level 4/card asset here.

The main level objective remains the noisy repair. Saved bonus-card ownership,
scanner interaction, rewards and personal claim tracking are planned. Permanent
non-consumed unlocks and a one-time collectible/optional first-claim currency
bonus are recommendations for review. This package grants no currency.

## Geometry and markers

- Room envelope: 18 × 18 m, wall centrelines; floors top out at Y=0.
- Hall: 14 × 10 m; repair room: 8 × 4 m; entry and exit: 6 × 4 m each.
- Bypass: 4 m nominal; reward room: 4 × 4 m.
- Ceiling underside: 4.2 m; walls: 0.20 m; floor slabs: 0.25 m.
- Hall obstacles: 3.4 m high. Both repair escape routes remain in place.
- Open-passage wall gaps: 3.2 × 3.8 m; exit gap: 2.35 × 3.0 m;
  reward gap: 2.2 × 3.1 m. Gate mesh clearance needs editor inspection.
- Green floors mark intended safe entry/completed exit. No safety is enforced.
- `08_Gameplay_Markers__Not_Wired` contains spawn, objective and reward markers,
  two safe-volume marker triggers and temporary closed exit/reward barriers.
- `09_Listener_Size_Guide__Enable_to_Check_Fit` is an inactive 2.75 × 3.2 × 1.6 m
  box, not a monster. The final Listener scale remains open.
- The historical `06_Exit_Shutter...` group is inactive; the linked gate replaces it.

For geometry inspection only, disable the relevant gate's door child and its
named `Exit_Blocker__...` or `Reward_Room_Blocker__...` in group 08. Restore both
afterward. The reward room is not marked safe; its small-door effect on the
Listener needs a design decision and playtest.

## Validation and remaining work

Source checks pass for YAML, unique IDs, local/material references and original
prefab object IDs. Five gate instance scales and panel overrides are checked.
A 0.10 m box-layout grid preserves the repair loop for player and giant proxies
when either repair opening is blocked. Closed barriers isolate the exit and
reward room; disabling the reward barrier connects the player through its wall
gap. These checks exclude linked gate meshes and do not prove Unity clearance.

Unity import, Play Mode, headset scale, hand/body collision, giant clearance,
NavMesh, AI, repair and capture rules, personal exit qualification, cross-level
cards/saving/rewards, travel runtime behavior, Photon PUN and Quest performance
remain pending. The scene has a SectorScene arrival context and terminal action
component, and is registered once at enabled build index 4. It has no extra rig,
camera or AudioListener. Start from Bootstrap for travel tests; opening Level 2
and pressing Play alone does not create the persistent rig or Photon session.

The current overhead plan is a schematic derived from this geometry. The complete
bundle labels older Blender/perspective assets as `Reference_v01`; those files
predate the gate changes and reward room. The current deliverable is the Unity
scene/prefab. Materials use the inspected project's built-in Standard shader.

Review branch: `codex/level2-blockout`, rebased onto `main` at `c388d87`.
The original map was built from `level1split` at `6443fd0`. The blockout is not
merged. Its travel wiring is implemented in this branch and awaits Unity testing.

## Travel wiring and future terminal

| Action | Arrival |
| --- | --- |
| Hub computer: Level 2, then Enter | Level2EntrySpawn in the safe entry |
| Insert both personal Level 1 cards | Same Level 2 entry, for the completing player |
| Future terminal: ReturnToLevelOne | Level1EntrySpawn in the safe cage room |
| Future terminal: ReturnToHub | HubReturnSpawn in front of the Hub computer |

All routes reuse SectorTravelService's fade, Loading scene, grounding, collision
checks and existing rig, while keeping the same Photon room. Level 1's existing
door return still uses the Hub hallway arrival. Level 2 uses sector Conditioning
and ZoneId.Level2; this does not implement Listener safe-room exclusion.

The `LevelTerminalActions` component is already attached to
`08_Gameplay_Markers__Not_Wired/HubReturnControlMarker`. Bind the future terminal's
local button UnityEvents to `ReturnToLevelOne()` and `ReturnToHub()`. It checks
that the local player's head is inside the assigned safe-entry area. No terminal
model or UI is generated. To test before the UI exists, start from Bootstrap,
travel to Level 2, stay in the safe room, and use this component's Play Mode
context-menu actions.

The Level 1 keybox keeps its existing two-card requirement. Each accepted card
must have been picked up by the local rig. Repeated collider contacts cannot
count a card twice. Completion starts travel; the gate remains a solid barrier
if loading fails. After a failed attempt, select the completed keybox again to
retry (or use its Play Mode context menu); no replacement cards are needed.
Completion door sound/animation polish is still pending. Leaving Level 1 and
returning starts a new scene visit with fresh cards and objective state. The
broader capture/drop/recovery behavior remains separate work.

Run **Tools > Runaway Chimps > Validate Sector Travel**, then test these four
routes, repeated input, rollback and keybox retry, two players finishing at
different times, and sector avatar/voice visibility. Source checks in
`Tools/Level2Blockout/travel_validation.json` are not Unity runtime validation.
