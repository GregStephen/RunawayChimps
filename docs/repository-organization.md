# Repository organization

This document records file-organization decisions for Runaway Chimps. Cleanup must preserve Unity 2022.3.55f1 asset identity and must not trade runtime reliability for a tidier Project window.

## Confirmed decisions

- Keep first-party game content under `Assets/RunawayChimps` when a move is demonstrably path-safe.
- Preserve every existing Unity `.meta` file when moving an asset or folder so serialized GUID references remain stable.
- Do not move `Resources`, `Scenes`, `Scripts`, `Editor`, `Plugins`, Photon, Oculus/Meta, PlayFab, TextMesh Pro, XRI/XR, ProBuilder, or other vendor/special-purpose roots merely for aesthetics. Move any of those only with a separate path-dependency audit and Unity validation.
- Do not rename assets/classes as part of organization-only work.
- Repository cleanup is not a gameplay or lore change.

## Implemented on `chore/file-organization`

The following project-owned, path-safe content roots were consolidated under `Assets/RunawayChimps/Shared` with their original folder `.meta` files and all child metadata preserved:

- `Assets/Animations` -> `Assets/RunawayChimps/Shared/Animations`
- `Assets/Materials` -> `Assets/RunawayChimps/Shared/Materials`
- `Assets/Models` -> `Assets/RunawayChimps/Shared/Models`
- `Assets/Music` -> `Assets/RunawayChimps/Shared/Music`
- `Assets/Textures` -> `Assets/RunawayChimps/Shared/Textures`

A new `Shared.meta` was added for the new parent folder. Code search found no hard-coded references to these five old roots before the move.

The tracked root `TempAssembly.dll` was removed after the existing codebase audit had already flagged it as a likely generated artifact and repository search found no runtime/code reference to it. `/TempAssembly.dll` is now ignored so it is not accidentally recommitted.

## Intentionally deferred

- `Assets/Scenes`: Build Settings and tooling can depend on serialized/path strings. Leave in place until a dedicated scene-path migration updates and validates every consumer.
- `Assets/Scripts`: editor checks, Python validation tools, and CI workflows currently contain explicit `Assets/Scripts/...` paths. Leave in place until those dependencies are migrated together.
- `Assets/Resources`: runtime `Resources.Load` calls depend on Resources-relative paths. Do not reorganize casually.
- Vendor/framework roots (`Photon`, `Oculus`, `PlayFabSDK`, `PlayFabEditorExtensions`, `TextMesh Pro`, `XRI`, `XR`, `Primer`, `MASH Virtual`, `Workplace Tools`, etc.): keep vendor layout intact unless an upgrade/removal task specifically requires changes.
- `UniversalRenderPipelineGlobalSettings.asset` and other project/render settings: leave at their known paths unless there is a functional reason to relocate them.

## Proposed future structure

This is a direction, not a completed migration:

```text
Assets/
  RunawayChimps/
    Level2Blockout/
    Shared/
      Animations/
      Materials/
      Models/
      Music/
      Textures/
  Resources/            # path-sensitive runtime content
  Scenes/               # deferred path-sensitive first-party content
  Scripts/              # deferred path-sensitive first-party content
  Photon/               # vendor/framework
  Oculus/               # vendor/framework
  PlayFabSDK/           # vendor/framework
  ...
```

If `Scenes` or `Scripts` are eventually moved under `Assets/RunawayChimps`, treat that as its own migration: update every hard-coded path, workflow and validator in the same branch and prove the Unity project still imports and plays correctly.

## Pending validation before merge

Source-level checks are necessary but not sufficient. Before merging this organization branch:

1. Run `python Tools/validate_repository_integrity.py`.
2. Run `python Tools/validate_source.py --syntax` and any Level 1/Level 2 contract validators used by current CI.
3. Open the project in Unity 2022.3.55f1 and allow a full import/compile; confirm there are no missing scripts, missing materials, broken animation/controller references, or pink materials caused by lost references.
4. Open the enabled scenes and inspect representative moved models/materials/textures/audio/animations.
5. Run the existing reliability/editor validation menu items.
6. Play from Bootstrap through Hub and Level 1/Level 2 entry paths.
7. Run the two-client Photon PUN smoke test and headset smoke test already required by the repository validation plan.

Until those Unity/runtime checks are recorded, this branch is **implemented but pending validation**, not merge-ready.
