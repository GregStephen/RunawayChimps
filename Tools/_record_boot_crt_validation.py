#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

RECORDS = {
    "docs/design-and-lore.md": '''## September 13 launch-terminal CRT/static source validation

**Validated source only:** final clean CRT-polish head `9f3e827c6004af395735cba98cf86d030323bc6a` passed Source Integrity run `34799347329`. The run passed the existing Level 1, PR #15, security-boot and PR #19 threat-feedback contracts plus the updated launch-presentation/CRT contracts, repository integrity, C# syntax/reference checks, Python compilation, merge-marker checks and human-authored whitespace. **Pending validation remains:** Unity 2022.3.55f1 import/compile, Play Mode visual review of static/scanlines/interference, headset comfort/stereo coverage, Photon startup/travel regression, native Quest system-splash handoff and Quest performance. Source validation does not establish visual comfort or device behavior.''',
    "docs/repository-improvement-plan.md": '''## September 13 launch-terminal CRT/static source-validation result

**Validated source only:** final clean CRT-polish head `9f3e827c6004af395735cba98cf86d030323bc6a` passed Source Integrity run `34799347329`, including the launch-presentation validator's bounded CRT/static rules together with the repository's existing security-boot, threat-feedback, Level 1 and PR #15 contracts. **Still pending:** Unity 2022.3.55f1 import/compile, Play Mode appearance/readability/flicker review, headset stereo/comfort, Photon regression, native Quest compositor splash behavior and Quest performance. Do not promote those runtime/device items to validated from this source result.'''
}

for relative, block in RECORDS.items():
    path = ROOT / relative
    current = path.read_text(encoding="utf-8-sig")
    heading = block.splitlines()[0]
    if heading not in current:
        path.write_text(current.rstrip() + "\n\n" + block.strip() + "\n", encoding="utf-8")
