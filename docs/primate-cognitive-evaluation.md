# Primate Cognitive Evaluation

Date: 2026-09-30. Implementation branch: `feature/primate-cognitive-test`, PR #77, based on main `930a8f830cae99e06dcaa0a78ab9edc968c42ae5`. Unity **2022.3.62f3 (96770f904ca7)**, Photon PUN and built-in rendering are unchanged.

## Status and correction

**Confirmed:** an optional, self-contained four-pad extending-sequence memory test. One initial step; retain the sequence and append one step after each successful round. Failure produces a dry assessment; a separate physical START / RESTART control starts again. The prototype has no rewards, voice acting, persistent scores or progression.

**Implemented, not merged:** game logic, physical-contact filtering, sector networking, Editor controls, placement command, and the actual serialized machine prefab with assigned display, materials and audio. Asset commit `4f213b9` supersedes the earlier source-only delivery. The earlier rejected write was not evidence of an inability to create these assets.

**Pending acceptance:** Unity import/compilation, rendering and audio output, physical trigger behavior, Editor placement/Undo, live Photon and headset tests. Source/managed/asset checks do not establish these results.

## Use the delivered prefab

Prefab: `Assets/RunawayChimps/Toys/PrimateCognitive/Prefabs/PrimateCognitiveEvaluation.prefab`.

Open the branch in Unity 2022.3.62f3. Outside Play Mode, choose **Tools > Runaway Chimps > Toys > Validate Cognitive Evaluation Prefab**, then **Tools > Runaway Chimps > Toys > Place Cognitive Evaluation in Hub**. The second command opens Hub additively when necessary, places one prefab instance, selects it and marks Hub dirty. It samples the Hub floor at X=2.4, Z=-1.2; inspect nearby geometry and move the instance as needed. It selects an existing machine, including inactive placements, rather than moving or replacing it. Creation and transform edits support Undo; the command does not force a save. Save the Hub manually only after accepting the placement.

The root is movable, unit-scale and floor-pivoted; its front faces **local -Z**. Keep it beneath the sector-owned `SpawnRoom` root when available so existing travel hides/unloads it normally. No runtime installer or automatic scene editing exists. The shipped prefab needs **no generation command, manual component assembly, audio assignment or other toy branch**.

For normal VR testing start through **Bootstrap** after saving the chosen Hub placement. Use the existing local rig/camera/listener. Both clients must use the same saved placement and machine ID. A deliberate second machine needs a unique `machineId` shared by all client builds; the Inspector includes an Undo-aware identity button. Duplicate IDs fail closed. The prototype placement command intentionally creates only one test instance.

## Test without a headset

Open Hub directly, enter Play Mode, select the machine and enable **Editor Controls** on its `CognitiveMachine` component. The custom Inspector then exposes **HOLD START / RESTART**, **HOLD PAD 1-4**, and **Release all virtual contacts**. Turn a toggle on to touch and off to release. These enter the actual contact gate, owner checks, command path and game model; they are not a second memory-game implementation. Leave all contacts released for at least 0.08 seconds before pressing again. Selection loss and disabling Editor Controls release virtual contacts.

Keep one pad held throughout a demonstration to test that it does not become player input at the phase transition. Release it and touch again to submit. The Inspector shows the live phase, round and operator. When entering via Bootstrap instead, wait for normal Hub readiness; the fixture does not bypass loading or sector gates. The Editor-only distance bypass is explicit and does not create keyboard/Inspector inputs in a player build. Leave Editor Controls off for headset reach tests.

## Authored assets and reach

The prefab has 214 serialized objects/components, eight feature-owned built-in Standard materials and six original short mono PCM WAV clips. It references the repository's existing LiberationSans SDF font and material; no font file is copied. The 3D assessment display, title, serial plate and permanent high-contrast number labels are authored objects. Four pad notes are 330, 440, 554 and 659 Hz, with a reserved success clip at index 4 and descending failure buzz at index 5. The final correct pad note remains audible on round success; the reserved success clip is not layered over it.

| Control | Authored prototype value / adjustment |
| --- | --- |
| Main caps | 0.18 x 0.16 m, arranged 2 x 2; permanent labels 1-4 |
| Main pad spacing | 0.34 m between columns; 0.26 m along the tilted face between rows |
| START / RESTART | Separate 0.24 x 0.10 m labeled cap below the four pads |
| Control deck | `Controls_AdjustHeightHere`: local Y=0.98 m, X tilt=30 degrees; move this group vertically for reach tuning |
| Approximate cap-center heights | Main rows 0.91 m and 1.13 m; start 0.75 m above the floor pivot |
| Cabinet | 0.86 m maximum width, approximately 1.79 m tall; keep root scale at 1 |
| Assessment display | 0.74 x 0.34 m text area, center height 1.47 m; assigned font size 30 at 0.01 text scale |
| Cap travel / release | `pressDepth` 0.012 m; `releaseSeconds` 0.08 s after all contacts leave |
| Operator distance | `operatorRadius` 1.8 m from `OperatorPresenceAnchor`; move this anchor with any substantial deck relocation |
| Speaker | Volume 0.65; fully spatial, linear attenuation 1-7 m, no looping, play-on-awake or Doppler |

These are implemented starting dimensions, **not accepted seated/standing reach measurements**. Inspect against the actual Gorilla rig and adjust the deck and anchor, rather than shrinking the whole machine. Five trigger controls have their own kinematic Rigidbodies. The cabinet/deck are static solids; moving caps are not locomotion supports. Illumination and mechanical cap depression are separate, so illuminated idle START is not mechanically held down.

## Contact and multiplayer boundaries

Contacts require `HandTag`, the actual local Gorilla rig marker, a descendant of either tracked hand transform (not the controller root), and no remote-owned PhotonView. This accepts the authored fingertips but rejects the 8 cm controller-root grab spheres that also carry `HandTag`. At the inspected baseline the authored fingertips use numeric layer 29 while the `FingerTip` name is on layer 28; the feature follows the real hierarchy rather than changing shared layer or rig settings. The existing collision matrix permits Default-layer machine triggers against the actual fingertip layer. Each pad aggregates all contact colliders and requires all contacts clear before rearming. Trigger-stay and phase changes never manufacture a fresh press.

The lowest eligible same-sector actor is the **controller**, not necessarily the **operator**. A valid start assigns one explicit operator; only that actor can submit/restart during its session. Spectators see the same sequence, round and assessments and receive the same short cues. Events 188/189 use protocol tag `rc.cognitive.1`, machine ID and sector, reliable targeted delivery, controller epochs, nonce handshakes, session/phase tokens and command/cue revisions. No scene PhotonView, new matchmaking or shared minigame framework is introduced. This is stale-message/accidental-input protection, not server-authoritative anti-cheat.

| Pacing / recovery field | Default |
| --- | --- |
| `maximumLength` | 8; clamped to 1-16, then Complete and easy restart |
| `demonstrationLead` | 0.7 s before the sequence |
| `stepSeconds` / `flashSeconds` | 0.65 s / 0.32 s; notes remain separated |
| `successSeconds` | 1.1 s before appending and demonstrating |
| `heartbeatTimeout` | 4 s; operator sends a heartbeat every 0.75 s |
| `inputIdleTimeout` | 20 s of no input during the input phase, even with valid heartbeats |
| `resultHoldSeconds` | 10 s before a failure/completion result returns to idle |

Pacing settings are read when a new authority term/model is created; restart Play Mode after tuning them. The controller checks current-sector presence and deadlines. Walking away requests release; travel, disconnect, pause, component disable and missing heartbeats release/reset through the corresponding lifecycle or timeout path. Authority changes create a fresh idle model instead of trying to preserve a half-demonstrated round. Command numbering survives scene/component replacement; idle recovery clears stale command floors, and heartbeat/release commands also require the current session. Old sessions cannot release a newly restarted game. A fresh sync handshake recovers late arrivals or a restarted controller without replaying old audio.

## Executed checks and reproducibility

Asset publication run [36777382426](https://github.com/GregStephen/RunawayChimps/actions/runs/36777382426) generated and committed the actual assets, ran both managed harnesses and the repository source suite, and removed its one-time publishing helpers. The published 34 feature asset/metadata files were downloaded and compared byte-for-byte with the locally checked output. The retained workflow is read-only and checks both rules and assets. PR #77 records the final-head CI runs and results.

The production rules/contact/replica harness passes **57,013 assertions** across 128 seeds and all sixteen rounds, including sequence extension, correct/wrong input, held/duplicate contact, state transitions, restart, completion, timeouts and controller-term replay. The existing card harness passes **18,015 assertions**. Asset checks inspect all local references and assigned material/font/clip identities, control dimensions, six bounded PCM waveforms and four distinct pitches; they reject four deliberately broken prefab mutations. Actual Bootstrap contact checks reject four filter mutations and verify both hand-child contacts, excluded grab spheres and the collision matrix. These remain data/managed/source checks, not native Unity tests.

From the repository root:

```sh
python -m pip install PyYAML==6.0.2
python Tools/PrimateCognitive/build_assets.py
python Tools/PrimateCognitive/validate_assets.py
python Tools/PrimateCognitive/validate_rig_contract.py
dotnet run --project Tools/PrimateCognitiveHarness/PrimateCognitiveHarness.csproj --configuration Release
```

`build_assets.py` defaults to read-only comparison. `--write` explicitly regenerates the feature assets with deterministic GUIDs; it does not touch scene placements. Do not regenerate over intentional prefab edits without reconciling the generator. No external download or Unity installation is needed to reproduce the asset data, but Unity is required to establish import/runtime acceptance.

## Pending acceptance checklist

**Solo / Editor:** clean Unity import/compile; run Validate Cognitive Evaluation Prefab; place, Undo, Redo and place again without duplicating/moving an existing instance. In Play Mode exercise one-step start, successful prefix extension, wrong input, failure restart, held demonstration contact, duplicate colliders and maximum completion. Verify all labels fit and face forward, each note is distinct, and no unexpected Console errors occur.

**Two clients:** same-sector cue/round agreement; simultaneous start grants one operator; spectator presses never enter/restart the sequence. Test late arrival, operator walking away, input inactivity despite heartbeats, disconnect/reconnect, Hub-sector travel and return, scene/component re-enable, controller departure and A-B-A authority changes. After every release another player must be able to start; stale release/input/cue packets must not alter the new session. Different-sector clients must receive no presentation.

**Headset:** either hand and alternating hands, both main rows and start reachable while seated/standing, no required deep lean, readable display/number labels without relying on color, no grab-volume activation or resting-contact chatter. Check button separation and cabinet collision, comfortable sound level/distance, Quest performance and repeated travel/return. Editor Controls must be off for this check.
