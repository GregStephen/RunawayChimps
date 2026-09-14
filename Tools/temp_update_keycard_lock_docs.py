from pathlib import Path

sections = {
    "docs/design-and-lore.md": """## September 13 separate keycard lock-progress indicator

**Confirmed design correction:** keep the physical keycard and reader variants reusable rather than building special one-card/two-card reader bodies. The reader's existing single status LED remains short-lived scan feedback: amber at standby, green while the matching color/symbol card is physically in its scan zone, then amber again after removal. Required-card count and persistent completion progress belong to a separate physical lock-status panel mounted above or near the reader/door. This supersedes the proposed idea of adding multiple persistent progress lamps directly to every reader.

For the current Level 1 completion lock, the separate panel represents the existing two-card requirement: two amber lamps at 0/2, one green plus one amber at 1/2, and two green at 2/2. Accepted progress remains green after the accepted card is removed/destroyed because the panel reflects the objective's accepted-card state rather than scan overlap. Wrong/unaccepted cards do not advance the panel. The same asset should support future locks with different card counts without creating different keycard or reader art variants.

**Implemented on `feature/keycard-reader-light-feedback`:** `KeyBox` now exposes `CurrentKeys`, normalized `RequiredKeys`, and a `ProgressChanged` event without changing its existing two-card Level 1 completion/travel rules. New `KeycardLockProgressIndicator` renders that progress using authored amber/green materials, and new prefab `Assets/RunawayChimps/Environment/KeycardReaders/Prefabs/Door_Keycard_Lock_Indicator.prefab` provides a compact four-slot reusable physical panel. The prefab previews two required lamps, hides unused slots, auto-binds only when exactly one same-scene `KeyBox` exists, and can be explicitly bound when a scene contains multiple locks. No runtime material or shader is created.

**Pending validation / placement:** the reusable asset and source behavior are implemented, but the exact Level 1 wall placement/orientation above the imported reader has not been authored in the scene on this branch. In Unity 2022.3.55f1, place the prefab above/near the intended reader, verify it binds the Level 1 completion `KeyBox`, then test 0/2, 1/2, and 2/2 persistence, wrong-card non-progression, Level 1 travel, two-client personal progress, readability at Gorilla scale, and Quest/headset presentation. Do not mark the panel runtime-validated until those checks pass.
""",
    "docs/repository-improvement-plan.md": """## September 13 separate keycard lock-progress panel

**Confirmed design correction:** do not create separate one-card/two-card keycard-reader art variants merely to communicate the number of credentials a door needs. Keep the imported keycards and color/symbol readers reusable. The reader's one amber/green LED remains transient card-match feedback; a distinct physical lock-status panel near the door owns requirement/progress communication. For Level 1, its two lamps show amber/amber -> green/amber -> green/green as the existing `KeyBox` accepts its two required local personal cards. This supersedes the proposed multi-progress-light reader-body approach.

**Implemented source/asset on `feature/keycard-reader-light-feedback`:** `KeyBox` now exposes read-only `CurrentKeys`/`RequiredKeys` state and raises `ProgressChanged` after an accepted card. New `Assets/Scripts/KeycardLockProgressIndicator.cs` subscribes to that state, activates only the number of lamp slots required, and keeps already accepted slots green. `Door_Keycard_Lock_Indicator.prefab` is a small built-in-cube housing with four available lamp slots, default two-slot preview, and the existing serialized `LED_Standby_Amber` / `LED_Accepted_Green` materials. It does not mutate card ownership, card consumption, travel, Photon ownership, or the reader's match logic. Automatic source binding is deliberately limited to a unique same-scene `KeyBox`; scenes with multiple locks must bind explicitly.

**Validation boundary:** the prefab has not yet been positioned in `Level1_Containment.unity`; exact placement is intentionally left for Unity visual authoring rather than guessing wall coordinates in serialized YAML. Unity 2022.3.55f1 must import/compile the new script/prefab, then the Level 1 instance should be placed above/near the intended reader and checked at 0/2, 1/2, and 2/2. Verify accepted-card persistence, wrong-card non-progression, successful existing Level 1 completion travel, personal/two-client behavior, no material instancing/pink shaders, Gorilla-scale readability, and Quest/headset presentation.
""",
}

for raw_path, section in sections.items():
    path = Path(raw_path)
    text = path.read_text(encoding="utf-8")
    heading = section.splitlines()[0]
    if heading not in text:
        path.write_text(text.rstrip() + "\n\n" + section.rstrip() + "\n", encoding="utf-8")
