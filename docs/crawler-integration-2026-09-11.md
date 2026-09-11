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

**Source finding:** the latest `CrawlerVisualController` already reparents Zombie Crawl with local position zero and a fixed local rotation, so the reported multi-meter separation is no longer explained by preserving the old scene-root position. The likely remaining alignment problem is the imported FBX/skeleton's internal visual pivot or bone hierarchy relative to its GameObject root. The Animator controller contains one default crawl state, so a visually static pose while its `Animator` is enabled points to clip/bone binding or rig evaluation that must be proven directly in Unity rather than papered over with additional speed/restart logic.

**Proposed corrective architecture, not yet implemented:** keep `MonsterNavigation`/`NavMeshAgent` as the invisible gameplay authority, but align Zombie Crawl to a calibrated body anchor such as chest/hips instead of assuming the FBX root equals the visible body center. Prove the crawl clip deforms the model in isolation first. Then use the already-installed Unity Animation Rigging package plus a short history of the gameplay root's vent-centerline motion to drive several body guide targets along the path. The front follows the current root while chest/hips/rear samples progressively older positions, letting the long body wrap around 90-degree corners instead of rotating as one rigid object. The authored crawl clip remains the base limb motion; procedural rigging should add only the path-following bend and, if still needed, restrained hand IK to keep palms/arms inside vent surfaces. Photon should continue syncing only the gameplay root/chase state; each client can derive the same visual trail locally rather than networking bones.

## Source inspection completed

The current Level 1 scene contains the established Crawler gameplay root, the Zombie Crawl visual object and the existing scaled capture capsule. The scene has two safe-room boundaries but only one separately-authored `Level1_Vents` trigger, which is why the directional boundary repair is required for the second route. The persistent Bootstrap `Main Camera` is tagged `MainCamera` and has a SphereCollider, matching the head-authoritative safe-room rule.

`MonsterNavigation` keeps world movement authoritative through the NavMesh/controller owner, returns immediately to patrol when a target becomes ineligible, and uses sector + `Level1_Vents` eligibility. `SectorMonsterSync` remains responsible for controller election and remote pose/chase replication. The authored VentGraph is still absent, so detection falls back to straight-line range when `VentGraph.Instance` is unavailable. Closest-player switching remains an open gameplay choice rather than a bug fixed by this branch.

No GitHub CI/status workflow currently validates this branch. Source inspection is not a substitute for Unity compilation or headset testing.

## Pending validation

Do not mark the follow-up validated until it is run in Unity 2022.3.55f1. Required checks:

1. Compile/import with no missing script or Animator errors and run the existing Runaway Chimps Editor/source validators.
2. Travel Hub → Level 1 and verify containment/vents are readable while still clearly horror-dark; return/re-enter and confirm no duplicate fill lights or Hub-lighting contamination.
3. Confirm only Zombie Crawl is visible and its physical scale is appropriate in the narrowest straight vent.
4. **Currently failing:** verify the visible body stays aligned to the gameplay/navigation root, including floor contact and the narrowest straight vent. Replace root-origin alignment with a calibrated body anchor if the imported visual pivot is offset.
5. **Currently failing:** prove `mixamo_com` visibly deforms Zombie Crawl in isolation before judging speed matching. Then verify stationary/patrol/chase animation behavior: continuous visible crawling while moving, no frozen-pose sliding, and no one-frame speed spike after travel/controller correction.
6. **Currently failing by architecture:** verify the long body bends through a 90-degree corner and junction without leaving the vent. A rigid child rotation is not sufficient; validate the proposed path-history/body-rig approach if implemented.
7. Stand in a safe room with the Crawler visible through the opening and confirm the moving shared Crawler still animates while local patrol/hunt audio remains correctly gated.
8. Enter both safe rooms with the tracked head: pursuit stops. Reach a hand across first: the player's zone must not change early. Exit either safe room toward the vents: `Level1_Vents` returns and the player becomes eligible again.
9. Verify pursuit at the 12 m prototype range and the 2.25/4.5 patrol/chase speeds. Adjust only from playtest evidence.
10. Verify ordinary head/body/hand contact in the vents starts exactly one capture. Begin overlap during a transient busy/zone state and confirm continued overlap captures once valid.
11. Capture while holding a card and confirm it drops at the Gorilla body position recorded before respawn; confirm no capture inside either safe room.
12. Verify target loss immediately returns to patrol and both patrol/chase audio states recover correctly.
13. Run two Photon clients: both see matching Crawler position/chase state and crawl animation; controller handoff leaves one functioning monster without a duplicate or persistent animation spike. If procedural body bending is added, derive it locally from synchronized root motion rather than sending per-bone transforms.
14. Validate all of the above in-headset on target Quest hardware and record frame-time/memory impact of the brighter fallback plus continuously animated/rigged shared Crawler.

Any visual offsets, scale, playback range, lighting intensity/color, or smoothing changes found during these checks are tuning work, not changes to the confirmed Crawler gameplay rules.
