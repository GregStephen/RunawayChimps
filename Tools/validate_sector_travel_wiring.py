#!/usr/bin/env python3
"""Static regression checks for issue #64 sector-travel scene wiring.

This validates committed Unity YAML only. Unity import, the Editor Sector Travel
validator, physical hand contact and headset travel remain separate runtime checks.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
BOOT = ROOT / "Assets/Scenes/Bootstrap.unity"
HUB = ROOT / "Assets/Scenes/Hub_Base.unity"
TAGS = ROOT / "ProjectSettings/TagManager.asset"

STALE_ZONE_GUID = "4bf5b5c1f296d6f40b9e1625cbdd493f"


def guid(path: str) -> str:
    text = (ROOT / path).read_text(encoding="utf-8-sig")
    match = re.search(r"^guid:\s*([0-9a-f]{32})$", text, re.MULTILINE)
    if not match:
        raise AssertionError(f"{path}: GUID not found")
    return match.group(1)


ZONE_STATE_GUID = guid("Assets/Scripts/Zones/ZoneStateService.cs.meta")
ZONE_DEBUG_GUID = guid("Assets/Scripts/Zones/ZoneDebugHUD.cs.meta")
LOCAL_RIG_GUID = guid("Assets/Scripts/PlayerScripts/LocalRigMarker.cs.meta")
PHYSICAL_BUTTON_GUID = guid("Assets/Scripts/Utils/PhysicalButton.cs.meta")
SECTOR_DOOR_GUID = guid("Assets/Scripts/Travel/SectorDoor.cs.meta")
LEGACY_BUTTON_GUID = guid("Assets/Scripts/Travel/LegacyHubButtonMarker.cs.meta")


def docs(text: str):
    result = {}
    for match in re.finditer(r"^--- !u!(\d+) &(-?\d+)\n", text, re.MULTILINE):
        start = match.start()
        next_match = re.search(r"^--- !u!\d+ &-?\d+\n", text[match.end():], re.MULTILINE)
        end = match.end() + next_match.start() if next_match else len(text)
        result[match.group(2)] = (int(match.group(1)), text[start:end])
    return result


def game_objects(parsed):
    result = {}
    for file_id, (type_id, body) in parsed.items():
        if type_id != 1:
            continue
        name = re.search(r"^  m_Name:\s*(.*)$", body, re.MULTILINE)
        result[file_id] = {
            "name": name.group(1) if name else "",
            "body": body,
            "components": re.findall(r"- component: \{fileID: (-?\d+)\}", body),
        }
    return result


def script_guid(body: str):
    match = re.search(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}", body)
    return match.group(1) if match else None


def transform_maps(parsed):
    go_to_transform = {}
    transforms = {}
    for file_id, (type_id, body) in parsed.items():
        if type_id != 4:
            continue
        go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
        father = re.search(r"m_Father: \{fileID: (-?\d+)\}", body)
        children = re.findall(r"- \{fileID: (-?\d+)\}", body)
        if not go:
            continue
        transforms[file_id] = {
            "go": go.group(1),
            "father": father.group(1) if father else "0",
            "children": children,
        }
        go_to_transform[go.group(1)] = file_id
    return transforms, go_to_transform


def named_go(objects, name):
    matches = [(fid, value) for fid, value in objects.items() if value["name"] == name]
    if len(matches) != 1:
        raise AssertionError(f"expected exactly one {name!r}, found {len(matches)}")
    return matches[0]


def component_docs(parsed, obj):
    return [(cid, parsed[cid][0], parsed[cid][1]) for cid in obj["components"] if cid in parsed]


def descendants(root_go, transforms, go_to_transform):
    root_transform = go_to_transform.get(root_go)
    if not root_transform:
        return []
    pending = list(transforms[root_transform]["children"])
    result = []
    while pending:
        transform_id = pending.pop(0)
        transform = transforms.get(transform_id)
        if not transform:
            continue
        result.append(transform["go"])
        pending.extend(transform["children"])
    return result


def has_ancestor_script(go_id, wanted_guid, parsed, objects, transforms, go_to_transform):
    transform_id = go_to_transform.get(go_id)
    while transform_id and transform_id != "0":
        transform = transforms.get(transform_id)
        if not transform:
            return False
        parent_transform_id = transform["father"]
        if parent_transform_id == "0":
            return False
        parent = transforms.get(parent_transform_id)
        if not parent:
            return False
        parent_go = objects.get(parent["go"])
        if parent_go:
            for _, type_id, body in component_docs(parsed, parent_go):
                if type_id == 114 and script_guid(body) == wanted_guid:
                    return True
        transform_id = parent_transform_id
    return False


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def validate():
    boot_text = BOOT.read_text(encoding="utf-8-sig")
    hub_text = HUB.read_text(encoding="utf-8-sig")
    tag_text = TAGS.read_text(encoding="utf-8-sig")

    require(STALE_ZONE_GUID not in boot_text,
            "Bootstrap still serializes the orphaned ZoneSystem MonoBehaviour from #64.")

    boot = docs(boot_text)
    boot_objects = game_objects(boot)
    boot_transforms, boot_go_to_transform = transform_maps(boot)

    _, zone_system = named_go(boot_objects, "ZoneSystem")
    zone_scripts = [
        script_guid(body)
        for _, type_id, body in component_docs(boot, zone_system)
        if type_id == 114
    ]
    require(zone_scripts == [ZONE_STATE_GUID, ZONE_DEBUG_GUID],
            f"ZoneSystem scripts changed: {zone_scripts!r}")

    layer_lines = re.search(r"^  layers:\n((?:  -.*\n)+)", tag_text, re.MULTILINE)
    require(layer_lines is not None, "TagManager layers block not found.")
    layers = [line[4:] for line in layer_lines.group(1).splitlines()]
    require(len(layers) > 29 and layers[25:30] == [
                "Left Hand", "Right Hand", "NonCollidable", "Trigger", "FingerTip"],
            "Layers 25-29 must remain Left Hand, Right Hand, NonCollidable, Trigger, FingerTip.")

    for name in ("LeftFingerCollider", "RightFingerCollider"):
        go_id, finger = named_go(boot_objects, name)
        require(re.search(r"^  m_Layer: 29$", finger["body"], re.MULTILINE) is not None,
                f"{name} must remain on FingerTip layer 29.")
        require(re.search(r"^  m_TagString: HandTag$", finger["body"], re.MULTILINE) is not None,
                f"{name} must remain tagged HandTag.")
        collider_bodies = [
            body for _, type_id, body in component_docs(boot, finger)
            if type_id in (64, 65, 135, 136)
        ]
        require(any("m_Enabled: 1" in body and "m_IsTrigger: 1" in body for body in collider_bodies),
                f"{name} needs an enabled trigger collider.")
        require(has_ancestor_script(
                    go_id, LOCAL_RIG_GUID, boot, boot_objects,
                    boot_transforms, boot_go_to_transform),
                f"{name} must stay under the local rig marker.")

    hub = docs(hub_text)
    hub_objects = game_objects(hub)
    hub_transforms, hub_go_to_transform = transform_maps(hub)
    root_id, _ = named_go(hub_objects, "StartLevel1Button")
    child_ids = descendants(root_id, hub_transforms, hub_go_to_transform)

    physical_matches = []
    for go_id in child_ids:
        obj = hub_objects.get(go_id)
        if not obj:
            continue
        for cid, type_id, body in component_docs(hub, obj):
            if type_id == 114 and script_guid(body) == PHYSICAL_BUTTON_GUID:
                physical_matches.append((go_id, obj, cid, body))
    require(len(physical_matches) == 1,
            f"StartLevel1Button must contain exactly one PhysicalButton, found {len(physical_matches)}.")

    button_go_id, button_go, _, button = physical_matches[0]
    require(button_go["name"] == "ButtonTrigger", "PhysicalButton must remain on the ButtonTrigger object.")
    require("requireLocalRig: 1" in button, "Level 1 button must require the local rig.")
    require("requiredTag: HandTag" in button and "requireTag: 1" in button,
            "Level 1 button must require HandTag.")
    require(re.search(r"pressLayers:\n\s+serializedVersion: 2\n\s+m_Bits: 536870912", button) is not None,
            "Level 1 button must filter to FingerTip layer 29.")
    require("m_MethodName: Travel" in button, "Level 1 button must invoke SectorDoor.Travel.")

    target = re.search(r"m_Target: \{fileID: (-?\d+)\}", button)
    require(target is not None and target.group(1) in hub, "Level 1 button travel target is unresolved.")
    target_type, target_body = hub[target.group(1)]
    require(target_type == 114 and script_guid(target_body) == SECTOR_DOOR_GUID,
            "Level 1 button target must be a SectorDoor.")
    require("destinationScene: Level1_Containment" in target_body,
            "Level 1 button must target the Level 1 SectorDoor.")

    button_scripts = [
        script_guid(body)
        for _, type_id, body in component_docs(hub, button_go)
        if type_id == 114
    ]
    require(PHYSICAL_BUTTON_GUID in button_scripts, "ButtonTrigger lost PhysicalButton.")
    require(LEGACY_BUTTON_GUID in button_scripts,
            "Legacy fieldless Hub component must remain explicitly bound, not Missing (Mono Script).")

    marker_source = (ROOT / "Assets/Scripts/Travel/LegacyHubButtonMarker.cs").read_text(encoding="utf-8-sig")
    require("sealed class LegacyHubButtonMarker : MonoBehaviour" in marker_source,
            "Legacy Hub button GUID must bind to the inert compatibility marker.")

    print("PASS: #64 committed sector-travel wiring is structurally intact.")
    print("PASS: both fingertips are local HandTag/FingerTip triggers and the Hub button remains local-only -> SectorDoor.Travel.")
    print("PASS: Bootstrap has no orphaned ZoneSystem script and the Hub legacy component has an explicit inert binding.")
    print("NOTE: Unity 2022.3.62f3 Sector Travel validation and headset button/travel checks remain pending.")


if __name__ == "__main__":
    try:
        validate()
    except AssertionError as exc:
        print(f"ERROR: {exc}")
        sys.exit(1)
