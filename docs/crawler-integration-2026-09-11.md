# Crawler / Zombie Crawl integration — 2026-09-11

## Confirmed design

Level 1 keeps the existing Crawler gameplay behavior: it is confined to the vents, chases eligible players in the vent zone, safe rooms end pursuit, and loss of a target returns it to patrol. `Zombie Crawl` replaces the visible `MiniGamesKidFirstRig` model while the established navigation, capture, audio, proximity, patrol and Photon synchronization systems remain on the existing gameplay root.

Level 1 must remain dark and threatening without becoming effectively unnavigable. The optional local vent headlamp discussed after play feedback remains a proposal, not an implemented requirement.

## Original integration merged through PR #8

- Added `CrawlerVisualController` to make the existing `MonsterNavigation` root adopt the authored `Zombie Crawl` object at runtime.
- Keeps `NavMeshAgent`, `MonsterNavigation`, `SectorMonsterSync`, capture colliders, audio and patrol references on the existing root instead of moving gameplay onto the imported model.
- Disables the legacy kid renderers and legacy Animator only after `Zombie Crawl` is found; gameplay colliders/components are retained.
- Forces Zombie Crawl root motion off so navigation/network synchronization remain authoritative for world movement.
- Moves the 180-degree model-forward correction to the visual child and clears the navigation root's model offset to avoid applying the correction twice.
- Drives crawl playback from measured root displacement rather than only `NavMeshAgent.velocity`, so non-authority Photon clients can visually match synchronized monster movement.
- Stops crawl playback while stationary, scales it with actual motion, lightly boosts chase playback, smooths transitions, and ignores large teleport/controller-handoff deltas.
- `MonsterActivationGate` can be rebound to the Zombie Crawl Animator.
- Added `Level1LightingBootstrap` after source inspection found no serialized Unity `Light` components in `Level1_Containment`.

PR #8 merged to `main` at `40a0aaee7332a105e761473012b6dc61be6a6183`. Its first lighting fallback and visual integration remained pending Unity/headset validation.

## Runtime follow-up on `codex/fix-level1-crawler-lighting`

Greg's first play feedback after PR #8 was that Level 1 remained much too dark, Zombie Crawl appeared too small, visibly slid rather than crawled, did not seem to chase, and did nothing on ordinary contact. The follow-up branch addresses those symptoms without replacing the existing gameplay root.

Implemented corrections:

- Raises the cool ambient floor and shadowless directional fill so Level 1 is readable while remaining dark. These are prototype fallback values, not final authored lighting.
- Preserves Zombie Crawl's authored **world scale** when reparenting beneath the already-scaled monster root. This avoids multiplying the model's authored scale by the gameplay root's non-unit scale a second time.
- Uses prototype tuning of 12 m detection, 2.25 m/s patrol, 4.5 m/s chase and 0.1 s target checks.
- Keeps crawl playback tied to measured root movement, searches only the loaded Level 1 scene for the Zombie object, includes inactive authored objects, and explicitly restarts a non-looping crawl state while the root continues moving.
- Keeps the shared Crawler Animator active even when the local viewer is in a safe room. Local audio remains zone-gated, but a shared monster visible through a doorway no longer becomes a frozen-pose slide just because that viewer is safe.
- Uses the tracked head as the authoritative crossing collider for both Level 1 safe-room boundaries, preventing a reaching hand from making the entire player safe before the head crosses.
- Repairs the second safe-room route's missing safe-to-vent publication by applying `Level1_Vents` when the tracked head exits that boundary toward the vent network.
- Accepts capture from any collider belonging to the local rig while still requiring the Containment sector and `Level1_Vents`.
- Retries guarded capture during continued overlap so an initial contact during a transient busy/zone state is not permanently missed.
- Records held-card drops from the Gorilla locomotion body position before respawn instead of the particular hand/head collider that touched the Crawler.

## Second runtime visual/animation follow-up

**Confirmed failing validation:** Greg reports that on the current follow-up the visible Zombie Crawl still does not visibly crawl, appears several meters away from the invisible/sliding gameplay rig at times, and can leave the vent volume. The long visual also cannot currently negotiate corners convincingly because the entire imported visual is still driven as one rigid child of the navigation root. Therefore the earlier source-level crawl-playback work must **not** be treated as runtime-validated or visually complete.

**Source finding:** the latest pre-fix `CrawlerVisualController` already reparents Zombie Crawl with local position zero and a fixed local rotation, so the reported multi-meter separation is no longer explained by preserving the old scene-root position. The remaining alignment problem can come from the imported FBX/skeleton's internal pivot and visible-bone positions relative to its GameObject root. The Animator controller contains one default crawl state, so a visually static pose while its `Animator` is enabled must be detected as a real clip/bone evaluation failure rather than hidden by additional movement-speed tuning.

## Implemented Crawler visual-path architecture

Implemented on `codex/fix-level1-crawler-lighting` in commits `5f5ea84` and `97b20e6`:

- `CrawlerVisualController` now creates a dedicated runtime `CrawlerVisualAnchor` beneath the established gameplay root. The authored Zombie keeps its intended world scale while model orientation remains isolated from navigation orientation.
- `CrawlerBodyPathFollower` resolves the Generic Mixamo torso by normalized bone names, preferring chest/`Spine2` and following the actual parent chain back through spine/hips. The visible front body anchor is horizontally aligned to the invisible gameplay leader rather than assuming the FBX object origin equals the creature's body center.
- Initial floor contact is corrected from the Zombie render bounds so the lowest rendered point begins just above the NavMesh floor instead of inheriting an arbitrary imported pivot height.
- The follower records the gameplay root's recent vent-centerline motion. Chest/spine/hips sample progressively older positions and headings, so a long body can remain in the old corridor while its front enters a 90-degree turn. The trail is seeded straight behind the leader on initialization and after teleports so the rig cannot collapse before real movement history exists.
- Torso path correction runs after normal Animator evaluation. The crawl clip remains responsible for limb motion; the path follower owns large-scale body placement/bending. This implementation uses lightweight post-Animator Generic-bone correction rather than constructing a runtime Animation Rigging graph. The installed Animation Rigging package remains available if playtesting shows authored constraints/IK are still preferable.
- Each hand gets a non-allocating vent-collision containment check from its upper arm toward the animated hand. If the animation would put the hand through a non-trigger vent surface, the hand is pulled back just inside the hit surface with a bounded correction. This must be judged visually in Unity because excessive correction could still require authored/two-bone IK.
- Crawl-state startup is now explicit: the Zombie Animator is rebound, forced into `Base Layer.mixamo_com`/`mixamo_com`, kept `AlwaysAnimate`, and still has root motion disabled.
- While the gameplay root is moving, limb rotations are probed over time. If Animator state time advances without visible probe-bone movement, the controller performs one automatic rebind/restart. A second failure emits an explicit clip/skeleton binding error instead of silently allowing a frozen-pose slide.
- Photon remains unchanged: only the gameplay Crawler root/chase state is synchronized. Every client derives body-trail bending from the synchronized root motion rather than sending per-bone state.

These commits are **source implementation only**. They do not prove that the imported `mixamo.com.anim` actually deforms the Zombie in Unity, that every resolved bone name matches the runtime FBX hierarchy, or that the resulting deformation stays visually acceptable at all corners.

## Source inspection completed

The current Level 1 scene contains the established Crawler gameplay root, the Zombie Crawl visual object and the existing scaled capture capsule. The scene has two safe-room boundaries but only one separately-authored `Level1_Vents` trigger, which is why the directional boundary repair is required for the second route. The persistent Bootstrap `Main Camera` is tagged `MainCamera` and has a SphereCollider, matching the head-authoritative safe-room rule.

`MonsterNavigation` keeps world movement authoritative through the NavMesh/controller owner, returns immediately to patrol when a target becomes ineligible, and uses sector + `Level1_Vents` eligibility. `SectorMonsterSync` remains responsible for controller election and remote pose/chase replication. The authored VentGraph is still absent, so detection falls back to straight-line range when `VentGraph.Instance` is unavailable. Closest-player switching remains an open gameplay choice rather than a bug fixed by this branch.

No GitHub CI/status workflow currently validates this branch. Source inspection is not a substitute for Unity compilation or headset testing.

## Pending validation

Do not mark the follow-up validated until it is run in Unity 2022.3.55f1. Required checks:

1. Compile/import with no missing script or Animator errors and run the existing Runaway Chimps Editor/source validators.
2. Travel Hub → Level 1 and verify containment/vents are readable while still clearly horror-dark; return/re-enter and confirm no duplicate fill lights or Hub-lighting contamination.
3. Confirm only Zombie Crawl is visible and its physical scale is appropriate in the narrowest straight vent.
4. Confirm the new calibrated chest/spine anchor eliminates the several-meter visual/gameplay-root separation and that render-bounds floor alignment does not float or bury the creature.
5. Prove `mixamo_com` visibly deforms Zombie Crawl: while moving, hands/forearms/head must visibly animate and the Console must not emit the persistent clip/skeleton binding error. Verify stationary/patrol/chase playback, no frozen-pose sliding, and no one-frame speed spike after travel/controller correction.
6. Verify the long body stays extended on spawn, bends through at least one 90-degree corner and one junction, and does not cut through the outer or inner vent walls. Tune trail spacing/body-follow weight only from this runtime result.
7. Watch both hands at straight sections and corners. Confirm wall/floor containment does not create unacceptable arm stretching; if it does, replace the final hand-position correction with authored/two-bone IK while keeping the same safe target calculation.
8. Stand in a safe room with the Crawler visible through the opening and confirm the moving shared Crawler still animates while local patrol/hunt audio remains correctly gated.
9. Enter both safe rooms with the tracked head: pursuit stops. Reach a hand across first: the player's zone must not change early. Exit either safe room toward the vents: `Level1_Vents` returns and the player becomes eligible again.
10. Verify pursuit at the 12 m prototype range and the 2.25/4.5 patrol/chase speeds. Adjust only from playtest evidence.
11. Verify ordinary head/body/hand contact in the vents starts exactly one capture. Begin overlap during a transient busy/zone state and confirm continued overlap captures once valid.
12. Capture while holding a card and confirm it drops at the Gorilla body position recorded before respawn; confirm no capture inside either safe room.
13. Verify target loss immediately returns to patrol and both patrol/chase audio states recover correctly.
14. Run two Photon clients: both see matching Crawler position/chase state, crawl animation, and locally derived corner bending; controller handoff leaves one functioning monster without a duplicate or persistent animation spike.
15. Validate all of the above in-headset on target Quest hardware and record frame-time/memory impact of the brighter fallback plus continuously animated/path-corrected shared Crawler.

Any visual offsets, scale, playback range, body-follow weight, trail spacing, hand-inset amount, lighting intensity/color, or smoothing changes found during these checks are tuning work, not changes to the confirmed Crawler gameplay rules.
