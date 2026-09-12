# Runaway Chimps repository improvement plan

Last updated: 2026-09-11. Maintained repository edition, migrated from `Runaway_Chimps_Repository_Review_and_Plan.docx`, Revision 2. The original review examined `main` at `4f6894141aa2b132d744cb2423c1ee227e0fc5be`; its evidence links remain pinned to that baseline. The Word document is a historical downloadable snapshot.

Read the [design and lore](design-and-lore.md) for intended behavior and [AGENTS.md](../AGENTS.md) for update rules. A confirmed finding describes source evidence; it does not mean its fix is implemented or tested.

## September 11 Photon player T-pose regression

**Confirmed regression cause:** this was not a Photon/XR tracking failure. In Play Mode Greg confirmed the expected `PhotonVRRigBinder` bound log and verified that the spawned Photon player's top-level `Head`, `LeftHand`, and `RightHand` transforms follow the headset/controllers while the rendered monkey remains T-posed. Repository history then isolated the visual-rig regression to the September 9 player-prefab edit in commit `dfa3adf`: both `FastIKFabric` hand targets and elbow poles were cleared, both hand-animation `PhotonView` references were cleared, and the right `XRHandController` was serialized as `Left`. The earlier runtime manager-binding hardening on `codex/fix-player-rig-tpose` was useful resilience work but was insufficient because the tracked transforms were already moving correctly.

**Implemented on `codex/fix-player-rig-tpose`:** `FastIKFabric` now first recovers a missing visual-arm target from the owning `PhotonVRPlayer` (`LeftHand` for `XRHandL`, `RightHand` for `XRHandController`) instead of silently creating a stationary fallback target; the right visual-hand side is also restored to `Right` during that recovery. `PlayerRigPrefabRepair` runs in the Editor and restores the exact known-good historical prefab wiring for left/right targets, left/right elbow poles, both hand `PhotonView` references, and the right-hand side setting. `ReliabilityRegressionChecks` now invokes that repair and asserts the restored target/pole/view/hand-side bindings. The existing independent tracked-pose copying and self-healing `PhotonVRRigBinder` changes remain in place.

**Source validation completed:** repository history confirms the old target, pole, view, and hand-side values and the exact September 9 diff that cleared/reset them. The new runtime path no longer depends on a serialized IK target being present to move the arms. **Pending validation:** pull the latest branch into Unity 2022.3.55f1; allow scripts/assets to recompile; confirm `[PlayerRigPrefabRepair] Restored Photon player...` once if the prefab still needs repair; run `Tools > Runaway Chimps > Run Reliability Regression Checks`; then test Play Mode/headset arm following, elbow bend direction, both hand/finger inputs, scene travel/re-entry, and a two-client Photon session so the remote avatar follows the correct hands. Do not mark the T-pose resolved until those runtime checks pass.

## September 12 Level 2 player-scale interaction correction

**Confirmed runtime correction:** keep the approved 36 × 36 m room layout, but scale the fuse objective hardware to the gorilla player instead of to the oversized environment/Listener. Greg tested the merged Level 2 in Unity and reported that the fuses, socket locations and charging handles were dramatically oversized and physically unreachable. Repository inspection of the active Bootstrap rig shows ~10 cm hand-contact spheres, a ~0.36 m-wide / 1.16 m-high body capsule, and a 1.5 m max arm length.

**Implemented on `fix/level2-player-scale-interactions`:** the central power island is reduced from 4.4 × 2.4 × 2.4 m to approximately 2.4 × 0.95 × 1.3 m while staying centered in the Repair Lab. Socket centers move from 1.28 m to ~0.58 m, charge handles from 1.55 m to ~0.70 m, the physical fuse prototype from ~42 cm long / 13–15 cm diameter to ~20 cm long / 5–7 cm diameter, and socket/lever interaction triggers are resized for the ~10 cm hand proxy. Fuse search cabinets and drawer travel are also reduced to player-scale. Room bounds, Repair Lab size, two escape routes, Listener clearance targets and the four-fuse objective are unchanged.

**Validation status:** generated-source/box-route validation and explicit player-scale geometry checks pass on the branch. Unity 2022.3.55f1 Play Mode/headset validation is still required for comfortable reach, grab orientation, insertion, lever hold and drawer interaction.

## September 11 Level 2 size correction and v0.4 design

**Confirmed design correction:** the current Level 2 v0.3 blockout is too small. Its implemented footprint is 18 × 18 m; the replacement must be at least twice that size in both horizontal dimensions, making **36 × 36 m the minimum v0.4 target**. This supersedes the earlier design instruction to keep the Listener level small. Preserve the confirmed four-fuse power-restoration objective, two distinct Repair Lab escape routes, safe entry and qualified safe exit, the bottom-right reward-room relationship, the established gate language, and the RETURN TO SECURITY control.

**Current implementation evidence:** PR #7 merged the 36 × 36 m v0.4 blockout and fuse prototype into `main`. The retained generator regenerates `Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity`, its reusable prefab and `Tools/Level2Blockout/layout.json` as a **36 × 36 m v0.4 review blockout**. It preserves `Level2EntrySpawn`, RETURN TO SECURITY and the existing sector/travel bindings while adding Test Hall A, Lower Service Hall, West Observation, Conditioning Hall B, East Bypass, North Gallery and a larger Repair Lab. Greg has now **confirmed the current v0.4 room sizing/proportions** shown by these bounds. Obstacle placements, the 5 m ceiling judgment, provisional repair-frame scaling and final Listener navigation clearances remain subject to Unity/runtime tuning.

**Implemented on the review branch:** authored geometry was rebuilt rather than scaling the v0.3 root. `Tools/Level2Blockout/build_level2.py`, `layout.json`, the linked-gate placement, reusable prefab and standalone scene are synchronized for v0.4. The safe-entry travel/sector components and RETURN TO SECURITY control are preserved. Navigation and Listener runtime behavior remain pending until the physical layout is approved.

**Source validation completed on v0.4:** generated scene/prefab IDs and local references resolve; five linked gate instances and required travel bindings remain present; a 0.35 m-radius / 2.0 m-height player proxy and conservative 1.375 m-radius / 3.2 m-height Listener proxy reach the gameplay spaces; either repair doorway can be blocked while the other route still reaches the Repair Lab; temporary exit/reward blockers isolate their spaces, and removing the reward blocker reconnects the player route. **Pending validation:** Unity 2022.3.55f1 import/compile; both Runaway Chimps Editor validators; actual framed-gate clearance; Gorilla hand/body collision; Listener animated turning/reach; NavMesh; safe-zone enforcement; travel markers; two-client Photon behavior; warning/audio pacing; Quest frame-time and memory.

## September 11 Level 2 four-fuse power objective

**Confirmed design correction:** replace the hammer/strike repair with four personal hand-held cylindrical power fuses. Fixed fuse-search containers are distributed across the enlarged level; opening a container and dropping a fuse are short audible events. Each fuse returns to a central four-slot power-distribution island in the Repair Lab. A nearby lever charges each inserted fuse while producing sustained noise that strongly attracts the Listener. A visible conduit leads from the qualified exit to the island, and the fourth completed charge powers the exit for the final run.

**Confirmed personal visit behavior:** fuse/socket completion belongs to the individual player. Friends can distract the shared Listener but cannot satisfy another player's requirement. Capture drops a carried fuse for its owner; installed/charged fuses persist through capture during that visit. Leaving Level 2 ends the visit and restores the four fuses to their fixed search containers. Exact charge duration, sound radius and final container dressing are tuning values rather than fixed implementation constants.

**Implemented on `design/level2-expanded-map-v04` for review:** the Repair Lab is deepened to approximately 16 × 10 m; the old hammer bench is removed; a central island, four socket/indicator/lever placeholders, a visible exit conduit and four fuse-cache/fuse-start markers are generated into the scene/prefab. The branch now also contains the first functional fuse prototype foundation: runtime-built low-poly grabbable fuse bodies using `XRGrabInteractable` and `HeldItemCollisionMode`, physical socket snapping, resumable 10-second prototype lever charging, charge progress/completion events, a reusable `Level2NoiseBus` for search/fuse-impact/charging events, `Level2FuseObjective` completion tracking, and noisy-search-container hooks. `Level2FuseRuntimeBootstrap` binds those components to the current v0.4 markers without changing the confirmed room geometry. Listener response, final art/audio, capture-specific fuse release/drop handling, personal Photon presentation and powered-exit activation remain unwired.

**Fuse hardening implemented on the review branch:** the prototype now uses explicit `Level2FuseState` and `Level2SocketState` transitions; prevents duplicate socket insertion/completion; handles multiple local hand colliders on a lever without false release; recovers loose fuses that fall below the level; drives socket indicators for empty/inserted/charging/charged state; records timestamped noise events with optional Scene-view gizmo/log diagnostics; exposes `Level2PowerState` as the clean downstream power endpoint; and makes the generated search drawer respond to a local-hand trigger with constrained movement plus search noise. `Level2FuseRuntimeBootstrap` is explicitly prototype-only and should be replaced by authored prefabs after Unity/XR scale validation. Source/marker checks pass; this is not Unity runtime validation.

**Pending validation/implementation:** Unity 2022.3.55f1 import/compile, actual gate/island clearance, Gorilla pickup/hold/drop behavior, drawer/cabinet animation/interaction, personal Photon fuse visibility/state, lever ergonomics, capture-specific fuse drop behavior, Listener consumption of the new noise bus, powered-exit response, and a dedicated Listener-sized navigation bake/agent footprint that excludes spaces too tight for the creature. Patrol and chase destinations must resolve onto that walkable area; test doorway approaches, corners, bypass offsets and full circulation around the power island for stuck cases. Two-client independence/cooperation, powered-exit feedback and Quest performance also remain pending.

## September 11 Hub entrance interaction correction

**Confirmed design correction:** Hub hallway → Level 1 must use the physical `StartLevel1Button` beside the gate as the only player-facing activation. A local monkey hand/fingertip touching the button should trigger travel; Grip, Trigger, and XR Select on the gate itself are not part of this interaction. This deliberately gives headset play and non-headset editor testing the same collision-based control. The gate remains a solid barrier/visual rather than an interactable.

**Current implementation evidence:** merged PR #3 already wires `StartLevel1Button.OnPressed` to `SectorDoor.Travel` and the travel validator requires that binding. However, `SectorDoor` still adds an `XRSimpleInteractable`, listens for `selectEntered`, and creates its interaction label, so the old direct door-selection shortcut remains in source. The September 11 correction is therefore **confirmed but not yet implemented**. Level 1 → Hub return-door interaction is unchanged by this correction.

**Required implementation:** remove/disable direct XR selection and the select prompt for the Hub → Level 1 gate while preserving `StartLevel1Button`, the solid door barrier, `SectorTravelService`, fade/Loading behavior, and `Level1EntrySpawn`. Do not globally remove `SectorDoor` selection unless the Level 1 return route is separately redesigned. Update the travel validator and README route instructions with the button-only Hub behavior. Validate both (1) headset hand/fingertip press and (2) non-headset editor testing by moving the monkey hand collider into the button. Confirm one press causes one travel request and held contact cannot spam transitions.

## September 10 audit, PR #4 merge, and PR #5 reconciliation

**Current base verified:** `main` at `8657c3e` includes merged PR #3 and PR #4. PR #4 adds the Level 2 blockout/travel, personal Level 1 completion handoff, RETURN TO SECURITY control, and the GUID-preserving `KeyCard`/`VRKeyCard` filename correction. **Implemented on draft PR #5:** `codex/codebase-reliability-audit` adds the confirmed source fixes in the [full audit and remaining-findings report](codebase-audit-2026-09-10.md). Merge commit `ad1a424` reconciles those fixes with current `main` without dropping PR #4 behavior. GitHub reports PR #5 mergeable state clean.

**Validated before reconciliation:** the audit branch's 97 C# files passed syntax/binding/metadata source checks; its four enabled scenes passed build-registration and local-reference checks; whitespace passed. PR #4 separately recorded source checks for its Level 2 assets and routes. **Pending after reconciliation:** rerun the offline source validator against the combined five-scene tree, then Unity 2022.3.55f1 compilation/import, both Runaway Chimps Editor validators, Play Mode, live services, two-client Photon and headset tests. A clean GitHub merge state is not gameplay validation.

**Conflict-resolution decisions implemented in `ad1a424`:** preserve PR #4's active `KeyCard` local-pickup ownership check and both script GUID identities; combine Level 2 destination/completion travel with PR #5 reconnect/pause/in-room-respawn safeguards; combine the Hub terminal's Level 2 menu with room-switch status/error handling under the corrected `ComputerTerminalUI.cs` filename; combine Level 2 validation with missing-script/null-trigger checks; keep current main's five enabled build scenes with Level 2 exactly once.

**Still open:** the authored vent graph/NavMesh boundary, production platform-proof verification/account migration, backend reward/ownership review, Listener/fuse-power gameplay, cross-level reward-card lifecycle, Zombie Crawl visual integration, and measured Quest performance. This audit does not upgrade SDKs or claim runtime validation.

## Earlier PR #2 implementation and validation status

[PR #2](https://github.com/GregStephen/RunawayChimps/pull/2), branch `codex/level1-reliability`, was merged on 2026-09-09 at 19:24:51 UTC (merge commit `2d3d690`). Commit `d4ee616303ea7a8c6a4aa8d877a3be8086e07939` contains the initial code fixes. It has the same complete file tree as bundled source commit `1eb089fdc54ea821d0f5e3ad90c8f3def9bbe9b7`; the publishing API created a new commit ID. The documentation migration follows as a separate commit.

| Area | Implementation | Validation and remaining work |
| --- | --- | --- |
| R04 audio and proximity | Partially implemented in `d4ee616`: AudioScaler now stops excluded playback and stops on disable; PlayerVentState uses the local zone service in Photon rooms; cross-zone and disabled reactors reset effects. | Unity, Play Mode, existing patrol/chase clips, two-client local filtering, and headset checks pending. Shared monster state and a complete audio-ownership design remain open implementation work. |
| R11 capacity | DefaultRoomLimit changed to ten in `d4ee616`. No serialized override was changed. | New public/private room checks pending. Existing rooms are not resized. The subsequent `level1split` travel implementation preserves the room; capacity across sectors still requires multi-client validation. |
| Material and proximity cleanup | MaterialSwapper guards destroyed renderers and invalid slots; proximity uses typed ZoneId access and a reused snapshot; AudioScaler mute logs respect debugLogs. | Runtime regression checks pending. Zombie Crawl renderer/slot binding, broader logging cleanup, and profiling remain. |
| README | Project layout and seven pre-merge checks are documented. | Follow the [README checklist](../README.md#checks-before-merging), including headset audio checks; source review is not runtime validation. |
| R01-R03, R05-R10, scene extraction, Zombie Crawl | Planned fixes or review findings; not implemented by this PR. | Use the acceptance checks below. Current card count and monster targeting are unchanged by the reliability commit. |
| Documentation workflow | Root AGENTS.md and maintained Markdown documents added by this documentation change. | Validated locally on 2026-09-09: source text/table coverage, all 47 source hyperlink targets, 21 relative links and anchors, four byte-identical images, and whitespace. Source editions are identified above; no gameplay behavior changes in the documentation commit. |

**Checks completed:** source-level review of the prepared changes, bundle verification, and `git diff --check`. **Pending:** Unity 2022.3.55f1 compilation, Play Mode, Photon sessions, audio playback, and headset testing. The PR is merged; no Unity or headset validation evidence was added by the merge. Those checks remain pending.

## Scene split checkpoint — merged PR #3, September 9, 2026

The scene registration and subsequent travel/cleanup patch were committed on `level1split` and merged through PR #3 at `c388d87c`. The earlier upload limitation is resolved. The implementation below is present on main; its Unity and headset checks remain pending.

- **Implemented asset:** `Assets/Scenes/Level1_Containment.unity` and its `.meta` file exist at `ac69718`. Greg reports the new scene and doors are committed. The actual filename supersedes the earlier planned `Level01_Containment` spelling.
- **Implemented configuration:** this follow-up enables `Level1_Containment` in `ProjectSettings/EditorBuildSettings.asset`, using GUID `1a67d7f7a6e6dc64094eff3267980158` from its metadata. Bootstrap, Loading, and Hub_Base retain enabled build indices 0–2; containment is index 3. Existing disabled prototypes and XR configuration are preserved. PR #4 adds Level 2 once at enabled index 4.
- **Confirmed correction:** the route determines Hub arrival. Returning through the Level 1 entrance door uses `HubDoorReturnSpawn` in the hallway. Level 2 RETURN TO SECURITY uses `HubReturnSpawn` in front of the computer. Both the Hub computer and hallway entrance load `Level1EntrySpawn` in the safe cage room. This supersedes the earlier all-returns-to-computer instruction.
- **Implemented in PR #3:** a persistent `SectorTravelService` serializes requests, releases held interactions, fades the XR view, reuses Loading without resetting startup or matchmaking, loads the destination additively, validates floor/head/body clearance, moves and resets the local locomotion rig, publishes arrival, unloads the old environment, and restores movement. Timeout/connection failures restore the source before commitment; native loads that cannot be cancelled are drained and discarded. A cooldown rejects repeated presses.
- **Implemented wiring in PR #3:** the existing Hub button targets `SectorDoor.Travel` and requires a local hand; both scene entrance doors were also given XR selection with solid barriers. The September 11 correction supersedes the Hub half of that dual-input design: `StartLevel1Button` should remain the only player-facing Hub hallway activation, while the Hub gate's direct XR selection and select prompt are pending removal. The Level 1 return door remains unchanged unless separately redesigned. The Hub computer has a Level 1 menu option; keyboard buttons also require the local rig. The default Hub spawn is retained for startup; separate computer, hallway, and Level 1 entry markers are serialized. One persistent XR interaction manager replaces Level 1's scene-owned manager and rebinds arriving interactables.
- **Implemented multiplayer foundation in PR #3:** `sector` player properties identify Hub, Containment, or transition. Remote renderers/names, colliders, and speaker playback are filtered locally by sector. Voice traffic still uses the existing Photon Voice configuration; bandwidth/interest-group routing is not implemented. The lowest active actor in an occupied level controls its monster and sends position/rotation/chase state; arrivals request current state and departure/pause flushes the last pose. The master can remain in the Hub. Target eligibility requires Containment and its vent zone; closest-player switching is retained, and loss returns immediately to patrol.
- **Implemented capture integration in PR #3:** the active capture component uses the shared guarded movement service while retaining the existing inventory drop call. Releasing XRI selections and unloading Level 1 ends its local scene visit. PR #4 connects the current local keybox completion to Level 2; the broader full capture/drop/re-entry lifecycle still requires validation.
- **Validated at source level, 2026-09-09:** changed C# files parse without syntax errors; changed scenes have unique object IDs and no unresolved local references; new travel assets have metadata. This does not establish Unity compilation or asset import.
- **Pending validation:** use `Tools > Runaway Chimps > Validate Sector Travel`, then execute the [route and multiplayer checks](../README.md#scene-travel-checks) in Unity 2022.3.55f1. Confirm the button-only Hub hallway interaction after its pending correction, scene extraction, navmesh activation, floor/head/body clearance, camera-space Loading in both eyes, voice, controller handover, capture, rollback, and repeated travel/memory. PR #5 adds reconnect/pause cases that must be included.

## Additional cleanup in the combined patch — September 9, 2026

| Area | Prepared fix | Remaining validation |
| --- | --- | --- |
| Computer Color option | Calls the existing `PhotonVRManager.SetColour` path, which saves locally and publishes appearance. Reopening Hub reads the manager's current color. Removes misleading `(log)` labels and shows save feedback. | Change color, reload Hub, restart, and compare with another client. |
| R09 player spawn lifecycle | Cancels pending work on leave/disconnect/disable; checks room identity and attempt before spawning; bounds the environment wait to 30 seconds; uses the persistent head in an active sector even when Hub is unloaded. Removes the out-of-room Photon destroy call. | Leave before spawn, rapidly rejoin, reconnect from Level 1, and force a missing environment. Exactly one local avatar should spawn for the current attempt. |
| R09 readiness ownership | Only the local avatar in the same current room can mark visuals ready. Disabling it cancels its settle coroutine. | A remote avatar settling must not satisfy local startup; disconnecting during the wait must not mark readiness later. |
| Physical buttons | Releases stored press state when the pressing collider is disabled/destroyed, and resets the visual/state when the button is disabled. | Press, disable/re-enable during travel or rollback, then press again; the event fires once per new press. For the Hub entrance, also verify the same local hand/fingertip collision works with a headset and by moving the monkey hand in non-headset editor testing. |
| Trigger/proximity logs | Removes logging-only `OnTriggerStay`; makes zone enter/exit, zone-service traces, and proximity diagnostics opt-in. Warnings/errors remain visible. | Check ordinary zone/audio behavior and enable diagnostics only when needed. No measured performance claim. |
| Material swapping | Changes renderer `sharedMaterials` bindings without cloning other material slots; skips an already-applied swap. Does not modify shared Material asset properties. | Verify proximity enter/exit restores appearance and inspect material instance counts after repeated swaps. |
| Hand-impact audio | Primary probe now follows hand velocity instead of pointing away from the approached surface. A surface-specific profile works without a default profile; an empty surface profile can fall back to the default. Reuses the fallback-direction array and resets contact/velocity history during travel, capture, and disable/re-enable. | With fallback casts off, approach a wall and ceiling, then move away: sound should require an inward impact. Check a surface profile with no default, an empty override with a valid default, normal rearming, and no spurious slap during arrival/capture. |
| Computer name saving | Rejects blank input instead of silently substituting Player. Allows one pending save per terminal, checks login before calling PlayFab, handles synchronous request errors, retains failed edits, and avoids updating destroyed terminal UI from callbacks. A successful in-flight save still persists after terminal unload. | Blank input must leave the current name unchanged. Repeated Enter during a delayed save must make one request; failure retains the typed name and permits retry. Travel during a save, then confirm the saved name locally and with another client. |
| Computer color preview | Adds a colored `COLOR` label and RGB hex value to the existing Color page before Apply. It previews the pending choice; Enter still commits it. | Check RGB adjustments, reset, dark colors, text fit/readability in the headset, and that changing the preview alone does not change the avatar. |
| Held-item collision layers and binding | Renames `DisableCollisionWhileHeld.cs` and its metadata to `HeldItemCollisionMode.cs`, preserving GUID `cceaa587972a4084385f9d2ce864386e`. Snapshots each child layer once on the first grab, keeps held layers until the last hand releases, and restores on disable. | Verify the existing Level 1 component imports without a missing script. Check mixed child layers, two-handed selection/release, disable/re-enable while held, and travel while holding the item. A missing HeldItem layer must leave layers unchanged. |

These source changes are merged in PR #3, with runtime validation still pending. The September 10 audit adds startup retry and matchmaking coordination. Level 2 completion is now merged through PR #4. Backend authentication security and Zombie Crawl integration remain separate work.

The held-item implementation uses the XR Interaction Toolkit 2.6 [first-select and last-release events](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.6/api/UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable.html#UnityEngine_XR_Interaction_Toolkit_XRBaseInteractable_firstSelectEntered). Source checks and patch reconstruction do not establish runtime selection behavior. The hand-audio fixes apply to the existing two Bootstrap components; the held-item GUID is referenced once in Level1_Containment. No scene geometry or serialized gameplay settings were changed in this additional pass.

## Level 2 travel and blockout checkpoint — merged PR #4, reconciled into PR #5

**Implemented on `main` through PR #4 at `8657c3e`:** Hub computer selection and personal Level 1 keybox completion lead to Level 2's safe entry. Level 2 currently exposes one large RETURN TO SECURITY wall button, returning to `HubReturnSpawn` in front of the Hub computer; direct Level 1 selection is deferred to a code/context-menu hook. **The implemented v0.3 scene remains 18 × 18 m; that footprint is now superseded as the design target by the September 11 minimum 36 × 36 m correction.**

The Level 2 scene is `Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity`, enabled once at build index 4 after Bootstrap, Loading, Hub_Base, and Level1_Containment. It uses `SectorId.Conditioning`, the existing `ZoneId.Level2`, `Level2EntrySpawn`, and safe-entry terminal actions. The Listener, noisy-repair objective, scanner/reward persistence and final exit qualification remain separate work. The `design/level2-expanded-map-v04` branch currently changes documentation only; Unity scene/prefab geometry is still v0.3 until the proposed larger layout is approved and built.

The existing two-card Level 1 keybox count is preserved. `KeyCard1.cs` is now `KeyCard.cs`; the separate former `KeyCard.cs` is `VRKeyCard.cs`; both `.meta` GUID identities are preserved. The active card must have been selected by the local XR rig before insertion can count, duplicate contacts cannot count twice, and failed Level 2 travel leaves completed local source state available for retry. PR #5's conflict resolution intentionally keeps this stronger PR #4 behavior while retaining the audit's filename/GUID cleanup.

`ReturnToSecurityButton` filters for the local hand/fingertip, suppresses repeated held contact, applies a cooldown and small cap depression, and delegates to `LevelTerminalActions`. Scene/prefab contain editable button geometry and a runtime TextMeshPro label. No screen, keyboard or new audio is included.

PR #5 merge commit `ad1a424` combines those routes with audit safeguards: `RespawnAt` requires active room membership; reconnect stores/restores sector and zone state; pause cancels an in-flight transition after flushing state; the terminal retains Level 2 plus room-switch status/error reporting; the Editor validator covers Level 2 plus missing scripts and null triggers.

**Source evidence before reconciliation:** PR #4 recorded C# parsing, card GUID preservation, scene/prefab local-reference checks, exact Level 2 spawn/safe-entry bindings, two source card instances and box-layout checks. The audit branch separately passed its validator before PR #4 was merged. **Pending now:** rerun `Tools/validate_source.py` on the reconciled tree, then Unity compilation/import and both Editor validators. After that test Hub → Level 2, Level 1 completion → Level 2, RETURN TO SECURITY → Hub, failed-load retry, reconnect/pause, two-client independent completion, sector visibility/voice, button filtering/reach, and headset clearance/performance. When v0.4 geometry is implemented, all Level 2 geometry/navigation/clearance/performance checks must be repeated against that new scene.

The optional bottom-right reward room, linked full/small gates, scanner/sign placeholder, collectible plinth and optional currency-cache placeholder are merged v0.3 geometry only. Cross-level key ownership, persistence/capture rules, scanning, door animation, personal/follower access, reward claiming and currency behavior remain planned and unwired. V0.4 keeps the bottom-right relationship, while its exact proposed 6 × 6 m size and coordinates remain open until layout approval.

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

**Current status, 2026-09-11:** The missing gate target and scene split/travel wiring are implemented in merged PR #3, and the existing `StartLevel1Button` calls `SectorDoor.Travel`. The September 11 design correction now requires that physical button to be the only Hub hallway activation. Current `SectorDoor` source still exposes direct XR selection on the Hub gate, so removing that shortcut/prompt and validating button-only hand collision remain pending. The audit still removes duplicate build registration and checks missing scripts.

P1 / unfinished feature with a missing event target. Hub_Base contains a PhysicalButton event named ActivateAndOpen, but its serialized target is fileID 0. It cannot invoke the intended action. Greg confirmed that disabled scene variants are experiments; their exclusion from the build is intentional. Hub_Base is the map to split.

Fix: keep the starting area, shop, and gate approach in Hub_Base. Extract Primate Containment into a new Level1_Containment scene and wire the gate to that destination. Use the [scene extraction procedure](#editor-extraction-procedure). Level1 and Level1_2_Hall stay outside the runtime route; copying an old prototype route would restore unfinished assumptions. For the current Hub gate interaction, retain the physical `StartLevel1Button` and remove the direct XR-select shortcut from that side only.

Acceptance: launch from Bootstrap in a build, press the physical entrance button once with the local monkey hand, and reach exactly one Level 1 environment with a working return route. Repeat in non-headset editor testing by moving the monkey hand collider into the button. The Hub gate itself must not show or accept an XR Select/Grip interaction. Audit persistent button events for missing targets as part of the build check.

Evidence: [Missing button target](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L397922)  |  [Alternate door wiring](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base_1.unity#L535499)  |  [Enabled scenes](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/ProjectSettings/EditorBuildSettings.asset#L8)

### R02 The active monster is not synchronized

**Current status, 2026-09-10:** Merged PR #3 adds sector controller election and pose/chase replication. This audit clears state on room changes and restores navigation after disable/rollback. Two-client handover/capture validation remains pending.

P1 / confirmed wiring and code gap. MonsterNavigation lets only PhotonNetwork.IsMasterClient drive the NavMeshAgent; every other client disables its agent. The active MiniGamesKidFirstRig root has no PhotonView or transform synchronization component, and the controller contains no position/state broadcast. Its other root behaviours are rigging, proximity, and material components. Other clients therefore have no implemented path for following the moving monster.

Fix first in the current Level 1: give the monster a stable network identity and synchronize position, rotation, patrol/chase state, and target actor. One controller drives navigation; other clients display the received state. Capture must refer to that same shared monster.

For independent scenes, select a controller among players who have the monster’s sector loaded. Transfer control and the last valid state when that player leaves, changes sector, or suspends the headset. Simply adding a PhotonView while retaining the global IsMasterClient check will still fail when the room master is elsewhere.

Acceptance: two players observe the same chase, then the controller leaves. The remaining player gets one continuing monster, without a reset, duplicate, or frozen agent.

Evidence: [Movement authority](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/MonsterScripts/MonsterNavigation.cs#L49)  |  [Monster components](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L815738)  |  [Photon handover guidance](https://doc.photonengine.com/pun/current/gameplay/hostmigration)

## Crawler behavior and sound

### R03 Target selection and patrol return differ from the design

**Current status, 2026-09-10:** Sector/vent eligibility and immediate patrol return are implemented in PR #3 and hardened by this audit. Closest-player switching remains the existing behavior. The missing authored vent graph and verified NavMesh boundaries remain open.

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

**Current status, 2026-09-10:** PR #4 merges personal local-pickup gating and Level 1 completion travel to Level 2. The PR #5 reconciliation resolves the filename collision by retaining the active script GUID on `KeyCard.cs` and the separate old inventory-style component GUID on `VRKeyCard.cs`. Duplicate key contacts cannot count twice. Full capture-drop, owner visibility, out-of-bounds recovery and re-entry behavior still requires runtime verification and any follow-up implementation discovered by those tests.

P1 / personal lifecycle and exit feature gap at the original baseline. The active keybox requires two cards. That count is a design option Greg is willing to keep, rather than a defect by itself. KeyCard1.cs inserted a physical card, incremented KeyBox, and destroyed the card. KeyBox opened SlidingDoor; the personal fade and Level 2 transition were missing.

A separate VRKeyCard implementation in the old KeyCard.cs destroyed the object immediately when grabbed and recorded an inventory ID. PlayerInventory could recreate those cards on capture. Neither path alone established the agreed full personal lifecycle at the original baseline.

Fix/validation target: use one personal objective-card visit state. Keep the card physically holdable. Track its fixed original spawn separately from its current position and whether it is held, dropped, or consumed. Prototype one card, then judge difficulty with the working Crawler; the current two-card count is intentionally preserved until playtesting justifies a change.

- Capture: release the held card at the capture position; only its owner sees and recovers it.
- Out of bounds: restore the fixed key-room spawn.
- Leave and re-enter: clear the previous visit and create the intended fresh card state. Another player’s card and completion are unaffected.

Other players seeing a held card remains optional. If added, use a visual representation that disappears on drop; do not turn the personal objective into a shared pickup.

Evidence baseline: [Two-card keybox](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L808062)  |  [Active insertion path](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/KeyCard1.cs#L7)  |  [Alternative pickup path](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/KeyCard.cs#L21)  |  [Recorded spawn origin](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Utils/RespawnToOriginalSpawn.cs#L23)

### R06 Capture needs one guarded rig transition

P1 / confirmed implementation risks at the original baseline. TeleportGorillaPlayerPhotonVR could start a coroutine on each eligible trigger entry, captured the drop position after the fade, left locomotion Update running, changed physics flags, and did not reset the locomotion caches.

Current source uses the shared guarded movement foundation and PR #5 adds additional grounding/state cleanup. Acceptance still requires repeated trigger, card drop, fade recovery and headset locomotion testing.

Evidence baseline: [Capture coroutine](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/TeleportGorillaPlayerPhotonVR.cs#L22)  |  [Locomotion caches](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/NewGorillaLocomotionScripts/Player.cs#L67)  |  [Velocity history](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/NewGorillaLocomotionScripts/Player.cs#L378)

## Scene travel and sector isolation

### R07 Loading needs a full enter and leave lifecycle

P1 / feature gap with a concrete preload hazard at the original baseline. LevelStreamService only added scenes and a held `allowSceneActivation=false` preload could stall later operations.

Current source implements serialized guarded travel, fade/suspend, destination activation and grounding checks, active-scene change, old-environment unload, rollback and cooldown. PR #5 adds room-membership guards, reconnect state preservation and pause cancellation. Repeated-travel, load failure, memory and headset validation remain pending.

Evidence baseline: [Loader lifecycle](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Streaming/LevelStreamService.cs#L35)  |  [Unity activation queue behavior](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AsyncOperation-allowSceneActivation.html)

### R08 Zone properties do not yet isolate players or voice

P1 before scene splitting / feature gap at the original baseline. Zone properties existed but the custom player code did not consume remote sector changes to hide avatars, names, interaction colliders, or speakers.

Current source distinguishes loaded sector from local danger subzone and filters remote presentation/collision/speaker playback by sector. Conditioning is now an accepted sector. Voice traffic still uses existing Photon Voice configuration; validate actual same-room cross-sector voice, late joins, reconnect and arrival refresh with two clients.

Evidence baseline: [Zone storage and callbacks](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Zones/ZoneStateService.cs#L39)  |  [Persistent avatars](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/Player/PhotonVRPlayer.cs#L29)

## Startup session reliability and release preparation

### R09 Failures can leave startup waiting indefinitely

**Current status, 2026-09-10:** PR #5 adds bounded authentication/startup waits, trigger/R retry, guarded late callbacks, player readiness reset and coordinated room join/create failure recovery. Runtime failure-injection and service tests remain pending; see the audit report.

The earlier combined patch already added bounded/cancelled player spawning and local-only visual readiness. The audit branch now also addresses authentication retry, startup failure UI and room-join coordination at source level.

P2 / original baseline gap: LoadingFlow waited for AppState.IsReady without timeout/failure handling; AuthOrchestrator's flags prevented a normal retry after failure; PlayerSpawner could wait indefinitely; RoomSwitchService did not fully own disconnection/create failure or competing pending intent.

Fix now implemented in source: explicit connection/loading states with cancellation, bounded waits, user-visible failure and retry; associate spawn work with the current room/attempt; let one coordinator own room requests; mark visual readiness locally. Acceptance requires live failure injection and rapid join/rejoin testing.

Evidence baseline: [Unbounded ready wait](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Bootstrap/LoadingFlow.cs#L36)  |  [Run flags](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Auth/AuthOrchestrator.cs#L54)  |  [Spawn wait](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/Player/PlayerSpawner.cs#L27)  |  [Join lifecycle](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Rooms/RoomSwitchService.cs#L56)

### R10 Production identity requires a verified login path

Release requirement / confirmed client-side gap. Bootstrap sets enforceQuestAuth to false. Even when Quest auth is enabled, the provider obtains UserProof but AuthOrchestrator uses only the Meta user ID to build a CustomID login. The proof is not consumed by the login path. BuildConfig.EnforcePlatformAuth is not referenced. Photon Connect also clears AuthValues; PlayFab identity is not currently bound through that path.

Fix before public account/economy rollout: keep development login explicit, use a supported platform proof validation and account-linking flow for release, and bind Photon identity if the game relies on it. Existing development accounts need a migration decision. Review the server implementation of GrantLoginCoconuts separately for trusted identity and repeat-grant rules; that backend is outside this checkout. PR #5 improves inventory pagination/removal/persistence source behavior but does not replace backend ownership authority.

Evidence: [Collected proof](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Auth/QuestMetaAuthProvider.cs#L42)  |  [CustomID login](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Auth/AuthOrchestrator.cs#L66)  |  [Unused build flag](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/BuildConfig.cs#L1)  |  [PlayFab login guidance](https://learn.microsoft.com/en-us/xbox/playfab/identity/player-identity/login/login-basics-best-practices)

### R11 Room capacity

**Current status:** the default is ten from `d4ee616`; new-room validation remains pending. The following paragraph describes the original sixteen-player baseline.

P2 / confirmed design mismatch. PhotonVRManager.DefaultRoomLimit was 16 at the original baseline. No override to ten was found in the manager prefab or Bootstrap instance; RoomSwitchService used the manager default. Public matching also filtered by requested maximum players.

Fix: establish one ten-player session setting for public creation, private creation, and matchmaking. Test with new rooms; an already existing room is not resized merely by changing the default. Keep capacity global across all sectors and communicate full-room or version-mismatch failures at the terminal. Include PR #5's coordinated room flow in this test.

Evidence baseline: [Capacity default](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/PhotonVRManager.cs#L49)  |  [Public matchmaking](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Resources/PhotonVR/Scripts/PhotonVRManager.cs#L450)  |  [Room limit selection](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scripts/Rooms/RoomSwitchService.cs#L96)

## Crawler integration and focused cleanup

### Preserve the monster systems while replacing its visual rig

The active monster is Level1Root/MiniGamesKidFirstRig. Its root carries navigation, capture collision, rigging, proximity, and material effects, with a 180-degree forward offset. Zombie Crawl is a separate disabled object in Hub_Base with its controller assigned and Apply Root Motion off. Its animation still needs validation in Unity.

- Preserve the existing gameplay root and its links as a starting point; adapt it into one reusable Crawler prefab. Put Zombie Crawl beneath a visual child, with independent scale, orientation, and floor-contact offsets.
- Keep MonsterNavigation and its patrol points, patrol/hunt audio, ProximityReactor callbacks, and capture-to-RespawnPoint link. Repair/validate the issues in R02-R06 as these systems are retained.
- Reassign MonsterActivationGate to the Zombie Animator explicitly. Inspect the new material layout before rebinding. Disable old skeleton-specific IK and rig constraints until deliberately retargeted.
- Let navigation move the root; drive crawl playback from actual speed and pause or idle when stationary. Tune body height and hand contact before speed, then corners and capture reach. Move the 180-degree mesh correction to the visual child without applying it twice.
- Test the narrowest straight, a 90-degree turn, a junction, and both room entrances. Fit the body as well as the agent; a round agent can fit where a long crawling body clips.

Evidence: [Current monster root](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L815738)  |  [Disabled Zombie Crawl](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L852780)  |  [Animation following navigation](https://docs.unity3d.com/Packages/com.unity.ai.navigation@1.1/manual/MixingComponents.html)

### Cleanup that supports the next milestone

Scene files are large: Hub_Base is approximately 41.0 MiB, Hub_Base_1 is 28.9 MiB, Hub_OG is 38.7 MiB, and Level1 is 88.4 MiB. These are serialized file sizes, not measured headset memory. Extract the level from Hub_Base, then consider reusable cage, vent, door, and room prefabs. Keep old scene iterations outside the build; archive them once the new split is verified.

The held-item script rename to `HeldItemCollisionMode.cs` preserves its existing GUID. PR #5 also corrects the remaining audited project-script filename/class mismatches while preserving `.meta` GUID identities, including `KeyCard`/`VRKeyCard`, `AntiHandPhase`, `RandomTileRegion`, `MonsterTouchRespawnPhotonVR`, `ComputerTerminalUI` and `LoadingDebugText`; it adds the missing `EconomyInventoryLoader.cs.meta`. The reconciled Editor validator scans scenes for missing scripts. Unity import/binding validation remains required before treating the rename work as validated.

Original cleanup recommendation: gate verbose ZoneTrigger/AudioScaler diagnostics, use direct zone access and reuse proximity snapshot collections. Earlier fixes implement the local logging guard, typed zone access and snapshot reuse; PR #5 adds further targeted cleanup. Keep Photon/PlayFab SDK code distinct from the customized PhotonVR framework; preserve Resources prefab paths used for network spawning.

The reliability branch adds a root README, source validator, Editor regression checks and codebase audit. Investigate the tracked TempAssembly.dll as a possible generated artifact. Profile CPU, GPU, physics, memory and transitions on the target Quest before simplifying materials or meshes based on guesses.

## Split Hub_Base at the Security Gate

Source: Hub_Base, with world positions preserved during extraction. Destination: Level1_Containment, committed and merged through PR #3. The following procedure remains the extraction verification checklist; committed files alone do not establish that every step is complete. The Security Gate separates the social area from the safe containment arrival; the locked objective door remains inside the level and serves completion.

| Placement | Existing objects and required handling |
| --- | --- |
| Keep in Hub_Base | SpawnRoom and HubSpawn, Computer, Shop, the approach hallway, and StartLevel1Button with its hub-side gate. Leave unrelated ForestMap and ChillRoom content in place during this focused split. |
| Move to containment | Level1Root: SmallRoom, Monkey Cages, VentSystem, KeyCards, KeyBox, RespawnPoint, NavMeshRoot, level lighting and triggers, and MiniGamesKidFirstRig with its linked children. |
| Review at the seam | Level1Root/Floors, Walls, and Ceiling may include approach geometry. Separate hub-side pieces first; divide any continuous mesh and its collider at the chosen gate threshold. |
| Handle separately | Zombie Crawl is outside Level1Root and inactive. Move it with the level for integration. Level1Root/XR Interaction Manager must not be the only manager for a persistent rig or hub interactions after unloading. |

### Editor extraction procedure

1. Save the existing scene and record the gate threshold in the Scene view; verify which floor, wall, ceiling, and collider objects cross it.
1. Open Level1_Containment additively beside Hub_Base and compare/regroup geometry without resetting world transforms.
1. Keep asset references shared: materials, audio clips, meshes, and animation controllers remain project assets. Do not duplicate the whole asset collection.
1. Verify a complete floor and doorway at each end. Confirm `Level1EntrySpawn`, `HubDoorReturnSpawn`, and `HubReturnSpawn` match the route-specific rule; Level 2 uses its own `Level2EntrySpawn`.
1. Verify the Hub-side `StartLevel1Button` remains clearly reachable and is the only Hub hallway activation after the September 11 correction; the gate itself should not expose XR Select/Grip.
1. Audit patrol points, both audio callbacks, capture and respawn references, objective bindings, and triggers. Use the persistent XR Interaction Manager.
1. Rebake/verify containment navigation with the intended vent surfaces and agent type. Check lighting, probes, reflection settings, and occlusion data for independent loading.
1. Verify each environment alone, then run guarded runtime travel and multiplayer checks across Hub, Level 1 and Level 2.

The extraction checkpoint is two correctly separated environments with intact local references. Runtime readiness additionally requires synchronized monster authority, safe load/unload, and remote avatar/voice filtering.

Evidence baseline: [Level1Root](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L850036)  |  [Separate Zombie Crawl](https://github.com/GregStephen/RunawayChimps/blob/4f6894141aa2b132d744cb2423c1ee227e0fc5be/Assets/Scenes/Hub_Base.unity#L852780)  |  [Unity multi-scene editing](https://docs.unity3d.com/2022.3/Documentation/Manual/setupmultiplescenes.html)

## Implementation order

Use small reviewable changes, each ending with a playable checkpoint. Effort labels are relative: Small is localized; Medium crosses several scripts or Inspector bindings; Large crosses network ownership or scene lifecycle. They are not calendar estimates.

| Batch | Work and main files | Exit condition |
| --- | --- | --- |
| 1 Map extraction Medium | Verify the merged Hub_Base/Level1_Containment split in Unity 2022.3.55f1. | Hub and containment geometry are separate and complete; local bindings survive. |
| 2 Crawler and chase Large | Integrate Zombie Crawl using the [Crawler integration guidance](#preserve-the-monster-systems-while-replacing-its-visual-rig). R02-R04: navigation, audio, zone eligibility, one network controller, and state handover. | The Crawler fits the vents on both clients. Safe rooms end pursuit, patrol and sound recover, and controller departure is handled. |
| 3 Personal attempt Medium | Finish/validate R05-R06: personal card visit state and guarded capture, now including the merged Level 1 → Level 2 completion route. | Each player can fetch, drop, recover, and use their own card; capture and success leave the other player unchanged. |
| 4 Sector travel Large | Implement/validate the September 11 button-only Hub entrance correction plus R01/R07/R08 across the merged Hub, Level 1 and Level 2 routes. | Hub hallway uses only the physical button; Hub → Level 1/2, Level 1 completion → Level 2 and return routes work in one room; old environments and visit objects clean up. |
| 5 Resilience and tuning Medium | Validate PR #5 R09 startup retry/cancellation, room coordination, reconnect/pause and Quest performance. | Connection failures recover; measured performance and difficulty guide changes. |
| 6 Level 2 v0.4 blockout Large | After the enlarged arrangement is approved, rebuild the Level 2 layout/generator/prefab/scene to at least 36 × 36 m while preserving travel wiring, gate standards, safe areas, reward-room relationship and two repair escape loops. | Authored v0.4 geometry is in source; both repair loops and all common Listener routes are navigable; source/Editor checks pass; headset scale and Quest performance are recorded. |
| 7 Release preparation Medium to Large | R10: platform login, account migration, inventory/ownership checks, and backend review before public account and economy rollout. | Release identity is validated and server-side rewards and ownership rules have been reviewed. |

### Recommended next validation session

Start from PR #5's reconciled tree, not either pre-merge branch. Run the offline source validator. Open Unity 2022.3.55f1 and run both Runaway Chimps validators before Play Mode. Resolve any compile/import/missing-script issue first. Implement and check the September 11 Hub entrance correction so `StartLevel1Button` is the only Hub hallway activation, then test it both by headset hand contact and by moving the monkey hand collider in non-headset editor testing. Continue with startup failure/retry, private/public room coordination, Hub → Level 1/Level 2, personal Level 1 completion → Level 2, RETURN TO SECURITY → Hub, failed-load rollback, reconnect/pause, and two-client sector/monster behavior. Keep Listener gameplay, reward-room persistence, shop expansion and camera-wall production work behind this dependable checkpoint. The v0.4 Level 2 branch is design-only until its room arrangement is approved; do not treat the old 18 × 18 m footprint as the future target.

## Acceptance checks and remaining choices

| Check | Required result |
| --- | --- |
| Source/import integrity | Offline source validator and both Editor validators pass against the reconciled five-scene tree with no missing scripts. |
| Same-sector chase | Both clients see matching monster position and chase state. Either player can become an eligible target. |
| Safe-room escape | Enter either safe room: that player stops being a target. With no other eligible target, the Crawler immediately resumes patrol. |
| Controller change | Controller leaves the level, disconnects, or suspends the headset. Remaining players keep one functioning monster. |
| Card independence | Both players retrieve cards. One drops or uses theirs; the other player retains their own state. |
| Capture and floor recovery | Repeated trigger contact starts one capture. Card drops at the recorded hit point; out-of-bounds returns it to the fixed room spawn. |
| Exit and return | Winner fades and arrives at Level 2 safe entry. Follower sees them disappear and stays in Level 1. RETURN TO SECURITY arrives at the Hub computer. Re-entry restores the intended fresh objective state. |
| Travel and visibility | Hub hallway → Level 1 is activated only by physically pressing `StartLevel1Button` with the local monkey hand/fingertip; the Hub gate has no XR Select/Grip activation. The same button works in non-headset editor testing by moving the monkey hand collider into it. Hub computer routes arrive at their intended Level 1/Level 2 spawns; Level 1 door return arrives in the Hub hallway. Same room code and actor membership persist across sectors. No cross-sector avatar collision or unwanted voice; arrival/reconnect refresh presentation. |
| Reconnect and pause | Disconnect/rejoin or pause/resume around travel restores the correct sector/zone without stale travel, duplicate rig, or stale monster snapshot. |
| Capacity and failure | Ten players remain ten even when split across levels. An eleventh cannot join that room. Failed login, join, or scene load offers recovery. |
| Quest stability | No stale locomotion impulse, stuck fade, missing floor, duplicate rig, or accumulated level objects after repeated travel. Record frame-time and memory measurements. |
| Expanded Level 2 geometry | Once v0.4 is implemented, the authored playable footprint is at least 36 × 36 m without a 2× root-scale shortcut. Safe entry/qualified exit remain the intended safe spaces; the bottom-right reward room retains its role; both repair exits feed distinct usable loops; actual gate meshes/colliders and every common route fit the Listener; travel markers remain clear; Quest performance is measured. |

### Targeted automated checks to add

PR #5 adds Editor reliability regression checks and extends the travel validator to detect missing scripts. The `codex/fix-player-rig-tpose` branch now also repairs and asserts the Photon player visual-hand IK target/pole/view bindings and right-hand XR side before the reliability suite continues. Update the travel validator for the September 11 button-only Hub entrance rule, then continue adding small tests for the personal visit lifecycle and eligible-target transitions. When v0.4 geometry is implemented, extend the Level 2 source/layout checks to enforce the new minimum footprint and required connectivity without confusing those source checks with Unity/NavMesh/headset validation. Use two-client and headset checks for network synchronization, fades, collision, voice and performance; source inspection cannot establish those outcomes.

### Choices still needed from Greg

- Level 2 v0.4 arrangement: the 36 × 36 m minimum is confirmed; approve or revise the proposed Test Hall A, Lower Service Hall, West Observation Wing, Conditioning Hall B, East Bypass, North Gallery, larger Repair Lab and 6 × 6 m bottom-right reward-room arrangement before scene implementation.
- Difficulty: prototype one personal card at the existing spot. Retain two only if playtests justify a second route and a clear containment-release purpose.
- Crawler targeting: keep its current target until escape/capture, or deliberately allow closest-player switching? Should patrol visit assigned points in order or randomly?
- Presentation: should other players see a held keycard? What exact scare should precede respawn?
- Reward rooms: choose stable card/source level, persistence/capture/restart policy, follower access, collectible and currency rules.

The recommendations preserve the agreed direction: open level access, individual wins, no saved partial-level progress, personal card resets on re-entry, a Crawler restricted to vents, a physical button-only Hub hallway entrance into Level 1, one RETURN TO SECURITY control in Level 2, a minimum 36 × 36 m replacement Level 2 footprint once v0.4 is implemented, and one ten-player Photon room across independently loaded sectors.
