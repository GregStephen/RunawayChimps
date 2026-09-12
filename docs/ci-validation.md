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
2. `python Tools/validate_source.py --syntax` passes. That validator checks the first-party C# trees under `Assets/Scripts` and `Assets/Resources/PhotonVR/Scripts` for component filename/class matching, `.meta` presence and duplicate GUIDs, and tree-sitter C# syntax. It also checks enabled Build Settings scenes for missing files, scene-GUID mismatches, duplicate local object IDs, and unresolved local references.
3. `python -m compileall -q Tools` passes so tracked Python tooling cannot contain basic syntax errors.
4. `git diff --check` passes for the entire pull-request diff or pushed commit range, catching whitespace errors introduced by the change.

## First validation results

The first workflow run exposed an existing syntax error in `Tools/Level2Blockout/draw_plan.py`: several intended multiline labels were stored as literal newlines inside single-quoted strings. The script was corrected to use escaped `\n` labels.

The pull-request run then exposed trailing whitespace in the three newly added Unity script metadata files for `RigFloorPenetrationGuard`, `VentHeadlampController`, and `CrawlerBodyPathFollower`. Those metadata files were normalized rather than weakening the whitespace rule.

After those corrections, both the **push** and **pull_request** Source integrity runs passed on `codex/fix-level1-crawler-lighting` on 2026-09-12. At that checkpoint the source validator reported **116 first-party scripts and 5 enabled scenes** passing its component, metadata, build-registration, local-reference, and C# syntax checks.

## What a green check does not prove

A green Source integrity check does **not** mean Unity successfully imports or compiles the project. It also does not run the two Unity Editor validators, Play Mode, Photon PUN sessions, XR interaction, lighting/animation appearance, scene travel, Quest builds, headset comfort, or target-hardware performance.

Before merging gameplay changes, continue to run Unity 2022.3.55f1 and the project-specific Editor/runtime/headset checks recorded in `README.md` and `docs/repository-improvement-plan.md`.

## Possible second CI tier

A future Unity-aware CI tier could open the project in Unity 2022.3.55f1 and run EditMode/PlayMode tests or a compile/import smoke test. That is deliberately **not implemented yet**: hosted Unity CI adds substantially more setup, runtime, and licensing/activation concerns, and it still cannot replace Photon multi-client or Quest-headset validation. Add that tier only when its maintenance cost is justified.

Once `Source integrity` is merged to `main` and has proven stable, it is a good candidate to make a required pull-request status check in repository branch/ruleset settings.
