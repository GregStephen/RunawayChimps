"""Place linked instances of existing Runaway Chimps sci-fi gate prefabs."""
import re

PREFAB_GUID='22d2c44fed4ffa743aac7afe4d993905'
MESH_GUID='eca90685bd7c0e14c985891ef5c0a3f3'
ROOT_T=4972167997025879799
ROOT_GO=4972167997025983191
FRAME_GO=4972167997025983197
LEFT_GO=4972167997025983193
RIGHT_GO=4972167997025983199
EXIT_SCALE=[1,1.1564301,.8]
ENTRY_SCALE=[1,1.46,1.085]
CHASE_SCALE=[1,1.55,1.42]
SMALL_GUID='b581fdbd127292d44ab21c4b16d91d05'
SMALL_SCALE=[1,1.3197935,1.2965604]
GATES=[
    dict(name='Entry_Frame_Only',center_x=3,z=0,scale=ENTRY_SCALE,y=2.07101,frame_only=True,axis='z'),
    dict(name='Repair_Gallery_Frame_Only',x=20,center_z=28,scale=CHASE_SCALE,y=2.16,frame_only=True,axis='x'),
    dict(name='Repair_Bypass_Frame_Only',center_x=33,z=24,scale=CHASE_SCALE,y=2.16,frame_only=True,axis='z'),
    dict(name='Exit_Gate__Matches_Level1_Scale',center_x=4,z=26,scale=EXIT_SCALE,y=1.65,frame_only=False,axis='z'),
    dict(name='Reward_Room_Gate__Level1_Small_Entrance',center_x=33,z=2,scale=SMALL_SCALE,y=1.5500001,frame_only=False,small=True,axis='z'),
]

def instance(g,index,parent):
    iid=900000+index*100; tid=iid+1
    small=g.get('small',False)
    prefab_guid=SMALL_GUID if small else PREFAB_GUID
    root_t=8875449794350432198 if small else ROOT_T
    root_go=8875449794350654438 if small else ROOT_GO
    def ref(fid): return f'{{fileID: {fid}, guid: {prefab_guid}, type: 3}}'
    props=[]
    def prop(fid,key,value):
        props.append(f'    - target: {ref(fid)}\n      propertyPath: {key}\n      value: {value}\n      objectReference: {{fileID: 0}}\n')
    offset=0 if small else .89539504*g['scale'][2]
    if g['axis']=='z':
        position=[g['center_x']+offset,g['y'],g['z']]
        rot=[0,.7071067812,0,.7071067812]; euler=90
    else:
        position=[g['x'],g['y'],g['center_z']+offset]
        rot=[0,0,0,1]; euler=0
    for key,values in [('m_LocalPosition',position),('m_LocalScale',g['scale'])]:
        for axis,value in zip('xyz',values): prop(root_t,key+'.'+axis,value)
    for axis,value in zip('xyzw',rot): prop(root_t,'m_LocalRotation.'+axis,value)
    prop(root_t,'m_LocalEulerAnglesHint.y',euler)
    prop(root_go,'m_Name',g['name'])
    for go in ([8875449794350654444] if small else [LEFT_GO,RIGHT_GO]):
        prop(go,'m_IsActive',0 if g['frame_only'] else 1)
    parts=[(FRAME_GO,4300004)] if g['frame_only'] else [(FRAME_GO,4300004),(LEFT_GO,4300000),(RIGHT_GO,4300002)]
    if small: parts=[(8875449794350654436,4300008),(8875449794350654444,4300006)]
    additions=''; colliders=''
    for j,(go,mesh) in enumerate(parts):
        local_go=iid+10+j*2; cid=local_go+1
        additions+=f'    - targetCorrespondingSourceObject: {ref(go)}\n      insertIndex: -1\n      addedObject: {{fileID: {cid}}}\n'
        colliders+=f'''--- !u!1 &{local_go} stripped
GameObject:
  m_CorrespondingSourceObject: {ref(go)}
  m_PrefabInstance: {{fileID: {iid}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!64 &{cid}
MeshCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {local_go}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 5
  m_Convex: 0
  m_CookingOptions: 30
  m_Mesh: {{fileID: {mesh}, guid: {MESH_GUID}, type: 3}}
'''
    text=f'''--- !u!1001 &{iid}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: {parent}}}
    m_Modifications:
'''+''.join(props)+f'''    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents:
{additions}  m_SourcePrefab: {{fileID: 100100000, guid: {prefab_guid}, type: 3}}
--- !u!4 &{tid} stripped
Transform:
  m_CorrespondingSourceObject: {ref(root_t)}
  m_PrefabInstance: {{fileID: {iid}}}
  m_PrefabAsset: {{fileID: 0}}
'''+colliders
    return tid,text

def apply_to_assets(asset_dir,parent_transform):
    additions=[instance(g,i,parent_transform) for i,g in enumerate(GATES)]
    for p in [*asset_dir.rglob('*.unity'),*asset_dir.rglob('*.prefab')]:
        s=p.read_text()
        pat=r'(^--- !u!4 &'+str(parent_transform)+r'\nTransform:\n(?:(?!^---).)*?  m_Children:) \[\]'
        children='\n'+''.join(f'  - {{fileID: {tid}}}\n' for tid,_ in additions).rstrip('\n')
        s,n=re.subn(pat,lambda m:m[1]+children,s,flags=re.M|re.S)
        assert n==1,(p,'gate parent not found')
        p.write_text(s+''.join(body for _,body in additions))
    return dict(prefab_path='Assets/MASH Virtual/Sci Fi Doors/Prefab/Sci Fi Gates.prefab',
                prefab_guid=PREFAB_GUID,source_scene='Assets/Scenes/Level1_Containment.unity',
                source_object='Level2_Entrance_Door',source_instance=702843205,
                exit_scale=EXIT_SCALE,entry_frame_scale=ENTRY_SCALE,chase_frame_scale=CHASE_SCALE,
                gates=GATES,small_prefab_path='Assets/MASH Virtual/Sci Fi Doors/Prefab/Gate_Small.prefab',
                small_prefab_guid=SMALL_GUID,small_source_object='Level1_Entrance_Door',
                small_source_instance=1294156995,small_scale=SMALL_SCALE,
                reference_status='Existing project assets required; no source prefab modified')
