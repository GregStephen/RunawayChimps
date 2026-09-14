#!/usr/bin/env python3
"""Source-level contracts for the reusable keycard reader/progress system.

This deliberately validates serialization and wiring that can be proven without opening
Unity. It does not claim XR trigger delivery, runtime physics, Photon behavior, travel,
or headset presentation.
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


def main() -> int:
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
        "gameplayCard.TryInsertInto(keyBox)" in controller,
        "Matching readers must submit through the shared KeyCard acceptance lifecycle.",
    )

    keycard = read("Assets/Scripts/KeyCard.cs")
    require(
        "if (!box.travelToLevelTwoOnComplete)" in keycard and "TryInsertInto(box);" in keycard,
        "Level 1 completion must remain reader-driven while legacy non-travel KeyBoxes keep direct insertion.",
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

    print("PASS: keycard reader source contracts are intact.")
    print("PASS: gameplay readers do not perform scene-wide KeyBox discovery.")
    print("PASS: all four reader variants serialize explicit credential identities.")
    print("PASS: Level 1 completion KeyBox remains authored beneath the Amber reader hierarchy.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
