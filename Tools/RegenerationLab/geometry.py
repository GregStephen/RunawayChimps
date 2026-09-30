"""Original offline mesh authoring helpers. Nothing in this file runs in Unity.

Coordinates: metres, +Y up, +Z front. Native meshes follow the repository's
existing serialized Mesh convention; OBJ companions use the same axes.
"""
from __future__ import annotations
import itertools
import math
import struct
import uuid
from pathlib import Path
import numpy as np

ROOT = 'Assets/RunawayChimps/Environment/RegenerationLab'
MATERIALS = ('RGL_PaintedMetal', 'RGL_DullSteel', 'RGL_PolymerAndLabels')
NAMESPACE = uuid.UUID('1ce6a9b3-dc7c-4d03-b4db-5150308b1c25')
YAML = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
# Rectangles use top-left pixel coordinates; converted to Unity's UV convention.
CELLS = {
    'rubber': (0, 0, 128, 128), 'ivory': (128, 0, 256, 128),
    'fluid': (256, 0, 384, 128), 'tissue': (384, 0, 512, 128),
    'recess': (512, 0, 640, 128), 'ochre': (640, 0, 768, 128),
    'treatment': (0, 160, 512, 256), 'delivery': (0, 288, 512, 384),
    'cold_storage': (0, 416, 768, 544), 'tissue_label': (0, 576, 384, 672),
    'sealed': (512, 160, 768, 256), 'rinse': (512, 288, 768, 384),
    'agent': (512, 576, 768, 672),
}


def guid(path: str) -> str:
    return uuid.uuid5(NAMESPACE, path.replace('\\', '/')).hex


def f(value: float) -> str:
    return format(float(value), '.9g') if abs(value) > 1e-12 else '0'


def vector(value) -> str:
    return '{' + ', '.join(k + ': ' + f(v) for k, v in zip('xyzw', value)) + '}'


def unit(v):
    v = np.asarray(v, dtype=float)
    length = np.linalg.norm(v)
    if length < 1e-10:
        raise ValueError('Degenerate direction')
    return v / length


def atlas_uv(cell, u, v):
    x0, y0, x1, y1 = CELLS[cell]
    pad = 48 if cell in ('rubber', 'ivory', 'fluid', 'tissue', 'recess', 'ochre') else 5
    return ((x0 + pad + u * (x1 - x0 - 2*pad)) / 1024,
            1 - (y1 - pad - v * (y1 - y0 - 2*pad)) / 1024)


def basis(axis):
    y = unit(axis)
    seed = np.array((0., 0., 1.)) if abs(y[2]) < .9 else np.array((1., 0., 0.))
    x = unit(np.cross(y, seed))
    z = np.cross(x, y)
    return np.column_stack((x, y, z))


class Mesh:
    def __init__(self, name):
        self.name = name
        self.triangles = []
        self.parts = []

    def polygon(self, points, material, cell=None, normals=None, uv=None):
        p = np.array(points, dtype=float)
        normal = unit(np.cross(p[1]-p[0], p[2]-p[0]))
        ns = np.tile(normal, (len(p), 1)) if normals is None else np.array(normals)
        if uv is None:
            # Physical planar mapping for tiled metal. Flat dielectric patches have
            # protected atlas gutters; no texture stretching to a whole prop.
            axes = np.argsort(np.abs(normal))[:2]
            uv = p[:, axes] * 2.0 + .17
            if cell:
                lo, hi = uv.min(axis=0), uv.max(axis=0)
                uv = (uv-lo) / np.maximum(hi-lo, .001)
        uv = np.array([atlas_uv(cell, *v) for v in uv]) if cell else np.array(uv)
        for i in range(1, len(p)-1):
            ids = [0, i, i+1]
            self.triangles.append((material, p[ids], ns[ids], uv[ids]))

    def box(self, name, center, size, material=0, bevel=.004, cell=None, rotation=None):
        start = len(self.triangles)
        h = np.array(size, float) / 2
        if min(h) <= 0:
            raise ValueError(name)
        b = min(bevel, min(h)*.8)
        c = np.array(center)
        r = np.eye(3) if rotation is None else rotation
        faces = []
        if b <= 0:
            for axis in range(3):
                others = [i for i in range(3) if i != axis]
                for sign in (-1, 1):
                    pts = []
                    for a, d in ((-1,-1), (1,-1), (1,1), (-1,1)):
                        p = np.zeros(3); p[axis] = sign*h[axis]
                        p[others[0]] = a*h[others[0]]; p[others[1]] = d*h[others[1]]
                        pts.append(p)
                    faces.append(pts)
        else:
            # Six inset face quads, twelve chamfer strips, eight corner triangles.
            for axis in range(3):
                others = [i for i in range(3) if i != axis]
                for sign in (-1, 1):
                    pts = []
                    for a, d in ((-1,-1), (1,-1), (1,1), (-1,1)):
                        p = np.zeros(3); p[axis] = sign*h[axis]
                        p[others[0]] = a*(h[others[0]]-b)
                        p[others[1]] = d*(h[others[1]]-b); pts.append(p)
                    faces.append(pts)
            for axis in range(3):
                others = [i for i in range(3) if i != axis]
                for a, d in itertools.product((-1,1), repeat=2):
                    pts = []
                    for end, edge in ((-1,0),(1,0),(1,1),(-1,1)):
                        p = np.zeros(3); p[axis] = end*(h[axis]-b)
                        p[others[0]] = a*(h[others[0]] - (b if edge else 0))
                        p[others[1]] = d*(h[others[1]] - (0 if edge else b))
                        pts.append(p)
                    faces.append(pts)
            for signs in itertools.product((-1,1), repeat=3):
                pts = []
                for axis in range(3):
                    p = np.array(signs)*(h-b); p[axis] = signs[axis]*h[axis]
                    pts.append(p)
                faces.append(pts)
        for pts in faces:
            pts = np.array(pts)
            if np.dot(np.cross(pts[1]-pts[0], pts[2]-pts[0]), pts.mean(axis=0)) < 0:
                pts = pts[::-1]
            self.polygon(pts @ r.T + c, material, cell)
        self.parts.append({'name': name, 'first_triangle': start, 'triangles': len(self.triangles)-start})

    def lathe(self, name, center, profile, material=1, cell=None, sides=16, axis=(0,1,0)):
        start = len(self.triangles)
        rot = basis(axis); c = np.array(center)
        angles = np.linspace(0, 2*math.pi, sides+1)
        def point(y, radius, angle):
            return c + rot @ np.array((radius*math.cos(angle), y, radius*math.sin(angle)))
        for (y0,r0), (y1,r1) in zip(profile[:-1], profile[1:]):
            slope = (r1-r0) / (y1-y0) if abs(y1-y0) > 1e-9 else 0
            for i in range(sides):
                a,b = angles[i:i+2]
                pts = [point(y0,r0,a),point(y1,r1,a),point(y1,r1,b),point(y0,r0,b)]
                if abs(y1-y0) < 1e-9:
                    ns = None
                else:
                    na = rot @ unit((math.cos(a),-slope,math.sin(a)))
                    nb = rot @ unit((math.cos(b),-slope,math.sin(b)))
                    ns = [na,na,nb,nb]
                self.polygon(pts, material, cell, ns,
                             [(i/sides,0), (i/sides,1), ((i+1)/sides,1), ((i+1)/sides,0)])
        for endpoint, sign in ((profile[0], -1), (profile[-1], 1)):
            y,rad = endpoint
            pts = [point(y,rad,a) for a in angles[:-1]]
            # Ascending angles have -Y winding for this coordinate basis.
            if sign > 0:
                pts.reverse()
            self.polygon(pts, material, cell)
        self.parts.append({'name': name, 'first_triangle': start, 'triangles': len(self.triangles)-start})

    def cylinder(self, name, center, radius, height, material=1, cell=None, sides=16, axis=(0,1,0)):
        self.lathe(name, center, [(-height/2,radius),(height/2,radius)], material, cell, sides, axis)

    def tube(self, name, points, radius=.007, material=2, cell='rubber', sides=8):
        start = len(self.triangles)
        p = np.array(points, float)
        frames = []
        for i, pt in enumerate(p):
            tangent = unit(p[min(i+1,len(p)-1)]-p[max(i-1,0)])
            if not frames:
                frames.append(basis(tangent))
            else:
                # Parallel-transport the previous frame: independently selecting a
                # new reference axis can twist a bend by 180 degrees.
                x = frames[-1][:,0]
                x = unit(x - np.dot(x,tangent)*tangent)
                frames.append(np.column_stack((x,tangent,np.cross(x,tangent))))
        rings = []
        norms = []
        for pt, frame in zip(p, frames):
            ns = [frame @ np.array((math.cos(a),0,math.sin(a))) for a in np.linspace(0,2*math.pi,sides,endpoint=False)]
            rings.append([pt+radius*n for n in ns]); norms.append(ns)
        for j in range(len(p)-1):
            for i in range(sides):
                k=(i+1)%sides
                self.polygon([rings[j][i],rings[j+1][i],rings[j+1][k],rings[j][k]],material,cell,
                             [norms[j][i],norms[j+1][i],norms[j+1][k],norms[j][k]],
                             [(0,0),(0,1),(1,1),(1,0)])
        self.polygon(rings[0],material,cell)
        self.polygon(rings[-1][::-1],material,cell)
        self.parts.append({'name': name, 'first_triangle': start, 'triangles': len(self.triangles)-start})

    def label(self, center, width, height, cell):
        x,y,z=center
        self.polygon([(x-width/2,y-height/2,z),(x+width/2,y-height/2,z),
                      (x+width/2,y+height/2,z),(x-width/2,y+height/2,z)],2,cell,
                     uv=[(0,0),(1,0),(1,1),(0,1)])

    def packed(self):
        vertices, indices, subs, lookup = [], [], [], {}
        for mat in sorted(set(t[0] for t in self.triangles)):
            first = len(indices)
            for material,p,n,uv in self.triangles:
                if mat != material:
                    continue
                edge1,edge2 = p[1]-p[0],p[2]-p[0]
                duv1,duv2 = uv[1]-uv[0],uv[2]-uv[0]
                determinant = duv1[0]*duv2[1]-duv1[1]*duv2[0]
                if abs(determinant)<1e-12:
                    raise ValueError('Degenerate UV chart in '+self.name)
                tan = (edge1*duv2[1]-edge2*duv1[1])/determinant
                bitan = (edge2*duv1[0]-edge1*duv2[0])/determinant
                for pos,normal,coord in zip(p,n,uv):
                    tangent = unit(tan-normal*np.dot(tan,normal))
                    handedness = -1. if np.dot(np.cross(normal,tangent),bitan)<0 else 1.
                    value = tuple(float(v) for v in (*pos,*normal,*tangent,handedness,*coord))
                    key = tuple(round(v,8) for v in value)
                    if key not in lookup:
                        lookup[key]=len(vertices); vertices.append(value)
                    indices.append(lookup[key])
            subs.append({'material':mat,'first':first,'count':len(indices)-first})
        # Canonical decimal precision removes CPU-dependent sub-float32 residuals and signed zero.
        v=np.round(np.asarray(vertices,dtype='<f8'),8).astype('<f4')
        v[v == 0] = 0.0
        ids=np.asarray(indices,dtype='<u2')
        if len(v)>65535:
            raise ValueError('16-bit mesh exceeded')
        return v,ids,subs

    def save(self, path: Path):
        v,ids,subs=self.packed()
        lo=v[:,:3].min(axis=0); hi=v[:,:3].max(axis=0)
        out=YAML+f'--- !u!43 &4300000\nMesh:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_Name: {self.name}\n  serializedVersion: 10\n  m_SubMeshes:\n'
        for sub in subs:
            used=ids[sub['first']:sub['first']+sub['count']]; p=v[used,:3]
            a,b=p.min(axis=0),p.max(axis=0)
            out+=f"  - serializedVersion: 2\n    firstByte: {2*sub['first']}\n    indexCount: {sub['count']}\n    topology: 0\n    baseVertex: 0\n    firstVertex: {int(used.min())}\n    vertexCount: {int(used.max()-used.min()+1)}\n    localAABB:\n      m_Center: {vector((a+b)/2)}\n      m_Extent: {vector((b-a)/2)}\n"
        out+='  m_Shapes:\n    vertices: []\n    shapes: []\n    channels: []\n    fullWeights: []\n  m_BindPose: []\n  m_BoneNameHashes:\n  m_RootBoneNameHash: 0\n  m_BonesAABB: []\n  m_VariableBoneCountWeights:\n    m_Data:\n  m_MeshCompression: 0\n  m_IsReadable: 1\n  m_KeepVertices: 0\n  m_KeepIndices: 0\n  m_IndexFormat: 0\n'
        out+=f'  m_IndexBuffer: {ids.tobytes().hex()}\n  m_VertexData:\n    serializedVersion: 3\n    m_VertexCount: {len(v)}\n    m_Channels:\n'
        channels={0:(0,3),1:(12,3),2:(24,4),4:(40,2)}
        for ch in range(14):
            off,dim=channels.get(ch,(0,0))
            out+=f'    - stream: 0\n      offset: {off}\n      format: 0\n      dimension: {dim}\n'
        out+=f'    m_DataSize: {v.nbytes}\n    _typelessdata: {v.tobytes().hex()}\n  m_CompressedMesh:\n'
        for key in ('Vertices','UV','Normals','Tangents','Weights','NormalSigns','TangentSigns','FloatColors','BoneIndices','Triangles'):
            out+=f'    m_{key}:\n      m_NumItems: 0\n'
            if key in ('Vertices','UV','FloatColors'):
                out+='      m_Range: 0\n      m_Start: 0\n'
            out+='      m_Data:\n      m_BitSize: 0\n'
        out+=f'    m_UVInfo: 0\n  m_LocalAABB:\n    m_Center: {vector((lo+hi)/2)}\n    m_Extent: {vector((hi-lo)/2)}\n  m_MeshUsageFlags: 0\n  m_CookingOptions: 30\n  m_BakedConvexCollisionMesh:\n  m_BakedTriangleCollisionMesh:\n  m_MeshMetrics[0]: 1\n  m_MeshMetrics[1]: 1\n  m_MeshOptimizationFlags: -1\n  m_StreamData:\n    offset: 0\n    size: 0\n    path:\n'
        path.write_text(out,encoding='utf-8')
        return {'name':self.name,'triangles':len(ids)//3,'vertices':len(v),
                'bounds_min_m':lo.astype(float).tolist(),'bounds_max_m':hi.astype(float).tolist(),
                'size_xyz_m':(hi-lo).astype(float).tolist(),'material_slots':[MATERIALS[s['material']] for s in subs],
                'parts':self.parts}

    def obj(self,path):
        v,idx,subs=self.packed()
        with path.open('w',encoding='utf-8') as out:
            out.write('# Original Runaway Chimps regeneration kit. Metres; Y up; +Z front.\nmtllib RegenerationLab.mtl\n')
            for row in v: out.write('v '+' '.join(f(a) for a in row[:3])+'\n')
            for row in v: out.write('vt '+' '.join(f(a) for a in row[10:12])+'\n')
            for row in v: out.write('vn '+' '.join(f(a) for a in row[3:6])+'\n')
            for sub in subs:
                out.write('usemtl '+MATERIALS[sub['material']]+'\n')
                for tri in idx[sub['first']:sub['first']+sub['count']].reshape(-1,3):
                    out.write('f '+' '.join(f'{int(i)+1}/{int(i)+1}/{int(i)+1}' for i in tri)+'\n')


def catmull(points, steps=4):
    p=[np.array(v,float) for v in points]; out=[]
    for i in range(len(p)-1):
        a,b,c,d=p[max(i-1,0)],p[i],p[i+1],p[min(i+2,len(p)-1)]
        for t in np.linspace(0,1,steps,endpoint=False):
            out.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    out.append(p[-1]); return out
