#!/usr/bin/env python3
"""Validate real serialized board wiring; Unity import/physics/font rendering is separate."""
from pathlib import Path, PurePosixPath, PureWindowsPath
import ast
import re
import json
import struct
import uuid

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets'
path = ASSETS / 'Resources/SocialSafety/PlayerBoard.prefab'
text = path.read_text()
blocks = {i: (kind, body) for kind, i, body in re.findall(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)}
ids = re.findall(r'^--- !u!\d+ &(\d+)', text, re.M)
assert len(ids) == len(set(ids)), 'duplicate prefab IDs'
assert not (set(re.findall(r'\{fileID: (\d+)\}', text)) - {'0'} - set(ids)), 'dangling prefab local reference'
guids = {}
for meta in ASSETS.rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', meta.read_text(), re.M)
    if match:
        guids[match[1]] = meta
packages = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']
# TextMeshPro.cs lives in the pinned package, outside Assets. This is its GUID
# already used by the project's authored computer labels.
package_guids = {'9541d86e2fd84c1d9990edf0852d74ab'} if 'com.unity.textmeshpro' in packages else set()
for guid in set(re.findall(r'guid: (\w+)', text)):
    assert guid.startswith('0000000000000000') or guid in guids or guid in package_guids, f'missing dependency {guid}'
board_guid = re.search(r'guid: (\w+)', (ASSETS / 'Scripts/SocialSafety/PlayerBoard.cs.meta').read_text())[1]
button_guid = re.search(r'guid: (\w+)', (ASSETS / 'Scripts/SocialSafety/PlayerBoardButton.cs.meta').read_text())[1]
board = next((i, body) for i, (kind, body) in blocks.items() if kind == '114' and f'guid: {board_guid},' in body)
assert len(re.findall(r'^  - root:', board[1], re.M)) == 5, 'five paged rows required'
buttons = [body for kind, body in blocks.values() if kind == '114' and f'guid: {button_guid},' in body]
assert len(buttons) == 20, f'expected 20 wired controls, found {len(buttons)}'
for body in buttons:
    assert f'  board: {{fileID: {board[0]}}}' in body, 'button must bind board'
    go = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)[1]
    components = re.findall(r'component: \{fileID: (\d+)\}', blocks[go][1])
    assert any(blocks[c][0] == '65' for c in components), 'button needs physical trigger'
    label = re.search(r'label: \{fileID: (\d+)\}', body)[1]
    assert blocks[label][0] == '114' and 'm_isRichText: 0' in blocks[label][1], 'plain text label required'
colliders = [body for kind, body in blocks.values() if kind == '65']
assert len(colliders) == 20 and all('m_IsTrigger: 1' in c for c in colliders), 'board must not block locomotion'
assert any(kind == '54' and 'm_IsKinematic: 1' in body and 'm_UseGravity: 0' in body for kind, body in blocks.values()), 'trigger rigidbody required'
physics = (ROOT / 'ProjectSettings/DynamicsManager.asset').read_text()
raw = re.search(r'm_LayerCollisionMatrix: (\w+)', physics)[1]
layers = struct.unpack('<32I', bytes.fromhex(raw))
for hand in [24, 25, 28]:
    assert layers[0] & (1 << hand), f'board Default layer must contact hand layer {hand}'
prefab_guid = re.search(r'guid: (\w+)', Path(str(path) + '.meta').read_text())[1]
placement = (ASSETS / 'Resources/SocialSafety/PlayerBoardHub.prefab').read_text()
assert placement.count(f'm_SourcePrefab: {{fileID: 100100000, guid: {prefab_guid}, type: 3}}') == 1, 'exactly one nested authored Hub board'
placement_ids = re.findall(r'^--- !u!\d+ &(\d+)', placement, re.M)
assert len(placement_ids) == len(set(placement_ids)), 'duplicate placement IDs'
assert not (set(re.findall(r'\{fileID: (\d+)\}', placement)) - {'0'} - set(placement_ids)), 'dangling placement reference'
scene = (ASSETS / 'Scenes/Hub_Base.unity').read_text()
assert 'm_Name: SpawnRoom\n' in scene, 'Hub placement anchor must exist'
# Script/resource lookup must resolve an actual prefab, not an editor-only factory.
shortcut = (ASSETS / 'Scripts/SocialSafety/PlayerBoardShortcut.cs').read_text()
assert 'Resources.Load<PlayerBoard>("SocialSafety/PlayerBoard")' in shortcut

# Execute only the pure GUID helper: importing the authoring script would rewrite
# assets. Rebuilding on Windows must preserve the IDs referenced by the Hub wrapper.
authoring_path = ROOT / 'Tools/PlayerBoard/build_player_board.py'
authoring = ast.parse(authoring_path.read_text())
helper = next(n for n in authoring.body if isinstance(n, ast.FunctionDef) and n.name == 'guid')
scope = {'uuid': uuid}
exec(compile(ast.Module(body=[helper], type_ignores=[]), str(authoring_path), 'exec'), scope)
authored_assets = [path.parent, path, path.with_name('PlayerBoardHub.prefab')]
authored_assets += [path.with_name(name + '.mat') for name in ('Frame', 'Screen', 'Button', 'Report', 'Row')]
for asset in authored_assets:
    relative = asset.relative_to(ROOT).as_posix()
    expected = re.search(r'^guid: (\w+)', Path(str(asset) + '.meta').read_text(), re.M)[1]
    for platform_path in (PurePosixPath(relative), PureWindowsPath(relative)):
        assert scope['guid'](platform_path) == expected, f'rebuilding changes GUID for {platform_path}'
print('PASS: player-board dependencies, local references, 20 controls, 5 rows, trigger physics, Hub placement prefab, and cross-platform asset GUIDs.')
