# Runaway Chimps design and lore

Last updated: 2026-09-11. Maintained repository edition, migrated from `Runaway_Chimps_Design_and_Lore.docx` version 0.9. The existing Word document is a downloadable snapshot; future edits belong here. See [AGENTS.md](../AGENTS.md) for the documentation workflow and the [repository improvement plan](repository-improvement-plan.md) for implementation evidence.

We are building a social VR horror game about gorillas escaping a laboratory that experiments on animals. This document records the current game rules, Level 1, the proposed Listener level, the story, and the scene architecture so future work can build on the same decisions.

## Implementation status

Design approval is separate from implementation and testing. **Confirmed** records an explicit design decision; **Planned** records an accepted direction awaiting implementation; **Proposed** is an idea awaiting agreement; **Open** is an unresolved choice. **Implemented** means code exists on a named branch or commit, not necessarily on `main`. **Pending validation** names checks not yet run. Mark something **Validated** only with an actual result, date, and tested version or commit.

As of 2026-09-09, [PR #2](https://github.com/GregStephen/RunawayChimps/pull/2), merged on 2026-09-09, contains the local vent-audio, proximity-reset, material-safety, and ten-player default changes in commit `d4ee616`. Unity compilation, Play Mode, Photon sessions, and headset checks are pending. These changes do not implement the scene split, shared monster synchronization, Zombie Crawl integration, or personal-card lifecycle. Follow [Checks before merging](../README.md#checks-before-merging).

On `level1split`, commit `ac69718` contains `Assets/Scenes/Level1_Containment.unity` and its metadata. Greg reports that the new scene and doors are committed. The follow-up build-settings change enables this scene after Bootstrap, Loading, and Hub_Base. The travel implementation prepared for `level1split` now adds guarded local loading, door/computer routes, separate Hub arrival markers, persistent XR interaction management, sector avatar/collision/voice presentation, and sector monster state messages. Unity compilation, extraction geometry, headset arrival clearance, and multiplayer validation remain pending; see the [travel checks](../README.md#scene-travel-checks).

The gameplay rules below describe the intended game. Reported local behavior is identified separately from what the repository review established.

The combined `level1split-travel.patch` also includes focused source cleanup for terminal color saving/preview and name-save handling, local startup readiness, cancelled/timeout player spawning, button release state, logging, material bindings, hand-impact audio, and held-item collision-layer restoration. The held-item script filename now matches its existing component class with its asset GUID preserved. The Level 1 travel/cleanup work is now on main through merged PR #3; no separate patch application is needed. These changes add no Level 1 terminal or new gameplay/progression rules; Unity and headset validation remain pending.

**September 10 integration status:** PR #4 is merged into `main` at `8657c3e`, including the Level 2 blockout, Level 2 travel routes, personal Level 1 completion handoff, and RETURN TO SECURITY control. PR #5 (`codex/codebase-reliability-audit`) was reconciled with that mainline in merge commit `ad1a424`. The conflict resolution preserves PR #4's Level 2 behavior and personal-card pickup checks while adding the audit branch's startup, room-switch, reconnect/pause, validator, filename, and reliability fixes. GitHub reports PR #5 mergeable/clean. This records implementation state only: the reconciled five-scene tree still requires the offline source checker to be rerun, followed by Unity 2022.3.55f1, Photon, and headset validation.

**September 11 Level 2 design correction:** Greg confirmed that the current 18 × 18 m Level 2 blockout is too small and that the replacement must be at least twice as large in both length and width, making **36 × 36 m the minimum target footprint**. This supersedes the earlier instruction to keep the Listener level small. Branch `design/level2-expanded-map-v04` now contains an **implemented v0.4 review blockout** at that minimum footprint, generated as the existing Level 2 scene/prefab with the travel contract preserved. Greg reviewed the v0.4 fuse-power blockout and confirmed the current room sizing/proportions. Obstacle placement, ceiling judgment, final Listener clearances and runtime navigation remain subject to validation/tuning. `main` still contains v0.3. Source/box-route validation has passed on v0.4; Unity 2022.3.55f1, Photon and headset validation remain pending.

**September 11 Level 1 runtime follow-up:** after the first Zombie Crawl integration and emergency lighting fallback reached `main`, Greg reported that Level 1 remained almost impossible to see, Zombie Crawl rendered far too small, translated without convincing crawl motion, did not appear to pursue the player, and did not capture on ordinary player contact. Branch `codex/fix-level1-crawler-lighting` now implements a focused correction: a brighter but still cool/shadowless readable baseline, authored-world-scale preservation for Zombie Crawl, retuned crawl/chase pacing and detection, local-rig contact capture, and a directional safe-room-to-vent zone repair for the second Level 1 route. This is implementation based on actual play feedback; Unity 2022.3.55f1, two-client Photon and headset validation remain pending. A local headset-style vent headlamp/flashlight cone is a **proposed** presentation idea only and is not implemented by this branch.

## Decision and correction record

| Recorded | Decision or correction | Status |
| --- | --- | --- |
| 2026-09-11 | Level 1 horror lighting must remain navigable rather than nearly black, and the Zombie Crawl replacement must appear at its intended physical size, visibly crawl while moving, pursue eligible vent players, and capture on normal local-rig contact while safe rooms remain safe. | Confirmed from Greg's runtime feedback. Corrective implementation is on `codex/fix-level1-crawler-lighting`; Unity/Photon/headset validation pending. A vent-only local headlamp remains proposed, not confirmed. |
| 2026-09-11 | Harden the Level 2 fuse prototype before Listener implementation: explicit fuse/socket state machines, multi-collider-safe lever holding, loose-fuse fall recovery, power-island indicator feedback, noise debug logging/gizmos, a reusable power-state endpoint, and a first physical noisy drawer interaction. Keep the runtime bootstrap prototype-only until authored prefabs replace it. | Implemented on `design/level2-expanded-map-v04`; source/marker hardening checks pass. Unity 2022.3.55f1/XR behavior, capture-drop positioning, personal Photon presentation, final drawer ergonomics, powered exit and Listener consumption remain pending. |
| 2026-09-11 | Implement the approved Level 2 fuse interaction foundation: physical grabbable prototype fuses, socket insertion, resumable lever charging, a shared Level 2 noise-event bus, objective completion tracking, and reusable noisy-search-container hooks. | Implemented on `design/level2-expanded-map-v04`; runtime bootstrap wires the current v0.4 markers without final art/audio or Listener response. Source/marker sanity checks pass; Unity 2022.3.55f1, XR ergonomics, capture/drop integration, personal Photon presentation and powered-exit behavior remain pending. |
| 2026-09-11 | Approve the current v0.4 room sizing/proportions shown in the fuse-power blockout. Keep Listener navigation as a separate implementation/validation concern: use a Listener-sized walkable area so narrow player-only pockets are excluded, and ensure patrol/chase targets stay on that area. | Confirmed room sizing. Listener NavMesh/agent setup, turning clearance and runtime stuck-case testing remain pending. |
| 2026-09-11 | Replace Level 2's hammer/strike repair with four personal hand-held cylindrical power fuses distributed around the map. Search containers make short noises; each fuse is returned to a central four-slot power island in the Repair Lab and charged with a nearby lever that creates sustained noise and attracts the Listener. A clearly visible power conduit leads from the exit to the island. | Confirmed objective correction. V0.4 blockout visuals/markers implemented on `design/level2-expanded-map-v04`; gameplay interaction, personal state, Listener sound response, exit power-up and runtime validation pending. |
| 2026-09-11 | Expand Level 2 to at least twice the current v0.3 blockout's length and width. Since v0.3 is 18 × 18 m, the replacement target is at least 36 × 36 m. Do not treat the earlier “keep the proposed small layout” instruction as active. Preserve the power-restoration loop, two Repair Lab escape routes, safe entry/qualified exit, bottom-right reward-room rule and established gate language while redesigning the larger space. | Confirmed size correction. V0.4 review geometry is implemented on `design/level2-expanded-map-v04` and passes generated-source/box-route checks; exact arrangement still proposed for review; Unity, Photon and headset validation pending. |
| 2026-09-11 | Hub hallway → Level 1 uses the physical `StartLevel1Button` beside the gate as the only player-facing activation. Touch/press it with the local monkey hand or fingertip; do not require Grip, Trigger, or XR Select on the door itself. This keeps headset play and non-headset editor testing on the same hand-collision interaction. The door remains a barrier/visual, not an interactable. | Confirmed correction; implemented on PR #6 (`codex/hub-level1-physical-button`). Source inspection found the existing button root authored inactive while its visible mesh, trigger, local-hand filter and `SectorDoor.Travel` binding remain intact. PR #6 removes Hub-door direct XR Select, reactivates the button root at runtime, and expands the validator for placement/visibility/wiring. Unity 2022.3.55f1 Play Mode/headset validation remains pending. Level 1 → Hub return interaction is unchanged. |
| 2026-09-10 | Reconcile the reliability-audit branch with merged PR #4 without dropping Level 2 or the audit fixes. Preserve the active `KeyCard`/`VRKeyCard` GUID identities, PR #4's local-pickup ownership check, Level 2 menu/completion routes, and current five-scene Build Settings; combine them with the audit reconnect/pause and missing-script safeguards. | Implemented in PR #5 merge commit `ad1a424`; GitHub mergeable/clean; source checker, Unity, Photon and headset validation pending |
| 2026-09-10 | Replace the proposed elaborate Level 2 terminal with one large wall button labeled RETURN TO SECURITY. It returns to the Hub computer; no Level 1 selection UI for now. Internal Hub naming is unchanged. | Confirmed; simple geometry and local-hand interaction merged through PR #4; Unity/headset checks pending |
| 2026-09-09 | Wire Level 1 completion and Hub selection to Level 2's safe entry; the future Level 2 terminal returns to Level 1's safe cage room or the Hub computer. | Confirmed; code and scene wiring merged through PR #4 and preserved by PR #5 conflict resolution; Unity/headset validation pending |
| 2026-09-09 | Add optional locked reward rooms opened by a card discovered on a different level, using the smaller Level 1 entrance door. Rewards may be a collectible, in-game currency, or both. Level 4 supplying Level 2 is an example, not a fixed assignment. | Confirmed direction; gameplay planned |
| 2026-09-09 | Put Level 2's reward room in the bottom-right area to spread out points of interest. This supersedes the assistant's suggested space between the exit and repair room. | Confirmed correction; blockout v0.3 merged through PR #4; v0.4 keeps the same bottom-right relationship while its exact size/coordinates remain proposed |
| 2026-09-09 | Reuse scaled sci-fi gate frames for open passages and the complete Level 1 exit gate at its existing scale for exits. | Confirmed; linked assets in blockout v0.3, Unity validation pending |
| 2026-09-09 | Greg requested a rough Level 2 map from the saved noisy-repair floorplan. Unity blockout v0.1 preserves that layout; 3.2 m openings are a proposed scale allowance for the giant. | Asset merged through PR #4; import and headset validation pending; its 18 × 18 m footprint is superseded by the September 11 size correction |
| 2026-09-09 | Runaway Chimps uses Unity 2022.3.55f1 and Photon PUN. Unity 6 belongs to the separate, non-horror Cheeky Chimps project. | Confirmed correction from the source document |
| 2026-09-09 | Use Hub_Base as the source for a split at the Security Gate. Disabled Level1 and Level1_2_Hall scenes are experiments. | Confirmed direction; scene committed on level1split; extraction validation pending |
| 2026-09-09 | Replace MiniGamesKidFirstRig with Zombie Crawl while preserving and repairing the existing monster systems. | Confirmed direction; visual integration merged through PR #8; follow-up runtime tuning is implemented on `codex/fix-level1-crawler-lighting`; validation pending |
| 2026-09-09 | Personal cards start at fixed locations; completion affects one player and leads to the next level. Separate room families, randomized card starts, group wins, automatic hub return, and monitor audio are superseded. | Recorded from the current design; runtime implementation still needs verification |
| 2026-09-09 | Prototype one card; two remain an option if gameplay and story justify them. Do not treat the active two-card keybox as a defect solely because of its count. | Prototype recommendation; final count open |
| 2026-09-09 | Maintain the design and improvement plan in the repository; update relevant sections after confirmed decisions, corrections, implementation, or meaningful test results. Keep Word exports as snapshots. | Confirmed workflow |

| 2026-09-09 | Earlier instruction: all Return to Hub actions arrive in front of the computer. | Superseded by the route-specific clarification below |
| 2026-09-09 | Hub computer or hallway door → fade, Loading, Level 1 safe room. Level 1 return door → Hub hallway on the other side of that door. Future terminal → Hub computer when returning to Hub; it can also select another level. | Confirmed correction; destination behavior remains correct, but the September 11 interaction correction supersedes direct Hub-door selection: the hallway route is now activated only by its physical button. Level 2 currently uses the simpler RETURN TO SECURITY control described above. Unity/headset validation remains pending. |
| 2026-09-09 | Defer placing a Level 1 terminal until more levels exist. Keep the door return as the current Level 1 route to Hub. | Confirmed; no Level 1 terminal added |
| 2026-09-09 | Use the committed scene name Level1_Containment, replacing the earlier planned spelling Level01_Containment. Greg reports the scene split and doors committed on level1split at ac69718. | Scene asset and metadata verified; geometry, door wiring, and runtime travel pending validation |

These dates record migration and clarification, not the original date of every earlier decision. Future corrections should name the superseded rule and update its affected sections.

## Current decisions

| Status | Decision |
| --- | --- |
| Confirmed | The overall objective is to escape the laboratory. Story clues should be distributed throughout the environment. |
| Confirmed | Players complete levels individually. One player winning does not win or reset the level for everyone. |
| Confirmed | Level 1 ends when a player returns the keycard to the cage room and opens the locked objective door. |
| Confirmed | Level-objective cards are personal, with fixed starting locations. Capture drops a held objective card; re-entry resets it. The new optional reward-room cards need separate cross-level state, described below. One card is the prototype recommendation; two remain an option if they improve the route and story. |
| Confirmed | The winner hears the door, fades to black, and arrives safely in Level 2. Other players see them disappear and stay in Level 1. |
| Confirmed | The Crawler has almost-severed legs and cannot leave the vents. Scratches and/or blood lead from its cage toward the vent. |
| Confirmed | Level 1 should remain dark and threatening but readable enough to navigate; near-black visibility is not the intended difficulty. |
| Confirmed | Hub hallway entry into Level 1 uses a physical hand-press button beside the gate. The gate itself is not selected with Grip/Trigger. Use the same local hand/fingertip collision in headset play and non-headset editor testing. |
| Confirmed | Level 2's replacement footprint is at least 36 × 36 m, twice the v0.3 blockout in each horizontal dimension. |
| Confirmed | Level 2 uses four personal cylindrical power fuses found in noisy search containers. Each fuse is inserted and charged at a central four-slot Repair Lab island; sustained charging attracts the Listener, and a visible conduit links the island to the exit. |
| Planned | One Photon PUN room holds up to 10 players across the hub and all levels. Each headset loads its current level scene. |
| Planned | About four silent monitors surround the hub computer. Each displays its level name; additional levels rotate onto the screens. |
| Planned | A control in each level starting room allows return to the hub. The current direction favors open level selection without mandatory unlocks. |
| Confirmed | Level 2 currently offers one RETURN TO SECURITY button. Direct Level 1 selection is deferred; its code hook remains for future use. Separate solo progression and permanent completion records are not committed features. |

### Latest flow change

Winning leads forward into the next level. Returning to the hub is an available choice from the next safe starting room, rather than the automatic result of a win. The September 10 correction limits Level 2's current physical control to RETURN TO SECURITY; direct selection of other levels is deferred. The September 11 correction makes the Hub hallway's physical entrance button the only player-facing way to activate Hub → Level 1 travel at that gate.

### Hub entrance interaction implementation

**Implemented on PR #6; validation pending:** the Hub `SectorDoor` no longer creates or enables an `XRSimpleInteractable` for Hub → Level 1, so Grip/XR Select on the gate is not a travel path. Repository inspection found the existing `StartLevel1Button` root at the intended gate approach authored inactive. Its `BigRedButton` children remain positioned roughly 1.5 m from the door, with an enabled visible mesh and enabled trigger; its `PhysicalButton` requires the local rig, `HandTag` and configured hand layer, and its one persistent `OnPressed` call targets that Hub `SectorDoor.Travel` method. PR #6 reactivates the button root when the Hub door awakens and validates that it stays visible, triggerable, within 2.5 m, on the approach side, and wired exactly once. This source inspection does not replace Unity Play Mode or headset testing.

### Level 2 travel implementation

**Implemented on `main` through merged PR #4 and preserved on PR #5 after conflict resolution:** Hub's existing computer menu includes Level 2. `SectorTravelService` accepts the registered Level 2 scene, reuses fade/Loading and destination floor/body/head checks, and publishes the Conditioning sector for avatar/voice presentation. Both Hub selection and personal Level 1 completion use `Level2EntrySpawn` at `(3, 0.05, -2.4)`, facing into the hall. Level 2's arrival context uses existing `ZoneId.Level2`; the Listener and its safety logic remain unimplemented.

The existing Level 1 two-card requirement is preserved. The keybox counts each local-picked card once and requests completion travel after the final card. Its completion gate stays solid instead of opening a passage, including during rollback. If loading fails, completed source-scene state remains available and locally selecting the keybox retries; leaving/re-entering the scene still resets the visit. Completion sound/animation polish and the broader capture/drop lifecycle remain pending.

`LevelTerminalActions` remains attached to the entry's `HubReturnControlMarker`. The simple `Return_To_Security_Button` on the west wall at `(0.28, 1.25, -2)` calls `ReturnToHub()` through `ReturnToSecurityButton`. Scene and prefab contain editable plate, cap, bolts and paint-chip shapes; the plain TextMeshPro label is created at runtime. A local hand/fingertip is required, held contact cannot repeatedly trigger travel, and the cap depresses slightly. The existing safe-entry and busy-travel guards remain in force. No new audio asset is included. `ReturnToLevelOne()` remains a code/context-menu hook only, not a player-facing option. Hub return arrives at the computer; the Level 1 door-to-Hub hallway route is unchanged.

**Validated before the PR #5 reconciliation at source level:** C# syntax, card script GUID preservation after matching filenames to classes, scene IDs/references, one Level 2 build registration, two Level 1 card instances, and safe-entry/terminal bindings. **Pending after reconciliation:** rerun the offline source checker against the five enabled scenes, then Unity compilation/import, Editor validator execution, all four travel routes, failed-load retry, two-client independent completion and visibility/voice, headset spawning and clearance. These are implemented routes, not claimed playtested behavior.

## Optional reward rooms and replay

**Confirmed direction, September 9, 2026:** add a small locked side room whose scanner requires a keycard found on a different level. Players first notice the locked room, later recognize the matching card, and return to claim a collectible, in-game currency, or both. This is optional exploration; Level 2's main objective remains the noisy repair. Reuse `Gate_Small.prefab`, confirmed in the source scene as `Level1_Entrance_Door`.

**Confirmed placement correction:** the Level 2 room belongs in the bottom-right area. Do not put it between the exit and repair room; Greg found that arrangement too busy. The implemented v0.3 blockout uses a 4 × 4 m room beneath the bypass inside its old 18 × 18 m envelope. The September 11 size correction keeps the bottom-right relationship but supersedes those envelope coordinates; the v0.4 plan currently proposes a larger 6 × 6 m room at the southeast edge. Exact room size and dressing remain proposed.

**Proposed readable clue:** use the same color plus a distinct symbol on the scanner sign and keycard, with a short readable name/code in the final art. Do not rely on color alone. The blockout's cyan plaque and three white bars are illustrative, not an approved card identity. Level 4 supplying this Level 2 room is Greg's example; the exact source level, placement, and card identity remain open. No Level 4 asset or card has been created.

**Planned requirement:** ownership must survive travel from the source level to Level 2. The attempt-based Level 1 card rules must not erase a newly acquired bonus-room card during that journey. **Recommended for review:** record a personal, permanent, non-consumed unlock when collected, surviving capture and game restarts. Present it as a reusable access card when scanning. Permanent persistence and capture behavior are recommendations, not confirmed rules.

**Reward proposal:** one collectible per player, with an optional first-claim currency bonus. Keep card ownership, room access, and reward-claim state separate so reopening a door does not award the same collectible or currency again. Whether currency is repeatable, its amount, the collectible type, how an already-claimed room appears, and whether friends may follow an unlocked door are open decisions. A reward should not disappear for everybody when one player claims theirs. Currency grants will need the project's account/economy integration; the blockout contains no award logic.

**Open monster rule:** this room is not marked as an additional safe room. Its small doorway may exclude the giant physically; decide and test that behavior before integrating AI so it does not accidentally become an unrestricted chase refuge.

**Implementation status:** v0.3 contains the room shell, original linked small-door prefab, temporary closed barrier, scanner/sign shapes, collectible plinth and optional currency-cache placeholder. Card pickup, saved ownership, scanning, door animation, reward claims and multiplayer behavior are planned and unwired. The v0.4 expanded layout has not yet changed this scene geometry.

## Facility lore

### Established premise

The player characters are laboratory gorillas trying to escape the facility that experiments on animals. The overall objective is to get outside the lab. The precise experiments and facility history remain to be written; each level has its own monster, objective, and environmental clues.

Confirmed creative constraint: other experiments and monsters need not be gorillas. Favor unsettling behavior and restrained designs. Develop one manageable level at a time for a solo developer; detailed concept art does not establish the required production scope.

### Working story for the hub

Proposed: a containment incident caused a staff evacuation and sealed the exterior exits. Escaped gorillas took over a security and observation office as a refuge. They are free from their cages but still trapped inside the facility. The computer and camera wall are surviving lab equipment.

This explanation supports a safe social space within the lab, but staff evacuation, barricades, and the exact lockdown cause are story proposals rather than established facts.

### Why the gorillas have no legs

Desired theme: include dark humor about the experiments and the players having no legs. Proposed explanation: researchers tried to restrict mobility through limb removal, but the subjects adapted to powerful arm-based movement. This explanation is not yet canon.

Possible report text: "Removal of lower limbs did not produce the expected reduction in mobility." A handwritten response could read: "They got faster."

### The wider escape

The numbered levels can form a recommended route through the facility, with each victory opening the next sector. The eventual final level should deliver the actual escape from the lab. Open selection permits players to visit chapters early; strict story order would require a different access rule. The final exit and replay explanation remain open.

## Hub and shared navigation

### Return and travel controls

The next level begins in a safe room where a player can regroup. Level 2 now has one simple RETURN TO SECURITY wall button, with no screen or keyboard. The destination remains Hub_Base; this label does not rename the scene or establish additional facility lore.

The earlier two-option terminal recommendation is deferred. Players continue by walking into Level 2, or return to Security and choose a level at the existing computer. Keep the return action confined to the safe starting room so it does not become an escape button during a chase.

Confirmed travel routes (Greg’s latest clarification supersedes the earlier all-returns-to-computer rule):

| Action | Destination marker | Arrival |
| --- | --- | --- |
| Hub computer: select Level 1 | Level1EntrySpawn | Level 1 safe cage room |
| Hub hallway: physically press `StartLevel1Button` with the local monkey hand/fingertip | Level1EntrySpawn | Same Level 1 safe cage room |
| Level 1 entrance door: return | HubDoorReturnSpawn | Hub hallway, on the Hub side of the door |
| Hub computer: select Level 2 | Level2EntrySpawn | Level 2 safe entry room |
| Complete the personal Level 1 keybox | Level2EntrySpawn | Same Level 2 safe entry room |
| Deferred Level 2 Level 1 action (code/context menu only) | Level1EntrySpawn | Level 1 safe cage room |
| Level 2 RETURN TO SECURITY button | HubReturnSpawn | In front of the Hub computer |

Every route fades to black, displays the Loading scene, then fades into the destination. Travel is an explicit interaction; walking into the door does not automatically transition. **Confirmed September 11:** the Hub hallway gate itself is not a Grip/Trigger/XR-select interactable; its physical `StartLevel1Button` is the single player-facing activation for that route. The same local hand/fingertip collision should work in a headset and in non-headset editor testing by moving the monkey hand into the button. **Implemented on PR #6:** direct Hub-gate XR Select is suppressed, and because the existing button root was found authored inactive, the active Hub `SectorDoor` reactivates that root before the player can interact. Source inspection confirms its visible mesh, trigger, local-hand filtering and one `Travel()` event binding; Play Mode/headset confirmation remains pending. Level 1's return-door interaction is unchanged by this correction. Level 2's single return button is merged through PR #4 and preserved in PR #5. Arrival markers exist in the scene assets; verify their clear floor space, facing, button-label readability and reach in Unity and on a headset.

A player who finishes Level 1 first can wait in the safe Level 2 starting room for a friend. Travel affects the interacting player only. Changing sectors, returning to the hub, or following a friend must preserve membership of the same 10-player Photon room.

### The cosmetic shop

Cosmetics are a planned hub feature. Proposed lore: the shop occupies a former Behavioral Enrichment supply room repurposed by escaped gorillas. Hats, toys, mirrors, and accessories come from enrichment supplies; goggles, badges, and uniforms can be scavenged staff belongings.

Optional details include SHOP scratched below the official sign and a repurposed supply dispenser. Enrichment tokens are a possible currency name only if a currency system is wanted. The shop name, purchase mechanism, and currency are unresolved.

### Surveillance wall

Planned layout: about four monitors surround the hub computer. Each displays its level name clearly. All footage, transitions, and scares are silent. Once more than four levels exist, rotate which levels appear on the screens.

Recommended schedule: show different available levels across the monitors and finish each level's view sequence before changing its assignment. Keep each level's scare counter when it rotates off a screen. Each level section defines its footage and scare sequence.

Recommended prototype: pan across saved wide images with grain and a separate monster variant. Simulated camera movement needs no running remote level scene. Short prerecorded clips can add perspective changes later. Label the loop as recorded footage; live sector occupancy remains an optional separate display using PUN player properties. See the technical references at the end of this document.

## Multiplayer and scene architecture

Confirmed editor: Unity 2022.3.55f1. Unity 6 belongs to Cheeky Chimps, the separate non-horror game Greg is making with his daughter. Planned architecture: one Photon PUN room per 10-player session, with independently loaded hub and level scenes. Sector travel keeps the same room.

| Scene or system | Responsibility |
| --- | --- |
| Bootstrap | Initialize persistent game systems once at startup. |
| Loading | Show startup loading and each sector transition. During travel, keep the current Photon room and persistent XR camera; do not restart startup/login. |
| Hub_Base | Retain the starting space, shop, computer, surveillance previews, and approach to the Security Gate. |
| Level1_Containment | Cage room, vent maze, crawler, keycard objective, and exit to Level 2. |
| Level02 and later | Each sector has its own safe entry, monster, objective, exit, and return control. |
| Persistent session systems | Keep room membership and player identity stable while local environments change. Avoid duplicate VR rigs and cameras. |

### Requirements to prove in a prototype

- Keep the 10-player cap across the whole session. Ten people spread between different scenes still occupy ten room slots.

- Use independent scene loading rather than PUN automatic synchronization to the Master Client scene.

- Filter player visibility, relevant network messages, and voice by sector. Handle arrival state explicitly; interest groups alone do not solve spawning and late arrivals.

- Assign one controller for each occupied level monster, with handover when its controller changes scenes or disconnects. That controller must have the level loaded.

- Load the current environment only during normal play. Scene activation and transition memory peaks still need Quest testing.

- Validate each personal keycard and exit. Only the completing player transitions; others see them disappear and stay in their level. Keep objective-card pickup, drop, floor recovery, and re-entry resets personal. Optional cross-level reward cards require their own lifecycle. Completion does not open a shared passage for followers.

### Earlier approaches that were superseded

Do not implement the previously discussed ABC123_Hub and ABC123_Level01 room family as the current plan: naming separate rooms does not enforce a shared 10-player limit. Shared group wins, a required party launch, and automatic hub return after every win also do not match the latest direction.

Random keycard locations and audio from the security monitors have also been superseded. The card has a fixed starting spot, and all monitors are silent.

## Level 1 Primate Containment

Working name: Sector 01 Primate Containment. The cages define the sector; the ventilation ducts are the escape route through it. The final name has not been fixed.

![Current Hub_Base map reference supplied by Greg](images/level1-map-reference.png)

Current map reference supplied by Greg. Door placement remains to be verified in the editor.

### Scene boundary

Hub_Base is the source map. Keep the starting area, shop, and approach up to the Security Gate in that scene. Extract the containment content into a new Level1_Containment scene. Level1, Level1_2_Hall, and disabled scene iterations are experiments, not destination scenes to restore.

Level1Root already groups most level content: cages, vent system, objective, respawn, navigation, and current monster. Check floors, walls, and ceilings at the gate before moving the group; shared meshes may need a physical split. Keep a complete threshold and matching arrival doorway on each side, with a fade covering travel.

### Individual attempt

Follow the marks from the safe cage room into a vent maze with dead ends and two paths into one keycard room. Each player has their own card at the same fixed starting spot. The Crawler stays in the vents; both rooms are safe. Either vent entrance can be used for the return trip. Use your card at the cage-room exit: hear the door, fade to black, and arrive safely in Level 2. Billy stays in Level 1 and sees you disappear; your completion does not open a shared passage for him.

Reported local capture behavior: respawn in the cage room and drop a held keycard at the capture location. It stays there for recovery; falling through the floor returns it to its original spot. Confirmed visibility rule: only the owner sees or recovers their dropped card. Capture does not reset the level or affect other players. This is the intended behavior and was reported during local testing; the repository baseline did not establish the complete personal-card lifecycle. See [R05](repository-improvement-plan.md#r05-the-active-keycard-path-does-not-implement-the-agreed-lifecycle) before assuming it is implemented.

### Leaving and returning

Leaving Level 1 for the hub or another level ends that visit. Re-entry restores your card to its original spot and clears its previous held or dropped state without duplicates. Other players' cards are unaffected. Partial-level progress is not saved.

### Level 1 lighting

Confirmed runtime correction: Level 1 is a horror environment, but it must not be so dark that walls, junctions, or the route are effectively invisible. The first emergency post-split fallback was still too dim in actual play. `codex/fix-level1-crawler-lighting` raises the cool ambient floor and shadowless directional fill while preserving a dark presentation. Final authored practical lights, contrast, flicker and Quest performance remain pending visual work.

**Proposed, not confirmed:** inside `Level1_Vents`, give only the local player a subtle head-mounted spot/headlamp effect—approximately a 40–50° cone with enough range to read the next junction but not the whole maze. Keep the global baseline readable so the headlamp is atmosphere and focus, not a requirement to see anything. Start shadowless/cheap on Quest and do not have remote players' lamps illuminate the local scene unless a later multiplayer presentation decision explicitly asks for it.

### Level 1 monster rules

Confirmed design: the Crawler is a gorilla with almost-severed legs. It moves and chases only inside the vents; the containment room and keycard room are safe. The two keycard-room entrances offer different routes back into the vents. `Zombie Crawl` is now the selected and integrated visible model on the existing Level 1 gameplay root; the old MiniGamesKid render is hidden while navigation, capture, patrol/audio and Photon state remain on that root.

Existing behavior reported by Greg: automated range detection starts pursuit. Players escape by moving quickly and losing the monster in the vents, reaching the keycard room, or retreating into the containment room. Sound 1 plays during normal patrol; Sound 2 plays while hunting.

Confirmed rule: when a player reaches either safe room or the monster loses the player, it returns to its set patrol path. Waiting at a safe-room entrance or adding a separate search phase is not the chosen behavior. Sight-based detection has not been selected.

**Implemented follow-up on `codex/fix-level1-crawler-lighting`:** preserve Zombie Crawl's authored world size when it is reparented below the scaled gameplay root; retune patrol/chase translation and animation playback so movement reads as crawling instead of sliding; expand prototype detection from the scene's 6 m value to a 12 m runtime range with faster checks and a clearer chase-speed increase; accept capture from any collider belonging to the local rig rather than only the camera collider; and repair the second safe-room route so exiting its correctly oriented safe boundary back toward the vents republishes `Level1_Vents`. The directional zone repair uses the tracked camera collider so a reaching hand cannot change the player's danger state early. These are implemented corrections, not yet validated Unity/headset behavior.

Pending validation: verify authored scale/floor contact in the narrowest vent, a 90-degree corner and junction; confirm crawl cadence while patrolling and chasing; confirm both safe-room entrances stop pursuit and both exits restore eligibility; confirm ordinary head/body/hand contact captures in the vents but cannot capture inside a safe room; confirm respawn/card drop and controller handover; and repeat on two Photon clients and target Quest hardware.

### The missing subject in Level 1

Chosen visual direction: scratches and/or blood run from a cage toward the vent, both guiding exploration and suggesting what escaped. The cage, trail, vent, and crawler should tell one connected story.

- Proposed: a damaged cage and matching subject number on the monster connect the creature to its former enclosure.
- Proposed: a maintenance request mentions damage inside the ducts before the broader incident.

- Proposed: a clipboard still labels the missing subject contained, suggesting negligence or a cover-up.

- Prototype recommendation: one personal staff-access card at the existing fixed spot. Keep two cards open for later testing only if a second location creates a different route or risk; putting both in the same room adds little. A two-credential containment release is optional lore, not a confirmed requirement.

### Level 1 camera sequence

Primate Containment cycles through three grainy views, using stills or a gentle side-to-side sweep. Proposed starting timing is 4 to 6 seconds per view, with brief visual static between cuts. All three views and the monster appearance are silent.

- Cage room: show the safe starting area, cages, and damaged enclosure so players recognize their arrival point.

- Keycard room: show the card at its fixed starting spot beside a recognizable feature. The image teaches where it belongs; it does not track a card dropped elsewhere during an attempt.

- Vent: usually show an empty duct. On approximately every fourth complete Primate Containment sequence, reveal the crawler for about one second before visual static interrupts the image.

Optional refinement: vary the interval between 3 and 6 sequences after testing. A partial view of the crawler near the lens preserves the full reveal for gameplay. The screens remain optional hints; in-level clues must still guide players.

## Proposed Level 2 Behavioral Conditioning

Working concept: a test wing with the Listener, a blind experiment that investigates noise. Two visual options are saved: Concept A, a lean eyeless biped with hearing cavities, and Concept B, a large dark creature with bloodstained bandages over its eyes. Both use rough low-poly graphics. Concept B is the current selected asset direction; final gameplay scale remains open and all movement stays on the floor.

### Four-fuse power restoration objective

**Confirmed September 11:** replace the hammer/strike repair with four personal, hand-held cylindrical power fuses. The fuses are distributed across the enlarged map so Level 2 requires exploration rather than immediately camping the Repair Lab. Each fuse is hidden in an obvious electrical/maintenance search container such as a drawer, cabinet or access panel. Container opening makes a short audible scrape/clunk that can attract the Listener; dropping a fuse also produces an audible impact.

The Repair Lab contains a large central power-distribution island with two fuse slots on each side and enough clearance for both the player and Listener to circle it. Insert a recovered fuse, then operate/hold the nearby charging lever. Charging creates a sustained electrical/mechanical sound that strongly attracts the Listener. The player can release the lever and flee if interrupted; exact charge duration is tuning, with roughly 8–12 seconds as the current starting range rather than a fixed rule.

A thick, clearly readable power conduit runs from the qualified exit back to the power island so a new player can discover the objective diegetically: locked door → follow cable → four empty sockets. Each charged fuse should make the system look progressively more alive. Completing the fourth charge powers the exit and creates the final noisy transition into a run toward the door; do not add another long master hold after all four fuses are complete.

### Layout and monster rules

**Confirmed size correction, September 11:** do not keep the old 18 × 18 m “small layout” as the Level 2 target. The replacement must be at least 36 × 36 m overall. Preserve the same gameplay relationships—safe entry, dangerous halls, bypass/looping routes, a repair room with two exits, a separate qualified exit, and the optional reward room in the bottom-right area—but use multiple connected spaces and large sightline-breaking geometry rather than uniformly scaling one hall. The detailed v0.4 arrangement remains proposed in [the expanded layout plan](level2-expanded-layout-v04.md).

Unlike the Crawler, the Listener can enter the objective room. It patrols, investigates the latest audible in-game impact, searches briefly, then resumes patrol if it hears nothing. New audible impacts update its destination; it does not magically know where a quiet player went. Only the entry and completed exit are safe. Give its approach an audible warning, then test whether both escape routes remain usable.

### Individual fuse progress and prototype scope

Fuse ownership, socket completion and exit qualification are personal per player. Friends may distract the shared Listener or deliberately create noise elsewhere, but they cannot satisfy another player's four-fuse requirement. A carried fuse drops at the capture location for its owner to recover. Installed/charged fuses remain completed through capture during the same Level 2 visit. Leaving Level 2 for the Hub or another level resets that visit and returns the four objective fuses to their fixed starting containers; no partial Level 2 attempt is saved across visits.

Search noise is intentionally tiered: opening drawers/cabinets is a short moderate event, dropping a fuse is a sharper impact, and charging is the sustained high-risk event. New audible events update the Listener's investigation destination rather than magically revealing a quiet player. Exact sound ranges, charge duration and container placement are tuning values.

Prototype the loop with four fixed fuse starts, readable maintenance-marked containers, the central island, four socket/lever pairs, per-player completion state, and one sustained noise event per active charge. Avoid pixel hunting: the challenge is safely searching noisy containers and transporting/charging the fuse, not finding tiny objects hidden among arbitrary clutter.

Proposed story clue: maintenance labels and trial notes can show that the exit power bus was intentionally isolated during auditory testing. The Hub preview can show the four-slot island or a powered conduit segment; all monitor footage stays silent.

## Listener concept A

Concept A is the first saved option. Greg approved this simpler level of detail after finding the first realistic render too polished for the game. It remains an alternate. Greg selected Concept B for the current monster asset work; Concept A is not the active map scale reference.

![Listener concept A with a lean eyeless silhouette](images/listener-concept-a.png)

Concept image only. The model, rig, and animations still need to be built.

### Visual and movement direction

Keep broad, simple shapes, rough low-resolution textures, and a readable silhouette. Preserve the blank face, hearing cavities, hunched stance, and identification cuff. Proposed movement: a slow floor walk, unnaturally still listening pauses, and a sharp head turn toward a hammer strike. Prototype with a conventional biped rig before adding detail.

## Listener concept B Bandaged giant

Confirmed current asset direction: Greg selected Concept B, the bandaged giant, for a faithful staged Blender rebuild. It has bloodstained eye bandages, a small recessed face, long heavy arms, and raised irregular shoulders. His latest appearance correction asks for a more uneven back and shoulders. Concept A remains an alternate; the repair-room floorplan remains the Level 2 layout reference.

![Listener concept B with bandaged eyes](images/listener-concept-b.png)

Concept B reference. Separate clay and historical rigged prototypes exist outside this map task. The Level 2 blockout includes only a disabled size guide, not a creature asset or animation. Final gameplay scale remains open.

### Appearance and story clues

Keep the huge raised shoulders, small recessed pale face, very long arms, large hands, and wrist restraints. Dirty off-white bandages wrap fully across the eyes, with dried blood stains on the cloth. Retain the rough textures and simple geometry. The dressing suggests a laboratory procedure involving its eyes; the exact experiment remains a story proposal.

### Why it is the Listener

Confirmed gameplay direction: it is blind and locates players through objective/search noise and loud in-game impacts. Fuse-container scrapes, dropped fuses and especially sustained charging noise provide investigation targets. It investigates the latest audible position, searches briefly, and moves on if it hears nothing further. Going quiet gives a player a chance to escape. On hearing a strong charge event, it stops and turns or tilts its head sideways to listen before approaching.

### Scale and movement prototype

Use a hunched floor walk and a short listening pause as the first animations. Preserve an audible approach warning. Test a looming size in the headset while ensuring the body can pass through both repair-room doorways and turn around the hall obstacles. The sketch's original 2 m openings were starting dimensions. The current blockout uses a proposed 3.2 m clear width; the expanded v0.4 plan proposes larger common routes and approximately 4 m actual framed clearance as a starting test target. Final creature scale and speed remain open.

### Expanded v0.4 review blockout

The September 11 correction sets **36 × 36 m as the minimum Level 2 footprint**. Branch `design/level2-expanded-map-v04` now implements that review blockout in the normal Level 2 scene/prefab. It keeps the existing safe-entry end where practical and expands mainly north/east into Test Hall A, Lower Service Hall, West Observation Wing, Conditioning Hall B, East Bypass, North Gallery, a larger Repair Lab, the qualified exit and the bottom-right reward room. This creates multiple connected loops instead of a single oversized room.

The Repair Lab has one route into North Gallery and another into the East Bypass. The confirmed fuse objective deepens it to roughly 16 × 10 m and replaces the north-wall hammer bench with a central four-slot power island that both player and Listener can circle. Large partitions, acoustic baffles, service blocks and offset bypass walls break long views without creating extra safe rooms. Source-level geometry checks confirm the player proxy and conservative Listener proxy can reach the Repair Lab through either route when the other repair doorway is blocked and can circulate around the island. Exact obstacle dressing, fuse-container placement, the 5 m ceiling target and provisional repair-frame scaling remain tuning/review items until Unity/headset validation. See [Level 2 expanded layout v0.4](level2-expanded-layout-v04.md).

## Listener level floorplan

The saved schematic below documents the **older 18 × 18 m v0.3 relationship** and remains useful for the original noisy-repair loop. Its footprint is superseded by the September 11 minimum 36 × 36 m correction; do not use the old overall dimensions as the v0.4 target.

![Proposed Listener level floorplan with a noisy repair room](images/listener-floorplan.png)

### Build and test the loop

Historical sketch sizes: test hall 14 × 10 m; repair room 8 × 4 m; entry and exit rooms each 6 × 4 m; bypass 4 m wide; door openings 2 m. Blockout v0.1 retained those nominal room sizes and proposed 3.2 m door widths for the giant. These values describe the current v0.3 asset, not the replacement footprint. V0.4 should keep both repair-room doorways usable by players and the Listener while testing larger route and clearance targets.

Place the mechanism away from the door openings. The two exits let players leave by the other route when the Listener arrives. Solid hall obstacles create corners for an escape; the bypass reconnects to the lower hall. The dashed line is one possible patrol route, not a fixed chase path. Test reach, turning space, warning time, and return opportunities in VR.

### Confirmed doorway standard

**Confirmed correction, September 9, 2026:** Greg requested reuse of the existing sci-fi gate frame for passages that do not need doors, scaled to fit each opening. Level exits must reuse the same complete gate as the Level 1 objective exit and preserve its size and scale for consistency. This supersedes treating the generic Level 2 shutter and identical 3.2 m dimensions for every doorway as the final direction.

The inspected Level 1 objective exit is `Level2_Entrance_Door`, an instance of `Assets/MASH Virtual/Sci Fi Doors/Prefab/Sci Fi Gates.prefab` (GUID `22d2c44fed4ffa743aac7afe4d993905`). Its root scale is `(1, 1.1564301, 0.8)` under an unscaled parent. It contains separate Frame, Door_Left and Door_Right children. Frame-only copies disable both door panels and retain frame collision; the original prefab remains unchanged.

**Implemented in v0.3 and merged through PR #4:** three linked frame-only instances at the entry and repair openings, the complete exit at the Level 1 scale, and the small Level 1 entrance gate for the bottom-right reward room. The interrupted v0.2 generation was rebuilt and source-checked before the merge. Both original prefab assets remain unchanged. Import requires the existing MASH sci-fi-door assets in this project. V0.4 should reuse the same standards while re-placing passages for the larger map.

### Editable Level 2 blockout v0.4 — branch review prototype

**Implemented on `design/level2-expanded-map-v04`:** the existing Level 2 scene path and reusable prefab are regenerated as `Level2_Blockout_v04` with a 36 × 36 m authored envelope, 5 m ceiling target, multiple connected chase spaces, two separated Repair Lab exits, retained Safe Entry/RETURN TO SECURITY wiring, the existing full exit gate family and bottom-right reward-room gate family. The v0.3 root was not uniformly scaled.

**Source validated on the branch:** generated Unity object IDs are unique, local serialized references resolve, five linked gate instances remain present, required travel bindings remain serialized, human and conservative Listener proxies reach the gameplay spaces, either repair doorway can be blocked while the other route still reaches the Repair Lab, and the temporary exit/reward blockers isolate their spaces as intended. These checks exclude actual linked-gate bevel/collider clearance, NavMesh, animated Listener dimensions, Unity import/runtime, Photon and headset performance.

**Pending validation:** Unity 2022.3.55f1 import/compile, both Runaway Chimps Editor validators, actual gate thresholds, Gorilla hand/body collision, 5 m scale judgment in headset, Listener turning/reach, NavMesh, safe-zone enforcement, repair pacing, travel, two-client Photon and Quest performance. `main` remains on v0.3 until this review branch is approved and merged.

### Editable Level 2 blockout v0.3

![Level 2 blockout with the optional reward room at bottom right](images/level2-blockout-floorplan.png)

**Implemented historical-size asset:** the editable rough map based on the saved Level 2 repair-room plan was developed on `codex/level2-blockout` and merged through PR #4 into `main` at `8657c3e`. PR #5 preserves those assets while layering the reliability audit changes on top. Runtime validation remains pending. Its 18 × 18 m size is now superseded as a design target by the September 11 correction, but the scene/prefab remains the current implemented Level 2 until v0.4 is built.

**Implemented asset:** `Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity` and `Prefabs/Level2_Blockout.prefab` contain independently editable floors, walls, ceilings, two solid obstacles, bypass, two repair passages, repair bench/hammer/conduit placeholders, linked sci-fi gates, the bottom-right reward room, scanner/sign and reward placeholders, and named gameplay markers. Entry and completed exit remain the intended safe rooms; marker triggers do not enforce safety. Static exit and reward barriers remain until the respective personal qualification logic is wired.

**Historical v0.3 dimensions:** the original hall, entry, exit, repair room and bypass retain their nominal sizes. The reward room is 4 × 4 m at X=14–18, Z=-4–0. Ceilings are 4.2 m, walls are 0.20 m and floor slabs are 0.25 m. Hall obstacles remain 3.4 m tall. Open-passage wall gaps are 3.2 × 3.8 m, the exit wall gap is 2.35 × 3.0 m and the reward wall gap is 2.2 × 3.1 m. These are wall openings, not measured clearances through the bevelled gate meshes. Full exit scale is preserved; frame-only scaling and small-gate placement need Unity inspection. The disabled giant guide remains 2.75 m wide × 3.2 m tall; final creature dimensions are open. These measurements remain useful for comparing v0.3, but they are not the approved v0.4 footprint.

**Validated outside Unity before merge, September 9–10, 2026:** generated YAML parses; local references and source-prefab object IDs resolve; all five linked gate instances have the expected scale and panel-visibility overrides. The 0.10 m box-layout grid connects the entry, exit approach, both repair passages and lower bypass for 0.35 m and 1.375 m radius proxies. Either repair opening can be blocked while the other route remains available. Closed barriers isolate the exit and reward room; removing the reward barrier connects the player proxy through its wall gap. These checks exclude the linked gate meshes and do not prove actual doorway, NavMesh or animated clearance. The overhead diagram uses schematic door symbols. Earlier Blender models and perspective images remain v0.1 references without the new reward room or gate revision.

**Pending validation:** rerun the reconciled source validator, then Unity 2022.3.55f1 import, gate/frame/threshold fitting, Gorilla hand/body collision, Listener turning and reach, safe-room exclusion, NavMesh, headset scale and performance, repair and capture rules, cross-level cards and saving, scanner/door interaction, personal rewards, exit qualification, sector travel and Photon PUN integration. The travel follow-up adds a SectorScene context, LevelTerminalActions binding and one enabled Build Settings entry. It reuses the persistent Bootstrap rig and adds no scene-owned rig, camera or Photon avatar. These checks apply to the current v0.3 asset; v0.4 will require the same checks again after geometry changes.

## Future levels and remaining decisions

| Open decision | Current recommendation or question |
| --- | --- |
| Starting-room navigation | Return to Hub is planned. Direct Select Sector is proposed; choose the final controls and wording. |
| Level access and records | Favor all levels available. Decide later whether individual completion badges or optional solo progression are useful. |
| Held card visibility | Cards and exits are personal. Decide whether other players see a held card; if shown, hide that visual on drop. The owner keeps their dropped card for recovery. |
| Crawler and difficulty | Validate the Zombie Crawl follow-up branch for size, crawl cadence, 12 m prototype detection, contact capture and both safe-room boundaries. Start with one card; add a second only if playtests justify a distinct route and story purpose. |
| Listener and final escape | Implement/tune the confirmed four-fuse search/charge loop, personal visit state and Listener sound response, then validate the final powered-door run. The eventual escape outside remains open. |
| Optional reward rooms | Choose card identities and source levels, persistence/capture rules, personal access versus following friends, collectible type, currency repeatability and room safety. |
| Hub lore and cosmetics | Decide the staff situation, cause of lockdown, shop identity, and missing-legs explanation. |

### Next prototype

For layout review, prototype with branch `design/level2-expanded-map-v04`; `main` still retains the merged v0.3 blockout until the enlarged geometry is approved and merged.

1. Verify the Hub/Level 1 scene split at the Security Gate; check each side has complete geometry and colliders.

1. Verify PR #6's Hub hallway `StartLevel1Button` appears before the gate in Play Mode, activates travel from a local hand/fingertip in both headset play and non-headset editor testing, and leaves the Hub gate itself non-selectable.

1. Verify that two clients share one Photon room while one stays in the hub and the other plays Level 1.

1. Test the personal exit and Level 2 return control. A follower sees the winner disappear, stays in Level 1, and still needs their own card.

1. Verify card drops, recovery after falling through the floor, and reset on re-entry without moving another player's card or creating duplicates.

1. Validate `codex/fix-level1-crawler-lighting`: Level 1 stays dark but readable; Zombie Crawl keeps authored world scale, visibly crawls during patrol/chase, pursues eligible vent players, both safe-room routes correctly end/restore eligibility, and local-rig contact captures only in the vents.

1. Test the fade and scene activation on the target Quest hardware, then add the cage-to-vent clues.

1. Review the implemented v0.4 blockout's room proportions, route readability and pacing. After layout approval, tune/regenerate the geometry as needed, then complete Level 2 navigation, travel, collision, Photon and headset validation on the enlarged map.

### Keeping this document current

Keep proposals labeled and organize each level by layout, monster, objective, resets, lore, and cameras. The source Word document version 0.9 confirmed Unity 2022.3.55f1, Hub_Base as the map to split, and Zombie Crawl as the replacement; one versus two cards remains a difficulty choice. Version 0.8 confirmed personal cards and exits. Earlier versions saved the Crawler rules, Listener options, noisy repair, and floorplan.

### Technical references

[Unity camera output to a Render Texture](https://docs.unity3d.com/2022.3/Documentation/Manual/class-RenderTexture.html)

[Unity Raw Image texture cropping and animation](https://docs.unity3d.com/2018.4/Documentation/Manual/script-RawImage.html)

[Photon PUN player properties and synchronization](https://doc.photonengine.com/pun/current/gameplay/synchronization-and-state)

[Photon PUN room capacity and lifetime](https://doc-api.photonengine.com/en/pun/current/class_photon_1_1_realtime_1_1_room_options.html)

[Photon PUN scene synchronization and loading API](https://doc-api.photonengine.com/en/pun/current/class_photon_1_1_pun_1_1_photon_network.html)

[Photon PUN interest groups and arrival limitations](https://doc.photonengine.com/pun/current/gameplay/interestgroups)