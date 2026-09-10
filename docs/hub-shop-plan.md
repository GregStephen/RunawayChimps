# Hub cosmetic shop — planning proposal

Recorded: 2026-09-10. Status: **Confirmed requirements and reclaimed supply-room visual direction; proposed layout and checkout; no shop implementation in this change.**
Source review: main at `c388d87c0443158011dd9b1ba5ef9c005cc44267`, Unity 2022.3.55f1 and Photon PUN.

## Confirmed requirements

Greg wants the existing large Shop room in Hub_Base developed into a cosmetic shop, keeping the current room as the starting point. It already contains a mirror and currency board. Provide separate seasonal stock, regular stock that rotates weekly, and a place to try on and switch cosmetics. Cosmetic items themselves have not been designed.

Work in stages: planning, mapping/blockout, then creating assets and connecting systems. Gorilla Tag is the requested reference; improvements are welcome. This request brings shop planning into active work, superseding the blanket recommendation to leave shop expansion on the backlog. It does not establish that Level 1 validation is complete.

## Existing room and systems — source inspected, runtime unvalidated

- `Assets/Scenes/Hub_Base.unity` contains a top-level Shop with Mirror, ShopWallSign, ShopWalls, ShopFloors and ShopCieling.
- The serialized main room spans approximately world X=4.5–13.5, Z=-6–2, with ceiling at Y=3. Wall surfaces reduce the usable interior slightly. The west opening is approximately Z=-3–-1; a short approach extends west. These estimates come from scene mesh bounds, prefab transform overrides, and the LabWall prefab scale; they are not headset clearance measurements.
- Mirror is at X=13.427, Y=1, Z=-3.1, approximately 2 m wide along Z and 2 m tall. Its existing camera renders to a texture. Verify avatar visibility, fit, clipping and cost in Unity.
- ShopWallSign is at X=6.81, Y=1.022, Z=-5.871 and contains “Current Coconuts” plus a CoconutDisplay binding.
- EconomyState tracks Coconuts and owned item IDs. AuthOrchestrator fetches PlayFab inventory and calls an external GrantLoginCoconuts function. This confirms existing naming and client plumbing, not a verified production economy or a newly agreed currency rename.
- PhotonVRManager.SetCosmetics/SetCosmetic publish and persist immediately. They must not be used directly for temporary trials.
- PhotonVRPlayer refresh skips empty cosmetic values and does not clear omitted slots. Reliable remove/revert is a specific integration task.
- The reviewed economy directory contains state/display code. No production shop, rotation, purchase, or wardrobe implementation was established by this review. Do not infer complete purchasing from the presence of SDK APIs.
- The earlier R10 findings about identity, ownership, inventory pagination and external backend review remain relevant. No Unity, Play Mode, Photon, backend, or headset validations were run for this proposal.

## Reference and intended improvement

Another Axiom's [official Steam description](https://store.steampowered.com/app/1533390/Gorilla_Tag/) describes a City store with rotating inventory to buy, play with and wear. Use that physical, social shopping direction. The exact current Gorilla Tag checkout interface was not verified in this review; do not present our suggested flow as a reproduction of its current UI.

For Runaway Chimps, combine outfit preview, owned-item selection and purchase confirmation around the existing mirror. Players can inspect combinations in one place, while seasonal and weekly items remain visibly distinct in the room. Keep each player's selections personal so several people can browse together.

## Visual direction — supported by Greg on 2026-09-10

Greg supports the reclaimed lab supply-room look. The specific Behavioral Enrichment supply-room lore remains proposed: escaped gorillas have repurposed a lab storeroom. Use simple worn metal racks, supply crates, rough sign plates, broad silhouettes and matte materials. Retain the dark, restrained lab aesthetic. Give merchandise and the mirror steady, readable light, with darker room edges. Avoid flickering light on prices or outfit previews.

The shop name, supply-room lore and particular fixtures remain proposals. Coconuts is the existing implementation term; retain it while designing unless Greg chooses a change.

## Proposed zoning — first pass, subject to mapping review

| Area | Placement in current room | Function |
| --- | --- | --- |
| Seasonal | North wall, facing the main room | Start with 4 adaptable display positions and a replaceable event sign. Show the event's end date. When no event is active, use an honest “No seasonal collection” state. |
| Weekly rotation | South wall, east of the existing currency board | Start with 6 adaptable display positions, a clear next-change time, and the same selection for everyone. Inventory can grow without rebuilding the room. |
| Mirror / fitting | Existing east-wall mirror | A clear standing and arm-movement area, with trial selection and purchase review beside the mirror. |
| Owned cosmetics | East wall, just north of the mirror | A simple locker-shaped selector for owned items, clear/remove actions, and outfit equip. Try-on and owned selection share the fitting area. |
| Currency | Existing southwest board | Retain the board. Repeat the personal balance on purchase review so players need not turn away from their outfit to check affordability. |
| Centre and entrance | Keep open | Direct approach to fitting, passing space, and room for friends. Start without a central display island. |

Four seasonal and six weekly positions are proposed capacities, not a requirement to create ten cosmetics immediately. Final rack dimensions, spacing, reach, signs and placement belong to the mapping stage. Start with around 1.5 m clear primary routes where geometry allows, then test actual gorilla arm movement and seated/standing reach on headset.

## Proposed checkout fixture

Following Greg's question about purchasing and a possible cash register, recommend a small self-service purchase panel on a worn lab counter beside the mirror. Use a simple metal box, readable item/price/balance display, and large CONFIRM PURCHASE / CANCEL controls. Avoid adding a detailed register mechanism just to make the purchase readable.

A player selects an item, tries it on if desired, then reviews that selected item at checkout. Show its price, current Coconuts and the remaining balance before confirmation. Only confirmation initiates spending; successful purchase updates ownership and balance, shows OWNED, and offers EQUIP. Trial selection does not require purchase. Keep checkout state personal even when several players use the same fixture.

Coconuts are deducted by the purchase service; players do not hand over physical cash or scan an objective keycard. Fixture styling and this interaction flow remain Proposed. No production purchase functionality is implemented by this document update.

## Proposed player flow

1. Browse a physical display showing the item, name, Coconuts price, and clear TRY ON / OWNED state.
2. Press TRY ON to apply a temporary cosmetic while inside the Shop. A new item in the same slot replaces the previous trial in that slot. Compatible slots can be combined.
3. At the mirror, switch among selected items or OWNED cosmetics. Mark trials distinctly and offer RESTORE OUTFIT.
4. Review a selected unowned item beside the mirror. Show item, price, current balance and remaining balance; use a separate CONFIRM PURCHASE action. Browsing or trying on never spends currency.
5. On a confirmed successful purchase, add ownership and offer EQUIP. Owned items remain available after their sale rotation ends. A failed request preserves the current outfit and explains the result.
6. Leaving the Shop ends unowned trials and restores the last deliberately equipped owned outfit. Sector travel, disconnect and reload must not preserve unowned trials.

Initial recommendation: purchase one selected item at a time, while allowing multiple compatible trial items. This keeps the first implementation understandable. Friends seeing temporary outfits inside the Shop is a proposed social feature; preview visibility must be distinguished from permanent ownership and must end outside the Shop. Saved outfit presets can be considered later.

## Build stages and acceptance

| Stage | Deliverable | Exit condition |
| --- | --- | --- |
| 1 — Planning | This proposal and project-document updates | Greg settles the room identity, display direction and try-on flow. All new suggestions remain Proposed until then. |
| 2 — Mapping | A scaled top-down layout and Unity blockout placed in the actual Hub; views from entrance and mirror | Verify room boundaries, sightlines, entrance clearance, multi-player standing space and seated/standing reach. Review the layout before finished fixture art. |
| 3 — Fixtures and trial prototype | Modular seasonal/weekly displays, signs and locker/console; temporary cosmetic placeholders | One item per test slot can be previewed, combined, removed and restored; leaving the Shop restores the saved outfit. |
| 4 — Integration | Catalog, schedule, purchase and wardrobe logic connected to existing economy/avatar systems | Successful ownership persists, balance refreshes, removal works, and other players see the intended permanent/trial states. |
| 5 — Validation | Recorded Unity, backend, two-client Photon and headset results | Validate the cases below before describing the shop as ready. |

Implementation direction, still proposed: data-driven catalog entries map stable cosmetic IDs to prefab, slot, display name, price/offer and availability. A trusted service controls active offers, weekly rollover and seasonal dates. Keep catalog scheduling separate from scene furniture. Use the existing PlayFab economy/account infrastructure after inspecting its backend configuration; do not create a separate local-only wallet.

A purchase must validate active offer, trusted price, balance and ownership, with atomic spend/grant and duplicate-request protection. Retry after a timeout reconciles the prior transaction instead of spending again. Inventory refresh must include all pages. Temporary preview state stays separate from owned inventory and saved equipment; Photon properties represent appearance, not proof of ownership.

Targeted acceptance:
- Select, replace, clear and combine supported slots; local mirror and remote avatar agree.
- Restore the last equipped outfit after trials, Shop exit, scene travel and disconnect/rejoin.
- Buy once, refresh balance/ownership, restart and equip again; owned rotated-out items stay in wardrobe.
- Repeated presses, lost responses, insufficient balance and an offer expiring during review do not create duplicate charges or stale-price purchases.
- Two players at the same display/selector have independent balances, selections and purchases.
- Weekly/seasonal rollover is consistent for clients and unaffected by headset-clock changes.
- Verify large text, comfortable physical controls, no hand snagging, clear entrance and acceptable mirror/display frame time on the target Quest.
