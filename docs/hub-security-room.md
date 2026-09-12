# Hub security room

Last updated: 2026-09-12.

This focused design note records the Hub security-room decisions while the room is being dressed. Status labels follow the repository convention: **Confirmed** is an approved design decision, **Proposed** is not yet approved, **Implemented** means an asset/code implementation exists, and **Pending validation** means Unity/headset/runtime proof is still required.

## Room identity

- **Confirmed:** The current empty Hub starting room is being built out as the facility security/control area. Existing Hub geometry and hallways remain the layout source; environmental dressing should fit the authored `Hub_Base` room rather than redesigning the room around props.
- **Confirmed:** The Level 1 hallway approach should use worn industrial floor lettering such as `SECTOR 01 — PRIMATE CONTAINMENT` with a directional arrow so the route reads diegetically.
- **Confirmed:** Include playful physical workplace props where they fit the environment. A rolling office chair and/or rolling utility cart are desired because chimps can shove, ride, spin, or otherwise play with them while the props still make sense in a security area.

## Employee code-of-conduct wall poster

- **Confirmed:** Present the game's player code of conduct diegetically as an oversized laboratory employee-compliance poster rather than as a visually separate modern rules board.
- **Confirmed:** VR readability takes priority over dense workplace-poster realism. The sign should occupy a large portion of a wall, use very large high-contrast headings, icons, and short rule statements, and avoid paragraph-sized copy that requires the player to stand immediately in front of it.
- **Confirmed:** Keep the ordinary conduct rules sincere and functional, then use facility-specific containment language to tie the poster into the horror setting. The current prototype includes a prominent warning not to feed, taunt, release, or befriend containment subjects plus containment-failure instructions.
- **Confirmed implementation contract:** Keep the printed poster face as its own mesh/material target (`Poster_Print_Surface`) so readability/localization revisions can replace the texture without rebuilding the frame.
- **Prototype scale:** approximately 5.4 m wide × 3.04 m tall with a shallow wall-mounted frame. This is a review scale, not final until checked in the authored Hub and in-headset.

## Security surveillance monitors

- **Confirmed:** Start with about four physical security monitors in the Hub security area, consistent with the existing surveillance-wall direction.
- **Confirmed:** Each physical monitor is reusable. The level/sector name is displayed **inside the screen content**, not printed permanently on the monitor bezel.
- **Confirmed:** When the game has more than four levels, the four physical monitors may rotate which levels they represent. The visible footage and the on-screen level label must switch together so the label always identifies the currently assigned level.
- **Confirmed:** Surveillance footage remains silent.
- **Confirmed:** The screen presentation should use grainy surveillance-style imagery. Keep the physical monitor simple/chunky and consistent with the game's rough low-poly facility aesthetic.
- **Confirmed implementation contract:** Keep the monitor's display face as a separate mesh/material target (`*_Screen_Surface`) so Unity can replace the picture/material/UI without modifying the physical monitor model.

### Display composition

Recommended screen composition:

1. level still/panning surveillance image or later prerecorded footage,
2. grain/scanline overlay,
3. dynamic sector/level text near the bottom of the image, for example `SECTOR 01 — PRIMATE CONTAINMENT`.

The level label should be data-driven from the screen's current assignment rather than baked into surveillance art.

## Implementation status

- **Employee poster asset prototype created outside the repository, 2026-09-12:** generated a Unity/Blender-ready wall model (`RunawayChimps_EmployeeCodePoster.obj` + MTL), exact 4096 × 2304 poster texture, preview image, and Blender generator script. The poster uses a separate `Poster_Print_Surface` and a wall-contact-centered origin for easy placement. No `.blend` binary was generated in this environment; the included Blender script recreates the asset for saving/exporting in Blender.
- **Employee poster not yet integrated:** no poster model, texture, Unity material/prefab, or Hub scene placement has been committed to the repository from this pass.
- **Employee poster pending validation:** Unity 2022.3.55f1 import/material assignment; authored Hub wall fit; headset readability from normal approach distances; texture Max Size/Quest memory tradeoff; lighting/glare; and final scale. Do not treat the 5.4 m prototype size or 4K texture setting as final until those checks pass.
- **Security-monitor asset prototype created outside the repository, 2026-09-12:** a reusable single-monitor model, a four-monitor 2×2 convenience layout, a Blender generator script, a grain overlay, a neutral static placeholder and a screen-layout guide were generated for review/import. The monitor uses a separate named screen surface.
- **Security monitors not yet integrated:** no monitor asset, Unity prefab, display controller, level-assignment scheduler, surveillance image sequence or Hub scene placement has been committed to the repository from this design pass.
- **Security monitors pending validation:** Blender appearance/scale review; Unity 2022.3.55f1 import/material setup; Hub wall placement; VR readability of the dynamic label; screen rotation behavior; Quest performance; and confirmation that all monitor playback remains silent.
