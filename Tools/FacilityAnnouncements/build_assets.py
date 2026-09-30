#!/usr/bin/env python3
"""Explicit developer-only authoring tool. Never runs in Unity or at game startup.

Requires Python 3 and native eSpeak 1.48.15 for speech, not MBROLA. Run from repo
root. Refuses to overwrite delivered assets unless --replace-generated is supplied.
Use Unity to edit individual prefab placements; this tool never opens scenes.
"""
import argparse
from array import array
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import tempfile
import uuid
import wave

ROOT = Path('Assets/RunawayChimps/FacilityAnnouncements')
SCRIPTS = Path('Assets/Scripts/FacilityAnnouncements')
FONT_GUID = '8f586378b4e144a9851e7b34d9b748ee'
LINES = [
    ('enrichment-privilege', 'Reminder: enrichment equipment is a privilege, not an escape opportunity.'),
    ('tissue-movement', 'Please report unexpected tissue movement to your supervisor.'),
    ('assigned-containment', 'Unauthorized subjects must return to their assigned containment area.'),
    ('cooperation-recorded', 'Your cooperation has been recorded. Your objections have not.'),
]
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
COMMON = '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'runaway-chimps/facility-announcements/' + str(path)).hex


def ref(path, file_id=11400000):
    asset_type = 3 if Path(path).suffix in ('.wav', '.prefab') else 2
    return '{fileID: %d, guid: %s, type: %d}' % (file_id, guid(path), asset_type)


def vector(values, names='xyz'):
    return '{' + ', '.join('%s: %g' % (key, value) for key, value in zip(names, values)) + '}'


def write(path, content):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding='utf-8')


def meta(path, folder=False):
    path = Path(path)
    if path.suffix == '.meta':
        return
    text = 'fileFormatVersion: 2\nguid: ' + guid(path) + '\n'
    if folder:
        text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    elif path.suffix == '.cs':
        text += 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    elif path.suffix == '.wav':
        text += ('AudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n'
                 '  defaultSettings:\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 22050\n'
                 '    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 1\n'
                 '  platformSettingOverrides: {}\n  forceToMono: 1\n  normalize: 0\n  preloadAudioData: 1\n'
                 '  loadInBackground: 0\n  ambisonic: 0\n  3D: 1\n')
    elif path.suffix == '.prefab':
        text += 'PrefabImporter:\n  externalObjects: {}\n'
    elif path.suffix in ('.mat', '.asset'):
        text += 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: %d\n' % (2100000 if path.suffix == '.mat' else 11400000)
    else:
        text += 'DefaultImporter:\n  externalObjects: {}\n'
    text += '  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    target = Path(str(path) + '.meta')
    if not target.exists():
        write(target, text)


class Prefab:
    def __init__(self):
        self.docs = []
        self.nodes = []
        self.next_id = 1000

    def allocate(self):
        self.next_id += 1
        return self.next_id

    def doc(self, class_id, object_id, name, body):
        self.docs.append('--- !u!%d &%d\n%s:\n%s%s' % (class_id, object_id, name, COMMON, body))

    def node(self, name, parent=None, position=(0, 0, 0), scale=(1, 1, 1), rect=False, active=1):
        node = dict(go=self.allocate(), tr=self.allocate(), components=[], name=name, parent=parent,
                    position=position, scale=scale, rect=rect, active=active, children=[])
        node['components'].append(node['tr'])
        if parent:
            parent['children'].append(node['tr'])
        self.nodes.append(node)
        return node

    def component(self, node, class_id, name, body):
        number = self.allocate()
        node['components'].append(number)
        self.doc(class_id, number, name, '  m_GameObject: {fileID: %d}\n' % node['go'] + body)
        return number

    def mono(self, node, script_guid, body=''):
        return self.component(node, 114, 'MonoBehaviour',
                              '  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n' % script_guid + body)

    def cube(self, name, parent, position, scale, material):
        node = self.node(name, parent, position, scale)
        self.component(node, 33, 'MeshFilter', '  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}\n')
        self.component(node, 23, 'MeshRenderer',
                       '  m_Enabled: 1\n  m_CastShadows: 1\n  m_ReceiveShadows: 1\n  m_DynamicOccludee: 1\n  m_StaticShadowCaster: 0\n  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n  m_RayTracingMode: 2\n  m_RenderingLayerMask: 1\n  m_RendererPriority: 0\n  m_Materials:\n  - ' + ref(ROOT / ('Materials/' + material + '.mat'), 2100000) + '\n  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n  m_StaticBatchRoot: {fileID: 0}\n  m_ProbeAnchor: {fileID: 0}\n  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 3\n  m_MinimumChartSize: 4\n  m_AutoUVMaxDistance: 0.5\n  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {fileID: 0}\n  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 0\n  m_AdditionalVertexStreams: {fileID: 0}\n')
        return node

    def save(self, path):
        for n in self.nodes:
            self.doc(1, n['go'], 'GameObject', '  serializedVersion: 6\n  m_Component:\n' +
                     ''.join('  - component: {fileID: %d}\n' % c for c in n['components']) +
                     '  m_Layer: %d\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: %d\n' %
                     (5 if n['rect'] else 0, json.dumps(n['name']), n['active']))
            body = ('  m_GameObject: {fileID: %d}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n'
                    '  m_LocalPosition: %s\n  m_LocalScale: %s\n  m_ConstrainProportionsScale: 0\n' %
                    (n['go'], vector(n['position']), vector(n['scale'])))
            body += '  m_Children:' + ('\n' + ''.join('  - {fileID: %d}\n' % c for c in n['children']) if n['children'] else ' []\n')
            body += '  m_Father: {fileID: %d}\n  m_RootOrder: 0\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n' % (n['parent']['tr'] if n['parent'] else 0)
            if n['rect']:
                body += n.get('layout', '  m_AnchorMin: {x: 0, y: 0}\n  m_AnchorMax: {x: 1, y: 1}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: 0, y: 0}\n  m_Pivot: {x: 0.5, y: 0.5}\n')
            self.doc(224 if n['rect'] else 4, n['tr'], 'RectTransform' if n['rect'] else 'Transform', body)
        write(path, HEADER + ''.join(self.docs))


def materials():
    for name, rgba, metal, smooth in [
        ('Housing', (0.27, 0.30, 0.27, 1), 0.35, 0.25),
        ('Grille', (0.035, 0.045, 0.043, 1), 0.6, 0.2),
        ('Hardware', (0.42, 0.43, 0.4, 1), 0.75, 0.35),
        ('Label', (0.73, 0.67, 0.48, 1), 0.05, 0.15),
    ]:
        write(ROOT / ('Materials/' + name + '.mat'), HEADER + '--- !u!21 &2100000\nMaterial:\n' + COMMON +
              '  serializedVersion: 8\n  m_Name: Facility_%s\n  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_LockedProperties: \n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats:\n    - _Metallic: %g\n    - _Glossiness: %g\n    - _Mode: 0\n    - _SrcBlend: 1\n    - _DstBlend: 0\n    - _ZWrite: 1\n    m_Colors:\n    - _Color: %s\n    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}\n  m_BuildTextureStacks: []\n' % (name, metal, smooth, vector(rgba, 'rgba')))


def caption_prefab():
    p = Prefab()
    root = p.node('Facility Captions (local camera only)', rect=True)
    canvas = p.component(root, 223, 'Canvas',
                         '  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 1\n  m_Camera: {fileID: 0}\n  m_PlaneDistance: 1.2\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 0\n  m_OverrideSorting: 1\n  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_VertexColorAlwaysGammaSpace: 0\n  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n  m_SortingLayerID: 0\n  m_SortingOrder: 100\n  m_TargetDisplay: 0\n')
    p.mono(root, '0cd44c1031e13a943bb63640046fad76',
           '  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1280, y: 720}\n  m_ScreenMatchMode: 0\n  m_MatchWidthOrHeight: 0.5\n  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n  m_DynamicPixelsPerUnit: 1\n  m_PresetInfoIsWorld: 0\n')
    panel = p.node('Caption background', root, rect=True, active=0)
    panel['layout'] = '  m_AnchorMin: {x: 0.19, y: 0.13}\n  m_AnchorMax: {x: 0.81, y: 0.37}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: 0, y: 0}\n  m_Pivot: {x: 0.5, y: 0.5}\n'
    p.component(panel, 222, 'CanvasRenderer', '  m_CullTransparentMesh: 1\n')
    image = p.mono(panel, 'fe87c0e1cc204ed48ad3b37840f39efc',
                   '  m_Material: {fileID: 0}\n  m_Color: {r: 0.018, g: 0.025, b: 0.023, a: 0.88}\n  m_RaycastTarget: 0\n  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 0\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_Sprite: {fileID: 0}\n  m_Type: 0\n  m_PreserveAspect: 0\n  m_FillCenter: 1\n  m_FillMethod: 4\n  m_FillAmount: 1\n  m_FillClockwise: 1\n  m_FillOrigin: 0\n  m_UseSpriteMesh: 0\n  m_PixelsPerUnitMultiplier: 1\n')
    text = p.node('Transcript', panel, rect=True)
    text['layout'] = '  m_AnchorMin: {x: 0, y: 0}\n  m_AnchorMax: {x: 1, y: 1}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: -36, y: -16}\n  m_Pivot: {x: 0.5, y: 0.5}\n'
    p.component(text, 222, 'CanvasRenderer', '  m_CullTransparentMesh: 1\n')
    label = p.mono(text, 'f4688fdb7df04437aeb418b961361dc5',
                   '  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 0\n  m_Maskable: 0\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_text: \n  m_isRightToLeft: 0\n  m_fontAsset: {fileID: 11400000, guid: ' + FONT_GUID + ', type: 2}\n  m_sharedMaterial: {fileID: 2180264, guid: ' + FONT_GUID + ', type: 2}\n  m_fontSharedMaterials: []\n  m_fontMaterial: {fileID: 0}\n  m_fontMaterials: []\n  m_fontColor32:\n    serializedVersion: 2\n    rgba: 4294967295\n  m_fontColor: {r: 0.95, g: 0.95, b: 0.9, a: 1}\n  m_enableVertexGradient: 0\n  m_colorMode: 3\n  m_fontSize: 26\n  m_fontSizeBase: 26\n  m_fontWeight: 400\n  m_enableAutoSizing: 0\n  m_fontSizeMin: 18\n  m_fontSizeMax: 40\n  m_fontStyle: 0\n  m_HorizontalAlignment: 2\n  m_VerticalAlignment: 512\n  m_textAlignment: 65535\n  m_characterSpacing: 0\n  m_wordSpacing: 0\n  m_lineSpacing: 0\n  m_lineSpacingMax: 0\n  m_paragraphSpacing: 0\n  m_charWidthMaxAdj: 0\n  m_enableWordWrapping: 1\n  m_wordWrappingRatios: 0.4\n  m_overflowMode: 0\n  m_linkedTextComponent: {fileID: 0}\n  parentLinkedComponent: {fileID: 0}\n  m_enableKerning: 1\n  m_enableExtraPadding: 1\n  checkPaddingRequired: 0\n  m_isRichText: 0\n  m_parseCtrlCharacters: 0\n  m_isOrthographic: 1\n  m_isCullingEnabled: 0\n  m_horizontalMapping: 0\n  m_verticalMapping: 0\n  m_geometrySortingOrder: 0\n  m_IsTextObjectScaleStatic: 0\n  m_VertexBufferAutoSizeReduction: 0\n  m_useMaxVisibleDescender: 1\n  m_pageToDisplay: 1\n  m_margin: {x: 0, y: 0, z: 0, w: 0}\n  m_isUsingLegacyAnimationComponent: 0\n  m_isVolumetricText: 0\n  m_hasFontAssetChanged: 0\n  m_baseMaterial: {fileID: 0}\n  m_maskOffset: {x: 0, y: 0, z: 0, w: 0}\n')
    component = p.mono(root, guid(SCRIPTS / 'FacilityAnnouncementCaptions.cs'),
                       '  canvas: {fileID: %d}\n  panel: {fileID: %d}\n  label: {fileID: %d}\n  backdrop: {fileID: %d}\n' % (canvas, panel['tr'], label, image))
    p.save(ROOT / 'Prefabs/FacilityAnnouncementCaptions.prefab')
    return component


def speaker_prefab(caption_id):
    p = Prefab()
    root = p.node('Facility Speaker')
    audio = p.component(root, 82, 'AudioSource',
                         '  m_Enabled: 1\n  serializedVersion: 4\n  OutputAudioMixerGroup: {fileID: 0}\n  m_audioClip: {fileID: 0}\n  m_PlayOnAwake: 0\n  m_Volume: 0.38\n  m_Pitch: 1\n  Loop: 0\n  Mute: 0\n  Spatialize: 0\n  SpatializePostEffects: 0\n  Priority: 180\n  DopplerLevel: 0\n  MinDistance: 1.5\n  MaxDistance: 14\n  Pan2D: 0\n  rolloffMode: 1\n  BypassEffects: 0\n  BypassListenerEffects: 0\n  BypassReverbZones: 0\n')
    p.mono(root, guid(SCRIPTS / 'FacilitySpeaker.cs'),
           '  sector: 1\n  collection: ' + ref(ROOT / 'Data/FacilityAnnouncements.asset') + '\n  source: {fileID: %d}\n  captionPrefab: ' % audio +
           ref(ROOT / 'Prefabs/FacilityAnnouncementCaptions.prefab', caption_id) +
           '\n  volume: 0.38\n  minimumDistance: 1.5\n  maximumDistance: 14\n  outputGroup: {fileID: 0}\n')
    p.cube('Painted steel housing', root, (0, 0, 0), (0.58, 0.43, 0.19), 'Housing')
    p.cube('Recessed black grille', root, (0, 0.03, 0.102), (0.48, 0.29, 0.025), 'Grille')
    for i in range(9):
        p.cube('Grille louver %02d' % (i + 1), root, (0, -0.088 + i * 0.03, 0.125), (0.45, 0.01, 0.018), 'Hardware')
    p.cube('Identification plate', root, (0, -0.166, 0.108), (0.24, 0.045, 0.016), 'Label')
    for x in (-0.263, 0.263):
        for y in (-0.185, 0.185):
            p.cube('Captive screw', root, (x, y, 0.105), (0.017, 0.017, 0.012), 'Hardware')
    for x in (-0.24, 0.24):
        p.cube('Rear mounting lug', root, (x, 0, -0.11), (0.05, 0.52, 0.025), 'Hardware')
    p.save(ROOT / 'Prefabs/FacilitySpeaker.prefab')


def save_wave(path, samples, rate=22050):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), 'wb') as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(rate)
        stream.writeframes(struct.pack('<%dh' % len(samples), *[max(-32767, min(32767, round(v * 32767))) for v in samples]))


def audio():
    version = subprocess.check_output(['espeak', '--version'], text=True).strip()
    manifest = {'speech_status': 'Recorded native-formant synthetic prototype, human listening acceptance pending',
                'generator': version, 'voice': 'native en-us (not MBROLA, no cloned voice)',
                'command': 'espeak -v en-us -s 145 -p 38 -a 85 -w OUTPUT.wav TEXT',
                'processing': 'First-order 140 Hz high-pass and 4400 Hz low-pass; peak normalized to 0.65; 10 ms endpoint fade',
                'clips': []}
    for stable_id, transcript in LINES:
        with tempfile.TemporaryDirectory() as temporary:
            raw = Path(temporary) / 'raw.wav'
            subprocess.run(['espeak', '-v', 'en-us', '-s', '145', '-p', '38', '-a', '85', '-w', str(raw), transcript], check=True)
            with wave.open(str(raw), 'rb') as stream:
                rate = stream.getframerate()
                if stream.getnchannels() != 1 or stream.getsampwidth() != 2:
                    raise RuntimeError('Expected native mono 16-bit eSpeak output')
                values = struct.unpack('<%dh' % stream.getnframes(), stream.readframes(stream.getnframes()))
        a_high = math.exp(-2 * math.pi * 140 / rate)
        a_low = 1 - math.exp(-2 * math.pi * 4400 / rate)
        filtered = []
        previous = high = low = 0.0
        for sample in values:
            current = sample / 32768
            high = a_high * (high + current - previous)
            previous = current
            low += a_low * (high - low)
            filtered.append(low)
        peak = max(abs(v) for v in filtered)
        if peak < 0.001:
            raise RuntimeError('Speech synthesis was silent: ' + stable_id)
        samples = [v * 0.65 / peak * min(1, i / (rate * 0.01), (len(filtered) - 1 - i) / (rate * 0.01)) for i, v in enumerate(filtered)]
        path = ROOT / ('Audio/' + stable_id + '.wav')
        save_wave(path, samples, rate)
        manifest['clips'].append({'id': stable_id, 'transcript': transcript, 'path': str(path), 'seconds': len(samples) / rate,
                                  'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'peak': max(abs(v) for v in samples)})
    for name, frequency in [('relay-start', 640), ('relay-end', 470)]:
        rate = 22050
        count = int(rate * 0.22)
        samples = []
        for i in range(count):
            t = i / rate
            envelope = math.exp(-t * 25) * min(1, t / 0.004) * min(1, (count - i - 1) / (rate * 0.008))
            samples.append(0.10 * envelope * (math.sin(2 * math.pi * frequency * t) + 0.18 * math.sin(2 * math.pi * frequency * 2.1 * t)))
        path = ROOT / ('Audio/' + name + '.wav')
        save_wave(path, samples)
        manifest['clips'].append({'id': name, 'path': str(path), 'seconds': count / rate,
                                  'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'kind': 'original nonverbal synthesized relay/chime'})
    write(ROOT / 'Audio/provenance.json', json.dumps(manifest, indent=2) + '\n')


def collection():
    text = HEADER + '--- !u!114 &11400000\nMonoBehaviour:\n' + COMMON + '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ' + guid(SCRIPTS / 'FacilityAnnouncementCollection.cs') + ', type: 3}\n  m_Name: FacilityAnnouncements\n  m_EditorClassIdentifier: \n  collectionId: facility-prototype\n  contentRevision: 1\n  entries:\n'
    for stable_id, transcript in LINES:
        text += '  - id: %s\n    enabled: 1\n    weight: 1\n    speechAvailable: 1\n    speech: %s\n    transcript: %s\n    provenance: %s\n' % (
            stable_id, ref(ROOT / ('Audio/' + stable_id + '.wav'), 8300000), json.dumps(transcript),
            json.dumps('User-provided prototype wording; offline native eSpeak en-us formant synthesis. See Audio/provenance.json and docs/facility-announcements.md. Listening/headset acceptance pending.'))
    text += '  startCue: ' + ref(ROOT / 'Audio/relay-start.wav', 8300000) + '\n  endCue: ' + ref(ROOT / 'Audio/relay-end.wav', 8300000) + '\n  minimumQuietSeconds: 180\n  maximumQuietSeconds: 300\n  allowSingleLineRepeat: 0\n  diagnosticScheduling: 0\n  diagnosticMinimumSeconds: 8\n  diagnosticMaximumSeconds: 12\n'
    write(ROOT / 'Data/FacilityAnnouncements.asset', text)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--replace-generated', action='store_true', help='Explicitly replace source prefab/material/audio assets, never scene instances')
    args = parser.parse_args()
    if ROOT.exists() and any(ROOT.rglob('*.prefab')) and not args.replace_generated:
        raise SystemExit('Delivered prefabs already exist. Use Unity for placement edits, or explicitly pass --replace-generated.')
    materials()
    audio()
    collection()
    speaker_prefab(caption_prefab())
    for root in (ROOT, SCRIPTS):
        meta(root, folder=True)
        for path in sorted(root.rglob('*')):
            meta(path, folder=path.is_dir())
    print('Created real serialized speaker/caption prefabs, four materials, four spoken WAVs, two original cues and metadata. No scenes or project settings modified.')


if __name__ == '__main__':
    main()
