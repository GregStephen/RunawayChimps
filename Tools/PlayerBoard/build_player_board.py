"""Rebuild the prototype prefab explicitly; never runs during Unity import.
Existing Hub placement prefab is preserved. Review overrides before regenerating.
"""
from pathlib import Path
import re, json, uuid
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/SocialSafety'
scene=ROOT/'Assets/Scenes/Hub_Base.unity'
scene_text=scene.read_text()
blocks={i:(k,b) for k,i,b in re.findall(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',scene_text,re.M|re.S)}
def guid(path):
    # Match the committed asset IDs on every authoring OS. The preserved Hub
    # placement prefab references these IDs, so native Windows separators would
    # otherwise break its nested board reference after rebuilding.
    return uuid.uuid5(uuid.NAMESPACE_URL,'runawaychimps/player-board/'+str(path).replace('\\','/')).hex
def meta(path, importer=None, folder=False):
    p=ROOT/path
    if importer is None: importer='DefaultImporter:\n  externalObjects: {}\n'
    Path(str(p)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid(path)+'\n'+('folderAsset: yes\n' if folder else '')+importer+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    return guid(path)
OUT.mkdir(parents=True,exist_ok=True)
meta(OUT.relative_to(ROOT),folder=True)
HEADER='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
BASE='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
materials={}
for name,color in [('Frame',(.09,.14,.13)),('Screen',(.013,.029,.026)),('Button',(.045,.14,.11)),('Report',(.20,.14,.065)),('Row',(.027,.062,.052))]:
    p=OUT/(name+'.mat')
    # Shared unlit shader avoids dependence on room illumination or extra lights.
    rgb=', '.join(f'{k}: {v}' for k,v in zip('rgb',color))+', a: 1'
    p.write_text(HEADER+'--- !u!21 &2100000\nMaterial:\n'+BASE+'  serializedVersion: 8\n  m_Name: PlayerBoard_'+name+'\n  m_Shader: {fileID: 4800000, guid: 86d26cfe9c915d569fa35bf50b418f3d, type: 3}\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats: []\n    m_Colors:\n    - _Color: {'+rgb+'}\n  m_BuildTextureStacks: []\n')
    materials[name]=meta(p.relative_to(ROOT),'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n')
objects=[];nextid=1000
def alloc():
    global nextid
    nextid+=1;return nextid
def vec(values,keys='xyz'):return '{'+', '.join(f'{k}: {v}' for k,v in zip(keys,values))+'}'
def node(name,pos=(0,0,0),scale=(1,1,1),parent=None,rotation=(0,0,0,1),rect=None):
    obj={'name':name,'go':alloc(),'tr':alloc(),'pos':pos,'scale':scale,'rot':rotation,'parent':parent,'children':[],'components':[],'rect':rect}
    if parent:parent['children'].append(obj)
    objects.append(obj);return obj
def component(obj,kind,body):
    i=alloc();obj['components'].append((i,kind,body));return i
renderer_template=blocks['1382642822'][1]
def render(obj,mat):
    body=renderer_template.replace('fileID: 1382642820',f"fileID: {obj['go']}")
    body=re.sub(r'  m_Materials:\n  - .*',f'  m_Materials:\n  - {mat}',body)
    body=body.replace('m_LightProbeUsage: 1','m_LightProbeUsage: 0').replace('m_ReflectionProbeUsage: 1','m_ReflectionProbeUsage: 0')
    return component(obj,23,body)
def box(name,pos,scale,mat,parent):
    o=node(name,pos,scale,parent)
    component(o,33,'MeshFilter:\n'+BASE+f"  m_GameObject: {{fileID: {o['go']}}}\n  m_Mesh: {{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}}\n")
    render(o,f'{{fileID: 2100000, guid: {materials[mat]}, type: 2}}')
    return o
def collider(o,center,size):
    return component(o,65,'BoxCollider:\n'+BASE+f"  m_GameObject: {{fileID: {o['go']}}}\n"+'''  m_Material: {fileID: 0}
  m_IsTrigger: 1
  m_Enabled: 1
  serializedVersion: 3
'''+f'  m_Size: {vec(size)}\n  m_Center: {vec(center)}\n')
def text(name,value,pos,width,height,size,parent):
    o=node(name,pos,(.01,.01,.01),parent,(0,1,0,0),(width*100,height*100))
    renderer=render(o,'{fileID: 2180264, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}')
    body=blocks['1382642821'][1].replace('fileID: 1382642820',f"fileID: {o['go']}").replace('fileID: 1382642822',f'fileID: {renderer}')
    fields={'m_text':json.dumps(value),'m_RaycastTarget':'0','m_fontSize':str(size),'m_fontSizeBase':str(size),'m_fontStyle':'0','m_HorizontalAlignment':'2','m_VerticalAlignment':'512','m_margin':'{x: 0, y: 0, z: 0, w: 0}','m_enableWordWrapping':'0','m_overflowMode':'1','m_isRichText':'0'}
    for k,v in fields.items():body=re.sub(r'^  '+k+r': .*$',f'  {k}: {v}',body,flags=re.M)
    body=re.sub(r'^  m_fontColor: .*$', '  m_fontColor: {r: 0.83, g: 0.95, b: 0.87, a: 1}',body,flags=re.M)
    return component(o,114,body)


def script(o,filename,fields=''):
    return component(o,114,'MonoBehaviour:\n'+BASE+f"  m_GameObject: {{fileID: {o['go']}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid('Assets/Scripts/SocialSafety/'+filename+'.cs')}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier:\n"+fields)
root=node('PlayerBoard')
board_id=alloc()
# Rigidbody belongs to the board; every action collider is trigger-only.
component(root,54,'Rigidbody:\n'+BASE+f"  m_GameObject: {{fileID: {root['go']}}}\n  serializedVersion: 4\n  m_Mass: 1\n  m_Drag: 0\n  m_AngularDrag: 0.05\n  m_UseGravity: 0\n  m_IsKinematic: 1\n  m_Interpolate: 0\n  m_Constraints: 0\n  m_CollisionDetection: 0\n")
box('Bezel',(0,0,-.035),(1.64,1.40,.07),'Frame',root)
box('Display',(0,0,.005),(1.58,1.34,.018),'Screen',root)
heading=text('Heading','PLAYERS / ROOM ROSTER',(0,.57,.023),1.46,.09,50,root)
text('Subtitle','SECURITY NETWORK  /  HOLD Y FOR PLAYERS', (0,.477,.023),1.45,.055,25,root)
status=text('Status','Mute only affects what you hear.',(0,-.57,.035),1.43,.10,28,root)
roster=node('Roster',parent=root)
report=node('ReportConfirmation',parent=root);report['active']=0

def button(name,value,pos,size,action,index,parent):
    o=node(name,pos,parent=parent)
    box('Cap',(0,0,0),(size[0],size[1],.025),'Report' if action in (1,5) else 'Button',o)
    collider(o,(0,0,.015),(size[0],size[1],.05))
    label=text('Label',value,(0,0,.018),size[0]-.018,size[1]-.012,30,o)
    return script(o,'PlayerBoardButton',f'  board: {{fileID: {board_id}}}\n  action: {action}\n  index: {index}\n  label: {{fileID: {label}}}\n')
rows=[]
for i in range(5):
    o=node('PlayerRow'+str(i+1),(0,.335-i*.16,.03),parent=roster)
    box('Background',(0,0,0),(1.48,.145,.015),'Row',o)
    name=text('Name','WAITING FOR PLAYER',(-.29,.03,.012),.8,.052,32,o)
    detail=text('Sector','',(-.29,-.028,.012),.8,.038,23,o)
    mute=button('Mute','MUTE',(.28,0,.026),(.23,.103),0,i,o)
    flag=button('Report','REPORT',(.585,0,.026),(.25,.103),1,i,o)
    rows.append((o['go'],name,detail,mute,flag))
prev=button('Previous','PREV',(-.56,-.445,.03),(.24,.09),2,0,roster)
next_button=button('Next','NEXT',(.56,-.445,.03),(.24,.09),3,0,roster)
page=text('Page','PAGE 1 / 1',(0,-.445,.033),.6,.07,28,roster)
report_heading=text('ReportTarget','REPORT PLAYER',(0,.36,.034),1.42,.09,38,report)
help_text=text('ReportHelp','Choose a reason, then SEND REPORT.',(0,.255,.034),1.44,.12,24,report)
reason_buttons=[]
for i,label in enumerate(['Harassment / bullying','Hate speech','Inappropriate name','Cheating / exploiting','Other misconduct']):
    reason_buttons.append(button('Reason'+str(i),'[ ] '+label,(0,.10-i*.11,.032),(1.20,.094),4,i,report))
submit=button('SendReport','SEND REPORT',(.34,-.47,.03),(.56,.10),5,0,report)
cancel=button('Back','BACK',(-.44,-.47,.03),(.32,.10),6,0,report)
close=button('Close','X',(.735,.59,.055),(.10,.10),7,0,root)
fields=''
for key,value in dict(heading=heading,status=status,pageText=page,reportHeading=report_heading,reportHelp=help_text,rosterPanel=roster['go'],reportPanel=report['go'],submit=submit,previous=prev,next=next_button,close=close).items():
    fields+=f'  {key}: {{fileID: {value}}}\n'
fields+='  rows:\n'
for values in rows:
    for i,(key,value) in enumerate(zip(['root','nameText','detailText','mute','report'],values)):
        fields+=('  - ' if i==0 else '    ')+f'{key}: {{fileID: {value}}}\n'
fields+='  reasons:\n'+''.join(f'  - {{fileID: {value}}}\n' for value in reason_buttons)+'  portable: 0\n'
actual_id=script(root,'PlayerBoard',fields)
root['components'][-1]=(board_id,114,root['components'][-1][2])
yaml=HEADER
for o in objects:
    yaml+=f"--- !u!1 &{o['go']}\nGameObject:\n"+BASE+'  serializedVersion: 6\n  m_Component:\n'+f"  - component: {{fileID: {o['tr']}}}\n"
    for i,_,_ in o['components']:yaml+=f'  - component: {{fileID: {i}}}\n'
    yaml+=f"  m_Layer: 0\n  m_Name: {o['name']}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: {o.get('active',1)}\n"
    rect=o['rect']
    yaml+=f"--- !u!{224 if rect else 4} &{o['tr']}\n{'RectTransform' if rect else 'Transform'}:\n"+BASE+f"  m_GameObject: {{fileID: {o['go']}}}\n"
    if not rect:yaml+='  serializedVersion: 2\n'
    yaml+=f"  m_LocalRotation: {vec(o['rot'],'xyzw')}\n  m_LocalPosition: {vec(o['pos'])}\n  m_LocalScale: {vec(o['scale'])}\n  m_ConstrainProportionsScale: 0\n"
    yaml+='  m_Children:'+ ('\n'+''.join(f"  - {{fileID: {c['tr']}}}\n" for c in o['children']) if o['children'] else ' []\n')
    yaml+=f"  m_Father: {{fileID: {o['parent']['tr'] if o['parent'] else 0}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: {180 if rect else 0}, z: 0}}\n"
    if rect:yaml+=f"  m_AnchorMin: {{x: 0.5, y: 0.5}}\n  m_AnchorMax: {{x: 0.5, y: 0.5}}\n  m_AnchoredPosition: {{x: {o['pos'][0]}, y: {o['pos'][1]}}}\n  m_SizeDelta: {vec(rect,'xy')}\n  m_Pivot: {{x: 0.5, y: 0.5}}\n"
    for i,c,t in o['components']:yaml+=f'--- !u!{c} &{i}\n'+t
p=OUT/'PlayerBoard.prefab';p.write_text('\n'.join(line.rstrip() for line in yaml.splitlines())+'\n')
pg=meta(p.relative_to(ROOT),'PrefabImporter:\n  externalObjects: {}\n')
instance=8300940000
placement=OUT/'PlayerBoardHub.prefab'
if not placement.exists():
    addition=HEADER+'''--- !u!1 &100
GameObject:
'''+BASE+'''  serializedVersion: 6
  m_Component:
  - component: {fileID: 101}
  m_Layer: 0
  m_Name: PlayerBoardHub
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &101
Transform:
'''+BASE+'''  m_GameObject: {fileID: 100}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 8300940001}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
'''
    addition+=f"--- !u!1001 &{instance}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {{fileID: 101}}\n    m_Modifications:\n"
    for prop,val in [('m_LocalPosition.x',-1.68),('m_LocalPosition.y',1.45),('m_LocalPosition.z',-3.10),('m_LocalRotation.x',0),('m_LocalRotation.y',0),('m_LocalRotation.z',0),('m_LocalRotation.w',1)]:
        addition+=f'    - target: {{fileID: {root["tr"]}, guid: {pg}, type: 3}}\n      propertyPath: {prop}\n      value: {val}\n      objectReference: {{fileID: 0}}\n'
    addition+=f"    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {{fileID: 100100000, guid: {pg}, type: 3}}\n--- !u!4 &{instance+1} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {root['tr']}, guid: {pg}, type: 3}}\n  m_PrefabInstance: {{fileID: {instance}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
    placement.write_text(addition)
    meta(placement.relative_to(ROOT),'PrefabImporter:\n  externalObjects: {}\n')
print('Authored PlayerBoard prefab with five paged rows, report form, and Hub placement prefab.')
