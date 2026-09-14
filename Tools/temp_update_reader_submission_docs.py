from pathlib import Path

sections = {
    "docs/design-and-lore.md": """## September 13 matching-reader objective submission correction

**Confirmed design correction:** presenting the correct color/symbol keycard to its matching physical reader is the Level 1 gameplay submission action. This supersedes the earlier visual-only reader boundary. Merely touching the legacy completion `KeyBox` must not count a Level 1 card. The reader's own LED remains immediate credential feedback, while the separate lock-status panel remains the persistent 0/2 -> 1/2 -> 2/2 objective display.

**Implemented on `feature/keycard-reader-light-feedback`:** a matching reader now resolves the presented gameplay `KeyCard` and routes acceptance through `KeyCard.TryInsertInto(KeyBox)`, which in turn preserves the existing `KeyBox.TryAddKey` local-player, active-scene, duplicate-card, completion, sector and travel guards. Successful acceptance keeps the existing consumed-card lifecycle, raises `KeyBox.ProgressChanged`, and therefore turns the next persistent lock-panel lamp green. The reader holds its green accepted light briefly after the accepted card is destroyed. A wrong color/symbol never submits. `KeyCard.OnTriggerEnter` now ignores the Level 1 travel-completion `KeyBox` directly, while older non-travel keyboxes retain their legacy direct-insertion path. Reader identity lookup also supports a reusable gameplay KeyCard wrapper whose imported colored/symbol card art is a child object.

**Pending validation:** Unity 2022.3.55f1 must still import/compile the branch and prove that the imported colored/symbol cards are used inside gameplay objects with `KeyCard`, XR grab, Collider/Rigidbody setup and the existing local-held lifecycle. Test every matching pair, preferably the full 4x4 wrong-card matrix, direct contact with the legacy completion KeyBox (must not count), 0/2 -> 1/2 -> 2/2 persistent panel progress, accepted-card destruction, the brief reader-green acknowledgement, Level 1 completion travel, two-client Photon behavior and headset/Quest readability. Do not mark this runtime-validated until those checks pass.
""",
    "docs/repository-improvement-plan.md": """## September 13 reader-driven keycard objective integration

**Confirmed correction:** the matching reader is now the authoritative physical submission point for Level 1 objective cards. This supersedes the prior branch boundary where reader matching was visual feedback only. The separate door lock panel still owns persistent required/accepted-card communication; the reader owns credential recognition and submission.

**Implemented source on `feature/keycard-reader-light-feedback`:** `KeyCard` now exposes `TryInsertInto(KeyBox)` so reader-driven and legacy accepted-card paths share one consume-on-success lifecycle. For the Level 1 travel-completion box, direct `KeyCard` trigger overlap no longer submits a card. `KeycardReaderLightController` resolves the presented credential from the collider/ancestor names or, for reusable gameplay wrappers, from the child visual hierarchy. On a matching credential it resolves the unique same-scene completion `KeyBox` (or an explicitly assigned box) and calls `TryInsertInto`; `KeyBox.TryAddKey` remains the authority for local-held, same-scene, duplicate, busy-sector and completion guards. Accepted cards are destroyed as before, the `ProgressChanged` event updates `KeycardLockProgressIndicator`, and the reader keeps a short accepted-green latch so destruction does not make the confirmation invisible. Wrong credentials never call the objective path. Matching visual art without a gameplay `KeyCard` logs a one-time warning rather than granting progress.

**Validation boundary:** the repository currently cannot prove the user's newly imported local colored-card setup until those assets/wrappers are present in the branch. In Unity 2022.3.55f1 verify each card used for gameplay has `KeyCard`, XR grab, Collider/Rigidbody behavior and a discoverable color/symbol identity in its root/child hierarchy. Then test all matching/wrong combinations, direct legacy-completion-box rejection, local-held enforcement, 0/2/1/2/2/2 panel persistence, card consumption, existing Level 1 travel, two-client Photon independence and headset/Quest presentation. Source implementation is complete; runtime validation remains pending.
""",
}

for raw_path, section in sections.items():
    path = Path(raw_path)
    text = path.read_text(encoding="utf-8")
    heading = section.splitlines()[0]
    if heading not in text:
        path.write_text(text.rstrip() + "\n\n" + section.rstrip() + "\n", encoding="utf-8")
