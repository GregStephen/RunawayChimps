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


def validate_bootstrap_pickup() -> None:
    blocks = unity_blocks(read("Assets/Scenes/Bootstrap.unity"))
    hands = [block for block in blocks
             if "guid: 4253f32900bcc4d499d675566142ded0" in block]
    require(len(hands) == 2, "Bootstrap must retain its two direct hand interactors.")
    for hand in hands:
        owner = block_game_object_id(hand)
        colliders = [block for block in blocks
                     if block_game_object_id(block) == owner
                     and re.match(r"--- !u!(?:65|135|136|64) ", block)]
        require(len(colliders) == 1 and block_file_id(colliders[0], 135) is not None,
                "Accurate direct-hand detection requires one same-object SphereCollider.")
        radius = float(re.search(r"m_Radius:\s*([\d.]+)", colliders[0]).group(1))
        require(0.02 <= radius <= 0.10,
                "Direct-hand acquisition must stay close to the hand, not use the old 0.60 m sphere.")
        require(not any(block_game_object_id(block) == owner and block_file_id(block, 54) is not None
                        for block in blocks),
                "A same-object Rigidbody silently disables XRI's accurate sphere-query mode.")
        require("m_ImproveAccuracyWithSphereCollider: 1" in hand
                and "m_PhysicsTriggerInteraction: 2" in hand,
                "Direct-hand queries must include trigger-only card acquisition colliders.")
        mask = re.search(r"m_PhysicsLayerMask:\s*serializedVersion:\s*\d+\s*m_Bits:\s*(\d+)", hand)
        require(mask is not None and int(mask.group(1)) & 1,
                "Direct-hand queries must include the card affordance's Default layer.")


def main() -> int:
    validate_bootstrap_pickup()
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
        "if (!box.RequiresMatchingReader)" in keycard and "TryInsertInto(box);" in keycard,
        "Level 1 completion must remain reader-driven while legacy non-travel KeyBoxes keep direct insertion.",
    )
    require(
        "[RequireComponent(typeof(BoxCollider), typeof(Rigidbody), typeof(XRGrabInteractable))]" in keycard
        and "[DefaultExecutionOrder(-200)]" in keycard
        and "ConfigurePhysicalCard();" in keycard
        and "FitColliderToVisualBounds(physicalCollider);" in keycard,
        "Gameplay KeyCard must require its physical/grab components and normalize its collider before ordinary physics scripts run.",
    )
    require(
        "physicalMassKg = 0.05f" in keycard
        and "colliderPaddingMeters = 0.002f" in keycard
        and "minimumColliderThicknessMeters = 0.006f" in keycard,
        "Gameplay KeyCard must retain the lightweight prop and thin-card collider defaults from runtime testing.",
    )
    require(
        "GetComponentsInChildren<Renderer>(true)" in keycard
        and "renderer.localBounds" in keycard
        and "renderer.transform.TransformPoint" in keycard
        and "renderer.bounds" not in keycard
        and "transform.InverseTransformPoint(worldCorner)" in keycard,
        "KeyCard collider fitting must derive from the actual rendered card bounds rather than a fixed metre-scale cube.",
    )
    require(
        "RigidbodyInterpolation.Interpolate" in keycard
        and "CollisionDetectionMode.ContinuousDynamic" in keycard,
        "Gameplay keycards must retain interpolated continuous Rigidbody handling for the thin moving prop.",
    )
    require(
        'GrabAffordanceObjectName = "Keycard_GrabAffordance"' in keycard
        and "grabPaddingMeters = 0.035f" in keycard
        and "minimumGrabThicknessMeters = 0.06f" in keycard
        and "grabAffordanceCollider.isTrigger = true;" in keycard
        and "grab.colliders.Clear();" in keycard
        and "grab.colliders.Add(grabAffordanceCollider);" in keycard,
        "Gameplay keycards must keep a generous trigger-only XR grab affordance separate from their tight solid physics collider.",
    )

    held_item_path = "Assets/Scripts/HeldItemCollisionMode.cs"
    held_item = read(held_item_path)
    held_item_guid = asset_guid(held_item_path)
    require(
        "[DisallowMultipleComponent]" in held_item,
        "HeldItemCollisionMode must not allow duplicate collision-state handlers on one prop.",
    )
    require(
        "XROrigin origin = player != null ? player.GetComponentInParent<XROrigin>() : null;" in held_item,
        "Held props must resolve the local Gorilla XROrigin before changing collision pairs.",
    )
    require(
        "GetComponentsInChildren<Collider>(true)" in held_item
        and "origin.GetComponentsInChildren<Collider>(true)" in held_item,
        "Held prop safety must compare the held object's colliders against the complete local rig collider hierarchy.",
    )
    require(
        "Physics.IgnoreCollision(itemCollider, rigCollider, true);" in held_item
        and "Physics.IgnoreCollision(pair.Item, pair.Rig, false);" in held_item,
        "Held props must ignore only local-rig collision pairs while selected and restore those pairs on release/disable.",
    )
    require(
        "RestoreRigCollisionsWhenSeparated()" in held_item
        and "Physics.ComputePenetration(" in held_item,
        "Held props must delay local-rig collision restoration until a released prop is physically separated.",
    )
    require(
        "RestoreLocalRigCollisions();" in held_item and "RestoreLayers();" in held_item,
        "Held prop release/disable must restore collision pairs and original layers.",
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
        "gameplayCard.TryInsertFromReader(keyBox, this)" in controller,
        "Matching readers must submit through the shared KeyCard acceptance lifecycle.",
    )
    require(
        controller.count("other == null || other.isTrigger") >= 2,
        "Reader enter/exit handling must ignore trigger-only grab affordances and scan only the physical card collider.",
    )

    keybox = read("Assets/Scripts/KeyBox.cs")
    require(
        "public event Action<int, int> ProgressChanged;" in keybox
        and "NotifyProgressChanged();" in keybox
        and "callback(acceptedKeys, requiredKeys);" in keybox
        and "listeners.GetInvocationList()" in keybox,
        "KeyBox must publish accepted-card progress for the separate lock indicator.",
    )

    base_path = "Assets/RunawayChimps/Environment/KeycardReaders/Prefabs/Reader_Base.prefab"
    base_guid = asset_guid(base_path)
    base = read(base_path)
    require("m_Name: ScanTarget_FRONT" in base, "Reader_Base must keep its authored scan target.")
    require("m_IsTrigger: 1" in base, "Reader_Base scan target must remain a trigger.")
    require(
        "m_LocalPosition: {x: 0, y: 0.155, z: 0.095}" in base
        and "m_Size: {x: 0.18, y: 0.16, z: 0.13}" in base,
        "Reader_Base must keep the enlarged forward scan envelope proven necessary by runtime reachability feedback.",
    )
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

    # Level 1 also serializes an older disabled legacy card. Validate the two active
    # Amber gameplay instances by their stable KeyCard component file IDs; Unity's
    # editor validator separately resolves activeInHierarchy after importing the scene.
    active_card_component_ids = (768804180, 2117548410)
    scene_card_blocks = []
    for component_id in active_card_component_ids:
        card_block = next(
            (
                block
                for block in blocks
                if block_file_id(block, 114) == component_id
                and f"m_Script: {{fileID: 11500000, guid: {keycard_guid}, type: 3}}" in block
            ),
            None,
        )
        require(card_block is not None, f"Missing Level 1 gameplay KeyCard component {component_id}.")
        scene_card_blocks.append(card_block)

    xr_grab_guid = "0ad34abafad169848a38072baa96cdb2"
    for card_block in scene_card_blocks:
        require(
            "credential: 1" in card_block,
            "Each Level 1 gameplay card must explicitly serialize AmberTriangle credential=1.",
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
            "Each Level 1 gameplay card must keep an enabled solid BoxCollider that KeyCard normalizes to its visual bounds at startup.",
        )
        require(
            any(
                f"m_Script: {{fileID: 11500000, guid: {xr_grab_guid}, type: 3}}" in block
                for block in same_object_blocks
            ),
            "Each Level 1 gameplay card must keep XRGrabInteractable.",
        )
        require(
            any(
                f"m_Script: {{fileID: 11500000, guid: {held_item_guid}, type: 3}}" in block
                for block in same_object_blocks
            ),
            "Each Level 1 gameplay card must keep HeldItemCollisionMode so held props cannot push the local Gorilla rig.",
        )

    print("PASS: keycard reader source contracts are intact.")
    print("PASS: gameplay readers and progress panels do not perform scene-wide KeyBox discovery.")
    print("PASS: reader variants and Level 1 gameplay cards serialize explicit credential identities.")
    print("PASS: matching cards retry submission while they remain inside the reader scan zone.")
    print("PASS: gameplay cards auto-fit their colliders to rendered bounds and use lightweight continuous Rigidbody handling.")
    print("PASS: dropped cards retain a trigger-only XR grab affordance and readers ignore that proxy for scanning.")
    print("PASS: reader scan geometry keeps the enlarged forward reach envelope.")
    print("PASS: held gameplay cards ignore local-rig collider pairs and restore them only after separation on release.")
    print("PASS: Level 1 card Rigidbody/collider/XR-grab/held-safety source wiring is intact.")
    print("PASS: Level 1 completion KeyBox remains authored beneath the Amber reader hierarchy.")
    print("PASS: Unity 2022.3.55f1, XR/physics, Photon, travel, and headset validation remain separate.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
