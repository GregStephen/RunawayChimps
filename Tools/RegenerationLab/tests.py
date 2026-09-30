#!/usr/bin/env python3
"""Negative controls proving that the read-back validator rejects damaged assets."""
from __future__ import annotations
import argparse
import re
import shutil
import tempfile
from pathlib import Path
import numpy as np
from validate import scan,ROOT


def run(root):
    assert scan(root)['status']=='PASS','Baseline kit must pass first'
    tests=[]
    def trial(name,mutate,wanted):
        with tempfile.TemporaryDirectory(prefix='regeneration-negative-') as temporary:
            copy=Path(temporary)/'tree';shutil.copytree(root/ROOT,copy/ROOT)
            shutil.copyfile((root/ROOT).with_name('RegenerationLab.meta'),(copy/ROOT).with_name('RegenerationLab.meta'))
            mutate(copy/ROOT)
            result=scan(copy)
            assert result['status']=='FAIL' and any(wanted in e for e in result['errors']),(name,result['errors'])
            tests.append(name)
    def replace(kit,path,old,new):
        p=kit/path;text=p.read_text();assert old in text; p.write_text(text.replace(old,new,1))
    def reverse_normals(kit):
        p=kit/'Meshes/RGL_TreatmentTrolley.asset';text=p.read_text()
        raw=re.search(r'_typelessdata: ([a-f0-9]+)',text)[1]
        data=np.frombuffer(bytes.fromhex(raw),dtype='<f4').copy().reshape(-1,12)
        data[:,3:6]*=-1
        p.write_text(text.replace(raw,data.tobytes().hex(),1))
    trial('Inverted normals',reverse_normals,'winding')
    trial('Non-Standard shader',lambda k:replace(k,'Materials/RGL_PaintedMetal.mat','m_Shader: {fileID: 46,','m_Shader: {fileID: 47,'),'not Standard')
    trial('Transparent material',lambda k:replace(k,'Materials/RGL_PaintedMetal.mat','_Mode: 0','_Mode: 3'),'not opaque')
    trial('Missing metadata',lambda k:(k/'Textures/PaintWear.png.meta').unlink(),'missing metadata')
    trial('Non-identity transform',lambda k:replace(k,'Prefabs/RGL_TreatmentTrolley.prefab','m_LocalScale: {x: 1, y: 1, z: 1}','m_LocalScale: {x: 2, y: 1, z: 1}'),'nonidentity')
    trial('Collider bounds change',lambda k:replace(k,'Prefabs/RGL_TreatmentTrolley.prefab','m_Size: {x: 0.758,','m_Size: {x: 4.758,'),'collider manifest mismatch')
    trial('Trigger collider',lambda k:replace(k,'Prefabs/RGL_TreatmentTrolley.prefab','m_IsTrigger: 0','m_IsTrigger: 1'),'invalid collision')
    trial('Wrong importer fileID',lambda k:replace(k,'Meshes/RGL_TreatmentTrolley.asset.meta','mainObjectFileID: 4300000','mainObjectFileID: 0'),'invalid native importer')
    trial('Scene-save hook',lambda k:replace(k,'Editor/RegenerationLabPlacement.cs','// Also guarded','// SaveScene(\n        // Also guarded'),'forbidden automatic')
    trial('Swallowed placement failure',lambda k:replace(k,'Editor/RegenerationLabPlacement.cs','                throw;\n            }','            }'),'must propagate placement failures')
    print('PASS: clean package plus '+str(len(tests))+' rejecting negative controls: '+', '.join(tests))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True)
    args=parser.parse_args();run(args.root)
