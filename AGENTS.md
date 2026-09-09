# Runaway Chimps repository guidance

## Project scope

This repository is Runaway Chimps, a VR horror game using **Unity 2022.3.55f1** and **Photon PUN**. Cheeky Chimps is a separate non-horror Unity 6 project. Keep its code, design decisions, and editor guidance separate.

## Read before proposing or implementing changes

Read the relevant sections of these maintained documents on the branch being discussed:

- [Design and lore](docs/design-and-lore.md): game rules, confirmed decisions, proposals, lore, level concepts, and corrections.
- [Repository improvement plan](docs/repository-improvement-plan.md): baseline evidence, implementation priorities, current work, unresolved choices, and acceptance checks.
- [README](README.md): project entry point, current layout, implemented reliability changes, and pre-merge Unity/headset checks.

Read the current implementation when a recommendation depends on runtime behavior or serialized Unity wiring. The design describes intent; code inspection establishes what is present; executed tests establish what works. If those disagree, record the discrepancy without silently treating the design as implemented. Report unavailable documents or evidence accurately rather than filling gaps from memory.

## Keep the documentation current

Update the relevant maintained sections whenever Greg confirms a decision, corrects earlier information, changes the design, or implementation work meaningfully changes project behavior. Record meaningful test outcomes too. Include the documentation update with the corresponding code change when possible.

Greg's latest explicit corrections take precedence over older project documentation. Record the correction, its date, and the rule it supersedes in the design's decision and correction record; update affected sections in both documents so stale instructions do not remain active. Check the actual branch and PR state before describing work as merged or current on `main`.

Use precise status labels:

| Status | Meaning |
| --- | --- |
| Proposed | An idea awaiting agreement. |
| Confirmed | A design decision explicitly chosen by Greg; this alone does not imply implementation. |
| Planned | An accepted direction awaiting implementation. |
| Open | An unresolved choice; preserve the alternatives until decided. |
| Implemented | Code or assets exist; identify the branch, commit, or PR and whether it is merged. |
| Pending validation | Specific compilation, Play Mode, multiplayer, or headset checks have not been completed. |
| Validated | A check was actually run; record its result, date, and tested version or commit. |

Keep design approval, implementation, and validation distinct. Source review and `git diff --check` do not establish Unity compilation, Photon behavior, or headset performance. An implemented fix may still be pending validation, and a partial fix does not close the broader finding.

Routine questions, explanations, and unaccepted brainstorming do not require a documentation edit. Make an edit when it records a meaningful decision, correction, implementation change, or test result. Preserve useful unresolved proposals without presenting them as agreed work.

## Maintained versions and snapshots

The Markdown files under `docs/` are the maintained versions. The earlier `Runaway_Chimps_Design_and_Lore.docx` and `Runaway_Chimps_Repository_Review_and_Plan.docx` are downloadable snapshots. Generate future Word/PDF exports from the maintained documents when requested, with their source revision and date; avoid maintaining a second independent set of decisions in an export.

Preserve the design references in `docs/images/` and their captions when editing the design. Keep source evidence links tied to the revision they describe, and distinguish historical findings from newer fixes.
