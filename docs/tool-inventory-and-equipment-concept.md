# Tool inventory, utility wheel, and multiplayer equipment concept

Last updated: 2026-09-12.

This document records a **proposed** Runaway Chimps tool/equipment system. It is not a confirmed control scheme or implemented inventory feature. The Level 1 vent headlamp is the first implemented gameplay utility effect, but its future inventory/wheel presentation and remote-player equipment model are still design work.

## Status summary

- **Confirmed / implemented for Level 1 prototype:** the local player gets a head-following vent light while in `Level1_Vents`. The runtime controller already separates `toolEquipped` from zone activation so a future inventory system can equip/stow it without rewriting the light logic.
- **Confirmed separation:** gameplay equipment should not be implemented by mutating cosmetic meshes. A top hat remains a top hat; equipping a headlamp must not simply add a lamp to the front of the hat model.
- **Proposed:** persistent tool inventory, a quick utility wheel, equipment mount points, remote-player equipment visuals, loadouts, unlock/persistence rules, and exact controller bindings.

## Why tools need a separate system from cosmetics

The existing Photon avatar already has cosmetic slots such as `Head` and `Face`. Those slots represent appearance choices. Gameplay tools have different requirements: they may turn on/off, produce gameplay effects, be required by a level, occupy a mount, need quick switching, and need authoritative/personal state.

Keep three layers separate:

1. **Cosmetics** — appearance only, such as a top hat, glasses, badge, or seasonal item.
2. **Equipment / utilities** — gameplay-capable tools such as a headlamp or future scanner.
3. **Gameplay effect** — the actual local light, scan, noise event, interaction ability, etc. The visual model and the effect should not be the same piece of state.

This prevents cosmetic conflicts and gives Quest/mobile VR a way to show equipment without forcing every remote visual effect to run at full cost.

## Proposed inventory + quick wheel

### Full inventory / loadout menu

Use a larger inventory surface in the Hub and safe rooms to browse tools the player has access to, read what they do, and choose a small active loadout. This can live on a wrist/palm panel or a simple world-space board/menu. Avoid forcing players to navigate a large menu during a chase.

The full inventory answers: **what tools do I have and what is in my loadout?**

### Quick utility wheel

Use a small radial wheel for moment-to-moment switching between the few tools in the active loadout. Target 3–5 wedges rather than a large RPG inventory.

Proposed interaction pattern:

- Hold one controller input to summon the wheel near the non-dominant hand/wrist.
- Point/select a wedge with thumbstick direction or controller direction.
- Release the button to equip the highlighted utility.
- A very short hold/release with no direction can return to the previous tool.

**Exact binding is open.** Do not take Grip/Trigger away from grabbing/locomotion. Source review did not find a Runaway-Chimps-specific face-button binding that is safe to claim for the wheel, so Quest controller mapping must be inspected and tested in-headset before choosing Y/B, thumbstick click, or another input.

The quick wheel answers: **what tool do I want active right now?**

## VR ergonomics

Runaway Chimps locomotion depends heavily on the player's hands, so tools should avoid making normal movement cumbersome.

Prefer utility mount types:

- `UtilityHead` — headlamp, goggles/scanner overlay, hearing/vision tools.
- `UtilityWrist` — scanner display, objective reader, compact map/status device.
- `Handheld` — temporary physical items that are worth occupying a hand, such as a throwable decoy or mission object.

A selected head/wrist utility can stay equipped while both hands remain free. A handheld utility should be easy to stow back into the wheel because occupying a hand affects locomotion and grabbing.

## Level-required tools and open level access

Runaway Chimps currently favors open level access. Do not create a situation where a player enters a level and discovers it is impossible because a required tool was missed elsewhere.

Proposed rule: if a level truly requires a utility, either:

- loan/auto-provision that utility for the level visit, or
- place/unlock it at the safe entry before it is required.

Discovery can still permanently add the tool to the player's collection later. This keeps tools interesting without turning open sector selection into accidental progression locks.

## Level 1 headlamp

### Implemented local effect

The current prototype auto-equips the headlamp and enables its real Spot Light only while the local player is in `Level1_Vents`. It follows the tracked XR camera/head, uses a roughly 46-degree beam, ~8 m range, cool-white color, no realtime shadows, and a short fade at vent/safe-room transitions.

The ambient/fill baseline remains readable without the headlamp. The lamp is intended to focus attention, improve junction readability, and make seeing the Crawler in a dark duct more dramatic—not to hide an otherwise unusable lighting setup.

### Future wheel behavior

Once the tool system exists, the current `VentHeadlampController.SetToolEquipped(bool)` path can be driven by the loadout/wheel. A reasonable first rule is:

- equipped + inside Level 1 vents → beam available/on;
- stowed or outside the vents → beam off.

Whether the player should also have a manual on/off toggle while it is equipped is still open. Avoid adding battery management until playtesting shows it adds meaningful tension rather than maintenance busywork.

## What other tools might be useful?

Keep the tool roster small. A good target is at most one genuinely new core utility concept per level, with reuse later.

Potential ideas, all **proposed**:

- **UV / inspection mode** — reveals laboratory markings, blood trails, maintenance codes, or hidden warnings. This could be a second mode of the headlamp instead of another physical item.
- **Wrist scanner** — reads experiment tags, powered equipment, symbols, or optional lore without occupying a locomotion hand.
- **Throwable noise decoy** — especially relevant to the Listener level, but it would materially change difficulty and AI behavior, so it needs dedicated design/testing before implementation.
- **Pry/maintenance tool** — opens a small set of noisy access panels or jammed containers in a future level. It should create a gameplay choice, not merely replace a button press.
- **Co-op beacon/ping utility** — makes it easier to signal a location to friends who are in the same sector without adding a full minimap.

Do **not** add tools simply because an inventory exists. Each tool should create a distinct decision, reveal, or risk.

## Multiplayer headlamp presentation

### Local illumination

Default Quest-friendly rule: **only the owning player's real headlamp Spot Light illuminates that player's client.** The light is attached to the local tracked camera and is not Photon-synchronized.

Benefits:

- one dynamic headlamp light per client instead of potentially ten;
- another player's equipment cannot wash out your horror lighting;
- no need to synchronize beam transforms every frame;
- lower Quest lighting cost and simpler visual consistency.

### What other players should see

Proposed remote presentation: other players see a small headlamp/utility housing attached to the remote avatar's `UtilityHead` anchor plus an emissive lens when active. A short fake cone/glow can be tested later, but it should not automatically be a real dynamic `Light`.

If co-op lighting from other players turns out to be important, test a capped solution later—for example only the nearest one or two remote beams—rather than enabling a realtime Spot Light for every player.

### Headlamp + top hat

A headlamp should **not** become part of the top-hat cosmetic mesh and should not replace the existing `Head` cosmetic slot.

Proposed avatar structure:

- `Head` cosmetic slot: top hat / seasonal hat / other appearance item.
- `UtilityHead` equipment anchor: headlamp or other gameplay head utility.
- Optional per-cosmetic mount override metadata: if a large hat blocks the default forehead position, move the utility to a temple/under-brim/fallback anchor.

For a top hat specifically, the likely presentation is a lamp/strap at the forehead or temple below/around the brim. The top hat itself stays unchanged. A special hat-with-built-in-lamp cosmetic could exist later, but that would be an intentionally authored variant rather than automatic behavior.

## Proposed network state

Do not overload the existing `Cosmetics` Photon property with gameplay equipment.

Future equipment state can use a separate personal property such as `Equipment` / `Utility`, containing only compact state needed for presentation and gameplay coordination, for example:

- equipped utility ID;
- mounted slot (`UtilityHead`, `UtilityWrist`, etc.);
- active/on state when other players need to see it.

Gameplay effects that matter to AI or objectives should continue to use explicit gameplay events/state rather than relying on whether a remote equipment mesh happens to be visible.

## Validation before confirming the tool wheel

1. Audit current Quest controller bindings and identify an input that does not conflict with Gorilla locomotion, grabbing, turn controls, menus, or existing interactions.
2. Prototype the wheel in-headset and test opening/selecting/stowing while stationary and while moving.
3. Verify it can be operated without accidental selections when arms are swinging for locomotion.
4. Test headlamp plus several representative head cosmetics, including a tall/top-hat shape, and define fallback utility anchors.
5. Test two clients: remote utility model state updates correctly while only the local real beam illuminates each client's scene.
6. Measure Quest frame time with one local spot light, remote emissive utility meshes, and any proposed fake beam presentation.
7. Only after those checks choose final control bindings, persistence/loadout rules, and remote beam presentation.
