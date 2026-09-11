from pathlib import Path
p=Path('Tools/Level2Blockout/apply_fuse_objective.py')
s=p.read_text()
repls={
"(2,'Service_Cabinet',25.5,12.4)":"(2,'Service_Cabinet',20.5,12.4)",
"'repair_lab':(28,27)":"'repair_lab':(24.2,27)",
"'fuse_2_cache':(25.5,11.5)":"'fuse_2_cache':(20.5,11.5)",
}
for old,new in repls.items():
    if old not in s:
        raise SystemExit(f'Expected helper text not found: {old}')
    s=s.replace(old,new,1)
p.write_text(s)
