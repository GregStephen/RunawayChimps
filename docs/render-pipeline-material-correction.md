# Render-pipeline and material correction

Last updated: 2026-09-13.

## Confirmed correction

Runaway Chimps uses Unity 2022.3.55f1 and currently renders with Unity's **Built-in Render Pipeline**, not URP. The Universal Render Pipeline package is present in `Packages/manifest.json`, but package presence does not make URP active.

Source evidence on `main`:

- `ProjectSettings/GraphicsSettings.asset` has `m_CustomRenderPipeline: {fileID: 0}`.
- Every quality tier in `ProjectSettings/QualitySettings.asset` has `customRenderPipeline: {fileID: 0}`.
- Existing project materials such as `Assets/RunawayChimps/Shared/Materials/Ceiling1.mat` and Level 2 `Grid.mat` use Unity's built-in Standard shader (`fileID: 46`).

This supersedes the September 12 material-planning assumption that new Runaway Chimps environment/computer materials should be authored as URP/Lit merely because URP 14 is installed.

## Material authoring rule

Until a deliberate project-wide render-pipeline migration is approved and implemented, new Runaway Chimps materials must default to the active **Built-in Standard** material path. Do not switch the project to URP simply to make an imported asset work.

For PBR materials, use the Standard shader's normal, metallic/smoothness and occlusion inputs. Preserve shared material assets and avoid per-renderer material instances unless gameplay requires them.

## Computer material package correction

The first generated computer-material package incorrectly referenced `Universal Render Pipeline/Lit`, causing the materials to render pink in the current project. A corrected package was regenerated against Built-in `Standard` while preserving the four material GUIDs so existing renderer references can survive an overwrite/import:

- `MAT_Computer_PaintedSecuritySteel`
- `MAT_Computer_BlackABS`
- `MAT_Computer_DarkOxidizedMetal`
- `MAT_Computer_ScreenGlass`

The corrected package also includes an Editor repair/validation command. This records the asset-package correction only; appearance on the actual computer mesh, UV scale, screen transparency, headset readability and Quest performance remain pending Unity validation.

## Follow-up

The Level 1 environment material plan should be interpreted using this correction: its visual language, scale, variation, anti-repetition and Quest-budget guidance remain active, but references to URP/Lit are superseded by Built-in Standard until the render pipeline itself changes.
