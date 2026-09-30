"""Read-only cognitive asset graph, dimensions, audio and source-boundary checks.

This does not import assets in Unity, simulate PhysX, render TMP, or run Photon.
"""
from __future__ import annotations

import copy
import hashlib
import math
import re
import struct
import wave

import yaml

from build_assets import ROOT, ASSET, PREFAB, FONT, TMP, NOTES, assets, script


def check(value, message):
    if not value:
        raise AssertionError(message)


def objects(text):
    headers = list(re.finditer(r'^--- !u!(\d+) &(\d+)\n', text, re.M))
    result = {}
    for i, header in enumerate(headers):
        kind, file_id = map(int, header.groups())
        end = headers[i + 1].start() if i + 1 < len(headers) else len(text)
        document = yaml.safe_load(text[header.end():end])
        check(file_id not in result, 'Duplicate local fileID')
        result[file_id] = (kind, next(iter(document.values())))
    return result


def references(value):
    if isinstance(value, dict):
        if 'fileID' in value:
            yield value
        for child in value.values():
            yield from references(child)
    elif isinstance(value, list):
        for child in value:
            yield from references(child)


def inspect_cap_clearance(graph):
    """Check authored cap cubes in full parent TRS, at rest and fully pressed."""
    transforms = {d['m_GameObject']['fileID']: i for i, (k, d) in graph.items() if k in (4, 224)}
    housing = next(i for i, (k, d) in graph.items() if k == 1 and d['m_Name'] == 'DisplayHousing')

    def world_point(transform_id, point, moving=0, depth=0):
        if not transform_id:
            return point
        data = graph[transform_id][1]
        vx, vy, vz = (point[i] * data['m_LocalScale'][a] for i, a in enumerate('xyz'))
        qx, qy, qz, qw = (data['m_LocalRotation'][a] for a in 'xyzw')
        # Rotate v by the authored quaternion, then translate through each parent.
        tx, ty, tz = 2 * (qy * vz - qz * vy), 2 * (qz * vx - qx * vz), 2 * (qx * vy - qy * vx)
        position = data['m_LocalPosition']
        point = (vx + qw * tx + qy * tz - qz * ty + position['x'],
                 vy + qw * ty + qz * tx - qx * tz + position['y'],
                 vz + qw * tz + qx * ty - qy * tx + position['z'] + (depth if transform_id == moving else 0))
        return world_point(data['m_Father']['fileID'], point, moving, depth)

    def heights(go, moving=0, depth=0):
        return [world_point(transforms[go], (x, y, z), moving, depth)[1]
                for x in (-.5, .5) for y in (-.5, .5) for z in (-.5, .5)]

    bottom = min(heights(housing))
    for kind, pad in graph.values():
        if kind != 114 or pad['m_Script']['guid'] != script('CognitivePad'):
            continue
        cap = graph[pad['capRenderer']['fileID']][1]['m_GameObject']['fileID']
        for depth in (0, pad['pressDepth']):
            top = max(heights(cap, pad['cap']['fileID'], depth))
            check(top <= bottom - .005, 'Pad ' + str(pad['index'] + 1) + ' cap intersects display housing or lacks 5 mm clearance')


def inspect_graph(graph):
    for file_id, (kind, data) in graph.items():
        check(kind not in (20, 81, 223), 'No added camera, listener or overlay Canvas')
        for target in references(data):
            if target['fileID'] and 'guid' not in target:
                check(target['fileID'] in graph, 'Dangling local fileID')
        if kind == 1:
            for item in data['m_Component']:
                component = graph[item['component']['fileID']][1]
                check(component['m_GameObject']['fileID'] == file_id, 'Component owner mismatch')
        if kind in (4, 224):
            for child in data['m_Children']:
                check(graph[child['fileID']][1]['m_Father']['fileID'] == file_id, 'Nonreciprocal transform child')
            parent = data['m_Father']['fileID']
            if parent:
                check({'fileID': file_id} in graph[parent][1]['m_Children'], 'Missing transform child')
    machines = [(i, v) for i, (k, v) in graph.items() if k == 114 and v['m_Script']['guid'] == script('CognitiveMachine')]
    check(len(machines) == 1, 'Exactly one authored machine')
    machine_id, machine = machines[0]
    check(len(machine['pads']) == 5 and len(machine['clips']) == 6, 'Complete pad/clip bindings')
    check(len({p['fileID'] for p in machine['pads']}) == 5, 'Distinct pads')
    check(machine['sector'] == 1 and machine['editorControls'] == 0, 'Hub; no default Editor bypass')
    check(machine['maximumLength'] == 8, 'Eight-round prototype')
    check(graph[machine['display']['fileID']][1]['m_fontAsset']['guid'] == FONT, 'Existing assigned font')
    check(graph[machine['audioSource']['fileID']][0] == 82, 'Assigned audio source')
    check(graph[machine['operatorAnchor']['fileID']][0] == 4, 'Assigned presence anchor')
    for index, binding in enumerate(machine['pads']):
        kind, pad = graph[binding['fileID']]
        check(kind == 114 and pad['m_Script']['guid'] == script('CognitivePad'), 'Bound actual pad script')
        check(pad['machine']['fileID'] == machine_id and pad['index'] == index, 'Correct pad machine/index')
        go = graph[pad['m_GameObject']['fileID']][1]
        components = [graph[c['component']['fileID']] for c in go['m_Component']]
        collider = next(data for kind, data in components if kind == 65)
        body = next(data for kind, data in components if kind == 54)
        check(collider['m_IsTrigger'] == 1 and collider['m_Enabled'] == 1, 'Pad is an enabled trigger, not locomotion support')
        check(body['m_IsKinematic'] == 1 and body['m_UseGravity'] == 0, 'Local trigger owns a kinematic body')
        check(graph[pad['cap']['fileID']][0] == 4, 'Unit-scale moving cap group')
        renderer = graph[pad['capRenderer']['fileID']][1]
        mat = renderer['m_Materials'][0]
        check(mat.get('guid'), 'Assigned cap material')
        cap_go = graph[pad['cap']['fileID']][1]['m_GameObject']['fileID']
        check(graph[cap_go][1]['m_Name'] == 'MovingCap', 'Correct moving group')
    labels = [d['m_text'] for k, d in graph.values() if k == 114 and d['m_Script']['guid'] == TMP]
    check(all(str(i) in labels for i in range(1, 5)) and 'START / RESTART' in labels, 'Persistent readable number/start labels')
    check(sum(k == 82 for k, _ in graph.values()) == 1, 'One speaker source')
    check(sum(k == 65 and d['m_IsTrigger'] for k, d in graph.values()) == 5, 'Five separate pad triggers')
    inspect_cap_clearance(graph)


def main():
    expected = assets()
    for name, contents in expected.items():
        check((ROOT / name).read_bytes() == contents, 'Generated bytes differ: ' + name)
    graph = objects((ROOT / PREFAB).read_text())
    inspect_graph(graph)
    by_name = {d['m_Name']: i for i, (k, d) in graph.items() if k == 1}
    def transform(name):
        return next(d for k, d in graph.values() if k in (4, 224) and d['m_GameObject']['fileID'] == by_name[name])
    check(list(transform('PrimateCognitiveEvaluation')['m_LocalScale'].values()) == [1, 1, 1], 'Movable unit-scale root')
    check(transform('Controls_AdjustHeightHere')['m_LocalEulerAnglesHint']['x'] == 30, 'Upward-facing control tilt')
    check(abs(transform('Pad2')['m_LocalPosition']['x'] - transform('Pad1')['m_LocalPosition']['x'] - .34) < 1e-6, '34 cm two-handed spread')
    check(abs(transform('Pad1')['m_LocalPosition']['y'] - transform('Pad3')['m_LocalPosition']['y'] - .26) < 1e-6, '26 cm row spacing')
    for k, d in graph.values():
        if k == 23:
            for binding in d['m_Materials']:
                if binding['guid'] == FONT:
                    continue
                matches = [p for p in (ROOT / ASSET / 'Materials').glob('*.meta') if binding['guid'] in p.read_text()]
                check(len(matches) == 1 and matches[0].with_suffix('').is_file(), 'Material GUID resolves')
    for path in (ROOT / ASSET / 'Materials').glob('*.mat'):
        data = next(iter(objects(path.read_text()).values()))[1]
        check(data['m_Shader']['fileID'] == 46, 'Built-in Standard materials only')
        if path.name.startswith('Pad'):
            check('_EMISSION' in data['m_ValidKeywords'], 'Pad illumination enabled in serialized shader variant')
    hashes = set()
    for index in range(6):
        path = ROOT / ASSET / 'Audio' / ('Note' + str(index + 1) + '.wav')
        hashes.add(hashlib.sha256(path.read_bytes()).hexdigest())
        with wave.open(str(path), 'rb') as wav:
            check((wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, 22050), 'Mono 16-bit PCM 22050 Hz')
            check(.23 <= wav.getnframes() / wav.getframerate() <= .33, 'Short bounded clips')
            frames = wav.readframes(wav.getnframes())
        samples = struct.unpack('<' + 'h' * (len(frames) // 2), frames)
        check(samples[0] == samples[-1] == 0, 'No hard waveform edge')
        check(1000 < max(map(abs, samples)) < 12000, 'Audible but unclipped source amplitude')
        if index < 4:
            energy = []
            for frequency in NOTES[:4]:
                a = sum(v * math.cos(2 * math.pi * frequency * n / 22050) for n, v in enumerate(samples))
                b = sum(v * math.sin(2 * math.pi * frequency * n / 22050) for n, v in enumerate(samples))
                energy.append(a * a + b * b)
            check(energy.index(max(energy)) == index, 'Distinct primary pitch for pad ' + str(index + 1))
    check(len(hashes) == 6, 'Six different assigned clips')
    mutants = []
    broken = copy.deepcopy(graph)
    first_go = next(d for k, d in broken.values() if k == 1)
    first_go['m_Component'][0]['component']['fileID'] = 99999999
    mutants.append(broken)
    broken = copy.deepcopy(graph)
    pad = next(d for k, d in broken.values() if k == 114 and d['m_Script']['guid'] == script('CognitivePad'))
    pad['index'] = 4
    mutants.append(broken)
    broken = copy.deepcopy(graph)
    trigger = next(d for k, d in broken.values() if k == 65 and d['m_IsTrigger'])
    trigger['m_IsTrigger'] = 0
    mutants.append(broken)
    broken = copy.deepcopy(graph)
    body = next(d for k, d in broken.values() if k == 54)
    body['m_IsKinematic'] = 0
    mutants.append(broken)
    broken = copy.deepcopy(graph)
    deck_go = next(i for i, (k, d) in broken.items() if k == 1 and d['m_Name'] == 'Controls_AdjustHeightHere')
    deck = next(d for k, d in broken.values() if k == 4 and d['m_GameObject']['fileID'] == deck_go)
    deck['m_LocalPosition']['y'] = .98  # Original overlap regression.
    mutants.append(broken)
    for broken in mutants:
        try:
            inspect_graph(broken)
        except (AssertionError, KeyError):
            pass
        else:
            raise AssertionError('Broken prefab mutation escaped validation')
    print('PASS: ' + str(len(graph)) + ' serialized objects; all local references, five pads, materials and assigned clips.')
    print('PASS: dimensions/labels; six PCM waveforms and four distinct pitches; rest/pressed cap clearance; five broken-asset mutations rejected.')
    print('SOURCE/ASSET DATA ONLY: Unity import/rendering/physics, Photon and headset acceptance remain pending.')


if __name__ == '__main__':
    main()
