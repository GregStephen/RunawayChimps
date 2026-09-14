#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def append_once(path: str, marker: str, text: str) -> None:
    target = ROOT / path
    current = target.read_text(encoding="utf-8-sig")
    if marker in current:
        return
    target.write_text(current.rstrip() + "\n\n" + text.strip() + "\n", encoding="utf-8")


append_once(
    "docs/design-and-lore.md",
    "## September 13 launch presentation source validation",
    '''## September 13 launch presentation source validation

**Validated source only:** Source Integrity run `34798289632` passed on `feature/launch-presentation-polish` commit `75104bd7051a772385636cab61f75e4e0b4e86ac`. The combined run passed first-party C# syntax/reference checks, Unity metadata/GUID integrity, Level 1 and PR #15 contracts, the merged security-boot contracts, PR #19 local threat-feedback contracts, the new launch-presentation contracts, Python compilation, merge-marker checks and human-authored whitespace. This is source/tooling evidence only.

**Still pending:** Unity 2022.3.55f1 import/compile, Editor splash apply/validate, installed Quest APK system-splash behavior, compositor-to-security-boot handoff, both-eye/peripheral comfort, Unity-license splash behavior, Photon startup/retry/two-client regression, black-only sector travel and Quest performance remain unvalidated.'''
)

append_once(
    "docs/repository-improvement-plan.md",
    "## September 13 launch presentation source-validation result",
    '''## September 13 launch presentation source-validation result

**Validated source only:** Source Integrity run `34798289632` passed on `feature/launch-presentation-polish` commit `75104bd7051a772385636cab61f75e4e0b4e86ac`, including the launch-presentation validator alongside the existing security-boot, threat-feedback, Level 1 and PR #15 contract checks. Repository integrity, C# syntax/reference checks, Python compilation, merge-marker checks and human-authored whitespace also passed.

**Validation boundary:** this does not establish Unity compilation, Android build success, Meta compositor splash behavior, first-frame handoff, headset comfort, Photon runtime behavior or Quest performance. Those remain pending and must be recorded from actual Unity/device tests.'''
)
