#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
old_helper = ROOT / "Tools/_apply_launch_review_fixes.py"
source = old_helper.read_text(encoding="utf-8")
start = source.index("# Commit Meta OpenXR splash assignment instead of mutating it during build.")
end = source.index("# Validators: protect architecture, not exact art coordinates.", start)
replacement = r'''# Commit Meta OpenXR splash assignment instead of mutating it during build.
p = "Assets/XR/Settings/OpenXR Package Settings.asset"
s = read(p)
pattern = re.compile(r"(  m_Name: MetaXRFeature Android[\s\S]*?\n  systemSplashScreen: )\{fileID: 0\}")
replacement_value = r"\1{fileID: 2800000, guid: 73af3b98a31d49a6a0b674b50ed8d20c, type: 3}"
s, changed = pattern.subn(replacement_value, s, count=1)
if changed == 0 and "m_Name: MetaXRFeature Android" in s:
    block_start = s.index("m_Name: MetaXRFeature Android")
    if "systemSplashScreen: {fileID: 2800000, guid: 73af3b98a31d49a6a0b674b50ed8d20c, type: 3}" not in s[block_start:]:
        raise RuntimeError("Meta Android systemSplashScreen field shape changed")
write(p, s)

'''
source = source[:start] + replacement + source[end:]
exec(compile(source, str(old_helper), "exec"), {"__file__": str(old_helper), "__name__": "__main__"})
for rel in ["Tools/_apply_launch_review_fixes_v2.py", ".github/workflows/apply-launch-review-fixes-v2.yml"]:
    path = ROOT / rel
    if path.exists():
        path.unlink()
