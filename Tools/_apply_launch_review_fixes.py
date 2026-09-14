#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
p = ROOT / "Tools/validate_level1_contracts.py"
text = p.read_text(encoding="utf-8-sig")
old = '        "GroundCorrect(spawnGo.transform.position, hubScene, locomotionPlayer)",\n'
new = '        "GroundCorrect(spawnPosition, hubScene, locomotionPlayer)",\n        "TryGetLocalSpawnPose(spawnGo.transform, out spawnPosition, out spawnRotation)",\n'
if text.count(old) != 1:
    raise RuntimeError(f"Level 1 Hub-grounding contract shape changed: {text.count(old)} matches")
p.write_text(text.replace(old, new, 1), encoding="utf-8")
Path(__file__).unlink()
