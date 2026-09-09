# Runaway Chimps repository improvement plan

Last updated: 2026-09-09. Maintained repository edition, migrated from `Runaway_Chimps_Repository_Review_and_Plan.docx`, Revision 2. The original review examined `main` at `4f6894141aa2b132d744cb2423c1ee227e0fc5be`; its evidence links remain pinned to that baseline. The Word document is a historical downloadable snapshot.

Read the [design and lore](design-and-lore.md) for intended behavior and [AGENTS.md](../AGENTS.md) for update rules. A confirmed finding describes source evidence; it does not mean its fix is implemented or tested.

## Current implementation and validation status

[PR #2](https://github.com/GregStephen/RunawayChimps/pull/2), branch `codex/level1-reliability`, is open for review. Commit `d4ee616303ea7a8c6a4aa8d877a3be8086e07939` contains the initial code fixes. It has the same complete file tree as bundled source commit `1eb089fdc54ea821d0f5e3ad90c8f3def9bbe9b7`; the publishing API created a new commit ID. The documentation migration follows as a separate commit.

| Area | Implementation | Validation and remaining work |
| --- | --- | --- |
| R04 audio and proximity | Partially implemented in `d4ee616`: AudioScaler now stops excluded playback and stops on disable; PlayerVentState uses the local zone service in Photon rooms; cross-zone and disabled reactors reset effects. | Unity, Play Mode, existing patrol/chase clips, two-client local filtering, and headset checks pending. Shared monster state and a complete audio-ownership design remain open implementation work. |
| R11 capacity | DefaultRoomLimit changed to ten in `d4ee616`. No serialized override was changed. | New public/private room checks pending. Existing rooms are not resized. Capacity across independently loaded sectors still depends on the unimplemented travel design. |
| Material and proximity cleanup | MaterialSwapper guards destroyed renderers and invalid slots; proximity uses typed ZoneId access and a reused snapshot; AudioScaler mute logs respect debugLogs. | Runtime regression checks pending. Zombie Crawl renderer/slot binding, broader logging cleanup, and profiling remain. |
| README | Project layout and seven pre-merge checks are documented. | Follow the [README checklist](../README.md#checks-before-merging), including headset audio checks; source review is not runtime validation. |
| R01-R03, R05-R10, scene extraction, Zombie Crawl | Planned fixes or review findings; not implemented by this PR. | Use the acceptance checks below. Current card count and monster targeting are unchanged by the reliability commit. |
| Documentation workflow | Root AGENTS.md and maintained Markdown documents added by this documentation change. | Validated locally on 2026-09-09: source text/table coverage, all 47 source hyperlink targets, 21 relative links and anchors, four byte-identical images, and whitespace. Source editions are identified above; no gameplay behavior changes in the documentation commit. |

**Checks completed:** source-level review of the prepared changes, bundle verification, and `git diff --check`. **Pending:** Unity 2022.3.55f1 compilation, Play Mode, Photon sessions, audio playback, and headset testing. The PR is currently marked ready for review; Unity and headset checks remain pending and should be completed before merging.

## Scene split checkpoint — level1split, September 9, 2026

- **Implemented asset:** `Assets/Scenes/Level1_Containment.unity` and its `.meta` file exist at `ac69718`. Greg reports the new scene and doors are committed. The actual filename supersedes the earlier planned `Level01_Containment` spelling.
- **Implemented configuration:** this follow-up enables `Level1_Containment` in `ProjectSettings/EditorBuildSettings.asset`, using GUID `1a67d7f7a6e6dc64094eff3267980158` from its metadata. Bootstrap, Loading, and Hub_Base retain enabled build indices 0–2; containment is index 3. Existing disabled prototypes and XR configuration are preserved.
- **Confirmed correction:** all Return to Hub actions use `HubReturnSpawn` in front of the hub computer. The hallway/gate return-spawn recommendation is superseded.
- **Pending validation:** inspect scene extraction and door bindings, place or verify the computer return spawn, confirm the Build Settings list in Unity, and test guarded local travel, floor collision, cleanup, avatar/voice filtering, and monster controller handover. No Unity, Photon, or headset tests were run for this configuration/documentation update.

## Baseline review and remaining work

The findings below describe the original review baseline unless a current-status note says otherwise. Preserve that distinction when updating a finding; do not mark an entire area complete because one local defect was fixed.


Begin with the existing Hub_Base map: separate the hub and shop from Primate Containment at the Security Gate. Then integrate Zombie Crawl using the current monster systems and prove a reliable two-player Level 1 with personal objectives, capture recovery, and individual travel.

This review traces source code and serialized Unity scene and prefab references against the agreed game design. The repository contains 77 scripts under Assets/Scripts plus the customized PhotonVR framework. The original review changed no game files. Compilation, Unity Play Mode, cloud configuration, and headset performance were not tested during that review. Later code changes and their pending checks are tracked above.

| Baseline | Observed at this commit |
| --- | --- |
| Editor | Unity 2022.3.55f1 is confirmed for Runaway Chimps. Unity 6 belongs to the separate non-horror project Cheeky Chimps. |
| Enabled build scenes | Bootstrap, Loading, Hub_Base. Disabled scenes are earlier iterations. Level1 and Level1_2_Hall are unused and are not planned destinations. |
| Current playable layout | Hub_Base is the canonical source and contains both hub and Level 1. Create the new containment scene from this content. |
| Networking | Photon PUN with persistent local rig and network avatars. Zone properties exist; full sector isolation and monster replication are incomplete. |

### Highest priorities

- P1: synchronize the shared monster and implement the personal keycard lifecycle actually used by the active scene.

- P1: repair patrol and safe-room behavior, audio gating, and capture recovery before tuning scares.

- P1: complete one scene-travel lifecycle with sector visibility, safe activation, cleanup, and return.

- P2: recover cleanly from connection failures, reconcile the room limit, and simplify obsolete scripts and scene variants.

P1 means a core gameplay or multiplayer blocker. P2 means reliability or maintenance work. Release requirements apply before public account/economy rollout. Findings identify code/configuration evidence; their exact in-headset symptoms still require the [acceptance checks below](#acceptance-checks-and-remaining-choices).

Evidence: [Editor version](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/ProjectSettings/ProjectVersion.txt#L1)  |  [Build scene list](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/ProjectSettings/EditorBuildSettings.asset#L8)

## Active wiring and the shared monster

### R01 The Security Gate transition is unfinished

P1 / unfinished feature with a missing event target. Hub_Base contains a PhysicalButton event named ActivateAndOpen, but its serialized target is fileID 0. It cannot invoke the intended action. Greg confirmed that disabled scene variants are experiments; their exclusion from the build is intentional. Hub_Base is the map to split.

Fix: keep the starting area, shop, and gate approach in Hub_Base. Extract Primate Containment into a new Level1_Containment scene and wire the gate to that destination. Use the [scene extraction procedure](#editor-extraction-procedure). Level1 and Level1_2_Hall stay outside the runtime route; copying an old prototype route would restore unfinished assumptions.

Acceptance: launch from Bootstrap in a build, press the entrance once, and reach exactly one Level 1 environment with a working return route. Audit persistent button events for missing targets as part of the build check.

Evidence: [Missing button target](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L397922)  |  [Alternate door wiring](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base_1.unity#L535499)  |  [Enabled scenes](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/ProjectSettings/EditorBuildSettings.asset#L8)

### R02 The active monster is not synchronized

P1 / confirmed wiring and code gap. MonsterNavigation lets only PhotonNetwork.IsMasterClient drive the NavMeshAgent; every other client disables its agent. The active MiniGamesKidFirstRig root has no PhotonView or transform synchronization component, and the controller contains no position/state broadcast. Its other root behaviours are rigging, proximity, and material components. Other clients therefore have no implemented path for following the moving monster.

Fix first in the current Level 1: give the monster a stable network identity and synchronize position, rotation, patrol/chase state, and target actor. One controller drives navigation; other clients display the received state. Capture must refer to that same shared monster.

For independent scenes, select a controller among players who have the monster’s sector loaded. Transfer control and the last valid state when that player leaves, changes sector, or suspends the headset. Simply adding a PhotonView while retaining the global IsMasterClient check will still fail when the room master is elsewhere.

Acceptance: two players observe the same chase, then the controller leaves. The remaining player gets one continuing monster, without a reset, duplicate, or frozen agent.

Evidence: [Movement authority](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/MonsterScripts/MonsterNavigation.cs#L49)  |  [Monster components](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L815738)  |  [Photon handover guidance](https://doc.photonengine.com/pun/current/gameplay/hostmigration)

## Crawler behavior and sound

### R03 Target selection and patrol return differ from the design

P1 / confirmed code behavior. Every detection interval replaces currentTarget with FindClosestPlayer. Hub_Base sets that interval to 0.2 seconds, so the monster can switch targets five times a second. It filters tagged bodies by distance, without checking whether they are in a safe room, another sector, or a transition.

The scene enables useVentGraph, but no serialized VentGraph component or custom runtime creation was found. The script silently falls back to straight-line distance. Even a graph would not itself establish safe-room eligibility. Wander selects a random assigned point; it is not an ordered patrol route.

On target loss, the controller only calls Wander after remainingDistance falls below 0.5. Until then it can keep its previous chase destination and speed; an unreachable safe-room destination can also delay recovery. This does not implement the immediate return to patrol we agreed on.

Fix: explicitly track Patrol and Chase. When the target becomes ineligible or is lost, clear the chase destination, restore patrol speed, and choose the patrol destination immediately. Filter targets by sector and danger volume. Decide whether to retain the current eligible target or keep closest-player switching; retaining the target is the proposed starting rule, not yet a confirmed choice.

Use the existing navigation approach for the prototype. If vent-path distance is wanted, attach and validate a graph; otherwise expose straight-line range honestly and tune it. Restrict the monster’s navigable surface to vents, independently of player detection.

Evidence: [Target and patrol logic](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/MonsterScripts/MonsterNavigation.cs#L49)  |  [Distance filter](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/MonsterScripts/MonsterNavigation.cs#L88)  |  [Configured values](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L815795)

### R04 Patrol and hunt audio have broken gates

**Current status:** partially implemented in `d4ee616`; Unity and headset validation pending. The paragraphs below record the original defects. The local stop, zone lookup, and proximity-reset fixes do not establish shared monster synchronization or complete audio ownership.

P1 / confirmed code defects. AudioScaler.StopIfPlaying contains a commented-out source.Stop call, so it does not stop anything. Both active audio scalers enable onlyWhenPlayerInVent. PlayerVentState requires a parent PhotonView to initialize its Local reference, but its Bootstrap placement is on the separate local GorillaPlayer rig, without that ancestor.

MonsterActivationGate responds to the local player’s zone, while the scene’s aiScripts array is empty. ProximityManager skips reactors in other zones without delivering an exit update. These overlapping rules can leave stale state or silence cues; they should not determine whether the shared monster is simulated for other players.

Fix: establish one reliable local vent/safe-room state and one audio owner. Explicitly stop or fade the previous loop on state change and sector exit. Drive patrol/hunt selection from the shared monster state. Keep simulation eligibility separate from local sound audibility.

Evidence: [Empty stop method](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Audio/AudioScaler.cs#L125)  |  [Local binding](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/PlayerScripts/PlayerVentState.cs#L10)  |  [Vent-state placement](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Bootstrap.unity#L2068)  |  [Skipped reactor updates](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/ProxManager/ProximityManager.cs#L111)

## Personal cards and capture recovery

### R05 The active keycard path does not implement the agreed lifecycle

P1 / personal lifecycle and exit feature gap. The active keybox requires two cards. That count is a design option Greg is willing to keep, rather than a defect by itself. KeyCard1.cs inserts a physical card, increments KeyBox, and destroys the card. KeyBox opens SlidingDoor; the personal fade and Level 2 transition are missing.

A separate VRKeyCard implementation in KeyCard.cs destroys the object immediately when grabbed and records an inventory ID. PlayerInventory can recreate those cards on capture. Neither script has a serialized scene/prefab reference in this checkout, and no runtime component creation was found. The active capture script therefore calls an inventory service that is not wired in this baseline. This differs from the drop-on-capture behavior reported during local testing.

Fix: use one personal card component and one Level 1 visit state. Keep the card physically holdable. Track its fixed original spawn separately from its current position and whether it is held, dropped, or consumed. Prototype one card, then judge difficulty with the working Crawler. A second card needs a distinct route and story purpose; two pickups in the same room add little. Remove the old visit objects after personal completion.

- Capture: release the held card at the capture position; only its owner sees and recovers it.

- Out of bounds: restore the fixed key-room spawn. RespawnToOriginalSpawn currently records its own Awake position, so a recreated dropped card would otherwise treat the drop point as its original spawn.

- Leave and re-enter: clear the previous visit and create one card at the fixed spot. Another player’s card and completion are unaffected.

Other players seeing a held card remains optional. If added, use a visual representation that disappears on drop; do not turn the personal objective into a shared pickup.

Evidence: [Two-card keybox](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L808062)  |  [Active insertion path](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/KeyCard1.cs#L7)  |  [Alternative pickup path](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/KeyCard.cs#L21)  |  [Recorded spawn origin](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Utils/RespawnToOriginalSpawn.cs#L23)

### R06 Capture needs one guarded rig transition

P1 / confirmed implementation risks. TeleportGorillaPlayerPhotonVR starts a coroutine on each eligible trigger entry without a re-entry guard. It captures the drop position after the fade, leaves locomotion Update running, changes physics flags, and does not reset GorillaLocomotion.Player’s stored hand/head positions or velocity history. Early failures after fading can leave the view black.

Fix: capture once, record the hit position immediately, release the held card there, and use one persistent headset-tested fade/teleport service. Suspend locomotion while moving the actual XR rig, reset its cached positions and velocity state, and restore previous physics/input flags on success or failure. Share this safe movement primitive with level travel, while keeping capture and successful completion as different actions.

Evidence: [Capture coroutine](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/TeleportGorillaPlayerPhotonVR.cs#L22)  |  [Locomotion caches](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/NewGorillaLocomotionScripts/Player.cs#L67)  |  [Velocity history](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/NewGorillaLocomotionScripts/Player.cs#L378)

## Scene travel and sector isolation

### R07 Loading needs a full enter and leave lifecycle

P1 / feature gap with a concrete preload hazard. LevelStreamService only adds scenes. It has no unload operation, no state invalidation after an external unload, and no change of the active scene. LoadingFlow unloads only Loading. The current services therefore cannot keep each client limited to the intended environment as players visit more sectors.

Preload holds allowSceneActivation at false. Unity documents that a pending operation in this state stalls later asynchronous operations, including unloads. DoorOpenWhenReady is configured in the alternate hub to disable its blocker at 0.9, before scene activation and destination colliders are ready. This is a hazard in that unfinished route, not a confirmed cause of the earlier reported floor fall.

Fix: serialize travel requests. Fade and suspend movement, load and activate the destination, verify its spawn and collision surface, place the rig, set the active scene and sector, complete the old visit cleanup, then unload the old environment and restore movement. Resolve any held preload before queueing later operations. If loading fails, keep or restore the old safe environment and offer retry.

Keep Bootstrap services and the XR rig persistent. Treat LevelStreamService as the loading mechanism under one travel coordinator. The hallway may preload one destination, but it should not unlock passage until the destination is safe to enter. Setting the active scene is separate from activating newly loaded objects.

Evidence: [Loader lifecycle](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Streaming/LevelStreamService.cs#L35)  |  [Unlock on preload](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/MotionScripts/DoorOpenWhenReady.cs#L70)  |  [Unity activation queue behavior](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AsyncOperation-allowSceneActivation.html)

### R08 Zone properties do not yet isolate players or voice

P1 before scene splitting / feature gap. ZoneStateService publishes zone properties and exposes change events. The custom player code does not consume remote zone changes to hide avatars, names, interaction colliders, or speakers. PhotonVRPlayer is persistent and follows tracking transforms; the player/voice prefabs use group 0. There is no custom sector voice-switch implementation.

Fix: distinguish the loaded sector from a local danger subzone. The cage room and vents belong to Level 1 even though only the vents permit Crawler targeting. Show and interact with remote avatars only in the same sector; filter voice by the chosen sector rule. Re-evaluate on local travel, remote property changes, avatar creation, and late arrival.

On leaving or joining a Photon room, clear old actor-zone caches and establish fresh visit state. Sector travel must keep the same Photon room and slot; explicit Join Public or Join Code remains a separate social action. Deprecate the old PhotonVRManager.SwitchScenes helpers, which load a scene and invoke new-room matchmaking.

Acceptance: you exit Level 1 and Billy sees you disappear while his card stays usable. You remain in the same ten-player session; returning restores visibility and appropriate voice.

Evidence: [Zone storage and callbacks](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Zones/ZoneStateService.cs#L39)  |  [Persistent avatars](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/Player/PhotonVRPlayer.cs#L29)  |  [Legacy scene switching](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/PhotonVRManager.cs#L435)

## Startup session reliability and release preparation

### R09 Failures can leave startup waiting indefinitely

P2 / confirmed code gap. LoadingFlow waits for AppState.IsReady without timeout or failure handling. AuthOrchestrator sets _hasRun and _isRunning before authentication and never clears them on failure, preventing a normal retry. PlayerSpawner waits indefinitely for Hub_Base, and its coroutine is not cancelled when a join attempt ends.

RoomSwitchService does not handle disconnection or room-creation failure, and it clears pending intent before an operation succeeds. Connecting from an initially disconnected state also needs one owner of the join request, so automatic public matchmaking and a pending private join cannot compete.

Fix: explicit connection/loading states with cancellation, bounded waits, user-visible failure, and retry. Associate spawn work with the current room/attempt and cancel it on leave. Let one coordinator decide which room request executes. Mark visuals ready for the local player only; PlayerVisualReadyReporter currently runs on every network avatar.

Evidence: [Unbounded ready wait](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Bootstrap/LoadingFlow.cs#L36)  |  [Run flags](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Auth/AuthOrchestrator.cs#L54)  |  [Spawn wait](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/Player/PlayerSpawner.cs#L27)  |  [Join lifecycle](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Rooms/RoomSwitchService.cs#L56)

### R10 Production identity requires a verified login path

Release requirement / confirmed client-side gap. Bootstrap sets enforceQuestAuth to false. Even when Quest auth is enabled, the provider obtains UserProof but AuthOrchestrator uses only the Meta user ID to build a CustomID login. The proof is not consumed by the login path. BuildConfig.EnforcePlatformAuth is not referenced. Photon Connect also clears AuthValues; PlayFab identity is not currently bound through that path.

Fix before public account/economy rollout: keep development login explicit, use a supported platform proof validation and account-linking flow for release, and bind Photon identity if the game relies on it. Existing development accounts need a migration decision. Review the server implementation of GrantLoginCoconuts separately for trusted identity and repeat-grant rules; that backend is outside this checkout. Cosmetics ownership enforcement and inventory pagination also need completion before a larger shop.

Evidence: [Collected proof](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Auth/QuestMetaAuthProvider.cs#L42)  |  [CustomID login](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Auth/AuthOrchestrator.cs#L66)  |  [Unused build flag](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/BuildConfig.cs#L1)  |  [PlayFab login guidance](https://learn.microsoft.com/en-us/xbox/playfab/identity/player-identity/login/login-basics-best-practices)

### R11 Room capacity

**Current status:** the default is ten on `codex/level1-reliability` in `d4ee616`; new-room validation remains pending. The following paragraph describes the original sixteen-player baseline.

P2 / confirmed design mismatch. PhotonVRManager.DefaultRoomLimit is 16. No override to ten was found in the manager prefab or Bootstrap instance; RoomSwitchService uses the manager default. Public matching also filters by requested maximum players.

Fix: establish one ten-player session setting for public creation, private creation, and matchmaking. Test with new rooms; an already existing room is not resized merely by changing the default. Keep capacity global across all sectors and communicate full-room or version-mismatch failures at the terminal.

Evidence: [Capacity default](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/PhotonVRManager.cs#L49)  |  [Public matchmaking](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/PhotonVRManager.cs#L450)  |  [Room limit selection](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Rooms/RoomSwitchService.cs#L96)

## Crawler integration and focused cleanup

### Preserve the monster systems while replacing its visual rig

The active monster is Level1Root/MiniGamesKidFirstRig. Its root carries navigation, capture collision, rigging, proximity, and material effects, with a 180-degree forward offset. Zombie Crawl is a separate disabled object in Hub_Base with its controller assigned and Apply Root Motion off. Its animation still needs validation in Unity.

- Preserve the existing gameplay root and its links as a starting point; adapt it into one reusable Crawler prefab. Put Zombie Crawl beneath a visual child, with independent scale, orientation, and floor-contact offsets.

- Keep MonsterNavigation and its 30 patrol points, patrol/hunt audio, ProximityReactor callbacks, and capture-to-RespawnPoint link. Repair the issues in R02-R06 as these systems are retained.

- Reassign MonsterActivationGate to the Zombie Animator explicitly. The old MaterialSwapper components target renderer slot 2; inspect the new material layout before rebinding. Disable old skeleton-specific IK and rig constraints until deliberately retargeted.

- Let navigation move the root; drive crawl playback from actual speed and pause or idle when stationary. Tune body height and hand contact before speed, then corners and capture reach. Move the 180-degree mesh correction to the visual child without applying it twice.

- Test the narrowest straight, a 90-degree turn, a junction, and both room entrances. Fit the body as well as the agent; a round agent can fit where a long crawling body clips. Small corner edits are preferable to adding complex body bending for this prototype.

Evidence: [Current monster root](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L815738)  |  [Disabled Zombie Crawl](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L852780)  |  [Animation following navigation](https://docs.unity3d.com/Packages/com.unity.ai.navigation@1.1/manual/MixingComponents.html)

### Cleanup that supports the next milestone

Scene files are large: Hub_Base is approximately 41.0 MiB, Hub_Base_1 is 28.9 MiB, Hub_OG is 38.7 MiB, and Level1 is 88.4 MiB. These are serialized file sizes, not measured headset memory. Extract the level from Hub_Base, then consider reusable cage, vent, door, and room prefabs. Keep old scene iterations outside the build; archive them once the new split is verified.

Rename mismatched component filenames while preserving .meta GUIDs and validating Unity bindings: KeyCard/KeyCard1, DisableCollisionWhileHeld/HeldItemCollisionMode, AntiHandPhasing/AntiHandPhase, FloorMaterialRegion/RandomTileRegion, and the alternate capture script. Check capitalization for ComputerTerminalUi and loadingDebugText. Consolidate the unused card/inventory and teleport alternatives after the replacement path is verified.

Original cleanup recommendation: gate verbose ZoneTrigger.OnTriggerStay and active AudioScaler debug logs; replace repeated reflection with direct zone access and reuse the proximity snapshot collection. The local AudioScaler logging guard, typed zone access, and snapshot reuse are implemented in `d4ee616`; broader logging cleanup and profiling remain. Keep Photon/PlayFab SDK code distinct from your customized PhotonVR framework; preserve Resources prefab paths used for network spawning.

The reliability branch adds a root README with the editor version, start scene, current layout, and two-client local-audio checks. Expand package/service documentation and the full two-player setup as the corresponding systems are implemented. Investigate the tracked TempAssembly.dll as a possible generated artifact. Profile CPU, GPU, physics, memory, and transitions on the target Quest before simplifying materials or meshes based on guesses.

## Split Hub_Base at the Security Gate

Source: Hub_Base, with world positions preserved during extraction. Destination: Level1_Containment, now committed on level1split at ac69718. The following procedure remains the extraction verification checklist; committed files alone do not establish that every step is complete. The Security Gate separates the social area from the safe containment arrival; the locked objective door remains inside the level and serves completion.

| Placement | Existing objects and required handling |
| --- | --- |
| Keep in Hub_Base | SpawnRoom and HubSpawn, Computer, Shop, the approach hallway, and StartLevel1Button with its hub-side gate. Leave unrelated ForestMap and ChillRoom content in place during this focused split. |
| Move to containment | Level1Root: SmallRoom, Monkey Cages, VentSystem, KeyCards, KeyBox, RespawnPoint, NavMeshRoot, level lighting and triggers, and MiniGamesKidFirstRig with its linked children. |
| Review at the seam | Level1Root/Floors, Walls, and Ceiling may include approach geometry. Separate hub-side pieces first; divide any continuous mesh and its collider at the chosen gate threshold. |
| Handle separately | Zombie Crawl is outside Level1Root and inactive. Move it with the level for integration. Level1Root/XR Interaction Manager must not be the only manager for a persistent rig or hub interactions after unloading. |

### Editor extraction procedure

1. Create an isolated branch and save the existing scene. Record the gate threshold in the Scene view; verify which floor, wall, ceiling, and collider objects cross it.

1. Open a new empty Level1_Containment additively beside Hub_Base. Regroup geometry to match the gate boundary, then move the level root into the new scene without resetting its transform. Move the inactive Zombie Crawl separately.

1. Keep asset references shared: materials, audio clips, meshes, and animation controllers remain project assets. Use Unity to move the scene objects and preserve their internal bindings, then audit the result; do not duplicate the whole asset collection.

1. Give each scene a complete floor and doorway at its end. Add ContainmentEntrySpawn in the safe containment arrival room and HubReturnSpawn in front of the hub computer. All Return to Hub actions use the computer return spawn. Each side owns its own trigger and collision surface; communicate travel by a destination ID instead of a direct cross-scene Transform.

1. Audit patrol points, both audio callbacks, capture and respawn references, objective bindings, and triggers. Establish one intended persistent XR Interaction Manager for persistent interactions and rebind any explicit references.

1. Rebake containment navigation with the intended vent surfaces and agent type. Check lighting, probes, reflection settings, and occlusion data for independent loading. Keep each mesh section and its collider together.

1. Save both scenes, register only the new destination alongside the existing startup scenes, and compare geometry with both scenes open. Then verify each environment alone, followed by the guarded runtime travel and multiplayer checks.

The extraction checkpoint is two correctly separated environments with intact local references. Runtime readiness additionally requires R02 and R07-R08: a monster controller present in the sector, safe load/unload, and remote avatar and voice filtering. Geometry separation alone does not complete those systems.

Evidence: [Level1Root](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L850036)  |  [Separate Zombie Crawl](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L852780)  |  [Unity multi-scene editing](https://docs.unity3d.com/2022.3/Documentation/Manual/setupmultiplescenes.html)

## Implementation order

Use small reviewable changes, each ending with a playable checkpoint. Effort labels are relative: Small is localized; Medium crosses several scripts or Inspector bindings; Large crosses network ownership or scene lifecycle. They are not calendar estimates.

| Batch | Work and main files | Exit condition |
| --- | --- | --- |
| 1  Map extraction Medium | Use Unity 2022.3.55f1. Split Hub_Base at the Security Gate using the [extraction procedure](#editor-extraction-procedure). Set the ten-player default and retain the existing Bootstrap startup. | Hub and containment geometry are separate and complete; local bindings survive. The new route stays controlled until travel is ready. |
| 2  Crawler and chase Large | Integrate Zombie Crawl using the [Crawler integration guidance](#preserve-the-monster-systems-while-replacing-its-visual-rig). R02-R04: navigation, audio, zone eligibility, one network controller, and state handover. | The Crawler fits the vents on both clients. Safe rooms end pursuit, patrol and sound recover, and controller departure is handled. |
| 3  Personal attempt Medium | R05-R06: one card/visit state, one exit action, one guarded capture/rig transition. Replace KeyCard1/KeyBox path and wire fixed spawn recovery. | Each player can fetch, drop, recover, and use their own card. Capture and success leave the other player unchanged. |
| 4  Sector travel Large | R01/R07/R08: canonical scene split, travel coordinator, LevelStreamService lifecycle, zone/voice presentation, session reset, destination spawn. | Hub to Level 1 to a placeholder Level 2 safe room and back works in one room. Old environments and visit objects are cleaned up. |
| 5  Resilience and tuning Medium | R09: startup retry and cancellation. Tune one-card completion, capture, return travel, and Quest performance before deciding on a second objective location. | Connection failures recover. New players can understand and complete the route; measured performance and difficulty guide changes. |
| 6  Release preparation Medium to Large | R10: platform login, account migration, inventory/ownership checks, and backend review before public account and economy rollout. | Release identity is validated and server-side rewards and ownership rules have been reviewed. |

### Recommended first implementation session

Start in Hub_Base and identify the exact geometry at the Security Gate. Extract the level while preserving world positions and local references. Use that stable map split for the Zombie Crawl work; compare before and after behavior so existing audio, capture, and patrol features remain accounted for.

Keep the Listener, shop expansion, camera wall, and additional levels on the design backlog until the Level 1 checkpoint is dependable. The placeholder Level 2 arrival room is sufficient to prove personal completion and regrouping without building the Listener level yet.

## Acceptance checks and remaining choices

| Check | Required result |
| --- | --- |
| Same-sector chase | Both clients see matching monster position and chase state. Either player can become an eligible target. |
| Safe-room escape | Enter either safe room: that player stops being a target. With no other eligible target, the Crawler immediately resumes patrol. |
| Controller change | Controller leaves the level, disconnects, or suspends the headset. Remaining players keep one functioning monster. |
| Card independence | Both players retrieve cards. One drops or uses theirs; the other player retains their own state. |
| Capture and floor recovery | Repeated trigger contact starts one capture. Card drops at the recorded hit point; out-of-bounds returns it to the fixed room spawn. |
| Exit and return | Winner fades and arrives in the next safe room. Follower sees them disappear and cannot follow without completing. Re-entry creates exactly one fresh card. |
| Travel and visibility | Return to Hub arrives in front of the computer. Same room code and actor membership across sectors. No cross-sector avatar collision or unwanted voice; arrival and return refresh visibility. |
| Capacity and failure | Ten players remain ten even when split across levels. An eleventh cannot join that room. Failed login, join, or scene load offers recovery. |
| Quest stability | No stale locomotion impulse, stuck fade, missing floor, duplicate rig, or accumulated level objects after repeated travel. Record frame-time and memory measurements. |

### Targeted automated checks to add

Add small tests for the personal visit lifecycle and eligible-target transitions, plus an Editor validation check for enabled destination scenes, spawn references, required monster components, and non-null persistent button targets. Use two-client and headset checks for network synchronization, fades, collision, voice, and performance; source inspection cannot establish those outcomes.

### Choices still needed from Greg

- Difficulty: prototype one personal card at the existing spot. Retain two only if playtests justify a second route and a clear containment-release purpose.

- Crawler targeting: keep its current target until escape/capture, or deliberately allow closest-player switching? Should patrol visit assigned points in order or randomly?

- Presentation: should other players see a held keycard? What exact scare should precede respawn? These choices do not block the core personal-card work.

The recommendations preserve the agreed direction: open level access, individual wins, no saved partial-level progress, personal card resets on re-entry, a Crawler restricted to vents, and one ten-player Photon room across independently loaded sectors.
