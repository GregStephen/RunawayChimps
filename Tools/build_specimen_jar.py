#!/usr/bin/env python3
"""Explicitly regenerate the original specimen/jar assets. Never opens/edits scenes.
This overwrites this feature's prefab defaults; commit intentional Inspector tuning first.
Python standard library only. Runtime consumes serialized assets, not this generator.
"""
from pathlib import Path
import math
import re
import struct
import uuid

ROOT = Path(__file__).resolve().parents[1]
BASE = 'Assets/RunawayChimps/Toys/ReactiveSpecimenJar'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
COMMON = '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'


def guid(path):
    existing = ROOT / (path + '.meta')
    if existing.exists():
        return re.search(r'^guid: ([0-9a-f]{32})$', existing.read_text(), re.M).group(1)
    return uuid.uuid5(uuid.NAMESPACE_URL, 'RunawayChimps/' + path).hex


def meta(path, kind='DefaultImporter', file_id=0):
    target = ROOT / (path + '.meta')
    if target.exists():
        return
    folder = (ROOT / path).is_dir()
    text = f'fileFormatVersion: 2\nguid: {guid(path)}\n'
    if folder:
        text += 'folderAsset: yes\n'
    text += f'{kind}:\n  externalObjects: {{}}\n'
    if kind == 'NativeFormatImporter':
        text += f'  mainObjectFileID: {file_id}\n'
    if kind == 'MonoImporter':
        text += '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    text += '  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    target.write_text(text)


def write(path, content, kind='NativeFormatImporter', file_id=0):
    p = ROOT / path
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content)
    if path.startswith('Assets/'):
        meta(path, kind, file_id)
        parent = p.parent
        while parent != ROOT / 'Assets':
            meta(parent.relative_to(ROOT).as_posix())
            parent = parent.parent


def v3(v):
    return '{' + ', '.join(f'{a}: {b:.8g}' for a, b in zip('xyz', v)) + '}'


class Mesh:
    def __init__(self):
        self.vertices = []
        self.parts = [[], [], []]

    def vert(self, p, n=(0, 0, 1), uv=(0, 0)):
        length = math.sqrt(sum(a*a for a in n))
        self.vertices.append((*p, *(a / length for a in n), *uv))
        return len(self.vertices) - 1

    def tri(self, a, b, c, part=0):
        self.parts[part].extend((a, b, c))

    def save(self, name):
        vertices = self.vertices
        parts = [part for part in self.parts if part]
        lo = [min(v[k] for v in vertices) for k in range(3)]
        hi = [max(v[k] for v in vertices) for k in range(3)]
        center = [(a+b)/2 for a,b in zip(lo,hi)]
        extents = [(b-a)/2 for a,b in zip(lo,hi)]
        aabb = f'      m_Center: {v3(center)}\n      m_Extent: {v3(extents)}\n'
        text = HEADER + '--- !u!43 &4300000\nMesh:\n' + COMMON
        text += f'  m_Name: {name}\n  serializedVersion: 10\n  m_SubMeshes:\n'
        offset = 0
        for part in parts:
            text += f'  - serializedVersion: 2\n    firstByte: {offset}\n    indexCount: {len(part)}\n    topology: 0\n    baseVertex: 0\n    firstVertex: 0\n    vertexCount: {len(vertices)}\n    localAABB:\n' + aabb
            offset += len(part) * 2
        text += '''  m_Shapes:
    vertices: []
    shapes: []
    channels: []
    fullWeights: []
  m_BindPose: []
  m_BoneNameHashes:
  m_RootBoneNameHash: 0
  m_BonesAABB: []
  m_VariableBoneCountWeights:
    m_Data:
  m_MeshCompression: 0
  m_IsReadable: 1
  m_KeepVertices: 0
  m_KeepIndices: 0
  m_IndexFormat: 0
'''
        indices = [i for part in parts for i in part]
        text += '  m_IndexBuffer: ' + struct.pack('<'+'H'*len(indices), *indices).hex() + '\n'
        text += f'  m_VertexData:\n    serializedVersion: 3\n    m_VertexCount: {len(vertices)}\n    m_Channels:\n'
        for k in range(14):
            offset, dim = {0:(0,3),1:(12,3),4:(24,2)}.get(k,(0,0))
            text += f'    - stream: 0\n      offset: {offset}\n      format: 0\n      dimension: {dim}\n'
        data = b''.join(struct.pack('<8f', *v) for v in vertices)
        text += f'    m_DataSize: {len(data)}\n    _typelessdata: {data.hex()}\n  m_CompressedMesh:\n'
        for name2 in ['Vertices','UV','Normals','Tangents','Weights','NormalSigns','TangentSigns','FloatColors','BoneIndices','Triangles']:
            text += f'    m_{name2}:\n      m_NumItems: 0\n'
            if name2 in ['Vertices','UV','FloatColors']:
                text += '      m_Range: 0\n      m_Start: 0\n'
            text += '      m_Data:\n      m_BitSize: 0\n'
        text += '    m_UVInfo: 0\n  m_LocalAABB:\n' + aabb.replace('      ', '    ')
        text += '''  m_MeshUsageFlags: 0
  m_CookingOptions: 30
  m_BakedConvexCollisionMesh:
  m_BakedTriangleCollisionMesh:
  m_MeshMetrics[0]: 1
  m_MeshMetrics[1]: 1
  m_MeshOptimizationFlags: -1
  m_StreamData:
    offset: 0
    size: 0
    path:
'''
        write(f'{BASE}/{name}.asset', text, file_id=4300000)
        return len(vertices), len(indices)//3


def specimen_mesh():
    mesh = Mesh()
    segments, rings = 24, 12
    top = mesh.vert((0,0,.046), (0,0,1))
    rows = []
    for i in range(1, rings):
        theta = math.pi*i/rings
        row = []
        for j in range(segments):
            phi = 2*math.pi*j/segments
            bulge = 1 + .045*math.sin(phi*3 + theta*2)*math.sin(theta)**2
            p = (.047*math.sin(theta)*math.cos(phi)*bulge,
                 .041*math.sin(theta)*math.sin(phi)*bulge, .046*math.cos(theta))
            row.append(mesh.vert(p, (p[0]/.047**2,p[1]/.041**2,p[2]/.046**2), (j/segments,i/rings)))
        rows.append(row)
    bottom = mesh.vert((0,0,-.046), (0,0,-1))
    for j in range(segments):
        n=(j+1)%segments
        mesh.tri(top,rows[0][j],rows[0][n])
        for i in range(len(rows)-1):
            mesh.tri(rows[i][j],rows[i+1][j],rows[i+1][n])
            mesh.tri(rows[i][j],rows[i+1][n],rows[i][n])
        mesh.tri(rows[-1][j],bottom,rows[-1][n])
    # A short asymmetric biological root. It is geometry, not a skeletal rig.
    tip = mesh.vert((.008,-.006,-.064), (0,0,-1))
    stalk=[]
    for j in range(12):
        a=2*math.pi*j/12
        stalk.append(mesh.vert((.014*math.cos(a),.013*math.sin(a),-.040), (math.cos(a),math.sin(a),-.3)))
    for j in range(12):
        mesh.tri(stalk[j],tip,stalk[(j+1)%12])
    # Slightly convex iris and pupil caps, clear of the body surface.
    for radius, depth, part, count in [(.022,.049,1,32),(.009,.051,2,24)]:
        c=mesh.vert((0,0,depth))
        rim=[]
        for j in range(count):
            a=2*math.pi*j/count
            rim.append(mesh.vert((radius*math.cos(a),radius*math.sin(a),depth-.003), (0,0,1),(.5+.5*math.cos(a),.5+.5*math.sin(a))))
        for j in range(count):
            mesh.tri(c,rim[j],rim[(j+1)%count],part)
    return mesh.save('SpecimenGrowth')


def jar_mesh():
    mesh=Mesh()
    # A deliberately stylized square glass specimen jar: four outward-facing panes.
    # BoxCollider is exactly the visible pane envelope; no expensive refraction/fluid.
    faces=[((0,0,1),[(-.17,-.23,.17),(.17,-.23,.17),(.17,.23,.17),(-.17,.23,.17)]),
           ((0,0,-1),[(.17,-.23,-.17),(-.17,-.23,-.17),(-.17,.23,-.17),(.17,.23,-.17)]),
           ((1,0,0),[(.17,-.23,.17),(.17,-.23,-.17),(.17,.23,-.17),(.17,.23,.17)]),
           ((-1,0,0),[(-.17,-.23,-.17),(-.17,-.23,.17),(-.17,.23,.17),(-.17,.23,-.17)])]
    for normal, points in faces:
        ids=[mesh.vert(p,normal,uv) for p,uv in zip(points,[(0,0),(1,0),(1,1),(0,1)])]
        mesh.tri(ids[0],ids[1],ids[2]);mesh.tri(ids[0],ids[2],ids[3])
    return mesh.save('JarGlass')


def frame_mesh():
    mesh = Mesh()
    # Nine opaque blocks share one serialized mesh/material and one renderer.
    blocks = [((0,.245,0),(.38,.03,.38)), ((0,-.245,0),(.38,.03,.38)),
              ((0,-.285,0),(.44,.05,.44)), ((0,-.605,0),(.085,.59,.085)),
              ((0,-.925,0),(.42,.05,.42))]
    blocks += [((x,0,z),(.009,.46,.009)) for x,z in
               [(-.17,-.17),(.17,-.17),(-.17,.17),(.17,.17)]]
    for position, size in blocks:
        for axis in range(3):
            for sign in (-1,1):
                normal = [0,0,0]; normal[axis] = sign
                u, v = (axis+1)%3, (axis+2)%3
                ids=[]
                for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]:
                    point=list(position)
                    point[axis] += sign*size[axis]/2
                    point[u] += a*size[u]/2
                    point[v] += b*size[v]/2
                    ids.append(mesh.vert(point,normal,((a+1)/2,(b+1)/2)))
                if sign < 0: ids.reverse()
                mesh.tri(ids[0],ids[1],ids[2]); mesh.tri(ids[0],ids[2],ids[3])
    return mesh.save('JarFrame')


def material(name, color, emission=(0,0,0), glass=False):
    text=HEADER+'--- !u!21 &2100000\nMaterial:\n'+COMMON
    text+=f'''  serializedVersion: 8
  m_Name: {name}
  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: [{'_ALPHABLEND_ON' if glass else '_EMISSION'}]
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {3000 if glass else -1}
  stringTagMap: {{RenderType: {'Transparent' if glass else 'Opaque'}}}
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
    - _Mode: {2 if glass else 0}
    - _SrcBlend: {5 if glass else 1}
    - _DstBlend: {10 if glass else 0}
    - _ZWrite: {0 if glass else 1}
    - _Glossiness: {0.2 if glass else 0.3}
    - _Metallic: 0
    - _SpecularHighlights: 0
    - _GlossyReflections: 0
    m_Colors:
    - _Color: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: {color[3]}}}
    - _EmissionColor: {{r: {emission[0]}, g: {emission[1]}, b: {emission[2]}, a: 1}}
  m_BuildTextureStacks: []
'''
    write(f'{BASE}/{name}.mat',text,file_id=2100000)


def prefab():
    parts=[]
    children=[]
    nodes=[]
    # Root is the jar's centre. Stand bottom is 0.95 m below the root.
    def node(name, pos, scale, mesh=None, mats=None, collider=False):
        i=1000+len(nodes)*100
        nodes.append((i,name,pos,scale,mesh,mats,collider))
        children.append(i+1)
        return i
    eye=node('Specimen', (0,0,0),(1,1,1),'SpecimenGrowth',['SpecimenBody','SpecimenIris','SpecimenPupil'])
    pane=node('Glass - fixed, not grabbable',(0,0,0),(1,1,1),'JarGlass',['JarTint'],True)
    node('Frame and stand',(0,0,0),(1,1,1),'JarFrame',['JarMetal'])
    def gameobject(i,name,components):
        return f'--- !u!1 &{i}\nGameObject:\n'+COMMON+'  serializedVersion: 6\n  m_Component:\n'+''.join(f'  - component: {{fileID: {c}}}\n' for c in components)+f'  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'
    def transform(i,go,pos,scale,child_ids,parent):
        return f'--- !u!4 &{i}\nTransform:\n'+COMMON+f'  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {v3(pos)}\n  m_LocalScale: {v3(scale)}\n  m_ConstrainProportionsScale: 1\n'+('  m_Children:\n'+''.join(f'  - {{fileID: {c}}}\n' for c in child_ids) if child_ids else '  m_Children: []\n')+f'  m_Father: {{fileID: {parent}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'
    parts += [gameobject(100,'Reactive Specimen Jar',[101,102]),transform(101,100,(0,0,0),(1,1,1),children,0)]
    script_guid=guid('Assets/Scripts/Toys/ReactiveSpecimenJar.cs')
    parts.append(f'''--- !u!114 &102
MonoBehaviour:
{COMMON}  m_GameObject: {{fileID: 100}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  specimen: {{fileID: {eye+1}}}
  glass: {{fileID: {pane+4}}}
  motionExtents: {{x: 0.095, y: 0.14, z: 0.095}}
  interestRadius: 0.55
  responseSpeed: 6
  turnSpeed: 8
  idleDrift: 0.008
  viewerDistance: 3
  handRadius: 0.05
  minimumTapSpeed: 0.3
  contactSkin: 0.008
  releaseMargin: 0.025
  rearmSeconds: 0.12
  reactionCooldown: 0.55
  recoilSeconds: 0.4
  maximumHandStep: 0.28
  maximumHandSpeed: 10
  watchChance: 0.35
  watchCooldown: 18
  awayDelay: 0.8
  watchSeconds: 4
  visibleHalfAngle: 75
  awayHalfAngle: 105
  editorPreview: 0
  previewHead: {{fileID: 0}}
  previewLeftHand: {{fileID: 0}}
  previewRightHand: {{fileID: 0}}
''')
    for i,name,pos,scale,mesh,mats,collider in nodes:
        parts.append(gameobject(i,name,[i+1,i+2,i+3]+([i+4] if collider else [])))
        parts.append(transform(i+1,i,pos,scale,[],101))
        ref=f'{{fileID: {mesh}, guid: 0000000000000000e000000000000000, type: 0}}' if isinstance(mesh,int) else f'{{fileID: 4300000, guid: {guid(BASE+"/"+mesh+".asset")}, type: 2}}'
        parts.append(f'--- !u!33 &{i+2}\nMeshFilter:\n'+COMMON+f'  m_GameObject: {{fileID: {i}}}\n  m_Mesh: {ref}\n')
        parts.append(f'''--- !u!23 &{i+3}
MeshRenderer:
{COMMON}  m_GameObject: {{fileID: {i}}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 0
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
'''+''.join(f'  - {{fileID: 2100000, guid: {guid(BASE+"/"+m+".mat")}, type: 2}}\n' for m in mats)+'''  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 0
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {fileID: 0}
''')
        if collider:
            parts.append(f'''--- !u!65 &{i+4}
BoxCollider:
{COMMON}  m_GameObject: {{fileID: {i}}}
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
  serializedVersion: 3
  m_Size: {{x: 0.34, y: 0.46, z: 0.34}}
  m_Center: {{x: 0, y: 0, z: 0}}
''')
    write(BASE+'/ReactiveSpecimenJar.prefab',HEADER+''.join(parts),'PrefabImporter')


def main():
    print('Original specimen vertices/triangles:',specimen_mesh())
    print('Original jar vertices/triangles:',jar_mesh())
    print('Combined frame vertices/triangles:',frame_mesh())
    material('SpecimenBody',(.62,.56,.48,1),(.14,.12,.095))
    material('SpecimenIris',(.14,.37,.33,1),(.025,.08,.065))
    material('SpecimenPupil',(.008,.011,.01,1),(.003,.004,.003))
    material('JarMetal',(.17,.20,.19,1),(.028,.034,.03))
    material('JarTint',(.30,.58,.52,.10),glass=True)
    for p in ['Assets/Scripts/Toys/SpecimenJarState.cs','Assets/Scripts/Toys/ReactiveSpecimenJar.cs','Assets/Scripts/Editor/ReactiveSpecimenJarEditor.cs']:
        meta(p,'MonoImporter')
    meta('Assets/Scripts/Toys')
    prefab()
    print('Serialized prefab written. No scene files changed.')


if __name__=='__main__':
    main()
