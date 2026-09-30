"""Reproduce the shipped cognitive machine. Default: read-only; --write authors assets.

No Unity installation, scene edits, runtime generation, third-party downloads or
other toy generators are involved. Requires PyYAML 6.0.2 for Unity YAML emission.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
from pathlib import Path
import struct
import wave

import yaml

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'Assets/RunawayChimps/Toys/PrimateCognitive'
PREFAB = ASSET + '/Prefabs/PrimateCognitiveEvaluation.prefab'
SCRIPTS = 'Assets/Scripts/Toys/PrimateCognitive/'
FONT = '8f586378b4e144a9851e7b34d9b748ee'  # Existing LiberationSans SDF; no font copy.
TMP = '9541d86e2fd84c1d9990edf0852d74ab'   # TextMeshPro (3D), not TextMeshProUGUI.
BUILTIN = '0000000000000000e000000000000000'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
NOTES = (330, 440, 554, 659, 880, 130)
COLORS = ((.15, .60, .72), (.78, .48, .13), (.54, .37, .72), (.36, .65, .28), (.60, .68, .64))


class Flow(dict):
    """Unity vector/reference mappings use flow style."""


class Dumper(yaml.SafeDumper):
    pass


Dumper.add_representer(Flow, lambda d, v: d.represent_mapping('tag:yaml.org,2002:map', v, flow_style=True))


def ref(file_id=0, guid=None, kind=2):
    result = Flow(fileID=file_id)
    if guid is not None:
        result.update(guid=guid, type=kind)
    return result


def vector(values, axes='xyz'):
    return Flow(zip(axes, values))


def color(values):
    return vector(tuple(values) + (1,), 'rgba')


def guid(path):
    return hashlib.sha256(('RunawayChimps.Cognitive/' + path).encode()).hexdigest()[:32]


def script(name):
    meta = (ROOT / (SCRIPTS + name + '.cs.meta')).read_text()
    return meta.split('guid: ', 1)[1].splitlines()[0]


def block(kind, file_id, name, fields):
    return f'--- !u!{kind} &{file_id}\n' + yaml.dump({name: fields}, Dumper=Dumper, sort_keys=False, width=1000)


def common(go=None):
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=ref(), m_PrefabInstance=ref(), m_PrefabAsset=ref())
    if go is not None:
        fields['m_GameObject'] = ref(go)
    return fields


def metadata(path, folder=False):
    result = dict(fileFormatVersion=2, guid=guid(path))
    if folder:
        result['folderAsset'] = 'yes'
    importer = dict(externalObjects={})
    if path.endswith('.wav'):
        importer.update(serializedVersion=7,
                        defaultSettings=dict(serializedVersion=2, loadType=0, sampleRateSetting=0,
                                             sampleRateOverride=22050, compressionFormat=0, quality=1,
                                             conversionMode=0, preloadAudioData=1),
                        platformSettingOverrides={}, forceToMono=1, normalize=0,
                        loadInBackground=0, ambisonic=0, **{'3D': 1})
        name = 'AudioImporter'
    elif path.endswith('.mat'):
        importer.update(mainObjectFileID=2100000)
        name = 'NativeFormatImporter'
    else:
        name = 'PrefabImporter' if path.endswith('.prefab') else 'DefaultImporter'
    importer.update(userData='', assetBundleName='', assetBundleVariant='')
    result[name] = importer
    return yaml.dump(result, sort_keys=False).replace("folderAsset: 'yes'", 'folderAsset: yes')


def material(name, rgb, metallic=0, emission=False):
    fields = common()
    fields.update(serializedVersion=8, m_Name=name,
                  m_Shader=ref(46, '0000000000000000f000000000000000', 0),
                  m_ValidKeywords=['_EMISSION'] if emission else [], m_InvalidKeywords=[],
                  m_LightmapFlags=4, m_EnableInstancingVariants=1, m_DoubleSidedGI=0,
                  m_CustomRenderQueue=-1, stringTagMap={}, disabledShaderPasses=[],
                  m_SavedProperties=dict(serializedVersion=3,
                      m_TexEnvs=[dict(_MainTex=dict(m_Texture=ref(), m_Scale=vector((1, 1), 'xy'), m_Offset=vector((0, 0), 'xy')))],
                      m_Ints=[], m_Floats=[dict(_Glossiness=.25), dict(_Metallic=metallic), dict(_Mode=0),
                                           dict(_SrcBlend=1), dict(_DstBlend=0), dict(_ZWrite=1)],
                      m_Colors=[dict(_Color=color(rgb)), dict(_EmissionColor=color(tuple(x * .12 for x in rgb) if emission else (0, 0, 0)))]),
                  m_BuildTextureStacks=[])
    return HEADER + block(21, 2100000, 'Material', fields)


def tone(index):
    """Original modest-volume mono PCM tones, ramped to avoid edge clicks."""
    rate = 22050
    duration = .24 if index < 4 else .32
    count = round(duration * rate)
    samples = []
    for n in range(count):
        t = n / rate
        envelope = min(1., t / .012, (duration - t) / .055)
        frequency = NOTES[index]
        phase = 2 * math.pi * frequency * t
        if index == 5:
            phase = 2 * math.pi * (130 * t - 40 * t * t)  # Soft descending failure buzz.
        value = .26 * max(0., envelope) * (math.sin(phase) + .12 * math.sin(2 * phase))
        samples.append(round(32767 * value))
    samples[0] = samples[-1] = 0
    stream = io.BytesIO()
    with wave.open(stream, 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(struct.pack('<' + 'h' * len(samples), *samples))
    return stream.getvalue()


class Prefab:
    def __init__(self):
        self.nodes = []
        self.components = []
        self.serial = 1000

    def node(self, name, parent=None, position=(0, 0, 0), scale=(1, 1, 1), tilt=0, rect=None):
        self.serial += 100
        node = dict(id=self.serial, name=name, parent=parent, position=position, scale=scale,
                    tilt=tilt, rect=rect, components=[self.serial + 1])
        self.nodes.append(node)
        return node

    def component(self, node, kind, name, fields):
        file_id = node['id'] + len(node['components']) + 1
        node['components'].append(file_id)
        self.components.append((kind, file_id, name, {**common(node['id']), **fields}))
        return file_id

    def mono(self, node, script_guid, fields):
        return self.component(node, 114, 'MonoBehaviour', dict(m_Enabled=1, m_EditorHideFlags=0,
                              m_Script=ref(11500000, script_guid, 3), m_Name='', m_EditorClassIdentifier='', **fields))

    def renderer(self, node, material_ref, shadow=True):
        return self.component(node, 23, 'MeshRenderer', dict(m_Enabled=1, m_CastShadows=int(shadow),
            m_ReceiveShadows=int(shadow), m_DynamicOccludee=1, m_StaticShadowCaster=0,
            m_MotionVectors=1, m_LightProbeUsage=1, m_ReflectionProbeUsage=1,
            m_RayTracingMode=2, m_RayTraceProcedural=0, m_RenderingLayerMask=1,
            m_RendererPriority=0, m_Materials=[material_ref],
            m_StaticBatchInfo=dict(firstSubMesh=0, subMeshCount=0), m_StaticBatchRoot=ref(),
            m_ProbeAnchor=ref(), m_LightProbeVolumeOverride=ref(), m_ScaleInLightmap=1,
            m_ReceiveGI=1, m_PreserveUVs=0, m_IgnoreNormalsForChartDetection=0, m_ImportantGI=0,
            m_StitchLightmapSeams=1, m_SelectedEditorRenderState=3, m_MinimumChartSize=4,
            m_AutoUVMaxDistance=.5, m_AutoUVMaxAngle=89, m_LightmapParameters=ref(),
            m_SortingLayerID=0, m_SortingLayer=0, m_SortingOrder=0, m_AdditionalVertexStreams=ref()))

    def collider(self, node, size=(1, 1, 1), center=(0, 0, 0), trigger=False):
        return self.component(node, 65, 'BoxCollider', dict(m_Material=ref(),
            m_IncludeLayers=Flow(serializedVersion=2, m_Bits=0), m_ExcludeLayers=Flow(serializedVersion=2, m_Bits=0),
            m_LayerOverridePriority=0, m_IsTrigger=int(trigger), m_ProvidesContacts=0,
            m_Enabled=1, serializedVersion=3, m_Size=vector(size), m_Center=vector(center)))

    def box(self, name, parent, pos, size, mat='Housing', solid=False):
        node = self.node(name, parent, pos, size)
        self.component(node, 33, 'MeshFilter', dict(m_Mesh=ref(10202, BUILTIN, 0)))
        renderer = self.renderer(node, ref(2100000, guid(ASSET + '/Materials/' + mat + '.mat')))
        if solid:
            self.collider(node)
        return node, renderer

    def text(self, name, parent, pos, size, words, font_size=32):
        node = self.node(name, parent, pos, (.01, .01, .01), rect=tuple(x * 100 for x in size))
        self.component(node, 33, 'MeshFilter', dict(m_Mesh=ref()))
        renderer = self.renderer(node, ref(2180264, FONT), False)
        component = self.mono(node, TMP, dict(m_Material=ref(), m_Color=color((.86, .95, .88)), m_RaycastTarget=0,
            m_text=words, m_isRightToLeft=0, m_fontAsset=ref(11400000, FONT), m_sharedMaterial=ref(2180264, FONT),
            m_fontSharedMaterials=[], m_fontMaterial=ref(), m_fontMaterials=[],
            m_fontColor32=Flow(serializedVersion=2, rgba=4294967295), m_fontColor=color((.86, .95, .88)),
            m_enableVertexGradient=0, m_fontColorGradientPreset=ref(), m_spriteAsset=ref(),
            m_tintAllSprites=0, m_overrideHtmlColors=0, m_fontSize=font_size, m_fontSizeBase=font_size,
            m_fontWeight=400, m_enableAutoSizing=0, m_fontSizeMin=font_size, m_fontSizeMax=font_size,
            m_fontStyle=0, m_HorizontalAlignment=2, m_VerticalAlignment=512, m_textAlignment=65535,
            m_characterSpacing=0, m_wordSpacing=0, m_lineSpacing=2, m_paragraphSpacing=0,
            m_charWidthMaxAdj=0, m_enableWordWrapping=1, m_wordWrappingRatios=.4,
            m_overflowMode=0, m_linkedTextComponent=ref(), m_enableKerning=1,
            m_enableExtraPadding=0, checkPaddingRequired=0, m_isRichText=0, m_parseCtrlCharacters=1,
            m_isOrthographic=0, m_isCullingEnabled=0, m_horizontalMapping=0, m_verticalMapping=0,
            m_uvLineOffset=0, m_geometrySortingOrder=0, m_firstVisibleCharacter=0,
            m_useMaxVisibleDescender=1, m_pageToDisplay=1, m_margin=vector((0, 0, 0, 0), 'xyzw'),
            m_havePropertiesChanged=1, m_isUsingLegacyAnimationComponent=0, m_isVolumetricText=0,
            m_hasFontAssetChanged=0, m_renderer=ref(renderer), m_maskType=0))
        return component

    def serialize(self):
        output = [HEADER]
        for node in self.nodes:
            go = node['id']
            output.append(block(1, go, 'GameObject', dict(**common(), serializedVersion=6,
                m_Component=[dict(component=ref(c)) for c in node['components']], m_Layer=0,
                m_Name=node['name'], m_TagString='Untagged', m_Icon=ref(), m_NavMeshLayer=0,
                m_StaticEditorFlags=0, m_IsActive=1)))
            angle = math.radians(node['tilt'] / 2)
            transform = dict(**common(go), serializedVersion=2,
                m_LocalRotation=vector((round(math.sin(angle), 8), 0, 0, round(math.cos(angle), 8)), 'xyzw'),
                m_LocalPosition=vector(node['position']), m_LocalScale=vector(node['scale']),
                m_ConstrainProportionsScale=0,
                m_Children=[ref(n['id'] + 1) for n in self.nodes if n['parent'] is node],
                m_Father=ref(node['parent']['id'] + 1 if node['parent'] else 0),
                m_LocalEulerAnglesHint=vector((node['tilt'], 0, 0)))
            if node['rect']:
                transform.update(m_AnchorMin=vector((.5, .5), 'xy'), m_AnchorMax=vector((.5, .5), 'xy'),
                    m_AnchoredPosition=vector((node['position'][0], node['position'][1]), 'xy'),
                    m_SizeDelta=vector(node['rect'], 'xy'), m_Pivot=vector((.5, .5), 'xy'))
            output.append(block(224 if node['rect'] else 4, go + 1, 'RectTransform' if node['rect'] else 'Transform', transform))
        output.extend(block(*component) for component in self.components)
        return ''.join(output)


def machine_prefab():
    p = Prefab()
    root = p.node('PrimateCognitiveEvaluation')
    machine_fields = {}
    machine_id = p.mono(root, script('CognitiveMachine'), machine_fields)
    # The component dict is filled after all referenced objects exist.
    target = next(c[3] for c in p.components if c[1] == machine_id)
    p.box('Foot', root, (0, .045, .17), (.84, .09, .62), 'Trim', True)
    p.box('Cabinet', root, (0, .43, .20), (.76, .77, .44), solid=True)
    p.box('RearSpine', root, (0, 1.14, .28), (.76, .66, .22), solid=True)
    p.box('DisplayHousing', root, (0, 1.49, .15), (.86, .60, .24), solid=True)
    p.box('DisplayGlass', root, (0, 1.47, .025), (.78, .37, .012), 'Screen')
    display = p.text('AssessmentDisplay', root, (0, 1.47, .016), (.74, .34),
                     'ROUND 0 / 8   IDLE\nVoluntary testing. Mandatory judgement.\nPRESS START\nAVAILABLE', 30)
    p.text('Title', root, (0, 1.735, .015), (.80, .13), 'PRIMATE COGNITIVE\nEVALUATION', 24)
    p.text('SerialPlate', root, (0, .47, -.026), (.64, .16), 'BEHAVIORAL SCIENCES\nPCE-04 / VOLUNTARY ASSESSMENT', 21)
    for x in (-.33, .33):
        for y in (1.23, 1.74):
            p.box('Fastener', root, (x, y, .016), (.018, .018, .015), 'Trim')
    for i in range(6):
        p.box('SpeakerSlot', root, (-.125 + i * .05, 1.215, .018), (.018, .045, .016), 'Screen')
    surface = p.node('Controls_AdjustHeightHere', root, (0, .98, -.06), tilt=30)
    p.box('ControlDeck', surface, (0, -.04, .06), (.80, .72, .08), 'Housing', True)
    pads = []
    positions = ((-.17, .155), (.17, .155), (-.17, -.105), (.17, -.105), (0, -.285))
    for i, (x, y) in enumerate(positions):
        pad = p.node('StartRestart' if i == 4 else 'Pad' + str(i + 1), surface, (x, y, 0))
        width, height = (.24, .10) if i == 4 else (.18, .16)
        p.box('Bezel', pad, (0, 0, .003), (width + .04, height + .04, .035), 'Trim')
        cap = p.node('MovingCap', pad, (0, 0, -.031))
        _, renderer = p.box('IlluminatedCap', cap, (0, 0, 0), (width, height, .046), 'Pad' + str(i + 1))
        p.box('HighContrastLabelWell', cap, (0, 0, -.024),
              (width - .025, height - .025, .002), 'Screen')
        p.text('PermanentLabel', cap, (0, 0, -.027), (width - .008, height - .006),
               'START / RESTART' if i == 4 else str(i + 1), 19 if i == 4 else 84)
        p.collider(pad, (width + .025, height + .025, .074), (0, 0, -.052), True)
        p.component(pad, 54, 'Rigidbody', dict(serializedVersion=4, m_Mass=1, m_Drag=0, m_AngularDrag=.05,
            m_CenterOfMass=vector((0, 0, 0)), m_InertiaTensor=vector((1, 1, 1)),
            m_InertiaRotation=vector((0, 0, 0, 1), 'xyzw'), m_ImplicitCom=1, m_ImplicitTensor=1,
            m_UseGravity=0, m_IsKinematic=1, m_Interpolate=0, m_Constraints=0, m_CollisionDetection=0))
        pads.append(p.mono(pad, script('CognitivePad'), dict(machine=ref(machine_id), index=i,
            cap=ref(cap['id'] + 1), capRenderer=ref(renderer), restingColor=color(tuple(v * .40 for v in COLORS[i])),
            illuminatedColor=color(COLORS[i]), releaseSeconds=.08, pressDepth=.012)))
    anchor = p.node('OperatorPresenceAnchor', root, (0, 1.05, -.35))
    audio = p.node('FacilitySpeaker', root, (0, 1.215, .018))
    sound = p.component(audio, 82, 'AudioSource', dict(m_Enabled=1, serializedVersion=4,
        OutputAudioMixerGroup=ref(), m_audioClip=ref(), m_PlayOnAwake=0, m_Volume=.65,
        m_Pitch=1, Loop=0, Mute=0, Spatialize=0, SpatializePostEffects=0, Priority=128,
        DopplerLevel=0, MinDistance=1, MaxDistance=7, Pan2D=0, rolloffMode=1,
        BypassEffects=0, BypassListenerEffects=0, BypassReverbZones=0))
    target.update(machineId='hub-cognitive-01', sector=1, pads=[ref(i) for i in pads], display=ref(display),
        audioSource=ref(sound), clips=[ref(8300000, guid(ASSET + '/Audio/Note' + str(i + 1) + '.wav'), 3) for i in range(6)],
        operatorAnchor=ref(anchor['id'] + 1), maximumLength=8, demonstrationLead=.7,
        stepSeconds=.65, flashSeconds=.32, successSeconds=1.1, heartbeatTimeout=4,
        inputIdleTimeout=20, resultHoldSeconds=10, operatorRadius=1.8, editorControls=0)
    return p.serialize()


def assets():
    output = {PREFAB: machine_prefab()}
    output[ASSET + '/Materials/Housing.mat'] = material('PCE painted steel', (.27, .32, .30), .25)
    output[ASSET + '/Materials/Trim.mat'] = material('PCE dark trim', (.07, .09, .085), .45)
    output[ASSET + '/Materials/Screen.mat'] = material('PCE screen', (.012, .028, .022), 0, True)
    for i, rgb in enumerate(COLORS):
        output[ASSET + '/Materials/Pad' + str(i + 1) + '.mat'] = material('PCE pad ' + str(i + 1), tuple(v * .4 for v in rgb), .1, True)
    for i in range(6):
        output[ASSET + '/Audio/Note' + str(i + 1) + '.wav'] = tone(i)
    for path in list(output):
        output[path + '.meta'] = metadata(path)
    for folder in (ASSET, ASSET + '/Prefabs', ASSET + '/Audio', ASSET + '/Materials'):
        output[folder + '.meta'] = metadata(folder, True)
    # Parent folder identities are shared with future toy branches: never replace an existing one.
    parent = 'Assets/RunawayChimps/Toys'
    if not (ROOT / (parent + '.meta')).exists():
        output[parent + '.meta'] = metadata(parent, True)
    return {path: value.encode('utf-8') if isinstance(value, str) else value for path, value in output.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true', help='Explicitly regenerate feature assets only; never scene placements.')
    args = parser.parse_args()
    expected = assets()
    changed = []
    for name, contents in expected.items():
        path = ROOT / name
        if not path.exists() or path.read_bytes() != contents:
            changed.append(name)
            if args.write:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(contents)
    if changed and not args.write:
        raise SystemExit('Generated asset mismatch; inspect before regenerating:\n' + '\n'.join(changed))
    print(('WROTE: ' if args.write else 'PASS: ') + str(len(expected)) + ' deterministic cognitive asset/metadata files.')


if __name__ == '__main__':
    main()
