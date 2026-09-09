"""Package generated Unity assets without requiring the Unity Editor."""
from pathlib import Path
import tarfile,io,re,sys,json
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1]
OUT=Path(sys.argv[1]);OUT.mkdir(parents=True,exist_ok=True)
asset=ROOT/'Assets/RunawayChimps/Level2Blockout'
paths=[asset.parent,asset]+sorted(asset.rglob('*'))
paths=[p for p in paths if p.suffix!='.meta' and Path(str(p)+'.meta').exists()]
destination=OUT/'RunawayChimps_Level2_Blockout.unitypackage'
def member(t,name,content):
    info=tarfile.TarInfo(name);info.size=len(content);info.mode=0o644;info.mtime=0
    t.addfile(info,io.BytesIO(content))
with tarfile.open(destination,'w:gz') as t:
    for p in paths:
        md=Path(str(p)+'.meta').read_bytes();g=re.search(rb'^guid: (\w+)',md,re.M)[1].decode()
        member(t,g+'/pathname',str(p.relative_to(ROOT)).replace('\\','/').encode())
        member(t,g+'/asset.meta',md)
        if p.is_file():member(t,g+'/asset',p.read_bytes())
with tarfile.open(destination) as t:
    names=t.getnames();assert len(names)==len(set(names))
    for p in paths:
        md=Path(str(p)+'.meta').read_bytes();g=re.search(rb'^guid: (\w+)',md,re.M)[1].decode()
        assert t.extractfile(g+'/asset.meta').read()==md
        if p.is_file():assert t.extractfile(g+'/asset').read()==p.read_bytes()
print(json.dumps({'package':str(destination),'entries':len(paths),'byte_verification':True}))
