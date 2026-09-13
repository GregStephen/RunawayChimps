# Runaway Chimps multiplayer development testing

Last updated: 2026-09-13.

This document is the maintained reference for testing multiple Runaway Chimps clients during development. Runaway Chimps uses **Unity 2022.3.55f1** and **Photon PUN**.

## Status

**Confirmed development workflow:** use one Unity Editor client and one Windows Development Build client on the same PC when a second headset or second development machine is not available. Both clients must join the same explicit private Photon room code so the test does not depend on public matchmaking.

**Current capability:** the existing Hub computer already supports entering a room code, and `RoomSwitchService.JoinPrivateRoom(...)` normalizes the code and joins/creates that private room. Source validation and the normal game startup remain unchanged.

**Planned tooling, not implemented yet:** a development-only local multiplayer harness should automate Client B launch, give each local instance a distinct development identity, auto-join a named test room, provide desktop keyboard/mouse movement for the non-headset client, and display a compact multiplayer/debug HUD. Until that tooling is implemented, do not mark keyboard-controlled local Client B testing as available.

**Final validation boundary:** local Editor + Development Build testing is a fast development workflow. It does not replace final two-headset / target-Quest multiplayer validation for XR tracking, comfort, input, audio, performance, or platform behavior.

## Standard local topology

Use this arrangement for ordinary two-client development checks:

| Client | Runtime | Preferred control | Purpose |
| --- | --- | --- | --- |
| Client A | Unity Editor, Unity 2022.3.55f1 | Headset when the feature needs XR; editor controls otherwise | Primary developer/client-under-test |
| Client B | Windows Development Build from the same branch/commit | Planned desktop test driver; headset only if separately available | Second Photon actor / remote-player observer |

Do **not** use two Unity Editor processes against the same project checkout/Library as the standard workflow. Use one Editor and a separate built player.

For repeatable tests, both clients should use an obvious private room code such as `RCDEV01`. The current terminal room-code field uppercases and sanitizes the code, and `RoomSwitchService` routes it through the private-room join path. Verify both clients display the same room name and a player count of `2` before judging multiplayer behavior.

## Manual same-room procedure

This is the reference procedure once both clients can be operated far enough to reach the Hub computer:

1. Check out/build the same commit for both clients. Do not compare an Editor on one branch with a Development Build from another branch.
2. Start Client A in the Unity Editor and Client B from the Windows Development Build.
3. Let both clients complete normal startup/authentication and reach the Hub.
4. On Client A, enter a private room code on the Hub computer and join it.
5. On Client B, enter the **same** private room code and join it.
6. Confirm both clients report the same Photon room and the room count is `2`.
7. Confirm each client has a distinct Photon ActorNumber before beginning player-specific tests.
8. Move/travel only the client required for the scenario. Keep the second client visible or instrumented so cross-player leakage is obvious.
9. Record the exact branch/commit and which client was monster controller when reporting a result.

If Client B cannot be operated without a headset, that is a limitation of the current tooling—not a reason to weaken a multiplayer acceptance check. Use the planned desktop test harness described below rather than pretending a single-client test proves player-specific behavior.

## Development identity rule

Development clients must represent **different players**, not two processes logged into the same development identity.

The current non-Quest authentication path uses `DevCustomIdAuthProvider`, which stores `PF_DEV_CUSTOM_ID` in `PlayerPrefs` and creates a PlayFab custom ID from it. The future local multiplayer harness must give each launched test instance its own deterministic development identity, for example `LOCAL_A` and `LOCAL_B`, instead of relying on whatever PlayerPrefs storage two processes happen to share.

The harness should also avoid treating local test launches as economy/reward test cases. Any option that suppresses login rewards or uses dedicated throwaway development accounts must remain development-only and must not change release authentication behavior.

## Planned local multiplayer harness

**Status: Planned, not implemented.**

The target workflow is one command/menu action that makes a usable second player available without a second headset.

Recommended Editor menu:

`Tools > Runaway Chimps > Local Multiplayer > Build & Launch Client B`

Recommended behavior:

- Build a Windows **Development Build** from the current checked-out commit.
- Launch the built client in a small resizable window.
- Assign explicit roles such as `TEST_A` and `TEST_B`.
- Give each instance a distinct development identity.
- Auto-join both instances to the same explicit private room code when a test-session flag is present.
- Never auto-join a production/public room from local-test flags.
- Add simple desktop movement/look controls for the non-headset client.
- Add development-only shortcuts for common test setup, such as entering Level 1, setting `Level1_Vents`, and moving to authored test markers.
- Keep all shortcuts behind `UNITY_EDITOR` and/or `DEVELOPMENT_BUILD` guards so release builds cannot invoke them.
- Do not fake Photon ActorNumbers, monster authority, target selection, zone state, or travel state. The harness may make setup easier, but the systems under test must still run through their real Photon/gameplay paths.

### Planned Client B controls

Exact bindings can change when implemented, but the desktop client should support at least:

- WASD: locomotion/test movement.
- Mouse: view direction.
- Shift: faster test movement.
- A deliberate key for respawn/reset, rather than automatic recovery that could hide gameplay bugs.
- Development-only shortcuts to travel to Hub / Level 1 / Level 2 through safe setup paths.
- Optional teleport-to-marker shortcuts for repeatable pursuit-distance tests.

The desktop driver is a test convenience. It must not become a second production locomotion system.

## Planned multiplayer debug HUD

**Status: Planned, not implemented.**

Each development client should be able to show a small local HUD containing at least:

- build/commit identifier;
- local Photon ActorNumber and nickname;
- room name and player count;
- current `SectorId`;
- current local `ZoneId`;
- elected monster-controller ActorNumber for the active level;
- monster `TargetActorNumber`;
- whether the local player is the current pursued target;
- local cached monster distance and sample age;
- `ThreatFeedbackController.DesiredThreat`;
- `ThreatFeedbackController.CurrentThreat`;
- whether the local threat vignette is currently visible.

The HUD is diagnostic presentation only and must not write monster/threat state.

## Threat-feedback two-client matrix

Use this matrix for PR #19 and future player-specific threat consumers.

| Scenario | Client A expected | Client B expected |
| --- | --- | --- |
| Crawler pursues A while B is nearby | Red peripheral threat ramps with A's distance | No threat vignette |
| Crawler retargets A -> B while shared chase remains active | A releases smoothly to zero | B acquires/rises smoothly |
| A reaches either Level 1 safe room | A clears immediately at the eligibility level, then presentation fades out | B remains independent |
| B reaches a safe room while A remains target | A remains threatened | B remains clear |
| Current monster controller leaves Level 1 | No stale target after handoff | New controller eventually publishes a fresh target; no stuck red |
| Late-arriving B joins the occupied level | A state remains correct | B requests current monster state but only sees threat if B is target |
| Target disconnects/reconnects | No old target identity leaks to another actor | Reconnected client does not inherit stale threat without fresh pursuit state |
| Two monsters threaten A | One bounded strongest threat value | Independent from A unless B is targeted |
| Monster 1 targets A and Monster 2 targets B | A sees its own strongest eligible source | B sees its own strongest eligible source |

For every scenario, note which actor is the elected sector monster controller. Repeat controller-handoff scenarios with A and B swapping the lower ActorNumber/controller role when practical.

## General multiplayer regression checklist

The same local two-client setup should eventually be reused for:

- same-room sector separation (Hub vs Level 1 vs Level 2);
- remote avatar visibility and collision filtering;
- voice-sector behavior;
- personal keycards and independent completion;
- Crawler controller election and handoff;
- capture/drop behavior while the other player remains unaffected;
- Level 2 personal fuse progress and cooperative Listener distraction;
- reconnect/pause/resume around scene travel;
- equipment/cosmetic synchronization;
- future reward-room personal ownership.

## Evidence to record

When a multiplayer test passes or fails, record:

- date;
- branch and commit SHA;
- Unity version/build type;
- Client A runtime (Editor/headset or desktop);
- Client B runtime;
- private room code;
- both ActorNumbers;
- elected monster controller;
- scenario performed;
- observed result;
- relevant Console/log excerpt or screenshot/video when a failure is visual/timing dependent.

A source/CI pass is **Source validated**. A one-PC Editor + Development Build pass is **local two-client validated** for the specific behavior tested. Only actual headset/Quest testing should be labeled **headset/Quest validated**.
