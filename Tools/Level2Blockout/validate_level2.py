"""Source/geometry checks, not Unity import, NavMesh, or headset validation."""
from pathlib import Path
import json, re, math, collections, subprocess
import yaml
import numpy as np
from scipy.ndimage import binary_erosion, label
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
d=json.loads((HERE/'layout.json').read_text());nodes=d['nodes'];byid={n['id']:n for n in nodes}
assets=ROOT/'Assets/RunawayChimps/Level2Blockout'
results={}
manifest=json.loads((HERE/'gate_manifest.json').read_text())
external={}
for path in [manifest['prefab_path'],manifest['small_prefab_path'],'Assets/MASH Virtual/Sci Fi Doors/Mesh/Sci Fi Gates.fbx',
             'Assets/Scripts/Travel/SectorScene.cs','Assets/Scripts/Travel/LevelTerminalActions.cs']:
    meta_path=ROOT/(path+'.meta')
    md=meta_path.read_text() if meta_path.exists() else subprocess.check_output(['git','show','HEAD:'+path+'.meta'],cwd=ROOT,text=True)
    guid=re.search(r'^guid: (\w+)',md,re.M)[1]
    source=(ROOT/path).read_bytes() if (ROOT/path).exists() else subprocess.check_output(['git','show','HEAD:'+path],cwd=ROOT)
    ids=set(re.findall(rb'^--- !u!\d+ &(\d+)',source,re.M)) if path.endswith('.prefab') else set(re.findall(rb'first:.*?(430\d+)',md.encode()))
    external[guid]={'path':path,'ids':{int(i) for i in ids}|{100100000}}
def enabled(n):
    if not n['active']:return False
    return enabled(byid[n['parent']]) if n['parent'] else True
for p in [*assets.rglob('*.unity'),*assets.rglob('*.prefab')]:
    s=p.read_text();ids=re.findall(r'^--- !u!\d+ &(\d+)',s,re.M)
    assert len(ids)==len(set(ids))
    known=set(map(int,ids))|{0}
    for m in re.finditer(r'\{fileID: (\d+)([^}]*)\}',s):
        if 'guid:' not in m[2]:assert int(m[1]) in known,(p,m[0])
        else:
            g=re.search(r'guid: (\w+)',m[2])[1]
            if g in external and external[g]['path'].endswith('.prefab'):
                assert int(m[1]) in external[g]['ids'],(p,m[0])
    cleaned=re.sub(r'^%.*\n','',s,flags=re.M)
    cleaned=re.sub(r'^--- !u!\d+ &\d+(?: stripped)?','---',cleaned,flags=re.M)
    docs=list(yaml.safe_load_all(cleaned));assert len(docs)==len(ids)
    instances=[doc['PrefabInstance'] for doc in docs if 'PrefabInstance' in doc]
    assert len(instances)==5
    for inst,g in zip(instances,manifest['gates']):
        overrides={p['propertyPath']:p['value'] for p in inst['m_Modification']['m_Modifications']}
        assert [overrides['m_LocalScale.'+a] for a in 'xyz']==g['scale']
        assert overrides['m_Name']==g['name']
        panel_values=[p['value'] for p in inst['m_Modification']['m_Modifications'] if p['propertyPath']=='m_IsActive']
        assert all(v==(0 if g['frame_only'] else 1) for v in panel_values)
    results[p.name]={'serialized_objects':len(ids),'yaml_parse':True,'local_references_resolve':True}
guids={re.search(r'^guid: (\w+)',p.read_text(),re.M)[1] for p in assets.rglob('*.meta')}
for p in [*assets.rglob('*.unity'),*assets.rglob('*.prefab'),*assets.rglob('*.mat')]:
    for g in re.findall('guid: ([0-9a-f]{32})',p.read_text()):
        assert g in guids or g in external or g.startswith('0000000000000000'),(p,g)
assert manifest['exit_scale']==[1,1.1564301,.8]
assert manifest['small_scale']==[1,1.3197935,1.2965604]
results['external_dependencies']={g:v['path'] for g,v in external.items()}
results['linked_gates']={'count':5,'frame_only':3,'exit_matches_level1_scale':True,'reward_uses_level1_small_gate':True}
results['connectivity_scope']='Box layout only; linked gate meshes, bevels and threshold clearance require Unity validation.'

step=.1
xs=np.arange(-1,19.001,step);zs=np.arange(-5,15.001,step)
X,Z=np.meshgrid(xs,zs,indexing='ij')
floor=np.zeros(X.shape,dtype=bool)
for _,x0,x1,z0,z1,_ in d['rooms']:
    floor|=(X>=x0-1e-6)&(X<=x1+1e-6)&(Z>=z0-1e-6)&(Z<=z1+1e-6)
solid=[];solid_bottom={};reward_blocker=None
for n in nodes:
    if not n['collider'] or n['trigger'] or not enabled(n):continue
    x,y,z=n['position'];sx,sy,sz=n['scale']
    if y+sy/2<=.01 or y-sy/2>=3.2:continue
    rect=(x-sx/2,z-sz/2,x+sx/2,z+sz/2)
    solid.append(rect)
    solid_bottom[rect]=y-sy/2
    if n['name'].startswith('Reward_Room_Blocker'):reward_blocker=rect
def exclusion(rect,radius):
    x0,z0,x1,z1=rect
    dx=np.maximum(np.maximum(x0-X,X-x1),0)
    dz=np.maximum(np.maximum(z0-Z,Z-z1),0)
    return dx*dx+dz*dz <= radius*radius + 1e-8
def route_checks(radius):
    body_height=2.0 if radius==.35 else 3.2
    obstacles=[rect for rect in solid if solid_bottom[rect]<body_height]
    r=math.ceil(radius/step)
    gx,gz=np.mgrid[-r:r+1,-r:r+1]
    kernel=(gx*step)**2+(gz*step)**2 <= (radius+.05)**2
    space=binary_erosion(floor,structure=kernel)
    for rect in obstacles:space&=~exclusion(rect,radius)
    def idx(p):return (round((p[0]+1)/step),round((p[1]+5)/step))
    labels,count=label(space)
    start=idx((3,6.6));component=labels[start];assert component>0
    endpoints={'entry':(3,-2.4),'repair_hall_door':(12,11.7),'repair_bypass_door':(16,11.7),
               'lower_bypass':(16,2.2),'front_of_shutter':(3,8.5)}
    for name,p in endpoints.items():assert labels[idx(p)]==component,(radius,name)
    for name,door,target in [('hall_door_blocked',(10.4,9.9,13.6,10.1),(16,11.7)),
                             ('bypass_door_blocked',(14.4,9.9,17.6,10.1),(12,11.7))]:
        cut=space & ~exclusion(door,radius)
        pieces,_=label(cut)
        assert pieces[start]>0 and pieces[start]==pieces[idx(target)],(radius,name)
    assert labels[idx((3,12))]!=component,'Closed shutter leaked'
    assert labels[idx((16,-2))]!=component,'Locked reward room leaked'
    if radius==.35:
        unlocked=binary_erosion(floor,structure=kernel)
        for rect in obstacles:
            if rect!=reward_blocker:unlocked&=~exclusion(rect,radius)
        areas,_=label(unlocked)
        assert areas[start]>0 and areas[start]==areas[idx((16,-2))],'Unlocked reward wall gap unreachable'
    return {'radius_m':radius,'height_m':body_height,'grid_step_m':step,'reachable_grid_points':int((labels==component).sum()),
            'endpoints':list(endpoints),'either_repair_door_can_be_blocked':True,
            'closed_exit_separates_exit':True,'locked_reward_room_separate':True}
results['human_proxy']=route_checks(.35)
results['giant_conservative_circular_proxy']=route_checks(1.375)
results['dimensions']={k:d[k] for k in ['open_passage_wall_gap_width','open_passage_wall_gap_height','exit_wall_gap','reward_wall_gap','room_height','wall_thickness']}
results['counts']={'mesh_boxes':sum(bool(n['material']) for n in nodes),
                   'box_triangles_including_inactive_guide':12*sum(bool(n['material']) for n in nodes),
                   'solid_box_colliders':sum(n['collider'] and not n['trigger'] for n in nodes),
                   'marker_trigger_volumes':sum(n['trigger'] for n in nodes)}
results['pending']=['Unity 2022.3.55f1 import','Unity Play Mode','Gorilla locomotion hand/body collision',
                    'Actual Listener animated clearance','NavMesh bake','Photon PUN integration','Quest performance']
(HERE/'validation.json').write_text(json.dumps(results,indent=2)+'\n')
print(json.dumps(results,indent=2))
