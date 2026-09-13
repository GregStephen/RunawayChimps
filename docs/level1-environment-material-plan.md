# Level 1 environment material plan

Last updated: 2026-09-12.

This document records the approved direction for replacing Level 1's generic/repeating lab surfaces with a coordinated material system that fits Runaway Chimps' aged institutional-laboratory aesthetic while remaining practical for Quest and Unity 2022.3.55f1 / URP.

## Status

- **Confirmed direction:** do not cover Level 1 with one obvious repeating wall, floor, or ceiling texture. Use small surface families plus sparse local variation.
- **Confirmed direction:** the lab should look functional, institutional, old and neglected rather than futuristic, pristine, or post-apocalyptic. Dirt, wear, patching and aging are preferred over heavy rust everywhere.
- **Confirmed direction:** Level 1 must remain dark/horror but readable. Materials must preserve enough value contrast for walls, junctions, route edges and the Crawler to read under the current lighting baseline and local vent headlamp.
- **Confirmed geometry fact:** the existing Level 1 ceiling modules are 1 m × 1 m. Ceiling-module textures treat one UV 0–1 span as one 1 m module at material tiling 1 × 1 rather than copying the legacy `Ceiling1` material's 0.25 × 0.25 tiling.
- **Asset-generated, not integrated:** a 67-file `RunawayChimps_Level1_LabMaterialPack` was generated on 2026-09-12. It contains the planned floor/wall/ceiling texture families, dark trim, a 4×4 reusable detail atlas, PBR maps, previews, a manifest and a Unity Editor material-creation helper. The generated deliverable is not yet committed into the repository scene/assets and has not been applied to Level 1.
- **Pending validation:** Unity import, in-scene scale/UV inspection, material assignment, Play Mode/headset readability, visible-repetition review, decal overdraw/z-fighting review, and Quest memory/GPU/frame-time measurements.

## Generated material set

### Floors

The generated floor maps use one full UV repeat over approximately 4 m × 4 m. The broader span is deliberate so a player does not see a recognizable one-meter texture repeated continuously.

| Material | Purpose | Starting use |
| --- | --- | --- |
| `MAT_Lab_Floor_Standard` | Main sealed industrial concrete / worn epoxy. Medium-dark cool gray-green with low-frequency sealer variation and small aggregate/pinhole detail. | Most cage-room, hallway and lab floor area. |
| `MAT_Lab_Floor_Traffic` | Same base floor with broader polished/scuffed traffic wear. | Long hall centers and frequently traveled routes. |
| `MAT_Lab_Floor_Stained` | Same base floor with stronger old staining and grime variation, but no story-specific blood. | Corners, maintenance/service areas and selected cage-adjacent dirty zones. |

Do not alternate these in a checkerboard. Change material by believable region: a traffic area can run for several meters; a stained region belongs near a leak, cage, drain, equipment cluster or neglected corner.

### Walls

The generated wall maps also use one full UV repeat over approximately 4 m × 4 m and replace the visual role of the current generic `WhiteWall` wallpaper-based surface rather than preserving its wallpaper look.

| Material | Purpose | Starting use |
| --- | --- | --- |
| `MAT_Lab_Wall_Standard` | Primary painted institutional concrete/plaster. Muted cold gray-green with subtle roller/mottle texture. | Majority of Level 1 walls. |
| `MAT_Lab_Wall_Worn` | Same paint family with more chips and abrasion. | Cage-room walls, door approaches, corners and maintenance areas. |
| `MAT_Lab_Wall_Damp` | Same family with restrained vertical damp/leak discoloration. | Only where pipes, vents, damaged utilities or ceiling leaks justify it. |

Long wall runs should keep one architectural base and be broken by trim, props, decals and occasional believable surface changes instead of swapping material every meter.

### Ceiling

The generated ceiling family is authored as true 1 m × 1 m modules matching the verified Level 1 ceiling geometry.

| Material/module | Purpose |
| --- | --- |
| `MAT_Lab_Ceiling_Base_A_1m` | Primary aged off-white institutional panel and dark frame. |
| `MAT_Lab_Ceiling_Base_B_1m` | Subtle surface-wear variation of Base A. |
| `MAT_Lab_Ceiling_Base_C_1m` | Third subtle base variation to stop identical dirt landmarks repeating. |
| `MAT_Lab_Ceiling_Light_1m` | Matching module with approximately 0.70 × 0.38 m fluorescent diffuser and emission map. |
| `MAT_Lab_Ceiling_Vent_1m` | Matching module with approximately 0.56 × 0.34 m dark louver grille. |
| `MAT_Lab_Ceiling_Stained_1m` | Water/grime variation with no opening. |
| `MAT_Lab_Ceiling_Damaged_1m` | Rare broken/service-panel variant with alpha cutout; use only where geometry above the ceiling supports the opening. |

Lights and vents should form intentional architectural rhythms. A starting corridor rhythm can be `Base, Base, Light, Base, Vent, Base`, but it must be adapted to actual room length and lighting needs rather than stamped everywhere unchanged. Damaged and stained modules remain exceptions.

### Trim

`MAT_Lab_Trim_Dark` provides the dark charcoal/oxidized-metal bumper, cove, edge or service trim used to break large floor/wall fields and reinforce the facility construction language. Its generated texture is intended to cover roughly 2 m × 0.25 m per full UV repeat.

## Reusable surface-detail atlas

The generated `Lab_SurfaceDetails_Atlas_4x4` is one 2048 × 2048 RGBA atlas containing 16 reusable details so variation can come from local overlays without adding a unique PBR material for every stain. The generated manifest records each cell and UV rectangle.

Included cells cover:

- three vertical leak streaks;
- three lower-wall grime shapes;
- two paint-patch shapes;
- two floor-scuff groups;
- two old-spill ghosts;
- one worn safety-yellow hazard stripe;
- two faded service markings;
- one minor crack.

Keep story-specific marks separate. The Level 1 cage-to-vent scratches/blood trail remains unique authored storytelling and is not part of this generic atlas.

For Quest, prefer simple quad/mesh overlays or another measured lightweight solution rather than assuming large numbers of projected decals are free.

## Visual language

Use one coherent facility palette across Level 1 and reusable security/lab props:

- cold institutional gray / gray-green painted surfaces;
- dark charcoal and oxidized steel for exposed hardware;
- faded off-white ceiling panels;
- sparse faded safety-yellow accents on service markings;
- localized grime, scuffs, cleaning streaks, leaks and repair patches;
- restrained rust only where exposed steel plausibly oxidized.

The facility should feel maintained badly, not abandoned for decades.

## Anti-repetition placement rules

Target approximate Level 1 usage rather than equal random distribution:

- floor: 70–80% Standard, 15–25% Traffic, 5–10% Stained;
- walls: 70–80% Standard, 15–25% Worn, 5–10% Damp;
- ceiling: 65–75% Base A/B/C, 10–15% Light, 8–12% Vent, 3–8% Stained, very few Damaged modules.

These are art-placement starting ranges, not runtime randomization rules.

Use geometry as part of the material system. Door frames, corner trim, conduit, cable trays, service boxes, security cameras, vent grilles, cages, utility carts, pipes, room signs and warning placards should interrupt large texture fields. A believable prop or trim line often hides repetition more cheaply than another full PBR material.

Do not begin with an expensive custom anti-tiling shader. First test whether the 4 m base spans, regional variants, ceiling A/B/C rotation, overlays and environment props solve the problem. Add room-scale macro variation only if profiling and visual review show it is still needed.

## Level 1 placement plan

### Safe cage / containment room

- Floor: mostly `MAT_Lab_Floor_Standard`, with `Traffic` between spawn, cages, vent approach and exit; limited `Stained` near cage/service edges.
- Walls: `Standard` dominates; `Worn` around cages, door frames, lower corners and contact-heavy zones.
- Ceiling: organized Base A/B/C, Light and Vent rhythm; one stained/damaged service area at most if it supports the missing-subject story.
- Unique dressing: cage damage, subject trail, signs, conduit, security hardware and clutter do most of the storytelling.

### Hallways / approaches

- Floor: continuous Standard with long Traffic regions; avoid one-meter material alternation.
- Walls: long Standard runs broken by door frames, signs, service boxes and occasional Worn sections.
- Ceiling: strongest regular light/vent rhythm so the facility architecture feels designed rather than randomized.
- Use faded safety-yellow only on selected service/hazard markings.

### Keycard room

- Slightly cleaner than containment/maintenance so the room reads as a staffed/access-controlled workspace.
- Floor: mostly Standard with modest Traffic.
- Walls: Standard with fewer Worn/Damp patches.
- Ceiling: intact Base A/B/C, Light and Vent modules; use Damaged only if later story dressing justifies it.
- Keep the fixed card location visually readable against the environment.

### Vent maze

The 1 m office-style ceiling family is for lab rooms/halls, not the inside of the ventilation ducts. The vent maze keeps its own metal material family and should use seams, bends, panels, grime gradients, scratches and the local headlamp response to break repetition.

## Generated pack structure and Unity helper

The generated pack is structured under:

`Assets/RunawayChimps/Environment/Level1Lab/`

It includes `Textures`, `Decals`, `Materials` and `Editor` folders plus external documentation/previews in the packaged deliverable.

`CreateLevel1LabMaterials.cs` adds:

`Tools > Runaway Chimps > Create Level 1 Lab Materials`

The helper is intended to:

- mark normal maps as Normal Map textures;
- keep metallic/smoothness, occlusion and alpha data linear;
- set environment maps to Repeat and the detail atlas to Clamp;
- use 1024 as the environment-map maximum and 2048 for the atlas;
- set Android ASTC 6×6 as a starting Quest compression setting;
- create/update shared URP/Lit materials for the generated floor/wall/ceiling/trim families.

The helper has not been compiled in Unity yet, so its import/material behavior remains pending validation.

## Migration from current Level 1 assets

Current source evidence includes the legacy `Ceiling1` material using the `OfficeCeiling001` texture set and a `WhiteWall` material family using a wallpaper texture set. Treat those as legacy appearance sources.

Recommended integration sequence:

1. Copy/import the generated pack without replacing current scene materials yet.
2. Run the generated Editor helper and resolve any Unity 2022.3.55f1 import/compiler issue before scene changes.
3. Apply `Floor_Standard`, `Wall_Standard` and a small Base A/B/C ceiling mix to one contained test area and validate UV/world scale.
4. Add first floor/wall variants and ceiling Light/Vent modules by believable region.
5. Add a small overlay/detail pass and physical trim/utility props to break remaining repetition.
6. Check the result under the corrected Level 1 ambient/fill and local vent headlamp.
7. Profile on target Quest before adding shader complexity or more variants.
8. Only after visual/runtime approval replace or retire legacy `Ceiling1` / `WhiteWall` usage.

## Acceptance checklist

Do not call the material pass complete until:

- obvious repeating texture landmarks are not visible during a normal walk through major Level 1 spaces;
- adjacent variants look like the same facility and texel density rather than different asset packs;
- 4 m floor/wall spans and 1 m ceiling modules look physically believable in headset;
- lights and vents form intentional architectural rhythms rather than random placement;
- walls, route edges and vent entrances remain readable under horror lighting;
- the keycard and important interactions remain visually distinct;
- overlays do not visibly z-fight or create excessive overdraw;
- material/texture memory and frame time are measured on target Quest;
- no material-instance explosion is introduced by per-object runtime modifications;
- the generated Editor helper successfully imports/configures assets under Unity 2022.3.55f1 / URP.
