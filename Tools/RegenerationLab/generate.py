#!/usr/bin/env python3
"""Opt-in, offline authoring of original baked Unity props into a NEW directory.

Usage: python Tools/RegenerationLab/generate.py --output /tmp/rgl-review
Never generates geometry in Unity, overwrites an existing output, opens a scene,
or edits project settings. The published Assets are the product, not this tool.
"""
from __future__ import annotations
import argparse
import json
import shutil
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from geometry import ROOT, MATERIALS, CELLS, YAML, Mesh, basis, catmull, guid, vector

SOURCE=Path(__file__).resolve().parent
LABELS={'treatment':'TREATMENT', 'delivery':'DELIVERY', 'cold_storage':'COLD STORAGE',
        'tissue_label':'TISSUE', 'sealed':'SEALED', 'rinse':'RINSE', 'agent':'AGENT'}
# Original 5x7 lettering, stored as editable bits rather than a distributed font.
GLYPHS={
 'A':['01110','10001','10001','11111','10001','10001','10001'],
 'B':['11110','10001','10001','11110','10001','10001','11110'],
 'C':['01111','10000','10000','10000','10000','10000','01111'],
 'D':['11110','10001','10001','10001','10001','10001','11110'],
 'E':['11111','10000','10000','11110','10000','10000','11111'],
 'F':['11111','10000','10000','11110','10000','10000','10000'],
 'G':['01111','10000','10000','10111','10001','10001','01110'],
 'H':['10001','10001','10001','11111','10001','10001','10001'],
 'I':['11111','00100','00100','00100','00100','00100','11111'],
 'J':['00111','00010','00010','00010','10010','10010','01100'],
 'K':['10001','10010','10100','11000','10100','10010','10001'],
 'L':['10000','10000','10000','10000','10000','10000','11111'],
 'M':['10001','11011','10101','10101','10001','10001','10001'],
 'N':['10001','11001','11001','10101','10011','10011','10001'],
 'O':['01110','10001','10001','10001','10001','10001','01110'],
 'P':['11110','10001','10001','11110','10000','10000','10000'],
 'Q':['01110','10001','10001','10001','10101','10010','01101'],
 'R':['11110','10001','10001','11110','10100','10010','10001'],
 'S':['01111','10000','10000','01110','00001','00001','11110'],
 'T':['11111','00100','00100','00100','00100','00100','00100'],
 'U':['10001','10001','10001','10001','10001','10001','01110'],
 'V':['10001','10001','10001','10001','10001','01010','00100'],
 'W':['10001','10001','10001','10101','10101','10101','01010'],
 'X':['10001','10001','01010','00100','01010','10001','10001'],
 'Y':['10001','10001','01010','00100','00100','00100','00100'],
 'Z':['11111','00001','00010','00100','01000','10000','11111'],
 ' ':['00000']*7,
}


def textures(folder, labels):
    rng=np.random.default_rng(417)
    for name,base,brushed in [('PaintWear',222,False),('SteelBrush',201,True)]:
        small=Image.fromarray(rng.integers(92,166,(32,32),dtype=np.uint8)).resize((512,512),Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(8))
        noise=np.array(small,dtype=float)-128
        fine=rng.normal(0,1.1,(512,512))
        if brushed:
            fine+=rng.normal(0,2.4,(512,1))
        values=np.clip(base+noise*.27+fine,0,255).astype(np.uint8)
        image=Image.fromarray(np.repeat(values[:,:,None],3,axis=2))
        draw=ImageDraw.Draw(image)
        for _ in range(32 if brushed else 19):
            x,y=map(int,rng.integers(4,498,2)); length=int(rng.integers(5,29))
            shade=int(base-rng.integers(14,35))
            draw.line((x,y,min(x+length,510),y+(0 if brushed else 2)),fill=(shade,)*3,width=1)
        image.save(folder/(name+'.png'))
    atlas=Image.new('RGB',(1024,1024),(37,44,43)); draw=ImageDraw.Draw(atlas)
    tones={'rubber':(39,46,47),'ivory':(207,207,183),'fluid':(143,119,75),
           'tissue':(133,106,97),'recess':(26,32,33),'ochre':(169,148,97)}
    for name,color in tones.items():
        draw.rectangle(CELLS[name],fill=color)
        x0,y0,x1,y1=CELLS[name]
        for y in range(y0+5,y1-5,9):
            draw.line((x0+5,y,x1-5,y),fill=tuple(max(v-3,0) for v in color),width=1)
    for key,text in labels.items():
        if key not in LABELS or not text or any(c not in GLYPHS for c in text):
            raise ValueError(f'Label {key}: use short upper-case A-Z text and spaces')
        x0,y0,x1,y1=CELLS[key]
        draw.rectangle((x0+2,y0+2,x1-2,y1-2),fill=(205,209,187))
        draw.rectangle((x0+9,y0+9,x1-9,y1-9),fill=(42,55,52))
        scale=min((x1-x0-42)//(6*len(text)-1),(y1-y0-34)//7)
        if scale<3: raise ValueError(f'Label {key} is too long')
        startx=x0+(x1-x0-(6*len(text)-1)*scale)//2
        starty=y0+(y1-y0-7*scale)//2
        for i,char in enumerate(text):
            for y,row in enumerate(GLYPHS[char]):
                for x,on in enumerate(row):
                    if on=='1':
                        left=startx+(i*6+x)*scale; top=starty+y*scale
                        draw.rectangle((left,top,left+scale-1,top+scale-1),fill=(225,227,205))
    atlas.save(folder/'PolymerLabels.png')


def native_meta(path, fileid):
    return f'fileFormatVersion: 2\nguid: {guid(path)}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: {fileid}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'


def texture_meta(path, clamp=False):
    # Opaque RGB albedo, mipmapped, no read/write, explicit Android ASTC 6x6.
    return f'''fileFormatVersion: 2
guid: {guid(path)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 2
    mipBias: 0
    wrapU: {int(clamp)}
    wrapV: {int(clamp)}
    wrapW: {int(clamp)}
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 0
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Android
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: 50
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  userData:
  assetBundleName:
  assetBundleVariant:
'''


MAT_SETTINGS=[('PaintWear',(.58,.66,.62),.12,.26),('SteelBrush',(.76,.79,.78),.65,.32),('PolymerLabels',(1,1,1),0,.22)]


def material(index):
    texture,color,metallic,smooth=MAT_SETTINGS[index]
    text=YAML+f'''--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {MATERIALS[index]}
  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
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
    m_TexEnvs:
'''
    for name in ('_BumpMap','_DetailAlbedoMap','_DetailMask','_DetailNormalMap','_EmissionMap','_MainTex','_MetallicGlossMap','_OcclusionMap','_ParallaxMap'):
        ref=f'{{fileID: 2800000, guid: {guid(ROOT+"/Textures/"+texture+".png")}, type: 3}}' if name=='_MainTex' else '{fileID: 0}'
        text+=f'    - {name}:\n        m_Texture: {ref}\n        m_Scale: {{x: 1, y: 1}}\n        m_Offset: {{x: 0, y: 0}}\n'
    text+='    m_Ints: []\n    m_Floats:\n'
    vals={'_BumpScale':1,'_Cutoff':.5,'_DetailNormalMapScale':1,'_DstBlend':0,'_GlossMapScale':1,'_Glossiness':smooth,
          '_GlossyReflections':1,'_Metallic':metallic,'_Mode':0,'_OcclusionStrength':1,'_Parallax':.02,
          '_SmoothnessTextureChannel':0,'_SpecularHighlights':1,'_SrcBlend':1,'_UVSec':0,'_ZWrite':1}
    text+=''.join(f'    - {key}: {value}\n' for key,value in vals.items())
    text+=f'    m_Colors:\n    - _Color: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}\n    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 1}}\n  m_BuildTextureStacks: []\n'
    return text


def common(go=None):
    text='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
    return text+(f'  m_GameObject: {{fileID: {go}}}\n' if go else '')


def prefab(name,body_mats,colliders):
    # Static, independent prefab: identity root; baked body + separate label plane.
    text=YAML
    for i,node in enumerate((name,'BakedMesh','EditableLabel')):
        go=100000+i*100; tr=400000+i*100
        comps=[tr] if i==0 else [tr,330000+i*100,230000+i*100]
        if i==0: comps += [650000+j for j in range(len(colliders))]
        text+=f'--- !u!1 &{go}\nGameObject:\n'+common()+ '  serializedVersion: 6\n  m_Component:\n'
        text+=''.join(f'  - component: {{fileID: {comp}}}\n' for comp in comps)
        text+=f'  m_Layer: 0\n  m_Name: {node}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'
        text+=f'--- !u!4 &{tr}\nTransform:\n'+common(go)+'  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n'
        text+=('  m_Children:\n  - {fileID: 400100}\n  - {fileID: 400200}\n' if i==0 else '  m_Children: []\n')
        text+=f'  m_Father: {{fileID: {0 if i==0 else 400000}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'
        if i:
            meshname=name+('_Label' if i==2 else '')
            mats=body_mats if i==1 else [MATERIALS[2]]
            text+=f'--- !u!33 &{330000+i*100}\nMeshFilter:\n'+common(go)+f'  m_Mesh: {{fileID: 4300000, guid: {guid(ROOT+"/Meshes/"+meshname+".asset")}, type: 2}}\n'
            text+=f'--- !u!23 &{230000+i*100}\nMeshRenderer:\n'+common(go)+f'''  m_Enabled: 1
  m_CastShadows: {1 if i==1 else 0}
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 0
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
'''
            text+=''.join(f'  - {{fileID: 2100000, guid: {guid(ROOT+"/Materials/"+mat+".mat")}, type: 2}}\n' for mat in mats)
            text+='''  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 2
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {fileID: 0}
'''
    for j,collider in enumerate(colliders):
        text+=f'--- !u!65 &{650000+j}\nBoxCollider:\n'+common(100000)+'''  m_Material: {fileID: 0}
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
'''+f'  m_Size: {vector(collider["size"])}\n  m_Center: {vector(collider["center"])}\n'
    return text


def trolley():
    m=Mesh('RGL_TreatmentTrolley'); label=Mesh(m.name+'_Label')
    for x in (-.32,.32):
        for z in (-.20,.20):
            m.lathe('Moulded caster tire',(x,.059,z),[(-.025,.049),(-.017,.059),(.017,.059),(.025,.049)],2,'rubber',16,(1,0,0))
            m.cylinder('Steel caster hub',(x,.059,z),.022,.053,1,sides=12,axis=(1,0,0))
            for dx in (-.034,.034): m.box('Caster fork',(x+dx,.111,z),(.012,.10,.057),1,.004)
            m.box('Fork bridge',(x,.161,z),(.083,.019,.06),1,.004)
            m.cylinder('Caster swivel',(x,.181,z),.025,.025,1,sides=12)
            m.box('Square tube leg',(x,.486,z),(.029,.602,.029),1,.003)
    for height in (.277,.514):
        m.box('Formed shelf deck',(0,height,0),(.681,.023,.432),1,.006)
        for z in (-.211,.211):m.box('Shelf rolled edge',(0,height+.019,z),(.68,.03,.013),0,.004)
        for x in (-.335,.335):m.box('Shelf end lip',(x,height+.019,0),(.013,.03,.41),0,.004)
    m.box('Upper painted apron',(0,.742,0),(.731,.083,.454),0,.007)
    m.box('Working tray',(0,.796,0),(.754,.025,.484),1,.008)
    for z in (-.234,.234):m.box('Tray rim',(0,.826,z),(.736,.042,.015),1,.005)
    for x in (-.369,.369):m.box('Tray rim end',(x,.826,0),(.015,.042,.46),1,.005)
    # Broad U-shaped push handle; no separate snagging colliders.
    pts=catmull([(.338,.72,-.18),(.429,.77,-.18),(.447,.878,-.15),(.447,.888,.15),(.429,.77,.18),(.338,.72,.18)],5)
    m.tube('Bent tubular push handle',pts,.014,1,None,12)
    m.tube('Rubber grip',[(.447,.888,-.10),(.447,.888,.10)],.018,2,'rubber',12)
    for x in (-.16,.11):
        m.box('Packaged dressing supplies',(x,.337,-.025),(.227,.092,.251),2,.009,'ivory')
        m.box('Package seam',(x,.385,-.025),(.224,.004,.018),2,.001,'rubber')
    m.box('Reusable instrument case',(-.11,.564,0),(.39,.068,.276),0,.007)
    m.box('Case handle',(-.11,.557,.15),(.10,.025,.026),2,.005,'rubber')
    m.box('Top supply tray base',(-.135,.828,-.018),(.288,.022,.24),1,.006)
    for z in (-.139,.103):m.box('Supply tray rolled lip',(-.135,.851,z),(.28,.025,.011),1,.004)
    for x in (-.274,.004):m.box('Supply tray short lip',(x,.851,-.018),(.011,.025,.23),1,.004)
    for x in (-.20,-.088):
        m.box('Sealed dressing packet',(x,.857,-.025),(.089,.034,.155),2,.006,'ivory')
    m.lathe('Treatment bottle',(.235,.81,-.08),[(0,.045),(.013,.051),(.14,.051),(.162,.032),(.18,.025)],2,'ivory',16)
    m.cylinder('Bottle cap',(.235,1.005,-.08),.03,.034,2,'rubber',16)
    m.label((.235,.895,-.027),.062,.035,'agent')
    m.box('Label backing plate',(-.055,.742,.231),(.45,.063,.006),2,.002,'rubber')
    label.label((-.055,.742,.235),.428,.057,'treatment')
    return m,label,[{'center':[0,.425,0],'size':[.758,.85,.49]}]


def stand():
    m=Mesh('RGL_ChemicalDeliveryStand'); label=Mesh(m.name+'_Label')
    m.lathe('Weighted rubber base',(0,0,0),[(0,.246),(.009,.272),(.027,.272),(.036,.255)],2,'rubber',16)
    m.lathe('Weighted cast plinth',(0,0,0),[(.031,.256),(.048,.247),(.082,.186),(.088,.17)],0,None,16)
    m.box('Upright support',(0,.624,-.037),(.05,1.10,.055),0,.005)
    m.box('Telescopic stainless extension',(0,1.319,-.037),(.032,.57,.035),1,.003)
    for h in (.49,1.104):
        m.box('Adjustment collar',(0,h,-.037),(.084,.078,.085),2,.007,'rubber')
        m.cylinder('Cam clamp axle',(.057,h,-.037),.012,.04,1,sides=12,axis=(1,0,0))
        m.box('Rounded clamp lever',(.079,h+.021,-.037),(.018,.079,.035),2,.008,'rubber')
    m.tube('Container support crossbar',[(-.235,1.569,-.037),(.235,1.569,-.037)],.015,1,None,12)
    for x,key in ((-.13,'agent'),(.13,'rinse')):
        m.box('Container cradle backing',(x,1.381,-.028),(.112,.25,.026),0,.009)
        for h in (1.256,1.463):
            m.box('Container collar bracket',(x,h,.03),(.129,.021,.142),1,.003)
        m.lathe('Treatment reservoir',(x,1.215,.044),[(0,.019),(.025,.022),(.056,.053),(.231,.053),(.245,.046)],2,'ivory',16)
        m.cylinder('Sealed reservoir lid',(x,1.478,.044),.059,.036,2,'rubber',16)
        m.cylinder('Delivery nipple',(x,1.209,.044),.014,.039,1,sides=12)
        m.box('Opaque fluid sight strip',(x+.029,1.356,.089),(.011,.106,.012),2,.002,'fluid')
        m.label((x-.005,1.39,.099),.069,.031,key)
        tube=catmull([(x,1.19,.044),(x,1.142,.055),(x*.70,1.113,.075),(x*.70,1.068,.075)],5)
        m.tube('Secured supply line',tube,.0065,2,'rubber',8)
        m.cylinder('Regulator inlet ferrule',(x*.70,1.071,.075),.014,.018,1,sides=12)
    m.box('Delivery regulator enclosure',(0,.979,.035),(.267,.174,.159),0,.013)
    m.box('Regulator faceplate',(0,.979,.12),(.239,.141,.013),1,.006)
    m.box('Label plate',(0,1.015,.131),(.224,.051,.006),2,.002,'rubber')
    label.label((0,1.015,.135),.208,.045,'delivery')
    m.cylinder('Mechanical dial',(-.058,.953,.139),.023,.018,2,'ivory',16,axis=(0,0,1))
    m.box('Dial index',(-.059,.961,.15),(.003,.02,.004),2,0,'rubber')
    m.cylinder('Flow control',(.061,.948,.14),.024,.027,2,'rubber',12,axis=(0,0,1))
    m.box('Control grip',(.061,.948,.157),(.047,.01,.012),2,.002,'rubber')
    returntube=catmull([(.087,.897,.08),(.092,.744,.065),(.096,.551,.066),(.079,.368,.07),
                       (-.054,.341,.069),(-.091,.417,.067),(-.084,.618,.069),(-.065,.674,.052)],5)
    m.tube('Clipped return loop',returntube,.007,2,'rubber',8)
    for h in (.57,.77):
        m.box('Tube retaining clip',(.043,h,.043),(.107,.027,.03),1,.004)
    m.lathe('Sealed return cartridge',(-.065,.625,.036),[(-.071,.026),(-.06,.034),(.045,.034),(.066,.02)],2,'ivory',12)
    m.cylinder('Cartridge collar',(-.065,.566,.036),.037,.028,0,sides=12)
    return m,label,[{'center':[0,.044,0],'size':[.544,.088,.544]},
                    {'center':[0,.841,.017],'size':[.272,1.506,.204]}]


def cabinet():
    m=Mesh('RGL_SpecimenColdCabinet'); label=Mesh(m.name+'_Label')
    for x in (-.32,.32):
        for z in (-.23,.23):
            m.lathe('Leveling foot',(x,0,z),[(0,.044),(.013,.047),(.05,.039),(.083,.028)],2,'rubber',12)
    m.box('Cabinet back',(0,.887,-.278),(.792,1.634,.046),0,.014)
    for x in (-.38,.38): m.box('Insulated side casing',(x,.887,0),(.063,1.634,.59),0,.014)
    m.box('Top casing',(0,1.673,0),(.824,.063,.602),0,.014)
    m.box('Bottom casing',(0,.106,0),(.801,.07,.586),0,.01)
    m.box('Front door gasket',(0,.662,.285),(.738,.629,.029),2,.009,'rubber')
    m.box('Closed storage door',(0,.666,.307),(.699,.59,.05),0,.012)
    # A recessed inspection display, not a transparent shader or an opening door.
    m.box('Display inner back',(0,1.296,.052),(.659,.572,.032),2,.006,'recess')
    for x in (-.323,.323):m.box('Display liner side',(x,1.289,.151),(.022,.558,.207),1,.006)
    m.box('Display liner floor',(0,1.023,.149),(.663,.028,.222),1,.006)
    m.box('Display liner ceiling',(0,1.556,.149),(.663,.024,.222),1,.006)
    for x in (-.344,.344):m.box('Display vertical frame',(x,1.291,.295),(.045,.594,.058),0,.009)
    for h in (1.006,1.575):m.box('Display horizontal frame',(0,h,.295),(.715,.041,.058),0,.009)
    m.box('Display raised retaining lip',(0,1.071,.287),(.639,.046,.02),1,.005)
    for x in (-.21,0,.21):
        m.lathe('Opaque sealed sample container',(x,1.049,.162),[(0,.047),(.014,.055),(.217,.055),(.235,.049)],2,'ivory',16)
        m.cylinder('Sample container lid',(x,1.30,.162),.061,.035,2,'rubber',16)
        m.box('Sample inspection inset',(x,1.177,.218),(.073,.135,.012),2,.008,'recess')
        # Quiet biological evidence: short irregular tissue strips, no eyes/faces.
        profile=[(-.045,.012),(-.032,.017),(-.012,.021),(.008,.018),(.026,.021),(.045,.01)]
        m.lathe('Inert tissue sample',(x,1.172,.225),profile,2,'tissue',9)
        m.label((x,1.277,.219),.077,.026,'sealed')
    m.box('Compressor service panel',(0,.237,.297),(.716,.214,.041),0,.01)
    for h in (.171,.202,.233,.264,.295):
        m.box('Recessed compressor louvre',(0,h,.319),(.464,.014,.009),2,.003,'recess')
        m.box('Louvre drip edge',(0,h+.007,.326),(.474,.005,.01),1,.001)
    handle=[(.271,.470,.336)]
    handle += [(.271,.505-.035*np.cos(t),.357+.035*np.sin(t)) for t in np.linspace(0,np.pi/2,9)]
    handle += [(.271,.68,.392)]
    handle += [(.271,.854+.035*np.sin(t),.357+.035*np.cos(t)) for t in np.linspace(0,np.pi/2,9)]
    handle += [(.271,.889,.336)]
    m.tube('Fixed insulated door handle',handle,.015,1,None,12)
    m.tube('Handle rubber grip',[(.271,.58,.392),(.271,.78,.392)],.018,2,'rubber',12)
    for h in (.458,.865):m.box('Captive hinge cover',(-.355,h,.332),(.052,.089,.025),1,.008)
    m.box('Cabinet title backing',(0,1.64,.3),(.662,.079,.018),2,.004,'rubber')
    label.label((0,1.64,.312),.638,.069,'cold_storage')
    m.label((-.052,.858,.334),.235,.055,'tissue_label')
    m.box('Door service badge',(-.18,.463,.335),(.109,.041,.004),2,.002,'ivory')
    m.box('Rear compressor service hatch',(0,.291,-.308),(.502,.276,.018),0,.007)
    for h in (.206,.244,.282,.320,.358):
        m.box('Rear recessed service vent',(0,h,-.319),(.374,.013,.006),2,.003,'recess')
    return m,label,[{'center':[0,.85225,.017],'size':[.824,1.7045,.646]}]


def metadata(destination):
    kit=destination/ROOT
    folders=[kit]+sorted(p for p in kit.rglob('*') if p.is_dir())
    for p in folders:
        rel=p.relative_to(destination).as_posix()
        p.with_name(p.name+'.meta').write_text(f'fileFormatVersion: 2\nguid: {guid(rel)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
    for p in sorted(kit.rglob('*')):
        if not p.is_file() or p.suffix=='.meta':continue
        rel=p.relative_to(destination).as_posix()
        if p.suffix=='.asset':text=native_meta(rel,4300000)
        elif p.suffix=='.mat':text=native_meta(rel,2100000)
        elif p.suffix=='.png':text=texture_meta(rel,p.stem=='PolymerLabels')
        elif p.suffix=='.prefab':text=f'fileFormatVersion: 2\nguid: {guid(rel)}\nPrefabImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        elif p.suffix=='.cs':text=f'fileFormatVersion: 2\nguid: {guid(rel)}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{fileID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        else:text=f'fileFormatVersion: 2\nguid: {guid(rel)}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
        p.with_name(p.name+'.meta').write_text(text,encoding='utf-8')


def generate(output: Path, labels_path=None):
    if output.exists():
        raise FileExistsError(f'Refusing to overwrite existing output: {output}')
    labels=dict(LABELS)
    if labels_path:
        labels.update(json.loads(Path(labels_path).read_text(encoding='utf-8')))
    kit=output/ROOT
    for directory in ('Meshes','Textures','Materials','Prefabs','Editor'):
        (kit/directory).mkdir(parents=True,exist_ok=True)
    exports=output/'Tools/RegenerationLab/Exports'; exports.mkdir(parents=True)
    textures(kit/'Textures',labels)
    for i,name in enumerate(MATERIALS):
        (kit/'Materials'/(name+'.mat')).write_text(material(i),encoding='utf-8')
    inventory={'schema':1,'basis':'metres; +Y up; +Z front; floor-contact root; identity transforms',
               'baseline':'930a8f830cae99e06dcaa0a78ab9edc968c42ae5',
               'editor':'Unity 2022.3.62f3 (96770f904ca7)', 'render_pipeline':'Built-in Standard',
               'materials':list(MATERIALS),'textures':{'PaintWear.png':[512,512],'SteelBrush.png':[512,512],'PolymerLabels.png':[1024,1024]},
               'props':[],'status':'Baked review assets; Unity and headset acceptance pending; not final room dressing'}
    for factory in (trolley,stand,cabinet):
        body,label,colliders=factory()
        info=body.save(kit/'Meshes'/(body.name+'.asset'))
        label_info=label.save(kit/'Meshes'/(label.name+'.asset'))
        body.obj(exports/(body.name+'.obj')); label.obj(exports/(label.name+'.obj'))
        (kit/'Prefabs'/(body.name+'.prefab')).write_text(prefab(body.name,info['material_slots'],colliders),encoding='utf-8')
        info['label_mesh']=label_info
        info['prefab']=ROOT+'/Prefabs/'+body.name+'.prefab'
        info['colliders']=colliders; info['total_triangles']=info['triangles']+label_info['triangles']
        inventory['props'].append(info)
    # These source templates are copied verbatim; no automatic Editor authoring.
    shutil.copyfile(SOURCE/'RegenerationLabPlacement.cs.txt',kit/'Editor/RegenerationLabPlacement.cs')
    (kit/'RegenerationLab.inventory.json').write_text(json.dumps(inventory,indent=2)+'\n',encoding='utf-8')
    (kit/'Labels.json').write_text(json.dumps(labels,indent=2)+'\n',encoding='utf-8')
    metadata(output)
    mtl=[]
    for name,(tex,tint,metal,smooth) in zip(MATERIALS,MAT_SETTINGS):
        texture_path='../../../'+ROOT+'/Textures/'+tex+'.png'
        mtl+=['newmtl '+name,'Kd '+' '.join(map(str,tint)),'Ks 0.25 0.25 0.25','Ns 30','map_Kd '+texture_path,'']
    (exports/'RegenerationLab.mtl').write_text('\n'.join(mtl),encoding='utf-8')
    print(json.dumps({p['name']:p['total_triangles'] for p in inventory['props']},indent=2))
    return inventory


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True,help='New, nonexistent output directory')
    parser.add_argument('--labels',type=Path,help='Optional short label overrides JSON')
    args=parser.parse_args()
    generate(args.output,args.labels)
