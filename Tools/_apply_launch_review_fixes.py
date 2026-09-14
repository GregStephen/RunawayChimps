#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
path = ROOT / "Assets/Scripts/Loading/SecurityBootPresentation.cs"
text = path.read_text(encoding="utf-8-sig")
old = "            legacyStatus = legacyStatusText;\n"
new = "            this.legacyStatus = legacyStatus;\n"
if text.count(old) != 1:
    raise RuntimeError("SecurityBootPresentation legacy status assignment shape changed")
text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8")
Path(__file__).unlink()
