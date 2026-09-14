# Hub surveillance monitor setup

Last updated: 2026-09-12.

This note covers the source implementation on `feature/hub-surveillance-monitors`. It does not mean the monitor model is imported, a Hub prefab is authored, or the feature has passed Unity/headset validation.

## What is implemented

- `SecurityMonitorFeed` is a ScriptableObject describing one sector label, its recorded still-image sequence, and an optional rare interrupt image.
- `SecurityMonitorDisplay` is the presentation adapter for one physical monitor. It expects a world-space UI with a footage `RawImage`, grain `RawImage`, TextMeshPro sector/name labels, and optional REC text.
- `SurveillanceWallController` drives the physical monitors, finishes a feed's full view sequence before rotating that monitor to another level, and keeps each feed's view/loop state while it is off-screen.
- `Tools > Runaway Chimps > Surveillance > Capture Still...` captures a selected Unity Camera to a clean 4:3 PNG and imports it under `Assets/Art/Surveillance/Captures`.

The runtime scripts intentionally do not require level scenes to remain loaded in the Hub. They play imported still textures only. No monitor audio is created.

## Capture a level still

1. Open the level scene you want to photograph.
2. Create or select a temporary Camera and frame it where an authored security camera belongs.
3. Open **Tools > Runaway Chimps > Surveillance > Capture Still...**.
4. Assign the Camera, give the still a descriptive name such as `Sector01_CageRoom_01`, and press **Capture Surveillance Still**.
5. The tool saves a 4:3 PNG. At the default 1024 width this is 1024 × 768. The imported texture uses clamp wrap, bilinear filtering, mipmaps, compressed texture import, and a 1024 maximum size.
6. Do not add grain or the sector name to the source image. Those remain monitor-side layers so the same display can represent another level later.

Suggested Level 1 starting captures remain the design-doc sequence: cage/start room, keycard room, normal empty vent, plus an optional Crawler interrupt image from the vent camera.

## Create a feed asset

In the Project window use **Create > Runaway Chimps > Surveillance > Monitor Feed**.

For Level 1:

- Sector Number: `1`
- Sector Name: `PRIMATE CONTAINMENT`
- Views: add the clean cage, keycard, and vent stills; approximately 4–6 seconds per view is the current design starting point.
- Rare Interrupt Image: optional Crawler vent still.
- Rare Interrupt Every Loops: `4` is the current prototype default, not a locked tuning value.
- Rare Interrupt Duration: `1` second is the current prototype default.

## Set up one imported monitor

The generated Blender/GLB prototype keeps `*_Screen_Surface` separate from the physical bezel. After importing the monitor:

1. Create a prefab from the imported model.
2. Add a world-space Canvas slightly in front of `*_Screen_Surface`.
3. Add a full-screen `RawImage` for footage.
4. Add a second full-screen `RawImage` for the transparent grain/scanline texture. Set the grain texture's **Wrap Mode to Repeat** so `SecurityMonitorDisplay` can scroll its UVs without smearing the edge pixels.
5. Add TextMeshPro text for the sector number and sector name near the bottom of the screen, plus optional `REC` text.
6. Add `SecurityMonitorDisplay` to the prefab root (or display root) and assign those references.
7. Duplicate the completed prefab for the physical wall instead of rebuilding the UI four times.

The label is always supplied by the current `SecurityMonitorFeed`; do not print a permanent sector name on the bezel.

## Set up the four-monitor wall

1. Place about four monitor prefabs in `Hub_Base`.
2. Create an empty object such as `SurveillanceWall` and add `SurveillanceWallController`.
3. Assign the four `SecurityMonitorDisplay` components in screen order.
4. Add available `SecurityMonitorFeed` assets to the Feeds list.
5. With four or fewer feeds, the first feeds stay assigned to the first monitors and unused monitors show static / `NO SIGNAL`.
6. With more physical level feeds than monitors, `rotateAdditionalFeeds` makes a monitor switch only after its current level completes a full view sequence. The next assignment excludes feeds already visible on another screen.

Each feed's completed-loop counter is stored independently by the wall controller, so rotating a level off-screen does not reset its rare-interrupt cadence during that Hub visit.

## Pending validation

Before describing the feature as working in-game, run:

- Unity 2022.3.55f1 import/compile with the scripts and imported monitor prefab;
- capture-tool test from Level 1 at 1024 × 768;
- one-monitor footage/grain/label readability test in Game view and headset;
- four-monitor playback and >4-feed rotation test;
- verify a full sequence finishes before an assignment changes and feed state resumes correctly later;
- confirm unused screens present acceptably;
- confirm playback is silent;
- Quest profiling for texture memory, UI cost and grain presentation.

This source implementation does not use live `RenderTexture` cameras in the Hub and therefore does not keep remote level scenes active merely to render surveillance screens.
