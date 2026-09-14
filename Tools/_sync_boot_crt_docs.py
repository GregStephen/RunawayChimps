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
    "## September 13 approved launch-terminal CRT/static polish",
    '''## September 13 approved launch-terminal CRT/static polish

**Confirmed correction from Greg:** the approved green facility security-system boot should not remain visually flat while the follow-up branch focuses only on the native Quest splash. The in-app terminal itself should have restrained static/CRT character so it reads more like an aging facility security display. This visual treatment must be visible in ordinary Unity Play Mode for iteration; the separate Meta/Horizon system splash is device/compositor behavior and still requires an Android/Quest build to see.

**Implemented on `feature/launch-presentation-polish` / PR #21, pending runtime validation:** `SecurityBootPresentation` now renders a low-contrast 64 × 48 procedural static texture behind the terminal text, faint fixed scanlines, and an occasional soft horizontal interference sweep. A small bounded static increase accompanies real startup milestones coming online. The treatment never moves the tracked camera, never flashes the complete stereo FOV, does not use a RenderTexture or shared `UnityEngine.Random`, and leaves successful Hub/level travel black-only. Prototype-only runtime/Inspector naming was removed from the active launch flow.

**Pending validation:** open `Bootstrap.unity` in Unity 2022.3.55f1 and judge the noise/scanline/interference strength, text readability, `ACCESS GRANTED` handoff and absence of uncomfortable flicker in Play Mode. Then repeat in headset/Quest when build setup is available. Source validation must pass on the final CRT-polish head; source checks do not establish visual comfort or compositor behavior.'''
)

append_once(
    "docs/repository-improvement-plan.md",
    "## September 13 launch-terminal CRT/static production polish",
    '''## September 13 launch-terminal CRT/static production polish

**Confirmed UX correction:** PR #21 must improve the Unity-rendered green security terminal itself, not only add native Quest splash plumbing. The terminal treatment is intended to be directly reviewable from `Bootstrap.unity` in Play Mode. The Meta system splash remains a separate pre-first-frame Quest compositor layer and therefore cannot be validated from Editor Play Mode.

**Implemented source on `feature/launch-presentation-polish`:** `SecurityBootPresentation` now owns one reusable 64 × 48 `Texture2D`/`Color32[]` noise buffer refreshed at a low rate, faint static scanlines, and one bounded horizontal interference sweep. Real readiness changes can briefly increase the panel-only static alpha. The implementation is local UI only, avoids RenderTexture allocation and `UnityEngine.Random`, does not change Photon/readiness authority, does not move the XR camera, and preserves black-only normal sector travel. `Tools/validate_launch_presentation.py` now protects the CRT treatment and rejects reintroduction of prototype-only runtime labels.

**Pending validation:** Unity 2022.3.55f1 import/compile and Play Mode visual review; text readability and flicker comfort; headset stereo/peripheral review; Quest frame/memory cost; native system-splash handoff; Photon startup/retry and normal black-travel regression. Keep these runtime/device items open even when Source Integrity is green.'''
)
