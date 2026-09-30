#!/usr/bin/env python3
"""Explicit offline authoring of this toy's serialized prefab and original PCM audio.
Not an Editor hook. Run from any directory; writes only the feature's asset subtree.
"""
from pathlib import Path
import math
import random
import struct
import uuid
import wave

ROOT = Path(__file__).resolve().parents[2]
ASSET = Path('Assets/RunawayChimps/KnockBackVent')
NS = uuid.UUID('39029611-14bb-47dd-8142-63a391140cf0')


def guid(path):
    return uuid.uuid5(NS, str(path).replace('\\', '/')).hex


def write(path, content):
    p = ROOT / path
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding='utf-8')


def meta(path, kind='DefaultImporter', extra=''):
    write(str(path) + '.meta', f'fileFormatVersion: 2\nguid: {guid(path)}\n{kind}:\n  externalObjects: {{}}\n{extra}  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


def folders(path):
    p = ROOT / path
    p.mkdir(parents=True, exist_ok=True)
    for d in [p] + list(p.parents):
        if d == ROOT / 'Assets':
            break
        rel = d.relative_to(ROOT)
        if not (ROOT / (str(rel) + '.meta')).exists():
            write(str(rel) + '.meta', f'fileFormatVersion: 2\nguid: {guid(rel)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


def audio(name, seconds, frequencies, decay, seed):
    path = ASSET / 'Audio' / (name + '.wav')
    folders(path.parent)
    rng = random.Random(seed)
    rate = 22050
    samples = []
    previous_noise = 0
    for i in range(round(rate * seconds)):
        t = i / rate
        attack = min(1, t / 0.0015)
        fade = min(1, (seconds - t) / 0.025)
        previous_noise = previous_noise * 0.5 + rng.uniform(-1, 1) * 0.5
        body = sum(math.sin(2 * math.pi * f * t) * math.exp(-t * (decay + j * 7)) / (1 + j * 0.7)
                   for j, f in enumerate(frequencies))
        samples.append((body + previous_noise * math.exp(-t * 130) * 0.75) * attack * fade)
    peak = max(abs(x) for x in samples)
    pcm = b''.join(struct.pack('<h', round(s * 0.72 / peak * 32767)) for s in samples)
    with wave.open(str(ROOT / path), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(pcm)
    meta(path, 'AudioImporter', '''  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
''')


def material(name, rgb, metallic):
    path = ASSET / 'Materials' / (name + '.mat')
    folders(path.parent)
    write(path, f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}
  m_ValidKeywords: []
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
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Glossiness: 0.27
    - _Metallic: {metallic}
    - _Mode: 0
    - _SrcBlend: 1
    - _DstBlend: 0
    - _ZWrite: 1
    m_Colors:
    - _Color: {{r: {rgb[0]}, g: {rgb[1]}, b: {rgb[2]}, a: 1}}
    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 1}}
  m_BuildTextureStacks: []
''')
    meta(path, 'NativeFormatImporter', '  mainObjectFileID: 2100000\n')


def vec(v):
    return '{' + ', '.join(f'{axis}: {value}' for axis, value in zip('xyzw', v)) + '}'


def ref(value):
    return f'{{fileID: {value}}}'


COMMON = '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
blocks = []


def block(tag, fid, name, body):
    blocks.append(f'--- !u!{tag} &{fid}\n{name}:\n' + COMMON + body)


nodes = []


def node(name, parent=0, position=(0, 0, 0), scale=(1, 1, 1), rotation=(0, 0, 0, 1), rect=False):
    n = dict(id=1000 + len(nodes) * 100, name=name, parent=parent, position=position, scale=scale,
             rotation=rotation, rect=rect, components=[], children=[])
    n['components'].append(n['id'] + 1)
    if parent:
        next(x for x in nodes if x['id'] + 1 == parent)['children'].append(n['id'] + 1)
    nodes.append(n)
    return n


def component(n, tag, name, body):
    fid = n['id'] + len(n['components']) + 1
    n['components'].append(fid)
    block(tag, fid, name, f'  m_GameObject: {ref(n["id"])}\n' + body)
    return fid


def mono(n, script, body):
    return component(n, 114, 'MonoBehaviour', f'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier:\n' + body)


def renderer(n, mat, fileid=2100000):
    return component(n, 23, 'MeshRenderer', f'''  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RenderingLayerMask: 1
  m_Materials:
  - {{fileID: {fileid}, guid: {mat}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_SortingLayerID: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
''')


def cube(name, parent, pos, scale, mat):
    n = node(name, parent, pos, scale)
    component(n, 33, 'MeshFilter', '  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}\n')
    renderer(n, guid(ASSET / 'Materials' / (mat + '.mat')))
    return n


def curve(name, value):
    return f'''  {name}:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 0
      value: {value}
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0.33333334
      outWeight: 0.33333334
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
'''


def voice(n, clip):
    return component(n, 82, 'AudioSource', f'''  m_Enabled: 1
  serializedVersion: 4
  OutputAudioMixerGroup: {{fileID: 0}}
  m_audioClip: {{fileID: 8300000, guid: {guid(ASSET / 'Audio' / (clip + '.wav'))}, type: 3}}
  m_PlayOnAwake: 0
  m_Volume: 0.55
  m_Pitch: 1
  Loop: 0
  Mute: 0
  Spatialize: 0
  SpatializePostEffects: 0
  Priority: 128
  DopplerLevel: 0
  MinDistance: 0.6
  MaxDistance: 8
  Pan2D: 0
  rolloffMode: 1
  BypassEffects: 0
  BypassListenerEffects: 0
  BypassReverbZones: 0
''' + curve('rolloffCustomCurve', 1) + curve('panLevelCustomCurve', 1) + curve('spreadCustomCurve', 0) + curve('reverbZoneMixCustomCurve', 1))


def main():
    blocks.clear()
    nodes.clear()
    folders(ASSET)
    material('PaintedMetal', (0.21, 0.26, 0.28), 0.45)
    material('DarkFrame', (0.065, 0.075, 0.08), 0.65)
    material('Screws', (0.46, 0.47, 0.43), 0.75)
    audio('Tap', 0.09, [670, 1097, 1873, 2861], 58, 410)
    audio('Reply', 0.24, [310, 537, 893, 1327], 23, 411)
    audio('Bang', 0.45, [137, 229, 411, 743, 1171], 13, 412)
    root = node('KnockBackVent')
    root_t = root['id'] + 1
    collider = component(root, 65, 'BoxCollider', '''  m_Material: {fileID: 0}
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
  serializedVersion: 3
  m_Size: {x: 0.9, y: 0.64, z: 0.1}
  m_Center: {x: 0, y: 0, z: 0}
''')
    mono(root, '9803f62141aa590419125376fb147df4', '')
    visual = node('PanelVisual', root_t)
    vt = visual['id'] + 1
    cube('MetalPanel', vt, (0, 0, 0), (0.8, 0.54, 0.06), 'PaintedMetal')
    cube('FrameTop', root_t, (0, 0.295, 0), (0.9, 0.05, 0.1), 'DarkFrame')
    cube('FrameBottom', root_t, (0, -0.295, 0), (0.9, 0.05, 0.1), 'DarkFrame')
    cube('FrameLeft', root_t, (-0.425, 0, 0), (0.05, 0.54, 0.1), 'DarkFrame')
    cube('FrameRight', root_t, (0.425, 0, 0), (0.05, 0.54, 0.1), 'DarkFrame')
    for y in [-0.205, -0.155, -0.105, -0.055]:
        cube('VentSlit', vt, (0, y, 0.032), (0.61, 0.014, 0.007), 'DarkFrame')
    for x in [-0.365, 0.365]:
        for y in [-0.235, 0.235]:
            cube('Bolt', vt, (x, y, 0.034), (0.022, 0.022, 0.015), 'Screws')
    label = node('WarningLabel', vt, (0, 0.12, 0.034), (0.1, 0.1, 0.1), (0, 1, 0, 0), rect=True)
    font = '8f586378b4e144a9851e7b34d9b748ee'
    text_renderer = renderer(label, font, 2180264)
    mono(label, '9541d86e2fd84c1d9990edf0852d74ab', f'''  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.9, g: 0.87, b: 0.7, a: 1}}
  m_RaycastTarget: 0
  m_Maskable: 0
  m_text: "DO NOT COMMUNICATE\\nWITH OCCUPANTS."
  m_isRightToLeft: 0
  m_fontAsset: {{fileID: 11400000, guid: {font}, type: 2}}
  m_sharedMaterial: {{fileID: 2180264, guid: {font}, type: 2}}
  m_fontSharedMaterials: []
  m_fontMaterial: {{fileID: 0}}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4290114789
  m_fontColor: {{r: 0.9, g: 0.87, b: 0.7, a: 1}}
  m_enableVertexGradient: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 5.2
  m_fontSizeBase: 5.2
  m_fontWeight: 700
  m_enableAutoSizing: 0
  m_fontSizeMin: 3
  m_fontSizeMax: 5.2
  m_fontStyle: 1
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_paragraphSpacing: 0
  m_enableWordWrapping: 0
  m_overflowMode: 0
  m_enableKerning: 1
  m_isRichText: 0
  m_parseCtrlCharacters: 1
  m_isOrthographic: 0
  m_isCullingEnabled: 0
  m_pageToDisplay: 1
  m_margin: {{x: 0, y: 0, z: 0, w: 0}}
  m_isVolumetricText: 0
  m_renderer: {ref(text_renderer)}
  m_maskType: 0
''')
    front = node('AcceptedTapAudio', root_t, (0, 0, 0.065))
    behind = node('BehindPanelAudio', root_t, (0, 0, -0.18))
    taps = [voice(front, 'Tap') for _ in range(8)]
    replies = [voice(behind, 'Reply') for _ in range(9)]
    mono(root, guid('Assets/Scripts/KnockBackVent/KnockBackVent.cs'), f'''  interactionId: knock-back-vent-hub-01
  sector: 1
  rhythm:
    maxTaps: 6
    minimumInterval: 0.12
    quietInterval: 0.65
    maximumRecording: 3.5
    replyDelay: 0.75
    cooldown: 2
    extraKnockChance: 0.06
    heavyBangChance: 0.04
    extraKnockGap: 0.28
  panelCollider: {ref(collider)}
  panelVisual: {ref(vt)}
  tapVoices:
''' + ''.join(f'  - {ref(v)}\n' for v in taps) + '  replyVoices:\n' + ''.join(f'  - {ref(v)}\n' for v in replies) + f'''  tapClip: {{fileID: 8300000, guid: {guid(ASSET / 'Audio/Tap.wav')}, type: 3}}
  replyClip: {{fileID: 8300000, guid: {guid(ASSET / 'Audio/Reply.wav')}, type: 3}}
  bangClip: {{fileID: 8300000, guid: {guid(ASSET / 'Audio/Bang.wav')}, type: 3}}
  tapVolume: 0.4
  replyVolume: 0.55
  bangVolume: 0.65
  movementMetres: 0.002
  minimumTapSpeed: 0.3
  maximumHandSpeed: 7
  maximumFrameStep: 0.22
  releaseSeconds: 0.07
''')
    for n in nodes:
        block(1, n['id'], 'GameObject', '  serializedVersion: 6\n  m_Component:\n' +
              ''.join(f'  - component: {ref(v)}\n' for v in n['components']) +
              f'  m_Layer: 0\n  m_Name: {n["name"]}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n')
        body = f'  m_GameObject: {ref(n["id"])}\n  serializedVersion: 2\n  m_LocalRotation: {vec(n["rotation"])}\n  m_LocalPosition: {vec(n["position"])}\n  m_LocalScale: {vec(n["scale"])}\n  m_ConstrainProportionsScale: 0\n'
        body += '  m_Children:\n' + ''.join(f'  - {ref(v)}\n' for v in n['children']) if n['children'] else '  m_Children: []\n'
        body += f'  m_Father: {ref(n["parent"])}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'
        if n['rect']:
            body += '  m_AnchorMin: {x: 0.5, y: 0.5}\n  m_AnchorMax: {x: 0.5, y: 0.5}\n  m_AnchoredPosition: {x: 0, y: 0.12}\n  m_SizeDelta: {x: 7.6, y: 1.8}\n  m_Pivot: {x: 0.5, y: 0.5}\n'
        block(224 if n['rect'] else 4, n['id'] + 1, 'RectTransform' if n['rect'] else 'Transform', body)
    path = ASSET / 'KnockBackVent.prefab'
    write(path, '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(blocks))
    meta(path, 'PrefabImporter')
    print('Authored KnockBackVent prefab, three materials and three original PCM clips.')


if __name__ == '__main__':
    main()
