# Launch presentation code-review resolution

Date: 2026-09-14. Branch: `feature/launch-presentation-polish`. Unity **2022.3.55f1**, Photon PUN.

## Implemented review resolutions

The September 14 review findings for PR #21 are addressed in source:

- The startup workstation is client-local, follows hidden `XROrigin` root relocation rather than remaining at a stale absolute world pose, does not follow normal head look, is fully covered by black, and is destroyed before Hub reveal. Interrupted entry can reconstruct the vignette.
- Legacy Loading status/error text stays available until the physical workstation terminal has actually been constructed, so startup failures retain readable retry feedback instead of becoming a black screen.
- Android build preprocessing validates the committed Meta OpenXR splash configuration instead of mutating and saving project settings during a build. `MetaXRFeature Android.systemSplashScreen` is committed to the Runaway Chimps system-splash asset.
- The workstation uses the referenced built-in Standard material at `Resources/LaunchPresentation/WorkstationBase` instead of runtime `Shader.Find` material discovery.
- Launch validation protects architectural bounds rather than exact sticky-note positions or one exact monitor scale.
- Cold-start Hub placement uses ten Photon room-owned slots. Clients claim a free slot with room-property compare-and-swap; the Master Client releases/reconciles stale claims; `RigSpawnSnapper` waits for Photon room membership and a slot before grounding; and the network avatar waits for `RigSnapped` before instantiation in the Hub. Route-specific return/level arrival markers are unchanged.
- The existing Level 1/runtime source contract now validates grounding against the **allocated `spawnPosition`** and requires `TryGetLocalSpawnPose(...)`, rather than incorrectly requiring the superseded single `HubSpawn` grounding call.

The ten slot poses are currently compact code-owned offsets around the existing authored `HubSpawn`. Their final spacing and floor/geometry clearance are **pending Unity/headset validation**, not visually approved level design.

## Pending validation

A fresh Source Integrity run must validate the current post-review branch head. Unity import/compile, Play Mode startup/retry/reveal, simultaneous two-client Photon slot claims, slot reuse and Master Client handoff, Hub spawn spacing, headset comfort, and Quest system-splash/material behavior remain pending until actually tested.
