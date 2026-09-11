"""Stdlib-only source/geometry validation for the Level 2 v0.4 blockout.

This proves generated YAML structure and conservative box-layout connectivity only.
It does not prove Unity import, linked mesh clearance, NavMesh, Photon, or headset behavior.
"""
from pathlib import Path
import collections,json,math,re
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
ASSETS=ROOT/'Assets/RunawayChimps/Level2Blockout'
d=json.loads((HERE/'layout.json').read_text()); nodes=d['nodes']; byid={n['id']:n for n in nodes}
manifest=json.loads((HERE/'gate_manifest.json').read_text())
assert d['version']=='0.4' and d['bounds']==[0,36,-4,32]
assert d['room_height']==5.0 and d['open_passage_wall_gap_width']==4.0
assert len(manifest['gates'])==5
results={'version':'0.4','bounds_m':d['bounds']}

# Serialized YAML/object-reference checks without requiring a YAML package.
for p in [ASSETS/'Scenes/Level2_BehavioralConditioning_Blockout.unity',ASSETS/'Prefabs/Level2_Blockout.prefab']:
    s=p.read_text(); ids=re.findall(r'^--- !u!\d+ &(\d+)',s,re.M)
    assert len(ids)==len(set(ids)),f'duplicate serialized id in {p}'
    known=set(map(int,ids))|{0}
    for m in re.finditer(r'\{fileID: (\d+)([^}]*)\}',s):
        if 'guid:' not in m[2]: assert int(m[1]) in known,(p,m[0])
    assert len(re.findall(r'^--- !u!1001 &',s,re.M))==5,p
    for g in manifest['gates']: assert g['name'] in s,(p,g['name'])
    for required in ['Level2_Blockout_v04','Level2EntrySpawn','HubReturnControlMarker','Return_To_Security_Button','RepairStrikeOrigin']:
        assert required in s,(p,required)
    for script_guid in ['f5804367cdd64d9988009ed4d4e40bb0','0a4725352759d7255301e5b06df6157b','1f618183dbed4d76a96a6e00b1057d73']:
        assert script_guid in s,(p,script_guid)
    results[p.name]={'serialized_objects':len(ids),'unique_ids':True,'local_references_resolve':True,'linked_gate_instances':5}

# Layout connectivity against authored box colliders.
def enabled(n): return n['active'] and (enabled(byid[n['parent']]) if n['parent'] else True)
solids=[]
for n in nodes:
    if not n['collider'] or n['trigger'] or not enabled(n):continue
    x,y,z=n['position']; sx,sy,sz=n['scale']
    if y+sy/2<=.01 or y-sy/2>=3.2:continue
    solids.append((x-sx/2,z-sz/2,x+sx/2,z+sz/2,n['name'],y-sy/2))
rooms=d['rooms']; step=.2; xmin,xmax,zmin,zmax=-1,37,-5,33
nx=round((xmax-xmin)/step)+1; nz=round((zmax-zmin)/step)+1
def pt(i,j): return xmin+i*step,zmin+j*step
def idx(p): return round((p[0]-xmin)/step),round((p[1]-zmin)/step)
def in_floor(x,z): return any(x0<=x<=x1 and z0<=z<=z1 for _,x0,x1,z0,z1,_ in rooms)
def rect_hit(x,z,r,rect):
    x0,z0,x1,z1=rect; dx=max(x0-x,0,x-x1); dz=max(z0-z,0,z-z1)
    return dx*dx+dz*dz <= r*r+1e-9
def passable(x,z,r,body_height,ignored_prefixes=(),virtual_blockers=()):
    samples=[(x,z)]+[(x+r*math.cos(k*math.pi/4),z+r*math.sin(k*math.pi/4)) for k in range(8)]
    if not all(in_floor(a,b) for a,b in samples): return False
    for x0,z0,x1,z1,name,bottom in solids:
        if bottom>=body_height:continue
        if any(name.startswith(q) for q in ignored_prefixes):continue
        if rect_hit(x,z,r,(x0,z0,x1,z1)): return False
    for rect in virtual_blockers:
        if rect_hit(x,z,r,rect): return False
    return True
def flood(radius,body_height,ignored_prefixes=(),virtual_blockers=()):
    start=idx((3,-2)); assert passable(*pt(*start),radius,body_height,ignored_prefixes,virtual_blockers)
    q=collections.deque([start]); seen={start}
    while q:
        i,j=q.popleft()
        for di,dj in ((1,0),(-1,0),(0,1),(0,-1)):
            ni,nj=i+di,j+dj
            if not (0<=ni<nx and 0<=nj<nz) or (ni,nj) in seen:continue
            if passable(*pt(ni,nj),radius,body_height,ignored_prefixes,virtual_blockers): seen.add((ni,nj)); q.append((ni,nj))
    return seen
def reachable(seen,p): return idx(p) in seen

human_targets={'test_hall':(10,6),'service':(24,7),'west_observation':(4,19),'conditioning':(16,19),'east_bypass':(33,16),'north_gallery':(14,28),'repair_lab':(28,28),'exit_approach':(4,24.2)}
giant_targets={'test_hall':(10,3),'service':(24,7),'west_observation':(4,19),'conditioning':(16,19),'east_bypass':(33,16),'north_gallery':(14,28),'repair_lab':(28,28)}
for label,radius,body_height,targets in [('human_proxy',.35,2.0,human_targets),('listener_proxy',1.375,3.2,giant_targets)]:
    seen=flood(radius,body_height)
    assert all(reachable(seen,p) for p in targets.values()),(label,{k:reachable(seen,p) for k,p in targets.items()})
    # Either repair doorway may be cut while the other still connects the repair lab.
    gallery_cut=(19.9,26,20.1,30); bypass_cut=(31,23.9,35,24.1)
    assert reachable(flood(radius,body_height,virtual_blockers=(gallery_cut,)),(28,28)),(label,'gallery cut')
    assert reachable(flood(radius,body_height,virtual_blockers=(bypass_cut,)),(28,28)),(label,'bypass cut')
    results[label]={'radius_m':radius,'height_m':body_height,'reachable_targets':list(targets),'either_repair_route_can_be_blocked':True,'reachable_grid_points':len(seen)}
# Safe exit and optional reward remain sealed by their temporary blockers.
human=flood(.35,2.0)
assert not reachable(human,(4,29)),'closed exit blocker leaked'
assert not reachable(human,(33,-2)),'locked reward blocker leaked'
unlocked_reward=flood(.35,2.0,ignored_prefixes=('Reward_Room_Blocker',))
assert reachable(unlocked_reward,(33,-2)),'reward room unreachable after blocker removal'
results['temporary_barriers']={'closed_exit_separates_safe_exit':True,'locked_reward_room_separate':True,'reward_reachable_when_blocker_removed':True}
results['counts']={'authored_objects':len(nodes),'mesh_boxes':sum(bool(n['material']) for n in nodes),'solid_box_colliders':sum(n['collider'] and not n['trigger'] for n in nodes),'marker_triggers':sum(n['trigger'] for n in nodes)}
results['pending']=['Unity 2022.3.55f1 import/compile','actual linked gate mesh clearance','NavMesh bake','Listener animated turning/reach','Play Mode travel','two-client Photon PUN','Quest headset scale/performance']
(HERE/'validation.json').write_text(json.dumps(results,indent=2)+'\n')
print(json.dumps(results,indent=2))
