#!/usr/bin/env python3
"""Unity repository hygiene checks that do not require launching Unity."""
from collections import defaultdict
from pathlib import Path
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"


def rel(path):
    return path.relative_to(ROOT).as_posix()


def read(path):
    return path.read_text(encoding="utf-8-sig")


def main():
    errors = []
    guid_paths = defaultdict(list)
    meta_count = 0

    for meta in ASSETS.rglob("*.meta"):
        if not meta.is_file():
            continue
        meta_count += 1
        match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", read(meta), re.M)
        if not match:
            errors.append(f"{rel(meta)}: missing 32-character Unity GUID.")
        else:
            guid_paths[match.group(1).lower()].append(meta)
        target = Path(str(meta)[:-5])
        if not target.exists():
            errors.append(f"{rel(meta)}: orphaned .meta; target is missing.")

    for guid, paths in guid_paths.items():
        if len(paths) > 1:
            errors.append(f"duplicate Unity GUID {guid}: " + ", ".join(rel(p) for p in paths))

    missing_meta = []
    for asset in ASSETS.rglob("*"):
        if not asset.is_file() or asset.suffix == ".meta" or asset.name.startswith("."):
            continue
        if not Path(str(asset) + ".meta").exists():
            missing_meta.append(rel(asset))
    for path in missing_meta[:50]:
        errors.append(f"{path}: Unity asset is missing its .meta file.")
    if len(missing_meta) > 50:
        errors.append(f"Assets: {len(missing_meta) - 50} additional files are missing .meta files.")

    folded = defaultdict(list)
    for root_name in ["Assets", "Packages", "ProjectSettings", "Tools", ".github", "docs"]:
        root = ROOT / root_name
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if path.is_file():
                name = rel(path)
                folded[name.casefold()].append(name)
    for paths in folded.values():
        names = sorted(set(paths))
        if len(names) > 1:
            errors.append("case-insensitive path collision: " + ", ".join(names))

    json_files = [ROOT / "Packages/manifest.json", ROOT / "Packages/packages-lock.json"]
    json_files += sorted(ASSETS.rglob("*.asmdef"))
    json_files += sorted(ASSETS.rglob("*.asmref"))
    json_files += sorted((ROOT / "Tools").rglob("*.json"))
    for path in json_files:
        if not path.exists():
            errors.append(f"{rel(path)}: required JSON file is missing.")
            continue
        try:
            json.loads(read(path))
        except json.JSONDecodeError as exc:
            errors.append(f"{rel(path)}: invalid JSON: {exc}.")

    for error in errors:
        print("ERROR:", error)
    if errors:
        print(f"FAILED: {len(errors)} repository integrity issue(s).")
        return 1

    print(f"PASS: {meta_count} Unity metadata files have unique GUIDs and valid asset pairing.")
    print("PASS: no case-only path collisions; package/asmdef/tool JSON parses.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
