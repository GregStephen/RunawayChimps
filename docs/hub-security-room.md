# Hub security room

Last updated: 2026-09-12.

This focused design note records the Hub security-room decisions while the room is being dressed. Status labels follow the repository convention: **Confirmed** is an approved design decision, **Proposed** is not yet approved, **Implemented** means an asset/code implementation exists, and **Pending validation** means Unity/headset/runtime proof is still required.

## Room identity

- **Confirmed:** The current empty Hub starting room is being built out as the facility security/control area. Existing Hub geometry and hallways remain the layout source; environmental dressing should fit the authored `Hub_Base` room rather than redesigning the room around props.
- **Confirmed:** The Level 1 hallway approach should use worn industrial floor lettering such as `SECTOR 01 — PRIMATE CONTAINMENT` with a directional arrow so the route reads diegetically.
- **Confirmed:** Include playful physical workplace props where they fit the environment. A rolling office chair and/or rolling utility cart are desired because chimps can shove, ride, spin, or otherwise play with them while the props still make sense in a security area.

## Security surveillance monitors

- **Confirmed:** Start with about four physical security monitors in the Hub security area, consistent with the existing surveillance-wall direction.
- **Confirmed:** Each physical monitor is reusable. The level/sector name is displayed **inside the screen content**, not printed permanently on the monitor bezel.
- **Confirmed:** When the game has more than four levels, the four physical monitors may rotate which levels they represent. The visible footage and the on-screen level label must switch together so the label always identifies the currently assigned level.
- **Confirmed:** Surveillance footage remains silent.
- **Confirmed:** The screen presentation should use grainy surveillance-style imagery. Keep the physical monitor simple/chunky and consistent with the game's rough low-poly facility aesthetic.
- **Confirmed implementation contract:** Keep the monitor's display face as a separate mesh/material target (`*_Screen_Surface`) so Unity can replace the picture/material/UI without modifying the physical monitor model.
- **Confirmed implementation direction:** Hub surveillance uses captured/recorded level imagery rather than keeping remote level scenes loaded solely to render live security feeds. Capture clean 4:3 images from authored in-level camera positions; apply grain/scanlines and dynamic sector text on the Hub monitor so the same physical screen can rotate to another level later.

### Display composition

Recommended screen composition:

1. level still/panning surveillance image or later prerecorded footage,
2. grain/scanline overlay,
3. dynamic sector/level text near the bottom of the image, for example `SECTOR 01 — PRIMATE CONTAINMENT`.

The level label should be data-driven from the screen's current assignment rather than baked into surveillance art.

## Implementation status

- **Asset prototype created outside the repository, 2026-09-12:** a reusable single-monitor model, a four-monitor 2×2 convenience layout, a Blender generator script, a grain overlay, a neutral static placeholder and a screen-layout guide were generated for review/import. The monitor uses a separate named screen surface.
- **Implemented on `feature/hub-surveillance-monitors`, pending validation:** `SecurityMonitorFeed` stores a sector label, captured still sequence and optional rare interrupt; `SecurityMonitorDisplay` drives one monitor's footage/grain/text UI; `SurveillanceWallController` completes a level's full view sequence before rotating that physical screen to another available feed and preserves each feed's loop state while off-screen; and **Tools > Runaway Chimps > Surveillance > Capture Still...** captures a clean 4:3 scene-camera PNG under `Assets/Art/Surveillance/Captures`.
- **Not yet authored in the Hub:** the generated monitor model is still external to the repository; no monitor prefab, world-space display Canvas, captured Level 1 stills, feed ScriptableObject assets, or `Hub_Base` monitor placement has been committed yet.
- **Pending validation:** Unity 2022.3.55f1 import/compile; capture-tool output; Blender monitor import/material setup; Hub wall placement; VR readability of the dynamic label; full-sequence and >4-feed rotation behavior; off-screen feed-state preservation; silent playback; and Quest texture/UI performance.

See [Hub surveillance monitor setup](surveillance-monitor-setup.md) for the source wiring and capture workflow.
