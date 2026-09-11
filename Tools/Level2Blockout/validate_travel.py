"""Source/serialized travel checks. Does not execute Unity, Photon, or XR code."""
from pathlib import Path
import json,re,subprocess
import yaml
from tree_sitter import Language,Parser
import tree_sitter_c_sharp
ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent

def read(path):
    p=ROOT/path
    return p.read_text() if p.exists() else subprocess.check_output(['git','show','HEAD:'+path],cwd=ROOT,text=True)

def guid(path):return re.search(r'^guid: (\w+)',read(path+'.meta'),re.M)[1]

def parse(s):
    return list(yaml.safe_load_all(re.sub(r'^--- !u!\d+ &-?\d+(?: stripped)?','---',re.sub(r'^%.*\n','',s,flags=re.M),flags=re.M)))

def refs(path):
    s=read(path); ids=re.findall(r'^--- !u!\d+ &(-?\d+)',s,re.M)
    assert len(ids)==len(set(ids)),path
    known=set(map(int,ids))|{0}
    for m in re.finditer(r'\{fileID: (-?\d+)([^}]*)\}',s):
        if 'guid:' not in m[2]:assert int(m[1]) in known,(path,m[0])
    return len(ids)

changed=['Assets/Scripts/KeyBox.cs','Assets/Scripts/KeyCard.cs','Assets/Scripts/VRKeyCard.cs',
         'Assets/Scripts/Computer/ComputerTerminalUi.cs','Assets/Scripts/Editor/SectorTravelValidator.cs',
         'Assets/Scripts/Travel/SectorPresence.cs','Assets/Scripts/Travel/SectorTravelService.cs',
         'Assets/Scripts/Travel/SectorDestinations.cs','Assets/Scripts/Travel/LevelTerminalActions.cs',
         'Assets/Scripts/Travel/ReturnToSecurityButton.cs']
parser=Parser(Language(tree_sitter_c_sharp.language()))
for path in changed:assert not parser.parse(read(path).encode()).root_node.has_error,path
assert guid('Assets/Scripts/KeyCard.cs')=='4e535a396467fea4587e1f53f2d211ec'
assert guid('Assets/Scripts/VRKeyCard.cs')=='eff9fc0b37638d040a380d842b649c82'
for path in ['Assets/Scripts/KeyCard.cs','Assets/Scripts/VRKeyCard.cs']:
    assert 'class '+Path(path).stem+' :' in read(path)

level2='Assets/RunawayChimps/Level2Blockout/Scenes/Level2_BehavioralConditioning_Blockout.unity'
prefab='Assets/RunawayChimps/Level2Blockout/Prefabs/Level2_Blockout.prefab'
level1='Assets/Scenes/Level1_Containment.unity'
build=parse(read('ProjectSettings/EditorBuildSettings.asset'))[0]['EditorBuildSettings']['m_Scenes']
enabled=[e['path'] for e in build if e['enabled']]
assert len(enabled)==len(set(enabled))
assert enabled[:5]==['Assets/Scenes/Bootstrap.unity','Assets/Scenes/Loading.unity','Assets/Scenes/Hub_Base.unity',level1,level2]
assert next(e for e in build if e['path']==level2)['guid']==guid(level2)
counts={p:refs(p) for p in [level1,level2,prefab]}
for path in [level2,prefab]:
    s=read(path);docs=parse(s);ids=[int(i) for i in re.findall(r'^--- !u!\d+ &(-?\d+)',s,re.M)]
    byid=dict(zip(ids,docs))
    scripts=[d['MonoBehaviour'] for d in docs if 'MonoBehaviour' in d]
    contexts=[b for b in scripts if b['m_Script']['guid']==guid('Assets/Scripts/Travel/SectorScene.cs')]
    assert len(contexts)==1;context=contexts[0]
    assert context['sector']==3 and context['entryZone']==4
    spawn=byid[context['arrivalSpawn']['fileID']]['Transform']
    assert byid[spawn['m_GameObject']['fileID']]['GameObject']['m_Name']=='Level2EntrySpawn'
    p=spawn['m_LocalPosition'];assert p=={'x':3,'y':.05,'z':-2.4}
    actions=[b for b in scripts if b['m_Script']['guid']==guid('Assets/Scripts/Travel/LevelTerminalActions.cs')]
    assert len(actions)==1
    buttons=[b for b in scripts if b['m_Script']['guid']==guid('Assets/Scripts/Travel/ReturnToSecurityButton.cs')]
    assert len(buttons)==1
    button=buttons[0]
    assert byid[button['actions']['fileID']]['MonoBehaviour']==actions[0]
    cap=byid[button['buttonVisual']['fileID']]['Transform']
    assert byid[cap['m_GameObject']['fileID']]['GameObject']['m_Name']=='Return_Button_Cap'
    button_go=byid[button['m_GameObject']['fileID']]['GameObject']
    button_transform=byid[button_go['m_Component'][0]['component']['fileID']]['Transform']
    assert button_transform['m_LocalPosition']=={'x':.28,'y':1.25,'z':-2}
    trigger=next(byid[c['component']['fileID']]['BoxCollider'] for c in button_go['m_Component'] if 'BoxCollider' in byid[c['component']['fileID']])
    assert trigger['m_IsTrigger']==1 and trigger['m_Size']=={'x':.16,'y':.32,'z':.36}
    area=byid[actions[0]['safeEntryArea']['fileID']]['BoxCollider'];assert area['m_IsTrigger']==1 and area['m_Enabled']==1
    go=byid[area['m_GameObject']['fileID']]['GameObject'];assert go['m_IsActive']==1
    t=byid[go['m_Component'][0]['component']['fileID']]['Transform']
    for axis in 'xyz':assert abs(p[axis]-t['m_LocalPosition'][axis])<=t['m_LocalScale'][axis]/2

blocks=re.split(r'(?=^--- !u!)',read(level1),flags=re.M)
objectives=[b for b in blocks if 'guid: '+guid('Assets/Scripts/KeyBox.cs') in b]
assert len(objectives)==1 and 'travelToLevelTwoOnComplete: 1' in objectives[0]
required=int(re.search(r'keysNeeded: (\d+)',objectives[0])[1]);assert required==2
inline=sum('guid: '+guid('Assets/Scripts/KeyCard.cs') in b for b in blocks)
card_prefab='Assets/prefabs/KeyCard1.prefab'
instances=sum(b.startswith('--- !u!1001') and 'guid: '+guid(card_prefab) in b for b in blocks)
assert guid('Assets/Scripts/KeyCard.cs') in read(card_prefab)
assert inline+instances>=required
report={'csharp_syntax_files':len(changed),'card_script_guid_preserved':True,
        'enabled_destinations':enabled,'serialized_reference_counts':counts,
        'level2_arrival':[3,.05,-2.4],'terminal_area_contains_spawn':True,
        'return_button_action_visual_trigger_bindings':True,
        'level1_required_cards':required,'level1_card_instances':inline+instances,
        'pending':['Unity compilation/import','Editor validator execution','Play Mode and travel rollback',
                   'Local card interaction and completion retry','Two-client sector visibility/voice','Headset arrival clearance',
                   'Return button label/font/orientation, local-hand filtering, hold/release and failed-load retry']}
(HERE/'travel_validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
