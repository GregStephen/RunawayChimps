#!/usr/bin/env python3
"""Offline checks for first-party script binding and enabled-scene serialization.

These checks do not compile C# or replace Unity/Photon/headset tests.
Pass --syntax to additionally parse C# with tree-sitter and tree-sitter-c-sharp.
"""
import argparse
from collections import Counter
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def validate(root, syntax=False):
    errors = []
    scripts = sorted((root / 'Assets/Scripts').rglob('*.cs'))
    scripts += sorted((root / 'Assets/Resources/PhotonVR/Scripts').rglob('*.cs'))
    if not scripts:
        errors.append('No first-party scripts found.')
    guids = {}
    parser = None
    if syntax:
        import tree_sitter
        import tree_sitter_c_sharp
        parser = tree_sitter.Parser(tree_sitter.Language(tree_sitter_c_sharp.language()))
    for path in scripts:
        name = path.relative_to(root)
        source = path.read_text(encoding='utf-8-sig')
        classes = re.findall(r'\bclass\s+(\w+)\s*:\s*(?:MonoBehaviour(?:Pun(?:Callbacks)?)?|ScriptableObject)\b', source)
        if classes and path.stem not in classes:
            errors.append(f'{name}: filename does not match component class {classes}.')
        meta = path.with_suffix('.cs.meta')
        match = re.search(r'^guid: ([0-9a-f]+)$', meta.read_text(), re.M) if meta.exists() else None
        if match is None:
            errors.append(f'{name}: script metadata/GUID is missing.')
        elif match[1] in guids:
            errors.append(f'{name}: duplicate GUID with {guids[match[1]]}.')
        else:
            guids[match[1]] = name
        if parser and parser.parse(path.read_bytes()).root_node.has_error:
            errors.append(f'{name}: C# syntax parse failed.')
    build = (root / 'ProjectSettings/EditorBuildSettings.asset').read_text()
    scenes = re.findall(r'- enabled: 1\s+path: (.+)\s+guid: (\w+)', build)
    for path, count in Counter(path for path, _ in scenes).items():
        if count > 1:
            errors.append(f'{path}: registered {count} times in the enabled build scenes.')
    for path, guid in scenes:
        scene = root / path
        if not scene.exists():
            errors.append(f'{path}: enabled scene is missing.')
            continue
        meta = Path(str(scene) + '.meta')
        if not meta.exists() or f'guid: {guid}' not in meta.read_text():
            errors.append(f'{path}: Build Settings GUID does not match scene metadata.')
        text = scene.read_text()
        ids = re.findall(r'^--- !u!\d+ &(-?\d+)', text, re.M)
        duplicates = [value for value, count in Counter(ids).items() if count > 1]
        if duplicates:
            errors.append(f'{path}: duplicate local object IDs {duplicates[:5]}.')
        local_refs = set(re.findall(r'\{fileID: (-?\d+)\}', text)) - {'0'}
        missing = local_refs - set(ids)
        if missing:
            errors.append(f'{path}: unresolved local references {sorted(missing)[:10]}.')
    return scripts, scenes, errors


def main():
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument('--syntax', action='store_true')
    parsed = args.parse_args()
    scripts, scenes, errors = validate(ROOT, parsed.syntax)
    for error in errors:
        print('ERROR:', error)
    if errors:
        print(f'FAILED: {len(errors)} issue(s).')
        return 1
    print(f'PASS: {len(scripts)} scripts and {len(scenes)} enabled scenes; component names, metadata, build registration and local references.')
    if parsed.syntax:
        print('PASS: C# syntax parsing. Unity compilation and runtime checks remain separate.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
