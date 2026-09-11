"""Generate Level 2 Behavioral Conditioning blockout v0.4 for Unity 2022.3."""
from pathlib import Path
import json, hashlib

ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'Assets/RunawayChimps/Level2Blockout'
OUT=ROOT/'Tools/Level2Blockout'
HEADER='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
BUILTIN='0000000000000000e000000000000000'
ROOM_HEIGHT=5.0
COLORS={
 'Concrete':[0.39,0.45,0.47],'Floor':[0.23,0.29,0.31],
 'SafeFloor':[0.19,0.38,0.34],'RepairFloor':[0.39,0.30,0.22],
 'BypassFloor':[0.25,0.33,0.37],'Obstacles':[0.23,0.32,0.36],
 'Metal':[0.15,0.19,0.20],'Amber':[0.94,0.51,0.13],
 'SafeTrim':[0.32,0.78,0.61],'LightStrip':[0.71,0.84,0.84],
 'RedMarker':[0.8,0.22,0.18],'Grid':[0.34,0.40,0.41],
 'RewardCyan':[0.15,0.68,0.80],
}
nodes=[]
def node(name,parent=None,pos=(0,0,0),scale=(1,1,1),material=None,collider=False,active=True,trigger=False,kind='box'):
 idx=1000+len(nodes)*10
 n=dict(id=idx,name=name,parent=parent,position=list(pos),scale=list(scale),material=material,collider=collider,active=active,trigger=trigger,kind=kind)
 nodes.append(n); return idx
def group(name,parent): return node(name,parent,kind='empty')
root=node('Level2_Blockout_v04',kind='empty')
floors=group('01_Floors',root); walls=group('02_Walls_and_Doorways',root)
ceilings=group('03_Ceilings__Toggle_for_Top_View',root); obstacles=group('04_Route_Shaping_Obstacles',root)
repair=group('05_Repair_Station__Visual_Only',root); shutter=group('06_Exit_Shutter__Historical_Inactive',root)
details=group('07_Trim_and_Wayfinding',root); markers=group('08_Gameplay_Markers__Not_Wired',root)
scale_group=node('09_Listener_Size_Guide__Enable_to_Check_Fit',root,active=False,kind='empty')
lights=group('10_Blockout_Lighting',root); gates=group('11_Existing_SciFi_Gates',root)
reward=group('12_Optional_Reward_Room__Visual_Only',root)
rooms=[
 ('Safe_Entry',0,6,-4,0,'SafeFloor'),
 ('Test_Hall_A',0,18,0,14,'Floor'),
 ('Lower_Service_Hall',18,30,0,14,'BypassFloor'),
 ('West_Observation_Wing',0,8,14,24,'Floor'),
 ('Conditioning_Hall_B',8,30,14,24,'Floor'),
 ('East_Bypass',30,36,2,24,'BypassFloor'),
 ('Exit_Approach',0,8,24,26,'Floor'),
 ('Completed_Exit',0,8,26,32,'SafeFloor'),
 ('North_Gallery',8,20,24,32,'BypassFloor'),
 ('Repair_Lab',20,36,24,32,'RepairFloor'),
 ('Optional_Reward_Room',30,36,-4,2,'BypassFloor'),
]
for name,x0,x1,z0,z1,mat in rooms:
 node(name+'_Floor',floors,((x0+x1)/2,-.125,(z0+z1)/2),(x1-x0,.25,z1-z0),mat,True)
 node(name+'_Ceiling',ceilings,((x0+x1)/2,ROOM_HEIGHT+.1,(z0+z1)/2),(x1-x0,.2,z1-z0),'Concrete',True)

def wall(name,a,b,height=ROOM_HEIGHT,y=None):
 x0,z0=a; x1,z1=b; assert x0==x1 or z0==z1
 return node(name,walls,((x0+x1)/2,height/2 if y is None else y,(z0+z1)/2),(.2 if x0==x1 else abs(x1-x0),height,.2 if z0==z1 else abs(z1-z0)),'Concrete',True)
def doorwall_h(name,z,lo,hi,openings,trim,opening_height=4.0):
 cursor=lo
 for i,(a,b) in enumerate(openings):
  if a>cursor: wall(name+'_Pier_'+str(i),(cursor,z),(a,z))
  wall(name+'_Header_'+str(i),(a,z),(b,z),ROOM_HEIGHT-opening_height,(ROOM_HEIGHT+opening_height)/2)
  node(name+'_Threshold_'+str(i),details,((a+b)/2,.006,z),(b-a,.012,.16),trim)
  cursor=b
 if cursor<hi: wall(name+'_Pier_End',(cursor,z),(hi,z))
def doorwall_v(name,x,lo,hi,openings,trim,opening_height=4.0):
 cursor=lo
 for i,(a,b) in enumerate(openings):
  if a>cursor: wall(name+'_Pier_'+str(i),(x,cursor),(x,a))
  wall(name+'_Header_'+str(i),(x,a),(x,b),ROOM_HEIGHT-opening_height,(ROOM_HEIGHT+opening_height)/2)
  node(name+'_Threshold_'+str(i),details,(x,.006,(a+b)/2),(.16,.012,b-a),trim)
  cursor=b
 if cursor<hi: wall(name+'_Pier_End',(x,cursor),(x,hi))

# Outer shell and isolated south rooms.
for name,a,b in [
 ('West_Outer',(0,-4),(0,32)),('North_Outer',(0,32),(36,32)),('East_Outer',(36,-4),(36,32)),
 ('Safe_South',(0,-4),(6,-4)),('Safe_East',(6,-4),(6,0)),
 ('South_Test',(6,0),(18,0)),('South_Service',(18,0),(30,0)),
 ('Reward_South',(30,-4),(36,-4)),('Reward_West',(30,-4),(30,2)),
 ('Exit_East',(8,26),(8,32)),('Gallery_Repair_North_Shell',(8,32),(20,32)),
]: wall(name,a,b)
# Safe entry, reward, exit, and repair gates.
doorwall_h('Entry_Door',0,0,6,[(1.4,4.6)],'SafeTrim',3.8)
doorwall_h('Reward_Door',2,30,36,[(31.9,34.1)],'RewardCyan',3.1)
doorwall_h('Exit_Door',26,0,8,[(2.825,5.175)],'SafeTrim',3.0)
doorwall_v('Repair_Gallery_Door',20,24,32,[(26,30)],'Amber',4.2)
doorwall_h('Repair_Bypass_Door',24,30,36,[(31,35)],'Amber',4.2)
# Internal route partitions. Openings deliberately create multiple loops.
doorwall_v('Test_Service_Divider',18,0,14,[(3.5,8.5)],'Amber',4.1)
doorwall_h('Hall_Middle_Divider',14,0,30,[(2,6),(11,16),(22,27)],'Amber',4.1)
doorwall_v('Observation_Conditioning_Divider',8,14,24,[(17,22)],'Amber',4.1)
doorwall_v('Service_Bypass_Divider',30,2,14,[(5,10)],'Amber',4.1)
doorwall_v('Conditioning_Bypass_Divider',30,14,24,[(17,22)],'Amber',4.1)
doorwall_h('Conditioning_Gallery_Divider',24,8,30,[(12,17)],'Amber',4.1)
wall('Exit_Approach_East',(8,24),(8,26))
# The exit approach remains open to the top of West Observation at z=24.

# Reward-room visual placeholders.
node('Reward_Scanner__Not_Wired',reward,(34.7,1.25,2.20),(.28,.44,.18),'Metal')
node('Reward_Scanner_Cyan_Face',reward,(34.7,1.27,2.31),(.22,.31,.025),'RewardCyan')
node('Required_Card_Sign__Cyan_Three_Bars_Proposed',reward,(34.7,1.92,2.20),(.56,.46,.09),'RewardCyan')
for x in [34.55,34.70,34.85]: node('Card_Symbol_Bar',reward,(x,1.92,2.255),(.06,.27,.025),'LightStrip')
node('Collectible_Plinth__Reward_TBD',reward,(33,0.45,-2.8),(1.1,.9,.65),'Metal',True)
node('Collectible_Placeholder__No_Grant',reward,(33,1.1,-2.8),(.3,.4,.3),'RewardCyan')
node('Currency_Cache_Placeholder__Optional_No_Grant',reward,(34,.22,-2.8),(.4,.44,.4),'Amber')
node('RewardRoomScannerMarker',markers,(34.7,1.25,2.32),kind='empty')
node('RewardRoomClaimMarker',markers,(33,.05,-2),kind='empty')
node('Reward_Room_Blocker__Remove_When_Unlock_Wired',markers,(33,1.55,2),(2.2,3.1,.2),collider=True,kind='empty')

# Test Hall A blockers.
for n,x,z,sx,sz in [('A',5.5,5.0,4.0,4.0),('B',12.0,8.0,2.0,6.0),('C',15.4,2.8,3.0,2.4)]:
 node('TestHall_Blocker_'+n,obstacles,(x,1.8,z),(sx,3.6,sz),'Obstacles',True)
 node('TestHall_Blocker_'+n+'_Top',details,(x,3.62,z),(sx+.06,.04,sz+.06),'Metal')
# Service machinery islands.
for n,x,z,sx,sz in [('A',22.0,4.0,5.0,2.0),('B',26.2,10.2,4.6,2.2)]:
 node('Service_Machinery_'+n,obstacles,(x,1.6,z),(sx,3.2,sz),'Metal',True)
 node('Service_Machinery_'+n+'_Band',details,(x,.65,z-sz/2-.02),(sx,.16,.03),'Amber')
# Conditioning acoustic baffles: staggered, not full walls.
for i,(x,z,sx,sz) in enumerate([(12.0,18.0,5.0,.7),(20.5,20.5,6.0,.7),(26.0,16.5,4.0,.7)]):
 node('Acoustic_Baffle_'+str(i+1),obstacles,(x,1.9,z),(sx,3.8,sz),'Obstacles',True)
# East bypass offset stubs, keeping >5m remaining clear width.
for i,(x,z) in enumerate([(35.6,7),(30.4,13),(35.6,19)]):
 node('Bypass_Offset_'+str(i+1),obstacles,(x,1.7,z),(.8,3.4,3.2),'Obstacles',True)

# Repair station on north wall, away from both escape gates.
node('Workbench_Top',repair,(28,1.0,31.45),(3.2,.14,.65),'Metal',True)
for x in [26.65,29.35]: node('Workbench_Leg',repair,(x,.47,31.45),(.12,.94,.5),'Metal',True)
node('Jammed_Release_Housing',repair,(28,1.45,31.38),(1.0,.75,.42),'Amber',True)
node('Strike_Plate',repair,(28,1.88,31.35),(.34,.10,.34),'LightStrip',True)
node('Hammer_Handle__Placeholder',repair,(29.0,1.10,31.42),(.05,.05,.45),'Concrete')
node('Hammer_Head__Placeholder',repair,(29.0,1.13,31.2),(.28,.15,.13),'Metal')
node('Conduit_Up',details,(28,3.0,31.78),(.07,2.4,.07),'Amber')
node('Conduit_West',details,(14,4.2,31.78),(28,.07,.07),'Amber')
node('Conduit_Down_To_Exit',details,(4,4.2,28.9),(.07,.07,5.8),'Amber')

# Historical exit shutter visual remains inactive; gate + blocker are authoritative blockout visuals.
node('Exit_Shutter_Leaf__Historical',shutter,(4,1.78,26),(3.16,3.56,.18),'Metal',True)
# Return control placeholder retained inactive; physical button below is wired.
placeholder=node('Hub_Return_Control_Placeholder',details,(.25,1.25,-2),(.3,.65,.5),'SafeTrim',True)

# Gameplay markers and patrol suggestion.
for name,p in [
 ('Level2EntrySpawn',(3,.05,-2.4)),('RepairStrikeOrigin',(28,1.9,31.35)),
 ('RepairApproach_Gallery',(20.8,.05,28)),('RepairApproach_Bypass',(33,.05,24.8)),
 ('ListenerSpawn',(7,.05,8)),('ExitQualificationMarker',(4,.05,25.2)),
 ('CompletedExitArrival',(4,.05,29)),('HubReturnControlMarker',(.5,1.3,-2)),
]: node(name,markers,p,kind='empty')
patrol=group('Suggested_Patrol_Points__Not_a_Chase_Path',markers)
patrol_pts=[(5,8),(15,11),(23,10),(33,8),(33,19),(33,27),(28,29),(20.8,28),(15,27),(15,20),(5,20),(4,12),(10,6)]
for i,(x,z) in enumerate(patrol_pts): node('Patrol_'+str(i+1),patrol,(x,.05,z),kind='empty')
node('Entry_Safe_Volume__Marker_Only',markers,(3,2,-2),(5.6,4,3.6),collider=True,trigger=True,kind='empty')
node('Completed_Exit_Safe_Volume__Marker_Only',markers,(4,2,29),(7.6,4,5.6),collider=True,trigger=True,kind='empty')
node('Listener_Envelope_2.75w_3.2h_1.6d',scale_group,(7,1.6,8),(2.75,3.2,1.6),'RedMarker')
# Qualification and reward blockers remain until gameplay rules are wired.
node('Exit_Blocker__Remove_When_Exit_Logic_Wired',markers,(4,1.5,26),(2.35,3.0,.2),collider=True,kind='empty')

# 2m floor grid to show scale without exploding object count.
for name,x0,x1,z0,z1,mat in rooms:
 for x in range(int(x0)+2,int(x1),2): node(name+'_Grid_X'+str(x),details,(x,.003,(z0+z1)/2),(.012,.006,z1-z0-.25),'Grid')
 for z in range(int(z0)+2,int(z1),2): node(name+'_Grid_Z'+str(z),details,((x0+x1)/2,.003,z),(x1-x0-.25,.006,.012),'Grid')
# Ceiling lights at readable landmarks.
light_pts=[(3,-2),(6,6),(14,11),(23,6),(5,19),(15,19),(25,19),(33,8),(33,19),(14,28),(28,28),(4,29)]
for x,z in light_pts:
 node('Ceiling_Light_Housing',details,(x,ROOM_HEIGHT-.18,z),(1.6,.12,.26),'Metal')
 node('Ceiling_Light_Diffuser',details,(x,ROOM_HEIGHT-.255,z),(1.35,.035,.18),'LightStrip')
node('Blockout_Fill_Light',lights,(18,8,14),kind='light')

# Physical RETURN TO SECURITY control, same local wiring contract as v0.3.
nodes[[n['id'] for n in nodes].index(placeholder)]['active']=False
button=node('Return_To_Security_Button',root,(.28,1.25,-2),collider=True,trigger=True,kind='empty')
node('Return_Button_Plate',button,(-.13,.12,0),(.08,.76,.62),'Metal')
node('Return_Button_Cap',button,(0,0,0),(.12,.30,.34),'RedMarker')
for y in [-.20,.44]:
 for z in [-.25,.25]: node('Return_Plate_Bolt',button,(-.08,y,z),(.025,.035,.035),'Concrete')
for y,z in [(.38,-.20),(-.18,.16),(.04,.24)]: node('Return_Plate_Paint_Chip',button,(-.088,y,z),(.008,.025,.065),'Concrete')
# Historical group hidden.
nodes[[n['id'] for n in nodes].index(shutter)]['active']=False

# --- Unity serialization helpers ---
def guid(path): return hashlib.md5(('RunawayChimps.Level2.v01/'+str(path)).encode()).hexdigest()
def relative(p): return str(p.relative_to(ROOT)).replace('\\','/')
def write(path,text): path.parent.mkdir(parents=True,exist_ok=True); path.write_text('\n'.join(line.rstrip() for line in text.splitlines())+'\n')
def meta(path,folder=False):
 rel=relative(path); g=guid(rel); importer='DefaultImporter'; extra=''
 if path.suffix=='.mat': importer='NativeFormatImporter'; extra='  mainObjectFileID: 2100000\n'
 elif path.suffix=='.prefab': importer='PrefabImporter'
 text=f'fileFormatVersion: 2\nguid: {g}\n'+('folderAsset: yes\n' if folder else '')
 text+=f'{importer}:\n  externalObjects: {{}}\n{extra}  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
 write(Path(str(path)+'.meta'),text); return g
def v3(vals): return '{'+', '.join(f'{k}: {v:.8g}' for k,v in zip('xyz',vals))+'}'
def base(cid,n): return f'  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {n["id"]}}}\n'
def block(tag,cid,title,body): return f'--- !u!{tag} &{cid}\n{title}:\n{body}'

for name,c in COLORS.items():
 p=ASSET/'Materials'/f'{name}.mat'; emission=c if name=='LightStrip' else [0,0,0]
 s=HEADER+block(21,2100000,'Material',f'''  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}
  m_ValidKeywords: {"[_EMISSION]" if name=='LightStrip' else '[]'}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _Glossiness: 0.15
    - _Metallic: 0
    - _Mode: 0
    - _SrcBlend: 1
    - _DstBlend: 0
    - _ZWrite: 1
    m_Colors:
    - _Color: {{r: {c[0]}, g: {c[1]}, b: {c[2]}, a: 1}}
    - _EmissionColor: {{r: {emission[0]}, g: {emission[1]}, b: {emission[2]}, a: 1}}
  m_BuildTextureStacks: []
''')
 write(p,s); meta(p)

def travel_component(n):
 byname={v['name']:v for v in nodes}
 if n['id']==root:
  return ('f5804367cdd64d9988009ed4d4e40bb0',f'  sector: 3\n  entryZone: 4\n  arrivalSpawn: {{fileID: {byname["Level2EntrySpawn"]["id"]+1}}}\n  doorArrivalSpawn: {{fileID: 0}}\n')
 if n['name']=='HubReturnControlMarker':
  return ('0a4725352759d7255301e5b06df6157b',f'  safeEntryArea: {{fileID: {byname["Entry_Safe_Volume__Marker_Only"]["id"]+4}}}\n')
 if n['name']=='Return_To_Security_Button':
  return ('1f618183dbed4d76a96a6e00b1057d73',f'  actions: {{fileID: {byname["HubReturnControlMarker"]["id"]+6}}}\n  buttonVisual: {{fileID: {byname["Return_Button_Cap"]["id"]+1}}}\n')
 return None

def serialize_nodes():
 blocks=[]
 for n in nodes:
  i=n['id']; comps=[i+1]
  if n['material']: comps += [i+2,i+3]
  if n['collider']: comps += [i+4]
  if n['kind']=='light': comps += [i+5]
  travel=travel_component(n)
  if travel: comps += [i+6]
  cs=''.join(f'  - component: {{fileID: {c}}}\n' for c in comps)
  blocks.append(block(1,i,'GameObject',f'''  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{cs}  m_Layer: 0
  m_Name: {json.dumps(n['name'])}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: {int(n['active'])}
'''))
  children=[v['id']+1 for v in nodes if v['parent']==i]
  ch='\n'+''.join(f'  - {{fileID: {c}}}\n' for c in children).rstrip('\n') if children else ' []'
  rot='{x: 0.3535534, y: -0.3535534, z: 0.1464466, w: 0.8535534}' if n['kind']=='light' else '{x: 0, y: 0, z: 0, w: 1}'
  blocks.append(block(4,i+1,'Transform',base(i+1,n)+f'''  serializedVersion: 2
  m_LocalRotation: {rot}
  m_LocalPosition: {v3(n['position'])}
  m_LocalScale: {v3(n['scale'])}
  m_ConstrainProportionsScale: 0
  m_Children:{ch}
  m_Father: {{fileID: {n['parent']+1 if n['parent'] else 0}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''))
  if travel:
   script_guid,fields=travel
   blocks.append(block(114,i+6,'MonoBehaviour',base(i+6,n)+f'''  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
'''+fields))
  if n['material']:
   blocks.append(block(33,i+2,'MeshFilter',base(i+2,n)+f'  m_Mesh: {{fileID: 10202, guid: {BUILTIN}, type: 0}}\n'))
   mg=guid(relative(ASSET/'Materials'/f'{n["material"]}.mat'))
   blocks.append(block(23,i+3,'MeshRenderer',base(i+3,n)+f'''  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {mg}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
'''))
  if n['collider']:
   blocks.append(block(65,i+4,'BoxCollider',base(i+4,n)+f'''  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: {int(n['trigger'])}
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {v3((.16,.32,.36) if n['name']=='Return_To_Security_Button' else (1,1,1))}
  m_Center: {{x: 0, y: 0, z: 0}}
'''))
  if n['kind']=='light':
   blocks.append(block(108,i+5,'Light',base(i+5,n)+'''  m_Enabled: 1
  serializedVersion: 10
  m_Type: 1
  m_Shape: 0
  m_Color: {r: 0.82, g: 0.9, b: 1, a: 1}
  m_Intensity: 0.9
  m_Range: 18
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.80208
  m_CookieSize: 10
  m_Shadows:
    m_Type: 0
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 1
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
  m_Cookie: {fileID: 0}
  m_DrawHalo: 0
  m_Flare: {fileID: 0}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {x: 1, y: 1}
  m_BounceIntensity: 1
  m_ColorTemperature: 6500
  m_UseColorTemperature: 0
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
'''))
 return ''.join(blocks)

render_settings=block(104,2,'RenderSettings','''  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {r: 0.1, g: 0.13, b: 0.15, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 100
  m_AmbientSkyColor: {r: 0.45, g: 0.48, b: 0.5, a: 1}
  m_AmbientEquatorColor: {r: 0.3, g: 0.32, b: 0.34, a: 1}
  m_AmbientGroundColor: {r: 0.15, g: 0.17, b: 0.18, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 3
  m_SubtractiveShadowColor: {r: 0.42, g: 0.48, b: 0.62, a: 1}
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_UseRadianceAmbientProbe: 0
''')
body=serialize_nodes()
for rel,contents in [('Prefabs/Level2_Blockout.prefab',HEADER+body),('Scenes/Level2_BehavioralConditioning_Blockout.unity',HEADER+render_settings+body)]:
 p=ASSET/rel; write(p,contents); meta(p)
for folder in [ASSET.parent,ASSET,*[p for p in ASSET.rglob('*') if p.is_dir()]]: meta(folder,True)
from gate_integration import apply_to_assets
gate_manifest=apply_to_assets(ASSET,gates+1)
write(OUT/'gate_manifest.json',json.dumps(gate_manifest,indent=2)+'\n')
layout=dict(version='0.4',units='metres',axes='Unity X right, Y up, Z north',bounds=[0,36,-4,32],
 open_passage_wall_gap_width=4.0,open_passage_wall_gap_height=4.2,entry_wall_gap=[3.2,3.8],
 exit_wall_gap=[2.35,3.0],reward_wall_gap=[2.2,3.1],
 gate_clearance_status='Wall gaps only; actual mesh opening and threshold need Unity inspection',
 room_height=ROOM_HEIGHT,wall_thickness=.2,room_dimensions='Wall centreline dimensions',rooms=rooms,colors=COLORS,nodes=nodes,gates=gate_manifest)
write(OUT/'layout.json',json.dumps(layout,indent=2)+'\n')
print(json.dumps({'version':'0.4','objects':len(nodes),'mesh_boxes':sum(bool(n['material']) for n in nodes),'colliders':sum(n['collider'] for n in nodes),'bounds':[0,36,-4,32]}))
