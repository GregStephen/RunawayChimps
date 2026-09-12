# Runaway Chimps

A Photon PUN VR horror game about escaping a laboratory. Open this project with **Unity 2022.3.55f1**. Cheeky Chimps is a separate Unity 6 project.

## Project documentation

- [Design and lore](docs/design-and-lore.md): maintained game rules, level concepts, images, proposals, and decision corrections.
- [Repository improvement plan](docs/repository-improvement-plan.md): review evidence, implementation status, priorities, and acceptance checks.
- [Automated source validation](docs/ci-validation.md): what the GitHub `Source integrity` check verifies, first-run findings, and what remains manual.
- [Codebase audit — September 10, 2026](docs/codebase-audit-2026-09-10.md): reliability findings, fixes, and remaining test scope for PR #5.
- [AGENTS.md](AGENTS.md): instructions to read and update the relevant documents when decisions, implementation, or test results meaningfully change.

The repository documents are the maintained versions; earlier Word documents are downloadable snapshots. Confirmed design, implemented code, and completed testing are separate statuses.

## Current project layout

Start from `Assets/Scenes/Bootstrap.unity`. Current `main` at `8657c3e` contains merged PR #3 and PR #4. The enabled build scenes are Bootstrap (0), Loading (1), Hub_Base (2), Level1_Containment (3), and Level2_BehavioralConditioning_Blockout (4). Disabled older scene iterations remain experiments.

`Assets/Scenes/Level1_Containment.unity` is the Level 1 destination; the earlier planned name `Level01_Containment` is superseded. Hub/Level 1 travel is merged through PR #3. Door returns use `HubDoorReturnSpawn` in the hallway; Hub-computer returns use `HubReturnSpawn` in front of the computer. Hub entries into Level 1 use `Level1EntrySpawn` in its safe cage room. Each transition fades through Loading while keeping the same Photon room and persistent XR rig.

PR #4 merges the editable Level 2 blockout and its travel wiring. Hub computer selection and personal Level 1 keybox completion both arrive at `Level2EntrySpawn`; Level 2's single **RETURN TO SECURITY** control returns to `HubReturnSpawn` in front of the computer. Direct Level 1 selection from Level 2 is deferred to a code/context-menu hook.

Draft PR #5 (`codex/codebase-reliability-audit`) adds the September 10 reliability fixes. Merge commit `ad1a424` reconciles those fixes with current main/PR #4. The resolution preserves the Level 2 assets/routes and active personal-card pickup behavior while adding startup/room recovery, reconnect/pause safeguards, corrected component filenames/GUID-safe bindings, cosmetics/economy fixes, and expanded validation. GitHub reports PR #5 mergeable/clean, but it remains a draft until the validation below is run.

PR #6 (`codex/hub-level1-physical-button`) implements the September 11 Hub-entry correction: Hub → Level 1 is activated only by the physical `StartLevel1Button`. The Hub gate itself rejects direct XR Select/Grip. Source inspection found the existing button root authored inactive even though its visible mesh, hand trigger, local-hand filter, and `SectorDoor.Travel` event binding are intact; the Hub `SectorDoor` now reactivates that root at runtime before Play Mode interaction. Unity 2022.3.55f1 and headset validation remain pending.

To add a scene through Unity 2022.3, drag its `.unity` asset from the Project window into **File > Build Settings > Scenes In Build**, or open it and click **Add Open Scenes**. Keep Bootstrap first and commit `ProjectSettings/EditorBuildSettings.asset` after changing this list.

MiniGamesKidFirstRig is the current Level 1 monster. The inactive Zombie Crawl object is the chosen visual replacement. Sector monster synchronization is implemented at source level and awaits multi-client validation. Zombie Crawl visual integration remains separate work.

## Level 2 map blockout

The editable Level 2 layout is in `Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity`, with a reusable prefab in the adjacent `Prefabs` folder. Open the scene, select `Level2_Blockout_v03`, and press **F** over Scene view. Toggle the ceiling group for an overhead view. See the [map setup and validation notes](Assets/RunawayChimps/Level2Blockout/README.md).

The map includes the repair-room escape loop, reused sci-fi gates and a locked reward-room blockout at the bottom right off the bypass. It references the existing MASH door assets. Scanner/reward shapes are placeholders. The Listener, repair progression, exit qualification, cross-level bonus-card saving and rewards remain unwired.

Level 2 is registered once at enabled build index 4 and uses Bootstrap's existing rig. The scene has no extra rig/camera/AudioListener. Source/box-layout checks were completed before the PR #5 reconciliation; Unity import, gate mesh fit, travel runtime, Photon and headset checks remain pending.

## September 10 reliability audit

PR #5 contains the codebase audit fixes and remains a draft. Before PR #4 was reconciled, the audit branch's offline checker passed its first-party C# component filenames/GUID metadata, enabled scene registration/local references and optional tree-sitter syntax parsing. That evidence applies to the pre-reconciliation audit tree.

The `ad1a424` merge preserves PR #4 while combining these reliability changes:

- bounded startup/auth waits with retry and guarded late callbacks;
- coordinated public/private room join/create failure recovery;
- room/attempt-scoped player spawning and local-only readiness;
- corrected project component filenames with existing `.meta` GUID identities preserved;
- room-specific monster state cleanup, safer navigation reactivation, and capture/grounding guards;
- reconnect sector/zone preservation and headset-pause cancellation during travel;
- cosmetic removal/persistence and Economy inventory pagination/stack fixes;
- targeted material, vent-graph, locomotion, keyboard, face-expression and exception cleanup;
- `Tools/validate_source.py` plus **Tools > Runaway Chimps > Run Reliability Regression Checks**;
- expanded **Validate Sector Travel** coverage, including Level 2 and missing-script detection.

The offline validator is generic and reads every enabled build scene, so the current five-scene tree does not require a hard-coded scene-count change.

## Automated source validation

PR #11 adds `.github/workflows/source-validation.yml`, a read-only **Source integrity** GitHub Actions check. It runs for pull requests, pushes to `main`, `codex/**`, and `design/**`, and manual dispatches.

The check pins its C# parser dependencies and verifies:

- `ProjectSettings/ProjectVersion.txt` still declares Unity **2022.3.55f1**;
- `python Tools/validate_source.py --syntax` passes for first-party component/class names, Unity script metadata/GUIDs, enabled scene registration, scene metadata, local object IDs/references, and C# syntax;
- tracked Python tools compile with `python -m compileall -q Tools`; and
- `git diff --check` passes for the full PR or pushed range.

The first run immediately caught a real syntax error in `Tools/Level2Blockout/draw_plan.py` and then caught trailing whitespace in three newly added Unity `.meta` files. Those issues were fixed rather than weakening the check. On 2026-09-12 both the push and pull-request **Source integrity** runs passed; the source validator reported 116 first-party scripts and 5 enabled scenes passing its checks.

A green Source integrity check is **not Unity validation**. It does not import or compile the project in Unity, run either Unity Editor validator, enter Play Mode, connect Photon clients, exercise XR interactions, or test Quest/headset performance. See [Automated source validation](docs/ci-validation.md) for the exact boundary and the possible future Unity-aware CI tier.

## Audio and proximity fixes

- `AudioScaler` stops its source when vent, patrol/chase, or distance filters exclude playback. Disabling the scaler also stops its source. Mute-change logging respects `debugLogs`.
- `PlayerVentState.LocalPlayerInVent` uses `ZoneStateService.LocalZone` when available in a Photon room. Older scenes without the service retain their fallback.
- `ProximityManager` resets effects when a reactor belongs to another zone. `ProximityReactor` clears active state on disable so returning can trigger a fresh entry.
- `MaterialSwapper` tolerates renderer destruction during unload and invalid material slots instead of throwing. Zombie Crawl renderer/slot bindings still need deliberate setup.
- Proximity checks use the typed `ZoneId` field and reuse their snapshot collection.
- `PhotonVRManager.DefaultRoomLimit` is ten. New rooms use this default; existing rooms are not resized merely by changing it.

## Checks before merging PR #5

The `Source integrity` workflow now runs the offline source check automatically, and it can still be run locally from the repository root:

```bash
python Tools/validate_source.py --syntax
```

Then open the reconciled branch in **Unity 2022.3.55f1** and, outside Play Mode and disconnected from Photon, run:

1. **Tools > Runaway Chimps > Run Reliability Regression Checks**
2. **Tools > Runaway Chimps > Validate Sector Travel**

Do not mark runtime items validated from source checks alone. After compilation/import and both Editor validators pass, test:

1. Startup failure/retry, delayed callbacks, disconnected startup, room-join/create failure, and rapid public/private changes. Confirm one current-room local avatar and no old-attempt spawn.
2. A new public and private room. Confirm maximum players is ten and room-switch error/status text recovers for another attempt.
3. Computer name/color save, failed/repeated name save, color persistence/remote visibility, cosmetic removal/reload, and inventory with multiple pages/stacks.
4. Initial grounding, capture grounding, smooth turning about the headset, and disable/re-enable of interaction/collision scripts. Confirm no stale locomotion impulse.
5. Vent patrol/chase audio, proximity reset, safe-room filtering, material restoration, and hand-impact audio with the existing clips.
6. Two clients in the same Photon room across Hub, Level 1 and Level 2. Validate remote avatar/name/collider/speaker presentation by sector, monster controller handover/state, capture isolation, reconnection and late arrival.
7. Headset pause/resume before/during travel. No stale transition, duplicate rig, stale monster state, stuck fade or incorrect sector/zone after resume/reconnect.
8. Repeated scene travel and Quest performance/memory. End with one active environment and no accumulated old visit objects.

Run headset audio/comfort checks with the existing game assets. Editor/source validation is not a substitute for headset behavior.

## Scene travel checks

Start from Bootstrap. Walking into a door should not automatically transition; use the intended physical button or route-specific XR interaction.

| Route | How to try it | Expected arrival |
| --- | --- | --- |
| Hub computer → Level 1 | Select Level 1, then Enter | `Level1EntrySpawn` in the safe cage room |
| Hub hallway → Level 1 | Touch/press the visible `StartLevel1Button` before the gate with the local monkey hand/fingertip. Do not Grip/Select the gate itself. | Same Level 1 safe cage room |
| Level 1 → Hub door | Select the matching entrance door from inside Level 1 | `HubDoorReturnSpawn` in the Hub hallway |
| Hub computer → Level 2 | Select Level 2, then Enter | `Level2EntrySpawn` in the Level 2 safe entry |
| Complete Level 1 | Insert both currently required local personal cards | Same Level 2 safe entry, for the completing player |
| Level 2 → Security | Press the west-wall RETURN TO SECURITY button | `HubReturnSpawn` in front of the Hub computer |
| Deferred Level 2 → Level 1 hook | Use `LevelTerminalActions.ReturnToLevelOne()` context/code hook | Level 1 safe cage room |

For travel validation:

- Confirm the Hub `StartLevel1Button` becomes visible before the gate in Play Mode, can be pressed by moving the local monkey hand/fingertip into its trigger in both headset and non-headset editor testing, and calls the Hub door's `Travel()` once. Touching/Grip-selecting the gate itself must do nothing.
- Confirm fade → Loading → destination works in both eyes and preserves the Photon room, actor membership and one local rig.
- Check floor/head/body clearance and comfortable facing at every arrival marker. Missing/obstructed arrival must reject/rollback safely.
- Repeat routes with repeated input, while holding an item, after a failed load, and after completed-keybox retry.
- Re-enter Level 1 and verify the intended fresh local objective state; test personal card drop/recovery and two-player independence separately.
- Keep one client in Hub while another enters a level; reunite them and verify sector presentation/voice refreshes.
- Test monster controller departure/disconnect/suspend and arrival during a chase. An empty level has no controller.
- Interrupt the connection or pause during travel, then resume/reconnect. Verify the previous/current sector and zone recover without a queued stale arrival.
- Profile transition frame time and memory on Quest.

## Level 2 return-button checks

The Level 2 west safe-entry wall has one **RETURN TO SECURITY** button. It calls `LevelTerminalActions.ReturnToHub()` and arrives at the Hub computer. Its plate/cap geometry is editable and the plain TextMeshPro label is created at runtime.

Only the local hand/fingertip should activate it. A remote hand, local head/body, or loose prop must not. Holding a hand against the cap must not repeatedly trigger travel. Check label readability/orientation, reach, cap release, cooldown, rollback/retry, and safe-entry-only behavior in Play Mode/headset.

`ReturnToLevelOne()` remains a code/context-menu hook only; there is no player-facing Level 1 selector in Level 2.

## Additional cleanup checks

1. At the computer, adjust Color and press Enter. Confirm the avatar changes, persists through Hub/restart, and updates remotely.
2. On the Name page, test blank input, repeated Enter while pending, failed/signed-out saves, retry, and travel while saving.
3. Press a physical button, disable the pressing collider/button, restore it, and press again. It should rest correctly and accept one new press.
4. Repeated proximity/material changes should restore the original state and should not create unnecessary material instances merely to swap one binding.
5. With hand-audio fallback casts temporarily off, approach a wall/ceiling and move away. Sound should require inward impact. Restore test settings.
6. Verify `HeldItemCollisionMode` imports without a missing script. Test mixed child layers, two hands, final release, disable/re-enable while held, and travel while holding the item.
7. Confirm the corrected component filenames (`ComputerTerminalUI`, `LoadingDebugText`, `AntiHandPhase`, `RandomTileRegion`, `MonsterTouchRespawnPhotonVR`, `KeyCard`, `VRKeyCard`) load with their expected classes and no missing script.

## Validation status

**Implemented:** PR #3 scene travel foundation; PR #4 Level 2 blockout/travel/RETURN TO SECURITY and card filename/GUID correction; PR #5 reliability fixes reconciled with both in `ad1a424`; PR #6 Hub → Level 1 button-only interaction and runtime recovery of the existing inactive entrance button; PR #11 adds the read-only repository-wide Source integrity workflow.

**Source validated on PR #11, 2026-09-12:** both push and pull-request Source integrity runs passed after the check first exposed and prompted fixes for the broken Level 2 floorplan drawing script and whitespace in three new Unity metadata files. This automated evidence covers source/serialization/tool syntax and whitespace only.

**Pending on the current branches:** Unity 2022.3.55f1 compile/import; both Editor validators; startup/live-service tests; Hub entrance button Play Mode/headset behavior; Level 1/2 routes; two-client Photon; capture/controller handover/reconnect; voice; headset pause/resume; repeated travel; Quest performance and comfort.
