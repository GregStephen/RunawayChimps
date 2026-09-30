#!/usr/bin/env python3
"""Independent read-back checks of published native Mesh/prefab/material assets.

This is NOT Unity import, native physics, headset, or GPU performance validation.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import re
from pathlib import Path
import numpy as np
from PIL import Image

ROOT='Assets/RunawayChimps/Environment/RegenerationLab'
BUILTIN='0000000000000000f000000000000000'


def read_mesh(path):
    text=path.read_text(encoding='utf-8')
    raw=bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)',text)[1])
    n=int(re.search(r'm_VertexCount: (\d+)',text)[1])
    assert len(raw)==int(re.search(r'm_DataSize: (\d+)',text)[1])
    vertices=np.frombuffer(raw,dtype='<f4').reshape(n,12)
    indices=np.frombuffer(bytes.fromhex(re.search(r'm_IndexBuffer: ([0-9a-f]+)',text)[1]),dtype='<u2')
    subtext=text.split('  m_SubMeshes:\n')[1].split('  m_Shapes:')[0]
    subs=[(int(a)//2,int(b)) for a,b in re.findall(r'firstByte: (\d+)\s+indexCount: (\d+)',subtext)]
    bound=text.split('  m_LocalAABB:')[1].split('  m_MeshUsageFlags:')[0]
    numbers=re.findall(r'[xyz]: (-?[0-9.e+-]+)',bound)
    return vertices,indices,subs,np.array(list(map(float,numbers))).reshape(2,3)


def scan(output, baseline=None):
    kit=output/ROOT
    assert kit.is_dir(),kit
    inventory=json.loads((kit/'RegenerationLab.inventory.json').read_text())
    errors=[]; results={}; guids={}; known={BUILTIN}
    def check(condition,message):
        if not condition:errors.append(message)
    if baseline:
        for meta in (baseline/'Assets').rglob('*.meta'):
            if baseline.resolve()==output.resolve() and (kit in meta.parents or meta==kit.with_name(kit.name+'.meta')):
                continue
            match=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(encoding='utf-8-sig'),re.M)
            if match:known.add(match[1])
    metas=list(kit.rglob('*.meta'))+[kit.with_name(kit.name+'.meta')]
    for meta in metas:
        source=meta.with_name(meta.name[:-5]);check(source.exists(),'orphan metadata: '+str(meta))
        text=meta.read_text(); match=re.search(r'^guid: ([a-f0-9]{32})$',text,re.M)
        check(match is not None,'invalid GUID '+str(meta))
        if source.suffix in ('.asset','.mat'):
            expected_id=4300000 if source.suffix=='.asset' else 2100000
            check('mainObjectFileID: '+str(expected_id) in text,'invalid native importer identity '+meta.name)
        if match:
            value=match[1]
            check(value not in guids,'duplicate GUID '+value)
            if baseline:check(value not in known,'GUID collides with baseline: '+value)
            guids[value]=source
    all_refs=known|set(guids)
    for path in kit.rglob('*'):
        if path.is_dir() or path.suffix=='.meta':continue
        check(path.with_name(path.name+'.meta').exists(),'missing metadata '+str(path))
        if path.suffix in ('.mat','.asset','.prefab'):
            text=path.read_text()
            for ref in re.findall(r'guid: ([a-f0-9]{32})',text):
                check(ref in all_refs,'unresolved GUID '+ref+' in '+path.name)
        if path.suffix=='.mat':
            check('m_Shader: {fileID: 46, guid: '+BUILTIN+', type: 0}' in text,'not Standard '+path.name)
            check('_Mode: 0' in text and '_ZWrite: 1' in text,'not opaque '+path.name)
            main=text.split('    - _MainTex:')[1].split('    - _MetallicGlossMap:')[0]
            check('fileID: 2800000' in main,'missing texture '+path.name)
        if path.suffix=='.prefab':
            blocks=re.findall(r'--- !u!(\d+) &(\d+)\n(.*?)(?=\n--- !u!|\Z)',text,re.S)
            ids=[b[1] for b in blocks];check(len(set(ids))==len(ids),'duplicate prefab IDs '+path.name)
            for cls,obj,body in blocks:
                check(int(cls) in (1,4,23,33,65),'disallowed component '+path.name+' '+cls)
                for ref in re.findall(r'\{fileID: (\d+)\}',body):
                    check(ref=='0' or ref in ids,'unresolved local reference '+ref)
                if cls=='4':
                    for field,value in [('m_LocalPosition','{x: 0, y: 0, z: 0}'),('m_LocalRotation','{x: 0, y: 0, z: 0, w: 1}'),('m_LocalScale','{x: 1, y: 1, z: 1}')]:
                        check(field+': '+value in body,'nonidentity transform '+path.name)
                if cls=='65':
                    check('m_IsTrigger: 0' in body and 'm_Enabled: 1' in body,'invalid collision '+path.name)
                    size=np.array(list(map(float,re.search(r'm_Size: \{x: (.*?), y: (.*?), z: (.*?)\}',body).groups())))
                    check(np.isfinite(size).all() and (size>0).all(),'bad collider extent '+path.name)
                if cls=='23':
                    part=body.split('  m_Materials:')[1].split('  m_StaticBatchInfo:')[0]
                    bindings=re.findall(r'guid: ([a-f0-9]{32})',part)
                    check(len(bindings) in (1,3),'unexpected material slots '+path.name)
                    check(all(ref in guids and guids[ref].suffix=='.mat' for ref in bindings),'bad material binding '+path.name)
    for prop in inventory['props']:
        prefab_text=(output/prop['prefab']).read_text()
        mesh_refs=re.findall(r'm_Mesh: \{fileID: 4300000, guid: ([a-f0-9]{32}), type: 2\}',prefab_text)
        mesh_names=[guids[ref].stem if ref in guids else '?' for ref in mesh_refs]
        check(mesh_names==[prop['name'],prop['label_mesh']['name']],'wrong prefab mesh assignment '+prop['name'])
        mat_lists=re.findall(r'  m_Materials:\n(.*?)  m_StaticBatchInfo:',prefab_text,re.S)
        assigned=[[guids[ref].stem if ref in guids else '?' for ref in re.findall(r'guid: ([a-f0-9]{32})',part)] for part in mat_lists]
        check(assigned==[prop['material_slots'],prop['label_mesh']['material_slots']],'wrong prefab material assignment '+prop['name'])
        collider_blocks=re.findall(r'--- !u!65 &\d+\n(.*?)(?=\n--- !u!|\Z)',prefab_text,re.S)
        actual_colliders=[]
        for body in collider_blocks:
            center=list(map(float,re.search(r'm_Center: \{x: (.*?), y: (.*?), z: (.*?)\}',body).groups()))
            size=list(map(float,re.search(r'm_Size: \{x: (.*?), y: (.*?), z: (.*?)\}',body).groups()))
            actual_colliders.append({'center':center,'size':size})
        check(len(actual_colliders)==len(prop['colliders']),'collider count mismatch '+prop['name'])
        for actual,expected in zip(actual_colliders,prop['colliders']):
            check(np.allclose(actual['center'],expected['center'],atol=1e-7) and np.allclose(actual['size'],expected['size'],atol=1e-7),'collider manifest mismatch '+prop['name'])
        for record in (prop,prop['label_mesh']):
            path=kit/'Meshes'/(record['name']+'.asset')
            v,ids,subs,bounds=read_mesh(path)
            check(np.isfinite(v).all(),'nonfinite vertices '+path.name)
            check(len(ids)%3==0 and len(ids)>0 and ids.max()<len(v),'bad indices '+path.name)
            p=v[ids.reshape(-1,3),:3].astype(np.float64)
            normals=v[ids.reshape(-1,3),3:6].mean(axis=1)
            cross=np.cross(p[:,1]-p[:,0],p[:,2]-p[:,0])
            area=np.linalg.norm(cross,axis=1)
            aligned=np.sum(cross*normals,axis=1)
            check((area>1e-10).all(),'degenerate triangles '+path.name)
            check((aligned>0).all(),f'normals disagree with winding {path.name}: {int((aligned<=0).sum())}')
            check(np.max(np.abs(np.linalg.norm(v[:,3:6],axis=1)-1))<.0001,'nonunit normals '+path.name)
            check(np.max(np.abs(np.sum(v[:,3:6]*v[:,6:9],axis=1)))<.0001,'nonorthogonal tangents '+path.name)
            check(np.allclose(bounds[0]-bounds[1],v[:,:3].min(axis=0),atol=1e-6) and np.allclose(bounds[0]+bounds[1],v[:,:3].max(axis=0),atol=1e-6),'wrong mesh bounds '+path.name)
            check(sum(count for start,count in subs)==len(ids),'incomplete submeshes '+path.name)
            expected_start=0
            for start,count in subs:
                check(start==expected_start and count%3==0,'noncontiguous submesh indices '+path.name)
                expected_start+=count
            check(len(ids)//3==record['triangles'] and len(v)==record['vertices'],'stats mismatch '+path.name)
            check(len(subs)==len(record['material_slots']),'slot mismatch '+path.name)
            if not record['name'].endswith('_Label'):
                check(abs(float(v[:,1].min()))<1e-6,'not floor pivot '+path.name)
            for (start,count),material in zip(subs,record['material_slots']):
                coords=v[ids[start:start+count],10:12]
                if material.endswith('PolymerAndLabels'):
                    check((coords>=0).all() and (coords<=1).all(),'atlas UV out of bounds '+path.name)
            # Reject UV-degenerate triangles even though the materials have no normal map.
            uv=v[ids.reshape(-1,3),10:12]
            d1=uv[:,1]-uv[:,0]; d2=uv[:,2]-uv[:,0]
            uvarea=np.abs(d1[:,0]*d2[:,1]-d1[:,1]*d2[:,0])
            check((uvarea>1e-12).all(),'degenerate UV triangles '+path.name)
            results[record['name']]={'triangles':len(ids)//3,'vertices':len(v),'material_slots':len(subs),
                                      'dimensions_m':(v[:,:3].max(axis=0)-v[:,:3].min(axis=0)).astype(float).tolist(),
                                      'minimum_triangle_double_area':float(area.min()),'normal_winding_failures':int((aligned<=0).sum())}
        # Single known 1-2 mm offset avoids coplanar main label/backing overlap.
        label=prop['label_mesh']
        check(label['triangles']==2,'unexpected main label geometry')
        for collider in actual_colliders:
            center=np.array(collider['center']); size=np.array(collider['size'])
            check((size>0).all() and (center-size/2)[1]>=-.0001,'below-floor collider '+prop['name'])
            check(np.max(center+size/2-np.array(prop['bounds_max_m']))<.04,'oversized collider '+prop['name'])
            check(np.max(np.array(prop['bounds_min_m'])-(center-size/2))<.04,'oversized collider '+prop['name'])
    for name,expected in inventory['textures'].items():
        with Image.open(kit/'Textures'/name) as image:
            check(list(image.size)==expected and image.mode=='RGB','incorrect texture dimensions/alpha '+name)
            image.verify()
    code=(kit/'Editor/RegenerationLabPlacement.cs').read_text()
    for required in ('Undo.RegisterCreatedObjectUndo','Undo.SetTransformParent','Undo.CollapseUndoOperations',
                     'Undo.RevertAllDownToGroup','PrefabUtility.InstantiatePrefab(prefabs[i], scene)',
                     'PrefabUtility.RecordPrefabInstancePropertyModifications','PrefabStageUtility.GetCurrentPrefabStage',
                     'scene.isLoaded','EditorSceneManager.IsPreviewScene','EditorSceneManager.MarkSceneDirty'):
        check(required in code,'missing editor guard: '+required)
    for forbidden in ('SaveScene(', 'OpenScene(', 'InitializeOnLoad', 'DestroyImmediate(', 'CreatePrimitive(', 'new Mesh(', 'new Material('):
        check(forbidden not in code,'forbidden automatic/destructive authoring: '+forbidden)
    # Check full paths, not same basename across mesh/prefab folders.
    paths=[p.relative_to(kit).as_posix().casefold() for p in kit.rglob('*')]
    check(len(paths)==len(set(paths)),'case-colliding asset paths')
    report={'status':'PASS' if not errors else 'FAIL','errors':errors,'meshes':results,'metadata_guids':len(guids),
            'total_triangles':sum(p['total_triangles'] for p in inventory['props']),
            'unique_materials':len(inventory['materials']),'textures':inventory['textures'],
            'boundary':'Offline read-back only. No Unity compile/import, physics, headset, or Quest performance claim.',
            'hashes':{p.relative_to(output).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(kit.rglob('*')) if p.is_file()}}
    return report


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',required=True,type=Path)
    parser.add_argument('--baseline',type=Path);parser.add_argument('--report',type=Path)
    args=parser.parse_args();report=scan(args.root,args.baseline)
    if args.report:args.report.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k!='hashes'},indent=2))
    raise SystemExit(0 if report['status']=='PASS' else 1)
