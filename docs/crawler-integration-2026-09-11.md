# Crawler / Zombie Crawl integration — 2026-09-11

## Confirmed design

Level 1 keeps the existing Crawler gameplay behavior: it is confined to the vents, chases eligible players in the vent zone, safe rooms end pursuit, and loss of a target returns it to patrol. `Zombie Crawl` replaces the visible `MiniGamesKidFirstRig` model while the established navigation, capture, audio, proximity, patrol and Photon synchronization systems remain on the existing gameplay root.

## Implemented on `codex/crawler-zombie-integration`

- Added `CrawlerVisualController` to make the existing `MonsterNavigation` root adopt the authored `Zombie Crawl` object at runtime.
- Keeps `NavMeshAgent`, `MonsterNavigation`, `SectorMonsterSync`, capture colliders, audio and patrol references on the existing root instead of moving gameplay onto the imported model.
- Disables the legacy kid renderers and legacy Animator only after `Zombie Crawl` is found; gameplay colliders/components are retained.
- Forces Zombie Crawl root motion off so navigation/network synchronization remain authoritative for world movement.
- Moves the 180-degree model-forward correction to the visual child and clears the navigation root's model offset to avoid applying the correction twice.
- Drives crawl playback from measured root displacement rather than only `NavMeshAgent.velocity`, so non-authority Photon clients can visually match synchronized monster movement.
- Stops crawl playback while stationary, scales it with actual motion, lightly boosts chase playback, smooths transitions, and ignores large teleport/controller-handoff deltas.
- `MonsterActivationGate` can now be rebound to the Zombie Crawl Animator and immediately reapplies the current local-zone gate state.

## Level 1 lighting regression fix

Source inspection after the scene split found that `Level1_Containment.unity` contains no serialized Unity `Light` components. That leaves the standalone Level 1 scene effectively black after `Hub_Base` unloads during travel.

Implemented on this branch:

- Added `Level1LightingBootstrap`, registered before scene loading and listening for additive scene activation.
- When `Level1_Containment` becomes active, applies a dim cool flat ambient baseline so geometry remains readable without turning the level bright.
- Creates one low-intensity, shadowless directional fill owned by the Level 1 scene, so unloading the Hub cannot remove the level's fallback illumination.
- Keeps the fallback deliberately inexpensive and separate from future authored horror-lighting polish.

This is a regression fix, not approval of final Level 1 lighting art direction. Final fixtures, contrast, flicker, practical lights and Quest performance still require visual tuning.

## Source inspection completed

`Level1_Containment` contains both the active `MiniGamesKidFirstRig` gameplay root and an active `Zombie Crawl` object. Zombie Crawl has the expected Animator/controller and root motion is handled by the integration controller at runtime. The scene currently has no serialized `Light` components, which is why the runtime fallback was added. The branch is based on `main` commit `1500103c7636506465a2c546698b394f00306a6e`.

## Pending validation

Do not mark this integration validated until it is run in Unity 2022.3.55f1. Required checks:

1. Compile/import with no missing script or Animator errors, including the new lighting bootstrap and `.meta` files.
2. Travel Hub → Level 1 and verify the containment room and vents are readable instead of pitch black, while still visibly dark.
3. Return to Hub and then re-enter Level 1; verify lighting is restored once and does not accumulate duplicate fill lights or alter Hub lighting.
4. Enter Level 1 and confirm only Zombie Crawl is visible; the old kid mesh stays hidden while capture/navigation still work.
5. Verify floor/hand contact and scale in the narrowest straight vent.
6. Verify a 90-degree corner, a junction, and both safe-room entrances without body/arm clipping that blocks gameplay.
7. Verify stationary, patrol and chase animation playback do not foot/hand-slide excessively.
8. Confirm safe-room entry or lost target immediately returns to patrol and both patrol/chase audio states still recover correctly.
9. Capture/respawn while holding a card and confirm the monster continues functioning afterward.
10. Run two Photon clients: only the elected controller navigates, the remote client sees the same Crawler pose/motion/chase state, and controller handoff does not produce a persistent animation-speed spike.
11. Validate lighting and Crawler behavior in-headset on target Quest hardware after desktop Play Mode passes.

Any visual offsets, scale, playback range, lighting intensity/color, or smoothing changes found during these checks are tuning work, not changes to the confirmed Crawler gameplay rules.
