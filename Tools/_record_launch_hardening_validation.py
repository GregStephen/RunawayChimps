from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MARKER = "**Validated source only — PR #21 launch/session hardening:**"
BLOCK = """**Validated source only — PR #21 launch/session hardening:** clean durable head `594a1ba3082e3ab98c0171a245b3902b5ee1644b` passed Source Integrity run `34885765274` on 2026-09-14. The run passed Unity 2022.3.55f1 version enforcement, first-party C# syntax/reference and enabled-scene checks, Unity metadata/GUID integrity, Level 1 and PR #15 contracts, security-boot contracts, local threat-feedback contracts, launch/workstation plus demand-driven Hub-slot/session/build contracts, Python compilation, merge-marker rejection and human-authored whitespace. This is source/tooling evidence only; Unity import/compile, Play Mode, two-client Photon, authored marker clearance and Quest/headset behavior remain pending.\n"""

for relative in [
    "docs/design-and-lore.md",
    "docs/repository-improvement-plan.md",
    "docs/launch-presentation.md",
    "docs/launch-review-resolution.md",
    "docs/launch-workstation-vignette.md",
]:
    path = ROOT / relative
    text = path.read_text(encoding="utf-8-sig")
    if MARKER in text:
        continue
    path.write_text(text.rstrip() + "\n\n" + BLOCK, encoding="utf-8", newline="\n")
