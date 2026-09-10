# Runaway Chimps design and lore

Last updated: 2026-09-10. Maintained repository edition, migrated from `Runaway_Chimps_Design_and_Lore.docx` version 0.9. The existing Word document is a downloadable snapshot; future edits belong here. See [AGENTS.md](../AGENTS.md) for the documentation workflow and the [repository improvement plan](repository-improvement-plan.md) for implementation evidence.

We are building a social VR horror game about gorillas escaping a laboratory that experiments on animals. This document records the current game rules, Level 1, the proposed Listener level, the story, and the scene architecture so future work can build on the same decisions.

## Implementation status

Design approval is separate from implementation and testing. **Confirmed** records an explicit design decision; **Planned** records an accepted direction awaiting implementation; **Proposed** is an idea awaiting agreement; **Open** is an unresolved choice. **Implemented** means code exists on a named branch or commit, not necessarily on `main`. **Pending validation** names checks not yet run. Mark something **Validated** only with an actual result, date, and tested version or commit.

As of 2026-09-09, [PR #2](https://github.com/GregStephen/RunawayChimps/pull/2), merged on 2026-09-09, contains the local vent-audio, proximity-reset, material-safety, and ten-player default changes in commit `d4ee616`. Unity compilation, Play Mode, Photon sessions, and headset checks are pending. These changes do not implement the scene split, shared monster synchronization, Zombie Crawl integration, or personal-card lifecycle. Follow [Checks before merging](../README.md#checks-before-merging).

On `level1split`, commit `ac69718` contains `Assets/Scenes/Level1_Containment.unity` and its metadata. Greg reports that the new scene and doors are committed. The follow-up build-settings change enables this scene after Bootstrap, Loading, and Hub_Base. The travel implementation prepared for `level1split` now adds guarded local loading, door/computer routes, separate Hub arrival markers, persistent XR interaction management, sector avatar/collision/voice presentation, and sector monster state messages. Unity compilation, extraction geometry, headset arrival clearance, and multiplayer validation remain pending; see the [travel checks](../README.md#scene-travel-checks).

The gameplay rules below describe the intended game. Reported local behavior is identified separately from what the repository review established.

The combined `level1split-travel.patch` also includes focused source cleanup for terminal color saving/preview and name-save handling, local startup readiness, cancelled/timeout player spawning, button release state, logging, material bindings, hand-impact audio, and held-item collision-layer restoration. The held-item script filename now matches its existing component class with its asset GUID preserved. GitHub upload of this patch did not complete; apply the latest patch before testing. These changes add no Level 1 terminal or new gameplay/progression rules; Unity and headset validation remain pending.

## Decision and correction record

| Recorded | Decision or correction | Status |
| --- | --- | --- |
| 2026-09-10 | Greg supports the reclaimed lab supply-room shop look. Asked how checkout should work; a simple purchase panel beside the mirror is proposed. | Visual direction confirmed; checkout fixture and interaction details proposed |
| 2026-09-10 | Develop the existing Hub Shop with seasonal stock, weekly rotating regular stock, and try-on/equip. Proceed through planning, mapping, then creation/integration; use Gorilla Tag as a reference. | Confirmed requirements; design proposal in hub-shop-plan.md; implementation pending |
| 2026-09-09 | Runaway Chimps uses Unity 2022.3.55f1 and Photon PUN. Unity 6 belongs to the separate, non-horror Cheeky Chimps project. | Confirmed correction from the source document |
| 2026-09-09 | Use Hub_Base as the source for a split at the Security Gate. Disabled Level1 and Level1_2_Hall scenes are experiments. | Confirmed direction; scene committed on level1split; extraction validation pending |
| 2026-09-09 | Replace MiniGamesKidFirstRig with Zombie Crawl while preserving and repairing the existing monster systems. | Confirmed direction; integration pending |
| 2026-09-09 | Personal cards start at fixed locations; completion affects one player and leads to the next level. Separate room families, randomized card starts, group wins, automatic hub return, and monitor audio are superseded. | Recorded from the current design; runtime implementation still needs verification |
| 2026-09-09 | Prototype one card; two remain an option if gameplay and story justify them. Do not treat the active two-card keybox as a defect solely because of its count. | Prototype recommendation; final count open |
| 2026-09-09 | Maintain the design and improvement plan in the repository; update relevant sections after confirmed decisions, corrections, implementation, or meaningful test results. Keep Word exports as snapshots. | Confirmed workflow |

| 2026-09-09 | Earlier instruction: all Return to Hub actions arrive in front of the computer. | Superseded by the route-specific clarification below |
| 2026-09-09 | Hub computer or hallway door → fade, Loading, Level 1 safe room. Level 1 return door → Hub hallway on the other side of that door. Future terminal → Hub computer when returning to Hub; it can also select another level. | Confirmed correction; Hub/Level 1 routes prepared in the local patch, pending application and Unity/headset validation. Future level terminal and other destinations remain planned. Final button-versus-door control remains open. |
| 2026-09-09 | Defer placing a Level 1 terminal until more levels exist. Keep the door return as the current Level 1 route to Hub. | Confirmed; no Level 1 terminal added |
| 2026-09-09 | Use the committed scene name Level1_Containment, replacing the earlier planned spelling Level01_Containment. Greg reports the scene split and doors committed on level1split at ac69718. | Scene asset and metadata verified; geometry, door wiring, and runtime travel pending validation |

These dates record migration and clarification, not the original date of every earlier decision. Future corrections should name the superseded rule and update its affected sections.

## Current decisions

| Status | Decision |
| --- | --- |
| Confirmed | The overall objective is to escape the laboratory. Story clues should be distributed throughout the environment. |
| Confirmed | Players complete levels individually. One player winning does not win or reset the level for everyone. |
| Confirmed | Level 1 ends when a player returns the keycard to the cage room and opens the locked objective door. |
| Confirmed | Cards are personal, with fixed starting locations. Capture drops a held card; re-entry resets it. One card is the prototype recommendation; two remain an option if they improve the route and story. |
| Confirmed | The winner hears the door, fades to black, and arrives safely in Level 2. Other players see them disappear and stay in Level 1. |
| Confirmed | The Crawler has almost-severed legs and cannot leave the vents. Scratches and/or blood lead from its cage toward the vent. |
| Planned | One Photon PUN room holds up to 10 players across the hub and all levels. Each headset loads its current level scene. |
| Planned | About four silent monitors surround the hub computer. Each displays its level name; additional levels rotate onto the screens. |
| Planned | A control in each level starting room allows return to the hub. The current direction favors open level selection without mandatory unlocks. |
| Planned | A starting-room terminal offers return to the Hub or travel to another level. Its UI, available destinations, and final appearance remain to be built. Separate solo progression and permanent completion records are not committed features. |

### Latest flow change

Winning leads forward into the next level. Returning to the hub is an available choice from the next safe starting room, rather than the automatic result of a win. Direct travel from a future starting-room terminal is now confirmed direction; additional destination scenes and terminal UI remain planned.

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

The next level begins in a safe room where a player can regroup. A future terminal will offer return to the Hub or travel to another level. The terminal is not placed yet; its appearance and exact destination list remain open.

Recommendation for review: offer Return to Hub and Select Sector at this terminal. A player can continue the story by walking into the current level, or jump elsewhere without an extra trip through the hub. Keep selection confined to safe starting rooms so it does not become an escape button during a chase.

Confirmed travel routes (Greg’s latest clarification supersedes the earlier all-returns-to-computer rule):

| Action | Destination marker | Arrival |
| --- | --- | --- |
| Hub computer: select Level 1 | Level1EntrySpawn | Level 1 safe cage room |
| Hub hallway door or its button | Level1EntrySpawn | Same Level 1 safe cage room |
| Level 1 entrance door: return | HubDoorReturnSpawn | Hub hallway, on the Hub side of the door |
| Future level terminal: return to Hub | HubReturnSpawn | In front of the Hub computer |

Every route fades to black, displays the Loading scene, then fades into the destination. Travel is an explicit interaction; walking into the door does not automatically transition. Both the existing Hub button and XR door selection are wired for testing while the final control choice remains open. The future terminal can use `SectorTravelService.ReturnToHub()`; that API does not mean its UI or Level 2 travel is built. Arrival markers exist in the scene assets; verify their clear floor space, facing, and reach in Unity and on a headset.

A player who finishes Level 1 first can wait in the safe Level 2 starting room for a friend. Travel affects the interacting player only. Changing sectors, returning to the hub, or following a friend must preserve membership of the same 10-player Photon room.

### The cosmetic shop

**Confirmed requirements — 2026-09-10:** develop the existing Hub Shop room with seasonal items, regular items rotating weekly, and cosmetic try-on/equip facilities. Greg requests planning first, then mapping, then asset creation and system integration, with Gorilla Tag as a reference and room for improvements. The cosmetics themselves remain undesigned.

**Source inspected on main at c388d87:** Shop contains the existing mirror and a “Current Coconuts” board bound to CoconutDisplay. EconomyState, PlayFab inventory loading and Photon avatar cosmetic setters exist; this is not a complete or validated shop. The earlier suggestion “Enrichment tokens” remains historical brainstorming and must not replace the existing Coconuts implementation without a new decision.

**Confirmed visual direction — 2026-09-10:** Greg supports the reclaimed lab supply-room look. Specific fixture designs and the exact Behavioral Enrichment story/name remain proposed.

**Proposed:** use simple worn racks, crates and sign plates. Separate seasonal and weekly displays would surround an open centre; try-on, owned-item selection and purchase confirmation would share the existing mirror area. For checkout, use a small self-service purchase panel on a worn counter beside the mirror, with an item/price/balance readout and large CONFIRM PURCHASE / CANCEL controls. The player spends Coconuts electronically after confirmation; no physical cash handoff is required. The panel design, layout, slot counts and interaction details await agreement.

See [Hub shop planning proposal](hub-shop-plan.md) for source evidence, tentative zoning, player flow and staged acceptance. No Shop scene, purchase or runtime changes are included in the planning update. Unity, Photon, backend and headset validation remain pending.

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

- Validate each personal keycard and exit. Only the completing player transitions; others see them disappear and stay in their level. Keep pickup, drop, floor recovery, and re-entry resets personal. Completion does not open a shared passage for followers.

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

Working concept: a small test wing with the Listener, a blind experiment that investigates noise. Two visual options are saved: Concept A, a lean eyeless biped with hearing cavities, and Concept B, a large dark creature with bloodstained bandages over its eyes. Both use rough low-poly graphics. Final appearance remains open; all movement stays on the floor.

### Noisy repair objective

Chosen direction: replace this level's proposed keycard with work that makes noise. Players must stop, flee when interrupted, and return to continue. Level 1 keeps its keycard objective.

Proposed mechanism: free the jammed emergency release for the exit shutter. A hammer rests beside the mechanism. Each deliberate strike moves a seized part slightly, advances that player's repair, and creates a noise at the workbench for the Listener to investigate. The tool is available here without a separate search; it returns to its rack if lost.

The player hears the Listener approaching, stops work, escapes through either doorway, and loops back once it moves on. Reaching the required repair amount enables that player's exit. Prototype the required strikes and approach timing in play; repeated visits should arise from its position and reactions, not a scripted interruption after a fixed number of hits.

### Layout and monster rules

Keep the proposed small layout: safe entry, test hall with two solid obstacles, a bypass, a repair room with two doorways, and a separate exit. The repair room replaces the test booth and remains dangerous. Both doorways must connect to a usable loop around the obstacles. A visible conduit can connect the release mechanism to the exit shutter.

Unlike the Crawler, the Listener can enter the objective room. It patrols, investigates the latest audible in-game impact, searches briefly, then resumes patrol if it hears nothing. New audible impacts update its destination; it does not magically know where a quiet player went. Only the entry and completed exit are safe. Give its approach an audible warning, then test whether both escape routes remain usable.

### Individual progress and prototype scope

Repair progress survives stopping and fleeing. Proposed visit rule: leaving for the hub or another level resets it; no saved partial attempt. Capture behavior remains open. Recommendation for review: retain completed work after capture during the same visit, while returning the player to the safe entry.

Each player has their own repair amount and exit qualification; repair sounds affect the shared Listener. A friend can distract it, but completing their repair does not complete yours. Enforce the qualification at the exit, including when another player opens a visible shared door.

First prototype: plain rooms, one floor-moving placeholder, one repair target, one progress counter per player, and a sound event per valid strike. Start with a button to simulate a strike, then add the hammer and reject repeated contact jitter. Leave physical bolt simulation, microphone listening, and detailed creature animation for later. Test interruption and return with two players before adding detail.

Proposed story clue: a maintenance note forbids impact tools during auditory trials. The hub preview can show the repair station and the Listener beside it; all monitor footage stays silent.

## Listener concept A

Concept A is the first saved option. Greg approved this simpler level of detail after finding the first realistic render too polished for the game. It remains available alongside Concept B; neither design is a final monster selection.

![Listener concept A with a lean eyeless silhouette](images/listener-concept-a.png)

Concept image only. The model, rig, and animations still need to be built.

### Visual and movement direction

Keep broad, simple shapes, rough low-resolution textures, and a readable silhouette. Preserve the blank face, hearing cavities, hunched stance, and identification cuff. Proposed movement: a slow floor walk, unnaturally still listening pauses, and a sharp head turn toward a hammer strike. Prototype with a conventional biped rig before adding detail.

## Listener concept B Bandaged giant

Saved as an additional visual option at Greg's request. This version emphasizes size and an unsettling face, with bloodstained bandages covering both eyes to communicate blindness. Concept A and the current repair-room floorplan remain available.

![Listener concept B with bandaged eyes](images/listener-concept-b.png)

Concept B reference. Proposed appearance only; no model, rig, or animation has been implemented.

### Appearance and story clues

Keep the huge raised shoulders, small recessed pale face, very long arms, large hands, and wrist restraints. Dirty off-white bandages wrap fully across the eyes, with dried blood stains on the cloth. Retain the rough textures and simple geometry. The dressing suggests a laboratory procedure involving its eyes; the exact experiment remains a story proposal.

### Why it is the Listener

Proposed behavior: it is blind and locates players through hammer strikes and loud in-game movement impacts. It investigates the last sound position, searches briefly, and moves on if it hears nothing further. Going quiet gives a player a chance to escape its search. On hearing a strike, it stops and turns or tilts its head sideways to listen before approaching.

### Scale and movement prototype

Use a hunched floor walk and a short listening pause as the first animations. Preserve an audible approach warning. Test a looming size in the headset while ensuring the body can pass through both repair-room doorways and turn around the hall obstacles. The existing 2 m door widths are starting dimensions to check against the model; final creature height and speed remain open.

## Listener level floorplan

Saved schematic of the proposed Behavioral Conditioning layout. The former keycard booth is now a repair room with a noisy mechanism. Dimensions are starting values for headset testing; this is not a finished Unity map.

![Proposed Listener level floorplan with a noisy repair room](images/listener-floorplan.png)

### Build and test the loop

Starting sizes: test hall 14 × 10 m; repair room 8 × 4 m; entry and exit rooms each 6 × 4 m; bypass 4 m wide; door openings 2 m. Keep both repair-room doorways usable by players and the Listener.

Place the mechanism away from the door openings. The two exits let players leave by the other route when the Listener arrives. Solid hall obstacles create corners for an escape; the bypass reconnects to the lower hall. The dashed line is one possible patrol route, not a fixed chase path. Test reach, turning space, warning time, and return opportunities in VR.

## Future levels and remaining decisions

| Open decision | Current recommendation or question |
| --- | --- |
| Starting-room navigation | Return to Hub is planned. Direct Select Sector is proposed; choose the final controls and wording. |
| Level access and records | Favor all levels available. Decide later whether individual completion badges or optional solo progression are useful. |
| Held card visibility | Cards and exits are personal. Decide whether other players see a held card; if shown, hide that visual on drop. The owner keeps their dropped card for recovery. |
| Crawler and difficulty | Integrate Zombie Crawl while retaining existing vent systems. Start with one card; add a second only if playtests justify a distinct route and story purpose. |
| Listener and final escape | Choose the repair mechanism and capture reset rule; tune the Listener. The eventual escape outside remains open. |
| Hub lore and cosmetics | Decide the staff situation, cause of lockdown, shop identity, and missing-legs explanation. |

### Next prototype

Prototype with the hub, Level 1, and a placeholder Level 2 safe room.

1. Split the current Hub_Base at the Security Gate into Hub_Base and Level1_Containment; verify each side has complete geometry and colliders.

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
