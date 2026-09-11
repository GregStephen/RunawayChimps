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

## Decision and correction record

| Recorded | Decision or correction | Status |
| --- | --- | --- |
| 2026-09-11 | Hub hallway → Level 1 uses the physical `StartLevel1Button` beside the gate as the only player-facing activation. Touch/press it with the local monkey hand or fingertip; do not require Grip, Trigger, or XR Select on the door itself. This keeps headset play and non-headset editor testing on the same hand-collision interaction. The door remains a barrier/visual, not an interactable. | Confirmed correction; implemented on PR #6 (`codex/hub-level1-physical-button`). Source inspection found the existing button root authored inactive while its visible mesh, trigger, local-hand filter and `SectorDoor.Travel` binding remain intact. PR #6 removes Hub-door direct XR Select, reactivates the button root at runtime, and expands the validator for placement/visibility/wiring. Unity 2022.3.55f1 Play Mode/headset validation remains pending. Level 1 → Hub return interaction is unchanged. |
| 2026-09-10 | Reconcile the reliability-audit branch with merged PR #4 without dropping Level 2 or the audit fixes. Preserve the active `KeyCard`/`VRKeyCard` GUID identities, PR #4's local-pickup ownership check, Level 2 menu/completion routes, and current five-scene Build Settings; combine them with the audit reconnect/pause and missing-script safeguards. | Implemented in PR #5 merge commit `ad1a424`; GitHub mergeable/clean; source checker, Unity, Photon and headset validation pending |
| 2026-09-10 | Replace the proposed elaborate Level 2 terminal with one large wall button labeled RETURN TO SECURITY. It returns to the Hub computer; no Level 1 selection UI for now. Internal Hub naming is unchanged. | Confirmed; simple geometry and local-hand interaction merged through PR #4; Unity/headset checks pending |
| 2026-09-09 | Wire Level 1 completion and Hub selection to Level 2's safe entry; the future Level 2 terminal returns to Level 1's safe cage room or the Hub computer. | Confirmed; code and scene wiring merged through PR #4 and preserved by PR #5 conflict resolution; Unity/headset validation pending |
| 2026-09-09 | Add optional locked reward rooms opened by a card discovered on a different level, using the smaller Level 1 entrance door. Rewards may be a collectible, in-game currency, or both. Level 4 supplying Level 2 is an example, not a fixed assignment. | Confirmed direction; gameplay planned |
| 2026-09-09 | Put Level 2's reward room in the bottom-right area to spread out points of interest. This supersedes the assistant's suggested space between the exit and repair room. | Confirmed correction; blockout v0.3 merged through PR #4 |
| 2026-09-09 | Reuse scaled sci-fi gate frames for open passages and the complete Level 1 exit gate at its existing scale for exits. | Confirmed; linked assets in blockout v0.3, Unity validation pending |
| 2026-09-09 | Greg requested a rough Level 2 map from the saved noisy-repair floorplan. Unity blockout v0.1 preserves that layout; 3.2 m openings are a proposed scale allowance for the giant. | Asset merged through PR #4; import and headset validation pending |
| 2026-09-09 | Runaway Chimps uses Unity 2022.3.55f1 and Photon PUN. Unity 6 belongs to the separate, non-horror Cheeky Chimps project. | Confirmed correction from the source document |
| 2026-09-09 | Use Hub_Base as the source for a split at the Security Gate. Disabled Level1 and Level1_2_Hall scenes are experiments. | Confirmed direction; scene committed on level1split; extraction validation pending |
| 2026-09-09 | Replace MiniGamesKidFirstRig with Zombie Crawl while preserving and repairing the existing monster systems. | Confirmed direction; integration pending |
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
| Confirmed | Hub hallway entry into Level 1 uses a physical hand-press button beside the gate. The gate itself is not selected with Grip/Trigger. Use the same local hand/fingertip collision in headset play and non-headset editor testing. |
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

**Confirmed placement correction:** the Level 2 room belongs at the bottom right. Do not put it between the exit and repair room; Greg found that arrangement too busy. The local blockout uses a proposed 4 × 4 m room beneath the bypass, with its door facing north into the bypass. It fits inside the existing 18 × 18 m envelope. Room size and detailed dressing remain adjustable.

**Proposed readable clue:** use the same color plus a distinct symbol on the scanner sign and keycard, with a short readable name/code in the final art. Do not rely on color alone. The blockout's cyan plaque and three white bars are illustrative, not an approved card identity. Level 4 supplying this Level 2 room is Greg's example; the exact source level, placement, and card identity remain open. No Level 4 asset or card has been created.

**Planned requirement:** ownership must survive travel from the source level to Level 2. The attempt-based Level 1 card rules must not erase a newly acquired bonus-room card during that journey. **Recommended for review:** record a personal, permanent, non-consumed unlock when collected, surviving capture and game restarts. Present it as a reusable access card when scanning. Permanent persistence and capture behavior are recommendations, not confirmed rules.

**Reward proposal:** one collectible per player, with an optional first-claim currency bonus. Keep card ownership, room access, and reward-claim state separate so reopening a door does not award the same collectible or currency again. Whether currency is repeatable, its amount, the collectible type, how an already-claimed room appears, and whether friends may follow an unlocked door are open decisions. A reward should not disappear for everybody when one player claims theirs. Currency grants will need the project's account/economy integration; the blockout contains no award logic.

**Open monster rule:** this room is not marked as an additional safe room. Its small doorway may exclude the giant physically; decide and test that behavior before integrating AI so it does not accidentally become an unrestricted chase refuge.

**Implementation status:** v0.3 contains the room shell, original linked small-door prefab, temporary closed barrier, scanner/sign shapes, collectible plinth and optional currency-cache placeholder. Card pickup, saved ownership, scanning, door animation, reward claims and multiplayer behavior are planned and unwired.

## Facility lore

### Established premise

The player characters are laboratory gorillas trying to escape the facility that experiments on them. The overall objective is to get outside the lab. The precise experiments and facility history remain to be written; each level has its own monster, objective, and environmental clues.

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

### Level 1 monster rules

Confirmed design: the Crawler is a gorilla with almost-severed legs. It moves and chases only inside the vents; the containment room and keycard room are safe. The two keycard-room entrances offer different routes back into the vents. The current monster is Level1Root/MiniGamesKidFirstRig. The selected replacement is Zombie Crawl, already present as a disabled object in Hub_Base with an animation controller assigned.

Existing behavior reported by Greg: automated range detection starts pursuit. Players escape by moving quickly and losing the monster in the vents, reaching the keycard room, or retreating into the containment room. Sound 1 plays during normal patrol; Sound 2 plays while hunting.

Confirmed rule: when a player reaches either safe room or the monster loses the player, it returns to its set patrol path. Waiting at a safe-room entrance or adding a separate search phase is not the chosen behavior. Exact detection and loss conditions need inspection in the current script; sight-based detection has not been selected.

Next implementation task: preserve the existing navigation, patrol points, chase sounds, proximity effects, and capture behavior while integrating Zombie Crawl. Rebind model-specific Animator and material references; do not reuse old bone targets blindly. Use navigation to move an upright root and let the crawl animation follow its speed. Tune body height, hand contact, corner clearance, and capture reach in the actual vents.

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

Working concept: a small test wing with the Listener, a blind experiment that investigates noise. Two visual options are saved: Concept A, a lean eyeless biped with hearing cavities, and Concept B, a large dark creature with bloodstained bandages over its eyes. Both use rough low-poly graphics. Concept B is the current selected asset direction; final gameplay scale remains open and all movement stays on the floor.

### Noisy repair objective

Chosen direction: replace this level's proposed keycard with work that makes noise. Players must stop, flee when interrupted, and return to continue. Level 1 keeps its keycard objective.

Proposed mechanism: free the jammed emergency release for the exit shutter. A hammer rests beside the mechanism. Each deliberate strike moves a seized part slightly, advances that player's repair, and creates a noise at the workbench for the Listener to investigate. The tool is available here without a separate search; it returns to its rack if lost.

The player hears the Listener approaching, stops work, escapes through either doorway, and loops back once it moves on. Reaching the required repair amount enables that player's exit. Prototype the required strikes and approach timing in play; repeated visits should arise from its position and reactions, not a scripted interruption after a fixed number of hits.

### Layout and monster rules

Keep the proposed small layout: safe entry, test hall with two solid obstacles, a bypass, a repair room with two doorways, a separate exit, and an optional reward room at the bottom right off the bypass. The repair room replaces the test booth and remains dangerous. Both doorways must connect to a usable loop around the obstacles. A visible conduit can connect the release mechanism to the exit shutter.

Unlike the Crawler, the Listener can enter the objective room. It patrols, investigates the latest audible in-game impact, searches briefly, then resumes patrol if it hears nothing. New audible impacts update its destination; it does not magically know where a quiet player went. Only the entry and completed exit are safe. Give its approach an audible warning, then test whether both escape routes remain usable.

### Individual progress and prototype scope

Repair progress survives stopping and fleeing. Proposed visit rule: leaving for the hub or another level resets it; no saved partial attempt. Capture behavior remains open. Recommendation for review: retain completed work after capture during the same visit, while returning the player to the safe entry.

Each player has their own repair amount and exit qualification; repair sounds affect the shared Listener. A friend can distract it, but completing their repair does not complete yours. Enforce the qualification at the exit, including when another player opens a visible shared door.

First prototype: plain rooms, one floor-moving placeholder, one repair target, one progress counter per player, and a sound event per valid strike. Start with a button to simulate a strike, then add the hammer and reject repeated contact jitter. Leave physical bolt simulation, microphone listening, and detailed creature animation for later. Test interruption and return with two players before adding detail.

Proposed story clue: a maintenance note forbids impact tools during auditory trials. The hub preview can show the repair station and the Listener beside it; all monitor footage stays silent.

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

Proposed behavior: it is blind and locates players through hammer strikes and loud in-game movement impacts. It investigates the last sound position, searches briefly, and moves on if it hears nothing further. Going quiet gives a player a chance to escape its search. On hearing a strike, it stops and turns or tilts its head sideways to listen before approaching.

### Scale and movement prototype

Use a hunched floor walk and a short listening pause as the first animations. Preserve an audible approach warning. Test a looming size in the headset while ensuring the body can pass through both repair-room doorways and turn around the hall obstacles. The sketch's original 2 m openings were starting dimensions. The current blockout uses a proposed 3.2 m clear width, described below; final creature scale and speed remain open.

## Listener level floorplan

Saved schematic of the proposed Behavioral Conditioning layout. The former keycard booth is now a repair room with a noisy mechanism. Dimensions are starting values for headset testing. A separate editable Unity map blockout now follows this sketch; its implementation and remaining checks are recorded below.

![Proposed Listener level floorplan with a noisy repair room](images/listener-floorplan.png)

### Build and test the loop

Original sketch sizes: test hall 14 × 10 m; repair room 8 × 4 m; entry and exit rooms each 6 × 4 m; bypass 4 m wide; door openings 2 m. Blockout v0.1 retains the nominal room sizes and proposes 3.2 m door widths for the giant. Keep both repair-room doorways usable by players and the Listener.

Place the mechanism away from the door openings. The two exits let players leave by the other route when the Listener arrives. Solid hall obstacles create corners for an escape; the bypass reconnects to the lower hall. The dashed line is one possible patrol route, not a fixed chase path. Test reach, turning space, warning time, and return opportunities in VR.

### Confirmed doorway standard

**Confirmed correction, September 9, 2026:** Greg requested reuse of the existing sci-fi gate frame for passages that do not need doors, scaled to fit each opening. Level exits must reuse the same complete gate as the Level 1 objective exit and preserve its size and scale for consistency. This supersedes treating the generic Level 2 shutter and identical 3.2 m dimensions for every doorway as the final direction.

The inspected Level 1 objective exit is `Level2_Entrance_Door`, an instance of `Assets/MASH Virtual/Sci Fi Doors/Prefab/Sci Fi Gates.prefab` (GUID `22d2c44fed4ffa743aac7afe4d993905`). Its root scale is `(1, 1.1564301, 0.8)` under an unscaled parent. It contains separate Frame, Door_Left and Door_Right children. Frame-only copies disable both door panels and retain frame collision; the original prefab remains unchanged.

**Implemented in v0.3 and merged through PR #4:** three linked frame-only instances at the entry and repair openings, the complete exit at the Level 1 scale, and the small Level 1 entrance gate for the bottom-right reward room. The interrupted v0.2 generation was rebuilt and source-checked before the merge. Both original prefab assets remain unchanged. Import requires the existing MASH sci-fi-door assets in this project.

### Editable Level 2 blockout v0.3

![Level 2 blockout with the optional reward room at bottom right](images/level2-blockout-floorplan.png)

**Confirmed request:** an editable rough map based on the saved Level 2 repair-room plan. The standalone scene and matching reusable prefab were developed on `codex/level2-blockout` and merged through PR #4 into `main` at `8657c3e`. PR #5 preserves those assets while layering the reliability audit changes on top. Runtime validation remains pending. This map task does not promote proposed gameplay rules to confirmed status.

**Implemented asset:** `Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity` and `Prefabs/Level2_Blockout.prefab` contain independently editable floors, walls, ceilings, two solid obstacles, bypass, two repair passages, repair bench/hammer/conduit placeholders, linked sci-fi gates, the bottom-right reward room, scanner/sign and reward placeholders, and named gameplay markers. Entry and completed exit remain the intended safe rooms; marker triggers do not enforce safety. Static exit and reward barriers remain until the respective personal qualification logic is wired.

**Proposed dimensions:** the original hall, entry, exit, repair room and bypass retain their nominal sizes. The new reward room is 4 × 4 m at X=14–18, Z=-4–0. Ceilings are 4.2 m, walls are 0.20 m and floor slabs are 0.25 m. Hall obstacles remain 3.4 m tall. Open-passage wall gaps are 3.2 × 3.8 m, the exit wall gap is 2.35 × 3.0 m and the reward wall gap is 2.2 × 3.1 m. These are wall openings, not measured clearances through the bevelled gate meshes. Full exit scale is preserved; frame-only scaling and small-gate placement need Unity inspection. The disabled giant guide remains 2.75 m wide × 3.2 m tall; final creature dimensions are open.

**Validated outside Unity before merge, September 9–10, 2026:** generated YAML parses; local references and source-prefab object IDs resolve; all five linked gate instances have the expected scale and panel-visibility overrides. The 0.10 m box-layout grid connects the entry, exit approach, both repair passages and lower bypass for 0.35 m and 1.375 m radius proxies. Either repair opening can be blocked while the other route remains available. Closed barriers isolate the exit and reward room; removing the reward barrier connects the player proxy through its wall gap. These checks exclude the linked gate meshes and do not prove actual doorway, NavMesh or animated clearance. The overhead diagram uses schematic door symbols. Earlier Blender models and perspective images remain v0.1 references without the new reward room or gate revision.

**Pending validation:** rerun the reconciled source validator, then Unity 2022.3.55f1 import, gate/frame/threshold fitting, Gorilla hand/body collision, Listener turning and reach, safe-room exclusion, NavMesh, headset scale and performance, repair and capture rules, cross-level cards and saving, scanner/door interaction, personal rewards, exit qualification, sector travel and Photon PUN integration. The travel follow-up adds a SectorScene context, LevelTerminalActions binding and one enabled Build Settings entry. It reuses the persistent Bootstrap rig and adds no scene-owned rig, camera or Photon avatar.

## Future levels and remaining decisions

| Open decision | Current recommendation or question |
| --- | --- |
| Starting-room navigation | Return to Hub is planned. Direct Select Sector is proposed; choose the final controls and wording. |
| Level access and records | Favor all levels available. Decide later whether individual completion badges or optional solo progression are useful. |
| Held card visibility | Cards and exits are personal. Decide whether other players see a held card; if shown, hide that visual on drop. The owner keeps their dropped card for recovery. |
| Crawler and difficulty | Integrate Zombie Crawl while retaining existing vent systems. Start with one card; add a second only if playtests justify a distinct route and story purpose. |
| Listener and final escape | Choose the repair mechanism and capture reset rule; tune the Listener. The eventual escape outside remains open. |
| Optional reward rooms | Choose card identities and source levels, persistence/capture rules, personal access versus following friends, collectible type, currency repeatability and room safety. |
| Hub lore and cosmetics | Decide the staff situation, cause of lockdown, shop identity, and missing-legs explanation. |

### Next prototype

Prototype with the hub, Level 1, and the Level 2 safe entry currently present in the merged blockout.

1. Verify the Hub/Level 1 scene split at the Security Gate; check each side has complete geometry and colliders.

1. Verify PR #6's Hub hallway `StartLevel1Button` appears before the gate in Play Mode, activates travel from a local hand/fingertip in both headset play and non-headset editor testing, and leaves the Hub gate itself non-selectable.

1. Verify that two clients share one Photon room while one stays in the hub and the other plays Level 1.

1. Test the personal exit and Level 2 return control. A follower sees the winner disappear, stays in Level 1, and still needs their own card.

1. Verify card drops, recovery after falling through the floor, and reset on re-entry without moving another player's card or creating duplicates.

1. Integrate and tune the Crawler model; verify vent limits, patrol return, both sound states, capture, respawn, controller handover, and arrival during a chase.

1. Test the fade and scene activation on the target Quest hardware, then add the cage-to-vent clues.

### Keeping this document current

Keep proposals labeled and organize each level by layout, monster, objective, resets, lore, and cameras. The source Word document version 0.9 confirmed Unity 2022.3.55f1, Hub_Base as the map to split, and Zombie Crawl as the replacement; one versus two cards remains a difficulty choice. Version 0.8 confirmed personal cards and exits. Earlier versions saved the Crawler rules, Listener options, noisy repair, and floorplan.

### Technical references

[Unity camera output to a Render Texture](https://docs.unity3d.com/2022.3/Documentation/Manual/class-RenderTexture.html)

[Unity Raw Image texture cropping and animation](https://docs.unity3d.com/2018.4/Documentation/Manual/script-RawImage.html)

[Photon PUN player properties and synchronization](https://doc.photonengine.com/pun/current/gameplay/synchronization-and-state)

[Photon PUN room capacity and lifetime](https://doc-api.photonengine.com/en/pun/current/class_photon_1_1_realtime_1_1_room_options.html)

[Photon PUN scene synchronization and loading API](https://doc-api.photonengine.com/en/pun/current/class_photon_1_1_pun_1_1_photon_network.html)

[Photon PUN interest groups and arrival limitations](https://doc.photonengine.com/pun/current/gameplay/interestgroups)
