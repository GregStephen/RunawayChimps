from pathlib import Path

root = Path(__file__).resolve().parents[1]

design_path = root / "docs/design-and-lore.md"
plan_path = root / "docs/repository-improvement-plan.md"

marker = "## September 14 keycard reader review hardening"

design_section = r'''

## September 14 keycard reader review hardening

**Confirmed implementation correction:** reader-driven objective submission must be explicitly authored to the intended lock. A reusable reader must never search the whole scene for a unique `KeyBox`, because a future reader for another credential could otherwise advance the wrong objective. An Inspector-assigned `KeyBox` is valid; the existing Level 1 arrangement also counts as explicit authoring because its single completion `KeyBox` was deliberately reparented beneath the Amber Triangle reader when that reader replaced the old KeyBox housing. No reader may fall back to a scene-wide `KeyBox` search. This supersedes the earlier branch wording that allowed the reader to resolve a unique same-scene completion box.

**Credential identity correction:** the four authored reader variants now serialize their own credential identity (`AmberTriangle`, `CyanThreeBars`, `RedCircle`, and `VioletDiamond`) instead of depending on the reader GameObject name. Name parsing remains only a compatibility fallback for legacy/name-authored readers and for the current imported card art until gameplay cards receive a dedicated credential field.

**Implemented on `feature/keycard-reader-light-feedback`:** `KeycardReaderLightController` now accepts only an explicitly assigned objective or exactly one `KeyBox` nested beneath its reader root; it contains no scene-wide `KeyBox` discovery path. The four reader prefab variants serialize their credential enum values. `Tools/validate_keycard_reader_contracts.py` protects the no-global-binding rule, explicit variant identities, reader trigger/controller wiring, reader-driven Level 1 insertion boundary, progress event contract, and the authored Level 1 Amber-reader/KeyBox hierarchy. Source Integrity now runs this validator on pull requests.

**Pending validation:** the new contract must pass the pull-request Source Integrity workflow, then Unity 2022.3.55f1 must import/compile it and prove the real XR path: first Amber card -> 1/2, second distinct Amber card -> 2/2 and existing completion travel; wrong credentials and direct contact with the legacy completion KeyBox must not advance progress. Two-client Photon independence and Quest/headset visibility remain separate runtime checks.
'''

plan_section = r'''

## September 14 keycard reader review hardening

**Review finding resolved in source:** the reader objective path no longer treats "the unique completion KeyBox in this scene" as sufficient authorization. `KeycardReaderLightController` now requires either a serialized `KeyBox` reference or exactly one `KeyBox` deliberately nested beneath that reader root. The current Level 1 Amber reader already owns the reparented completion KeyBox hierarchy from the reader replacement work, so it retains the intended objective path without creating a scene-wide discovery dependency. A future Cyan/Red/Violet reader in Level 1 will remain visual-only unless its own objective is explicitly authored.

**Reader identity hardening:** `Reader_Amber_Triangle`, `Reader_Cyan_ThreeBars`, `Reader_Red_Circle`, and `Reader_Violet_Diamond` now serialize `expectedCredential` values 1-4 on the inherited reader controller. Renaming a reader scene instance therefore cannot silently change its credential. The string parser remains a fallback for legacy readers and for current imported card-art identity only.

**Automated source protection:** new `Tools/validate_keycard_reader_contracts.py` fails if scene-wide KeyBox discovery returns, if any reader variant loses its explicit credential, if the scan target stops being a trigger/controller host, if Level 1 direct completion-box insertion is restored, if the progress event contract disappears, or if the Level 1 completion KeyBox is no longer nested beneath the Amber reader hierarchy. `.github/workflows/source-validation.yml` now runs this check in the existing Source Integrity job.

**Validation boundary:** these review fixes are implemented source/serialization behavior, not Unity runtime proof. The branch still needs pull-request Source Integrity, Unity 2022.3.55f1 import/compile, all matching/wrong-card scans, 0/2 -> 1/2 -> 2/2 persistence, direct legacy-box rejection, card consumption, Level 1 travel, two-client Photon checks, and headset/Quest presentation before the feature is considered validated.
'''

for path, section in ((design_path, design_section), (plan_path, plan_section)):
    text = path.read_text(encoding="utf-8")
    if marker not in text:
        path.write_text(text.rstrip() + section + "\n", encoding="utf-8")
