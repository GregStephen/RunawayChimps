# Level 1 environment material plan

Last updated: 2026-09-12.

This document records the approved direction for replacing Level 1's generic/repeating lab surfaces with a small coordinated material system that fits Runaway Chimps' aged institutional-laboratory aesthetic while remaining practical for Quest and Unity 2022.3.55f1 / URP.

## Status

- **Confirmed direction:** do not cover Level 1 with one obvious repeating wall, floor, or ceiling texture. Use small surface families plus sparse local variation.
- **Confirmed direction:** the lab should look functional, institutional, old and neglected rather than futuristic, pristine, or post-apocalyptic. Dirt, wear, patching and aging are preferred over heavy rust everywhere.
- **Confirmed direction:** Level 1 must remain dark/horror but readable. Materials must preserve enough value contrast for walls, junctions, route edges and the Crawler to read under the current lighting baseline and local vent headlamp.
- **Confirmed geometry fact:** the existing Level 1 ceiling modules are 1 m × 1 m. New ceiling-module textures should treat one UV 0–1 span as one 1 m module at material tiling 1 × 1 rather than copying the legacy `Ceiling1` material's 0.25 × 0.25 tiling.
- **Planned, not implemented:** the material names, variant counts, placement rules, decal families and migration sequence below.
- **Pending validation:** Unity import, in-scene scale/UV inspection, Play Mode/headset readability, two-client visual consistency where relevant, and Quest memory/GPU/frame-time measurements.

## Visual language

Use one coherent facility palette across Level 1 and reusable security/lab props:

- cold institutional gray / gray-green painted surfaces;
- dark charcoal and oxidized steel for exposed hardware;
- faded off-white ceiling panels;
- sparse faded safety-yellow accents on service markings, not as a dominant wall/floor color;
- localized grime, scuffs, cleaning streaks, leaks and repair patches;
- restrained rust only where exposed steel plausibly oxidized.

The facility should feel maintained badly, not abandoned for decades.

## Material families

### Floors

Create three URP/Lit materials that share the same overall hue and texel density so they can meet without looking like three different buildings.

| Planned material | Purpose | Starting use |
| --- | --- | --- |
| `MAT_Lab_Floor_Standard` | Main sealed industrial concrete / worn epoxy. Medium-dark cool gray-green, rough, subtle scratches and old sealer variation. | Most cage-room, hallway and lab floor area. |
| `MAT_Lab_Floor_Traffic` | Same base floor with broader polished/scuffed traffic wear and slightly different grime distribution. | Long hall centers, routes between doors, around computer/work areas and frequently traveled paths. |
| `MAT_Lab_Floor_Stained` | Same base floor with stronger local staining, old spill ghosts and dirt accumulation but no baked-in story-specific blood. | Corners, maintenance/service areas, cage-adjacent dirty zones and selected dead ends. |

Do not alternate these in a checkerboard. Change material by believable region: a traffic strip can run for several meters; a stained region belongs near a leak, cage, drain, equipment cluster or neglected corner.

### Walls

Create three coordinated wall materials. These replace the visual role of the current generic `WhiteWall` wallpaper-based surface rather than preserving its wallpaper look.

| Planned material | Purpose | Starting use |
| --- | --- | --- |
| `MAT_Lab_Wall_Standard` | Primary painted institutional concrete/plaster. Muted cold gray-green, subtle roller texture, mild vertical cleaning marks. | Majority of Level 1 walls. |
| `MAT_Lab_Wall_Worn` | Same paint family with more chips, abrasion, lower-wall grime and patch repairs. | Cage-room walls, corners, door approaches, maintenance areas. |
| `MAT_Lab_Wall_Damp` | Same family with restrained vertical damp/leak staining and discoloration. | Only where ceiling leaks, pipes, vents or damaged utilities justify it. |

Long wall runs should not swap A/B/A/B every meter. Use one base for the architectural run, then break repetition with trim, props, decals and occasional believable surface changes.

### Ceiling

The existing Level 1 ceiling geometry is modular at 1 m × 1 m, so use true one-module variants. The ceiling needs architectural rhythm, not random noise.

| Planned material/module | Purpose | Starting use |
| --- | --- | --- |
| `MAT_Lab_Ceiling_Base` | Aged off-white institutional panel and dark frame. | Most modules. |
| `MAT_Lab_Ceiling_Light` | Matching module with approximately 0.70 × 0.38 m fluorescent diffuser and emission mask. | Repeating practical-light positions; pair with actual/baked lighting as appropriate. |
| `MAT_Lab_Ceiling_Vent` | Matching module with approximately 0.56 × 0.34 m dark louver grille. | Regular HVAC/service rhythm. |
| `MAT_Lab_Ceiling_Stained` | Base module with water/grime variation but no hole. | Near damp walls, pipes and neglected rooms. |
| `MAT_Lab_Ceiling_Damaged` | Rare broken/service-panel variant that may expose simple pipes/cables above. | One-off focal areas; not repeated frequently. |

A starting corridor rhythm can be `Base, Base, Light, Base, Vent, Base`, but it should be adapted to actual room length and lighting needs instead of stamped everywhere unchanged. Damaged and stained modules are exceptions, not part of the regular rhythm.

## Anti-repetition layers

### 1. Base material distribution

Target approximate Level 1 usage rather than equal random distribution:

- floor: 70–80% Standard, 15–25% Traffic, 5–10% Stained;
- walls: 70–80% Standard, 15–25% Worn, 5–10% Damp;
- ceiling: 65–75% Base, 10–15% Light, 8–12% Vent, 3–8% Stained, very few Damaged modules.

These are art-placement starting ranges, not runtime randomization rules.

### 2. Decal / overlay family

Use sparse decals or simple overlay meshes for unique local storytelling instead of baking every event into a repeating surface texture.

Planned reusable decal families:

- `DECAL_Leak_Vertical_01..03`
- `DECAL_Grime_LowerWall_01..03`
- `DECAL_PaintPatch_01..03`
- `DECAL_Floor_Scuff_01..03`
- `DECAL_Floor_Spill_01..03`
- `DECAL_HazardStripe_Worn`
- `DECAL_ServiceMarking_01..03`
- `DECAL_Crack_Minor_01..03`

Keep story-specific marks separate: the Level 1 cage-to-vent scratches/blood trail should remain unique authored storytelling, not part of the generic reusable grime set.

For Quest, prefer simple quad/mesh overlays or another measured lightweight solution over assuming a large number of expensive projected decals is free.

### 3. Geometry breaks

Use existing/new environment props to interrupt large texture fields:

- door frames and corner trim;
- wall conduit and cable trays;
- electrical/service boxes;
- security cameras;
- vents and grilles;
- cage structure;
- utility carts / lab furniture;
- pipes and ceiling service pieces;
- room/sector signs and warning placards.

A prop or trim line often hides repetition more cheaply and convincingly than adding another full PBR material.

### 4. Macro variation

If a lightweight URP solution is later justified by profiling, add broad room-scale color/grime variation that changes over several meters rather than one tile. Do not start by creating a custom expensive shader. First prove that texture-family placement plus overlays is insufficient.

## Level 1 placement plan

### Safe cage / containment room

- Floor: mostly `MAT_Lab_Floor_Standard`, with `Traffic` between spawn, cages, vent approach and exit; limited `Stained` near cage/service edges.
- Walls: `Standard` dominates; `Worn` around cages, door frames, lower corners and contact-heavy zones.
- Ceiling: organized Base/Light/Vent rhythm; one stained/damaged service area at most if it supports the missing-subject story.
- Unique dressing: cage damage, subject trail, signs, conduit, security hardware and clutter do most of the storytelling.

### Hallways / approaches

- Floor: continuous Standard with long Traffic bands; avoid one-meter material alternation.
- Walls: long Standard runs broken by door frames, signs, service boxes and occasional Worn sections.
- Ceiling: strongest regular light/vent rhythm so the facility architecture feels designed rather than randomized.
- Use faded safety-yellow only on selected service/hazard markings.

### Keycard room

- Slightly cleaner than the containment/maintenance areas so the room reads as a staffed/access-controlled workspace.
- Floor: mostly Standard, modest Traffic.
- Walls: Standard with fewer Worn/Damp patches.
- Ceiling: intact Base/Light/Vent modules; damaged module only if later story dressing justifies it.
- Keep the fixed card location visually readable against the environment.

### Vent maze

The 1 m office-style ceiling system is for lab rooms/halls, not the inside of the ventilation ducts. Vents should retain their own metal material family and use seams, bends, panels, grime gradients, scratches and the local headlamp response to break repetition. Do not paste ceiling-panel materials inside the vent maze.

## Texture and material budget targets

Starting target for Quest prototype:

- 1024 × 1024 maps for 1 m ceiling modules and comparable close-view environment surfaces unless testing proves 2K necessary;
- Base Color + Normal + packed Metallic/Smoothness where useful; Occlusion only where it materially improves the result;
- reuse maps/material assets broadly rather than creating a unique material instance per mesh;
- use URP/Lit first; avoid parallax/displacement and unnecessary transparent layers on environment surfaces;
- use emission only on actual light fixtures/indicators, not on ordinary ceiling panels;
- preserve GPU instancing/static batching opportunities by sharing materials and avoiding per-object property changes without need.

Final compression/import settings must be checked on Android/Quest, not assumed from desktop appearance.

## Migration from current Level 1 assets

Current source evidence includes the legacy `Ceiling1` material using the `OfficeCeiling001` texture set and a `WhiteWall` material family using a wallpaper texture set. Treat those as legacy appearance sources.

Recommended migration sequence:

1. Import/create the new surface texture families without replacing current materials yet.
2. Create the planned shared URP/Lit materials under one Level 1/facility environment folder.
3. Apply `Floor_Standard`, `Wall_Standard` and `Ceiling_Base` to one contained test area and validate UV/world scale.
4. Add the first floor/wall variants and ceiling Light/Vent modules by believable region.
5. Add a small decal/overlay pass and physical trim/utility props to break remaining repetition.
6. Check the result under the corrected Level 1 ambient/fill plus local vent headlamp.
7. Profile on target Quest before adding shader complexity or more variants.
8. Only after visual/runtime approval replace or retire the legacy `Ceiling1` / `WhiteWall` usage.

## Acceptance checklist

Do not call the material pass complete until:

- obvious repeating texture landmarks are not visible during a normal walk through major Level 1 spaces;
- adjacent variants look like the same facility and texel density rather than different asset packs;
- the 1 m ceiling module scale is physically believable in headset;
- lights and vents form intentional architectural rhythms rather than random placement;
- walls, route edges and vent entrances remain readable under horror lighting;
- the keycard and important interactions remain visually distinct;
- decals/overlays do not visibly z-fight or create excessive overdraw;
- material/texture memory and frame time are measured on target Quest;
- no material-instance explosion is introduced by per-object runtime modifications.
