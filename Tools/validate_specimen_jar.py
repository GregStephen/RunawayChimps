#!/usr/bin/env python3
"""Read-only serialized wiring/geometry and boundary contracts, not Unity validation."""
from pathlib import Path
import math
import re
import struct
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'Assets/RunawayChimps/Toys/ReactiveSpecimenJar'


def mesh_data(path):
    text = path.read_text()
    count = int(re.search(r'm_VertexCount: (\d+)', text)[1])
    raw = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)', text)[1])
    assert len(raw) == count * 32, f'{path.name}: packed vertex stride/count'
    vertices = list(struct.iter_unpack('<8f', raw))
    raw = bytes.fromhex(re.search(r'm_IndexBuffer: ([0-9a-f]+)', text)[1])
    indices = list(struct.unpack('<' + 'H' * (len(raw)//2), raw))
    counts = list(map(int, re.findall(r'    indexCount: (\d+)', text)))
    assert sum(counts) == len(indices) and len(indices) % 3 == 0, 'Submesh coverage'
    assert all(0 <= i < count for i in indices), 'Index range'
    assert all(math.isfinite(a) for v in vertices for a in v), 'Finite vertices'
    assert all(abs(sum(a*a for a in v[3:6])-1) < .001 for v in vertices), 'Unit normals'
    for i in range(0, len(indices), 3):
        a,b,c = (vertices[k] for k in indices[i:i+3])
        u=[b[k]-a[k] for k in range(3)]; v=[c[k]-a[k] for k in range(3)]
        cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
        assert sum(n*n for n in cross) > 1e-16, f'{path.name}: degenerate triangle {i//3}'
        normal=[a[k+3]+b[k+3]+c[k+3] for k in range(3)]
        assert sum(x*y for x,y in zip(cross,normal)) > 0, f'{path.name}: reversed triangle {i//3}'
    return vertices, indices, len(counts)


def contracts(runtime, editor, prefab):
    errors=[]
    def require(condition, message):
        if not condition: errors.append(message)
    require('Player currentRig = Player.Instance;' in runtime and 'rig.leftHandTransform' in runtime
            and 'rig.rightHandTransform' in runtime, 'Only exact local rig hand sources')
    require('hand.IsChildOf(origin.transform)' in runtime and 'view == null || view.IsMine' in runtime,
            'Remote hierarchy/ownership exclusion')
    require('CommonUsages.isTracked' in runtime and 'CommonUsages.trackingState' in runtime,
            'Actual XR tracking availability')
    require('ReferenceEquals(room, currentRoom)' in runtime and 'actor != currentActor' in runtime,
            'Room object/actor session reset')
    require('SectorTravelService.I.IsBusy' in runtime and 'gameObject.scene != SceneManager.GetActiveScene()' in runtime,
            'Travel and active scene guard')
    require('maximumHandStep' in runtime and 'delta.magnitude / dt > maximumHandSpeed' in runtime,
            'Tracking discontinuity rejection')
    require(runtime.count('SampleHand(lh,') == 1 and runtime.count('SampleHand(rh,') == 1,
            'Sample once per anatomical hand')
    require(not re.search(r'\b(OnTriggerEnter|OnTriggerStay|OnCollisionEnter|OnCollisionStay)\s*\(',runtime),
            'No collider-driven duplicate reaction route')
    require(not re.search(r'\b(RPC|RaiseEvent|SetCustomProperties|FindObjectsOfType|FindAnyObjectByType)\s*\(',runtime),
            'No network writes or arbitrary hand discovery')
    require('Vector3.Lerp(specimen.localPosition, Bound(goal), blend)' in runtime,
            'Continuous bounded specimen motion')
    require('#if UNITY_EDITOR\n            preview = editorPreview;' in runtime,
            'Preview must be ignored in builds')
    require('Tools/Runaway Chimps/Toys/Place Reactive Specimen Jar in Hub' in editor,
            'Feature-specific menu')
    require('Undo.RegisterCreatedObjectUndo' in editor and 'existing != null' in editor,
            'Undo and existing-placement preservation')
    require(not re.search(r'\b(SaveScene|SaveOpenScenes|SaveAssets|OpenScene)\s*\(',editor),
            'No implicit opening or forced saving')
    require('  editorPreview: 0\n' in prefab, 'Shipping prefab is not a desktop preview')
    require(prefab.count('\nBoxCollider:\n')==1 and prefab.count('\nMonoBehaviour:\n')==1,
            'One solid glass collider and only the local toy component')
    require(prefab.count('\nMeshRenderer:\n')==3 and '\nRigidbody:\n' not in prefab,
            'Small fixed three-renderer asset')
    return errors


def main():
    prefab=(BASE/'ReactiveSpecimenJar.prefab').read_text()
    runtime=(ROOT/'Assets/Scripts/Toys/ReactiveSpecimenJar.cs').read_text()
    editor=(ROOT/'Assets/Scripts/Editor/ReactiveSpecimenJarEditor.cs').read_text()
    errors=contracts(runtime,editor,prefab)
    assert not errors, '; '.join(errors)
    ids=re.findall(r'^--- !u!\d+ &(\d+)$',prefab,re.M)
    assert len(ids)==len(set(ids)), 'Unique prefab object IDs'
    for ref in re.findall(r'\{fileID: (\d+)\}',prefab):
        assert ref=='0' or ref in ids, f'Unresolved local fileID {ref}'
    metas=[*BASE.glob('*.meta'),ROOT/'Assets/Scripts/Toys/ReactiveSpecimenJar.cs.meta']
    assets={re.search(r'^guid: (\w+)$',p.read_text(),re.M)[1]:Path(str(p)[:-5]) for p in metas}
    for guid in re.findall(r'guid: (\w+)',prefab):
        assert guid in assets, f'Unresolved prefab asset GUID {guid}'
    assert 'specimen: {fileID: 1001}' in prefab and 'glass: {fileID: 1104}' in prefab, 'Assigned references'
    expected={'SpecimenGrowth':(337,596,3),'JarGlass':(16,8,1),'JarFrame':(216,108,1)}
    for name, (nv,nt,ns) in expected.items():
        vs, ix, sm=mesh_data(BASE/(name+'.asset'))
        assert (len(vs),len(ix)//3,sm)==(nv,nt,ns), f'{name}: geometry budget'
        if name=='SpecimenGrowth':
            radius=max(math.sqrt(sum(a*a for a in v[:3])) for v in vs)*1.015
            assert radius+.095 < .17 and radius+.14 < .23, 'Whole mesh stays inside glass at every rotation/breath'
    for path in BASE.glob('*.mat'):
        mat=path.read_text()
        assert 'm_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}' in mat, 'Built-in Standard only'
        assert 'm_Texture: {fileID: 0}' in mat, 'No texture dependency'
    tint=(BASE/'JarTint.mat').read_text()
    assert '_ALPHABLEND_ON' in tint and '_ZWrite: 0' in tint and 'a: 0.1}' in tint, 'Readable simple tint'
    # Negative contracts demonstrate that the checker rejects meaningful regressions.
    mutations=[(runtime.replace('hand.IsChildOf(origin.transform)','true'),editor,prefab),
               (runtime.replace('actor != currentActor','false'),editor,prefab),
               (runtime.replace('SectorTravelService.I.IsBusy','false'),editor,prefab),
               (runtime+'\nvoid OnTriggerStay(Collider c) {}',editor,prefab),
               (runtime.replace('Vector3.Lerp(specimen.localPosition, Bound(goal), blend)','goal'),editor,prefab),
               (runtime,editor+'\nSaveScene(scene);',prefab),
               (runtime,editor,prefab.replace('editorPreview: 0','editorPreview: 1'))]
    for mutation in mutations: assert contracts(*mutation), 'Negative mutation escaped contracts'
    print('PASS: specimen prefab GUID/fileID wiring, 569 vertices / 712 triangles / 3 renderers / 5 material slots.')
    print('PASS: finite geometry, winding, normals, full-rotation/breath containment, assigned built-in materials.')
    print('PASS: local ownership/tracking/lifecycle/tap/preview/authoring contracts; 7 negative mutations rejected.')
    print('Unity import/rendering, native query execution, actual XR/Photon and Quest acceptance remain separate.')
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (AssertionError, ValueError, AttributeError, OSError) as exc:
        print('ERROR:', exc)
        sys.exit(1)
