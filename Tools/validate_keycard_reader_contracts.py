#!/usr/bin/env python3
"""Source-level contracts for the reusable keycard reader/progress system.

Source Integrity runs this validator on pull requests. It deliberately checks only
serialization and wiring that can be proven without opening Unity; it does not claim
XR trigger delivery, runtime physics, Photon behavior, travel, or headset presentation.
"""

from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8-sig")


def asset_guid(path: str) -> str:
    meta = read(path + ".meta")
    match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", meta, re.MULTILINE)
    if not match:
        raise AssertionError(f"Missing Unity GUID for {path}")
    return match.group(1)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def unity_blocks(text: str) -> list[str]:
    return [block for block in re.split(r"(?=^--- !u!)", text, flags=re.MULTILINE) if block.startswith("--- !u!")]


def block_file_id(block: str, unity_type: int) -> int | None:
    match = re.match(rf"--- !u!{unity_type} &(\d+)(?: stripped)?\s*$", block.splitlines()[0])
    return int(match.group(1)) if match else None


def block_game_object_id(block: str) -> int | None:
    match = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
    return int(match.group(1)) if match else None


def main() -> int:
    credential_path = "Assets/Scripts/KeycardCredential.cs"
    credential_source = read(credential_path)
    expected_credentials = {
        "Auto": 0,
        "AmberTriangle": 1,
        "CyanThreeBars": 2,
        "RedCircle": 3,
        "VioletDiamond": 4,
    }
    for name, value in expected_credentials.items():
        require(
            re.search(rf"\b{name}\s*=\s*{value}\b", credential_source) is not None,
            f"Shared KeycardCredential must keep {name}={value} for serialized compatibility.",
        )

    keycard_path = "Assets/Scripts/KeyCard.cs"
    keycard = read(keycard_path)
    keycard_guid = asset_guid(keycard_path)
    require(
        "[SerializeField] private KeycardCredential credential" in keycard
        and "public KeycardCredential Credential => credential;" in keycard,
        "Gameplay KeyCard must expose an explicit serialized credential identity.",
    )
    require(
        "if (!box.travelToLevelTwoOnComplete)" in keycard and "TryInsertInto(box);" in keycard,
        "Level 1 completion must remain reader-driven while legacy non-travel KeyBoxes keep direct insertion.",
    )

    controller_path = "Assets/Scripts/KeycardReaderLightController.cs"
    controller = read(controller_path)
    reader_script_guid = asset_guid(controller_path)

    require(
        "FindObjectsOfType<KeyBox>" not in controller and "FindObjectsByType<KeyBox>" not in controller,
        "Reader gameplay submission must not discover KeyBoxes scene-wide.",
    )
    require(
        "autoBindUniqueKeyBoxInScene" not in controller,
        "Reader gameplay submission must not restore the old unique-scene KeyBox fallback.",
    )
    require(
        "readerRoot.GetComponentsInChildren<KeyBox>(true)" in controller,
        "Reader may only fall back to the deliberately nested authored KeyBox hierarchy.",
    )
    require(
        "public void BindObjective(KeyBox source)" in controller,
        "Reader must retain an explicit objective-binding API.",
    )
    require(
        "gameplayCard.Credential" in controller,
        "Reader matching must prefer the gameplay card's explicit credential identity.",
    )
    require(
        "pendingGameplayCards" in controller
        and "RetryPendingSubmissions();" in controller
        and "TrySubmitPendingCard" in controller
        and "HasMatchingOverlap" in controller,
        "Reader must retry a matching card while it remains physically inside the scan zone.",
    )
    require(
        "gameplayCard.TryInsertInto(keyBox)" in controller,
        "Matching readers must submit through the shared KeyCard acceptance lifecycle.",
    )

    keybox = read("Assets/Scripts/KeyBox.cs")
    require(
        "public event Action<int, int> ProgressChanged;" in keybox
        and "ProgressChanged?.Invoke(CurrentKeys, RequiredKeys);" in keybox,
        "KeyBox must publish accepted-card progress for the separate lock indicator.",
    )

    base_path = "Assets/RunawayChimps/Environment/KeycardReaders/Prefabs/Reader_Base.prefab"
    base_guid = asset_guid(base_path)
    base = read(base_path)
    require("m_Name: ScanTarget_FRONT" in base, "Reader_Base must keep its authored scan target.")
    require("m_IsTrigger: 1" in base, "Reader_Base scan target must remain a trigger.")
    require(
        f"m_Script: {{fileID: 11500000, guid: {reader_script_guid}, type: 3}}" in base,
        "Reader_Base scan target must keep KeycardReaderLightController.",
    )

    variant_expectations = {
        "Reader_Amber_Triangle.prefab": 1,
        "Reader_Cyan_ThreeBars.prefab": 2,
        "Reader_Red_Circle.prefab": 3,
        "Reader_Violet_Diamond.prefab": 4,
    }
    prefab_dir = Path("Assets/RunawayChimps/Environment/KeycardReaders/Prefabs")
    for filename, expected_value in variant_expectations.items():
        variant = read((prefab_dir / filename).as_posix())
        pattern = re.compile(
            rf"- target: \{{fileID: 114500, guid: {re.escape(base_guid)}, type: 3\}}\s*"
            rf"propertyPath: expectedCredential\s*value: {expected_value}\s*"
            r"objectReference: \{fileID: 0\}",
            re.MULTILINE,
        )
        require(
            pattern.search(variant) is not None,
            f"{filename} must serialize expectedCredential={expected_value} instead of relying on its object name.",
        )

    indicator_path = "Assets/Scripts/KeycardLockProgressIndicator.cs"
    indicator = read(indicator_path)
    indicator_guid = asset_guid(indicator_path)
    require(
        "FindObjectsOfType<KeyBox>" not in indicator and "FindObjectsByType<KeyBox>" not in indicator,
        "Lock progress panels must not discover KeyBoxes scene-wide.",
    )
    require(
        "GetComponentInParent<KeyBox>()" in indicator and "public void Bind(KeyBox source)" in indicator,
        "Lock progress panels must use explicit/runtime binding or an authored parent KeyBox only.",
    )

    indicator_prefab_path = (
        "Assets/RunawayChimps/Environment/KeycardReaders/Prefabs/Door_Keycard_Lock_Indicator.prefab"
    )
    indicator_prefab = read(indicator_prefab_path)
    require(
        f"m_Script: {{fileID: 11500000, guid: {indicator_guid}, type: 3}}" in indicator_prefab,
        "Door keycard lock indicator prefab must keep KeycardLockProgressIndicator.",
    )
    require(
        "previewRequiredKeys: 2" in indicator_prefab,
        "Door keycard lock indicator prefab must preview the current two-card Level 1 requirement.",
    )
    require(
        "autoBindUniqueKeyBoxInScene" not in indicator_prefab,
        "Door keycard lock indicator prefab must not retain the old scene-wide auto-binding flag.",
    )
    for renderer_id in (230100, 230200, 230300, 230400):
        require(
            f"- {{fileID: {renderer_id}}}" in indicator_prefab,
            "Door keycard lock indicator prefab must keep four reusable progress-lamp renderer references.",
        )

    standby_guid = asset_guid(
        "Assets/RunawayChimps/Environment/KeycardReaders/Materials/LED_Standby_Amber.mat"
    )
    accepted_guid = asset_guid(
        "Assets/RunawayChimps/Environment/KeycardReaders/Materials/LED_Accepted_Green.mat"
    )
    require(
        f"standbyMaterial: {{fileID: 2100000, guid: {standby_guid}, type: 2}}" in indicator_prefab
        and f"acceptedMaterial: {{fileID: 2100000, guid: {accepted_guid}, type: 2}}" in indicator_prefab,
        "Door keycard lock indicator must keep the authored amber/green materials.",
    )

    # The current Level 1 reader was authored by replacing the old KeyBox housing and
    # deliberately reparenting the objective beneath the Amber reader. Prove that this
    # structural binding still exists so removing scene-wide discovery cannot break Level 1.
    scene = read("Assets/Scenes/Level1_Containment.unity")
    blocks = unity_blocks(scene)
    keybox_guid = asset_guid("Assets/Scripts/KeyBox.cs")
    objective_blocks = [
        block
        for block in blocks
        if f"m_Script: {{fileID: 11500000, guid: {keybox_guid}, type: 3}}" in block
        and "travelToLevelTwoOnComplete: 1" in block
    ]
    require(len(objective_blocks) == 1, "Level 1 must contain exactly one travel-completion KeyBox objective.")

    game_object_match = re.search(r"m_GameObject: \{fileID: (\d+)\}", objective_blocks[0])
    require(game_object_match is not None, "Could not resolve the Level 1 completion KeyBox GameObject.")
    objective_go = int(game_object_match.group(1))

    objective_transform = next(
        (
            block
            for block in blocks
            if block_file_id(block, 4) is not None
            and f"m_GameObject: {{fileID: {objective_go}}}" in block
        ),
        None,
    )
    require(objective_transform is not None, "Could not resolve the Level 1 completion KeyBox Transform.")

    parent_match = re.search(r"m_Father: \{fileID: (\d+)\}", objective_transform)
    require(parent_match is not None and int(parent_match.group(1)) != 0, "Level 1 completion KeyBox must be nested beneath its reader.")
    parent_id = int(parent_match.group(1))
    parent_block = next((block for block in blocks if block_file_id(block, 4) == parent_id), None)
    require(parent_block is not None, "Could not resolve the authored parent of the Level 1 completion KeyBox.")

    amber_variant_guid = asset_guid((prefab_dir / "Reader_Amber_Triangle.prefab").as_posix())
    require(
        amber_variant_guid in parent_block,
        "Level 1 completion KeyBox must remain structurally nested beneath the Amber Triangle reader instance.",
    )

    # The two currently active Level 1 cards are scene-authored Amber Triangle FBX instances.
    # Their KeyCard components must serialize identity directly rather than relying on object names.
    scene_card_blocks = [
        block
        for block in blocks
        if f"m_Script: {{fileID: 11500000, guid: {keycard_guid}, type: 3}}" in block
    ]
    require(
        len(scene_card_blocks) == 2,
        "Level 1 must keep exactly two scene-authored gameplay KeyCard components for its two-card objective.",
    )

    xr_grab_guid = "0ad34abafad169848a38072baa96cdb2"
    for card_block in scene_card_blocks:
        require(
            "credential: 1" in card_block,
            "Each active Level 1 gameplay card must explicitly serialize AmberTriangle credential=1.",
        )
        card_go = block_game_object_id(card_block)
        require(card_go is not None, "Could not resolve a Level 1 gameplay card GameObject.")
        same_object_blocks = [block for block in blocks if block_game_object_id(block) == card_go]
        require(
            any(block_file_id(block, 54) is not None for block in same_object_blocks),
            "Each Level 1 gameplay card must keep a Rigidbody for reader trigger delivery.",
        )
        require(
            any(
                block_file_id(block, 65) is not None and "m_IsTrigger: 0" in block and "m_Enabled: 1" in block
                for block in same_object_blocks
            ),
            "Each Level 1 gameplay card must keep an enabled solid BoxCollider.",
        )
        require(
            any(
                f"m_Script: {{fileID: 11500000, guid: {xr_grab_guid}, type: 3}}" in block
                for block in same_object_blocks
            ),
            "Each Level 1 gameplay card must keep XRGrabInteractable.",
        )

    print("PASS: keycard reader source contracts are intact.")
    print("PASS: gameplay readers and progress panels do not perform scene-wide KeyBox discovery.")
    print("PASS: reader variants and active Level 1 cards serialize explicit credential identities.")
    print("PASS: matching cards retry submission while they remain inside the reader scan zone.")
    print("PASS: Level 1 card Rigidbody/collider/XR-grab source wiring is intact.")
    print("PASS: Level 1 completion KeyBox remains authored beneath the Amber reader hierarchy.")
    print("PASS: Unity 2022.3.55f1, XR/physics, Photon, travel, and headset validation remain separate.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
