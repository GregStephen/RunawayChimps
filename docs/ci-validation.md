# Automated source validation

Last updated: 2026-09-12.

## Implemented check

`.github/workflows/source-validation.yml` provides the repository-wide **Source integrity** GitHub Actions check. It is intentionally a fast, read-only source gate rather than a claim of Unity or headset validation.

It runs on:

- every pull request;
- pushes to `main`;
- pushes to `codex/**` branches;
- pushes to `design/**` branches; and
- manual `workflow_dispatch` runs.

The workflow uses read-only repository permissions and a 10-minute timeout. It runs on Ubuntu with Python 3.11 and pinned parser dependencies (`tree-sitter==0.25.2`, `tree-sitter-c-sharp==0.23.5`).

## What Source integrity checks

1. `ProjectSettings/ProjectVersion.txt` still declares **Unity 2022.3.55f1**.
2. `python Tools/validate_source.py --syntax` checks the first-party C# trees under `Assets/Scripts` and `Assets/Resources/PhotonVR/Scripts` for component filename/class matching, script `.meta` presence and duplicate script GUIDs, and tree-sitter C# syntax. It also checks enabled Build Settings scenes for missing files, scene-GUID mismatches, duplicate local object IDs, and unresolved local references.
3. `python Tools/validate_repository_integrity.py` scans the full `Assets` tree for missing/orphaned `.meta` files and duplicate Unity asset GUIDs, checks case-insensitive path collisions, and parses the Unity package manifests, asmdef/asmref files, and tracked tool JSON.
4. `python Tools/validate_level1_contracts.py` protects the active Level 1 implementation from source-level regression: local-only vent headlamp gating/equip behavior, Crawler visual anchor/path-following and no-root-motion assumptions, head-authoritative safe boundaries, guarded capture/drop behavior, XR startup settling, and the bounded non-allocating floor-penetration guard. It also checks the Level 1 scene still serializes at least two safe-boundary zone triggers, a vent zone trigger, and the Crawler capture component.
5. `python -m compileall -q Tools` prevents tracked Python tooling from containing basic syntax errors.
6. A merge-marker scan rejects unresolved `<<<<<<<`, `=======`, and `>>>>>>>` conflict markers.
7. `git diff --check` runs against the complete pull-request diff or pushed commit range and catches whitespace errors introduced by the change.

The contract checks intentionally protect behavior categories rather than exact tuning values where practical. For example, they require positive trail/reset/headlamp settings and the local-only/vent-gated architecture without freezing the current 8 m beam range forever.

## Findings produced by the first runs

The initial workflow exposed an existing syntax error in `Tools/Level2Blockout/draw_plan.py`: several intended multiline labels were stored as literal newlines inside single-quoted strings. The script was corrected to use escaped `\n` labels.

The first pull-request run then exposed trailing whitespace in the three newly added Unity script metadata files for `RigFloorPenetrationGuard`, `VentHeadlampController`, and `CrawlerBodyPathFollower`. Those metadata files were normalized rather than weakening the whitespace rule.

After the repository-wide metadata check was added, CI found five tracked `*.png~` editor/backup copies under `Assets/Models/Materials/Faces` plus an orphaned `Assets/StreamingAssets.meta` whose target folder no longer existed. The backup files duplicated real face textures, the orphaned folder GUID had no repository references, and all six stale entries were removed. The existing `.gitignore` already contains `*~`; these files remained only because they had been committed before the ignore rule could help.

After those corrections, both **push** and **pull_request** Source integrity runs passed on the same PR head. The core source validator reports **116 first-party scripts and 5 enabled scenes** passing its component, metadata, build-registration, local-reference, and C# syntax checks; the additional repository and Level 1 contract checks also pass.

## What a green check does not prove

A green Source integrity check does **not** mean Unity successfully imports or compiles the project. It also does not run the two Unity Editor validators, Play Mode, Photon PUN sessions, XR interaction, actual Crawler animation deformation/corner appearance, actual headlamp illumination, scene travel, Quest builds, headset comfort, or target-hardware performance.

Before merging gameplay changes, continue to run Unity 2022.3.55f1 and the project-specific Editor/runtime/headset checks recorded in `README.md` and `docs/repository-improvement-plan.md`.

## Possible second CI tier

A future Unity-aware CI tier could open the project in Unity 2022.3.55f1 and run EditMode/PlayMode tests or a compile/import smoke test. That is deliberately **not implemented yet**: hosted Unity CI adds substantially more setup, runtime, and licensing/activation concerns, and it still cannot replace Photon multi-client or Quest-headset validation. Add that tier only when its maintenance cost is justified.

Once `Source integrity` is merged to `main` and has proven stable, it is a good candidate to make a required pull-request status check in repository branch/ruleset settings.
