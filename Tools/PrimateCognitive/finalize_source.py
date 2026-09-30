"""One-time branch-only source/doc corrections; removed by the finishing workflow."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
path = root / 'Assets/Scripts/Toys/PrimateCognitive/CognitiveMachine.cs'
text = path.read_text()
text = text.replace('if (!replica.Elect(elected, Guid.NewGuid().ToString("N"))) return;', 'if (elected == replica.Authority) return;\n            if (!replica.Elect(elected, Guid.NewGuid().ToString("N"))) return;')
text = text.replace('if (!editorControls || pads == null || index < 0 || index >= pads.Length) return;', 'if (pads == null || index < 0 || index >= pads.Length || (held && !editorControls)) return;')
text = text.replace('state.Phase == CognitivePhase.Input ? "YOUR TURN  " + state.InputIndex + " / " + state.Round : "PRESS RESTART TO TRY AGAIN";', 'state.Phase == CognitivePhase.Input ? "YOUR TURN  " + state.InputIndex + " / " + state.Round :\n                state.Phase == CognitivePhase.Success ? "ROUND PASSED - NEXT TEST" : "PRESS RESTART TO TRY AGAIN";')
path.write_text(text)
section = '''## September 30 Primate Cognitive Evaluation optional toy

**Confirmed decision:** Greg requested a small optional facility memory-test machine, separate from the strength tester and all other toy branches. Four numbered/position-distinct, differently toned physical pads demonstrate an extending sequence; either hand repeats it. Start at one step, append one per successful round, show dry facility assessments/round reached, and provide physical restart with a bounded end. No rewards, progression, persistent leaderboard, voice acting or general minigame framework.

**Implemented source, unmerged draft:** `feature/primate-cognitive-test` starts from freshly fetched main `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`. Feature-scoped rules, all-contact-clear debouncing, local-rig filtering, phase/session token checks, one operator per machine, sector-controller arbitration, recoverable heartbeat/input/result deadlines, stale-authority handshakes and Editor held-contact controls are present. Existing PhysicalButton, Hub/Bootstrap, packages, Unity 2022.3.62f3, PUN, built-in rendering and XR configuration remain untouched. A pure managed harness exercises production rules/contact/replica code; the PR records executed source-check results separately from runtime acceptance.

**Incomplete asset delivery:** the serialized machine prefab, materials and audio clips were not created because the asset-generation write was blocked by the authoring tool. This branch is NOT a playable delivered feature. Editor placement/validation commands name the intended prefab but currently report its absence. Keep the PR draft and do not treat source implementation or CI as feature completion.

**Pending validation:** complete authored assets, confirm exact local-rig seated/standing reach, run Unity 62f3 import/compile and Editor authoring checks, then solo desktop, two-client Photon and headset acceptance. Shared sequence/audio, physical trigger behavior, sector/authority/pause recovery and placement clearance remain unexecuted. [Feature notes](primate-cognitive-evaluation.md) distinguish source evidence, intended setup/tuning and the full pending checklist.

'''
for name in ['design-and-lore.md', 'repository-improvement-plan.md']:
    path = root / 'docs' / name
    text = path.read_text()
    assert '## September 30 Primate Cognitive Evaluation optional toy' not in text
    text = text.replace('Last updated: 2026-09-22.', 'Last updated: 2026-09-30.', 1)
    position = text.index('\n## ')
    text = text[:position + 1] + section + text[position + 1:]
    path.write_text(text)
print('Updated only cognitive source and the two maintained documents; assets remain incomplete.')
