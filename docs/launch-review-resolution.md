# Launch presentation code-review resolution

Date: 2026-09-14. Branch: `feature/launch-presentation-polish`. Unity **2022.3.55f1**, Photon PUN.

## Current confirmed presentation

**Superseding correction:** Greg rejected the physical workstation/desk vignette and asked for the **old flat green security terminal back**, positioned roughly twice as far away. The active startup target is therefore the green facility terminal alone in black space at approximately **3.9 m** from the initial horizontal player view. Earlier workstation-specific review notes are retained only as historical context in git history and must not be used to restore the desk/prop presentation.

The tracked camera is never moved. The terminal anchor follows hidden `XROrigin` root relocation during startup so Hub snapping cannot leave it behind, while ordinary head look and room-scale motion do not head-lock it.

## Implemented review resolutions that remain active

- Legacy Loading status/error text remains available until the custom terminal is successfully constructed, so startup failures retain readable retry feedback rather than becoming a black screen.
- The final reveal uses an explicit retirement state so the startup terminal cannot reconstruct itself after it has been intentionally covered/removed under black; an interrupted entry/retry may clear that state and rebuild it.
- The dedicated `LoadingPresentation` render layer keeps additively loaded Hub world/UI content hidden until the controlled reveal.
- Android build preprocessing validates committed Meta OpenXR splash configuration instead of mutating/saving project settings during a build. The Meta Android system splash and black loading background remain configured.
- Cold-start Hub placement uses ten Photon room-owned slots. Clients claim slots through room-property compare-and-swap; the Master Client releases/reconciles stale claims; slot acquisition is demand-driven by Hub placement; and the network avatar waits for `RigSnapped` before Hub instantiation.
- A new Photon-room session invalidates stale Hub placement when appropriate and requires a fresh slot snap before Hub avatar spawn. Existing room-owned slot recovery and retry behavior are preserved.
- The ten Hub slot poses are authored in `Assets/Resources/HubSpawn/HubSpawnSlots.prefab` rather than hard-coded networking coordinates. Route-specific return/level-arrival markers remain independent.
- `PlayerVisualReadyReporter` reuses a material list through `Renderer.GetSharedMaterials` instead of allocating copied material arrays during startup settling.
- Existing Level 1/runtime source contracts validate grounding against the allocated Hub `spawnPosition` rather than the superseded single `HubSpawn` position.

## Presentation-specific implementation correction

`SecurityWorkstationVignette` remains the legacy class/file name, but it no longer builds workstation geometry. It creates only the world-space terminal canvas. The previous desk, monitor shell/stand, keyboard, mug, badge, clipboard, sticky-note copy, runtime primitive construction and `Resources/LaunchPresentation/WorkstationBase` material dependency are removed from the active presentation.

The terminal canvas retains its prior physical scale and uses `TerminalDistance = 3.90f`, exactly twice the former 1.95 m workstation distance. Source validators now require the distant panel and reject reintroduction of workstation props/materials.

## Pending validation

- Unity **2022.3.55f1** import/compile.
- Play Mode: 3.9 m distance, eye-height placement, readability and CRT comfort.
- Head-turn/room-scale behavior and hidden `XROrigin` relocation.
- Startup failure/retry restoration.
- `ACCESS GRANTED` -> full black -> terminal absent -> clean Hub reveal with no reconstruction or UI/world leak.
- Two-client simultaneous slot claims and avatar placement.
- Hub room switch/rejoin/reconnect placement.
- Slot reuse after leave/disconnect and Master Client handoff/reconciliation.
- Authored Hub marker floor/wall/prop clearance and comfortable multiplayer separation.
- Successful Hub/Level 1/Level 2 travel remains black-only.
- Quest Android build/install, compositor splash -> green terminal handoff, stereo/peripheral coverage, recenter/pause-resume, comfort and performance.

Source Integrity is meaningful source/tooling evidence only. It does not prove Unity runtime, Photon sessions, XR comfort or Quest device behavior.

**Validated source only — restored distant green terminal:** clean head `cdf6b87a55d429351acf0d6b96a632fda4847893` passed Source Integrity run `34917902460` on 2026-09-14. The run passed Unity 2022.3.55f1 version enforcement, first-party C# syntax/reference and enabled-scene checks, Unity metadata/GUID integrity, Level 1 and PR #15 contracts, the restored distant security-boot contracts, local threat-feedback contracts, launch/Quest/Hub-slot/session contracts, Python compilation, merge-marker rejection and human-authored whitespace. This validates source/tooling only; Unity import/compile, Play Mode, Photon multi-client behavior and Quest/headset behavior remain pending.

## September 21 visual regression correction

**Runtime finding:** the restored flat terminal's first 3.90 m world-space placement was too far away and the canvas appeared reversed/mirrored.

**Implemented:** reduce the panel anchor to **2.50 m** and remove the 180-degree local Y rotation from the world-space canvas. Validators now require the front-facing identity rotation and reject the superseded mirrored transform. The 2.50 m distance is pending Greg's visual approval; the multiplayer/session/build hardening remains unchanged.
