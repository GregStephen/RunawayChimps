# Primate Strength Test

Status: code/prefab implemented on `feature/hub-strength-tester`; scene integration and Unity/headset validation pending.

## Required one-time scene integration

Greg approved delivering the scene change as a small patch because the full Hub scene exceeds the connected uploader's practical transfer capacity. The published branch intentionally retains the original Hub scene. With this branch checked out, Unity closed, and no uncommitted changes to `Hub_Base.unity`, run from the repository root:

```sh
git apply Tools/StrengthTesterHarness/HubScene.patch
```

This adds the single authored prefab instance and its parent reference. It does not regenerate the Hub. If Git reports a conflict or that the patch was already applied, stop rather than force it. Review the resulting scene diff, test in Unity, then commit and push `Assets/Scenes/Hub_Base.unity` on this same branch. The draft PR must not merge until that scene commit and the runtime acceptance checks are complete.

The strength tester is an optional physical toy in the Hub. Swing either hand into the pad, withdraw, and try to beat your own visit best. The shared last-hit readout lets nearby friends watch each attempt. The score is an arcade value based on hand speed, not a measurement of real force.

## Find and edit it

- Scene: `Assets/Scenes/Hub_Base.unity`, under `SpawnRoom/PrimateStrengthTester`.
- Prefab: `Assets/RunawayChimps/Environment/StrengthTester/PrimateStrengthTester.prefab`.
- Initial placement: `(1.65, 0.005, -3.25)`, to the right of the terminal when facing it, front facing +Z.
- Move/rotate the whole scene instance normally. Keep the root and `StrikeFace` at unit scale; tune `padHalfSize` if changing the pad's dimensions. No authoring menu or runtime generator needs to run.
- `StrikeFace` is the fixed scoring plane at the rubber face. `PadVisual` only moves for feedback; its animation cannot create another hit.
- The two short mono WAVs are original synthesized assets. Replace `impactClip` / `peakClip` in the inspector to change the sound without changing code.

## Default behavior

| Setting | Default |
| --- | --- |
| Scoring range | 1–999 |
| Minimum inward hand speed | 0.4 m/s |
| Speed for maximum score | 5.5 m/s |
| Minimum time between local hits | 0.8 seconds, shared by both hands |
| Withdrawal | 10 cm beyond the contact radius, continuously for 0.10 seconds |
| Hand contact radius | 6 cm |
| Sound range | Spatial, 0.8 m minimum / 8 m maximum |
| Best-score lifetime | Current station/Hub visit and Photon room only |

Only the local tracked hands can generate a hit. Generic collider contacts, remote avatars and thrown props cannot score. Walking into the pad with a stationary hand does not add swing speed. Back/side misses, resting contact, brief jitter, large pose discontinuities and long frame gaps are rejected. A new strike must cross the front face; pushing harder after a slow touch does not score until withdrawing again.

Assessments are short on-screen text; they are not recorded voice lines. Hits of 900 or more also play a short assessment tone. There is no camera shake, flashing light, real-time light, reward, saved leaderboard or currency hook.

Photon event **182** is reserved for this station's cosmetic last-hit presentation. The payload is `{ protocolVersion: 1, stationId, score, serverTimestamp }` encoded as an object array. Only current Hub players are targeted and accepted; there is no event cache. Stable timestamp/actor ordering resolves simultaneous reports. Each client retains its own best. Another player cannot change that local best, but shared scores are still client-reported and are not intended for competitive prizes.

## Verification

Run `dotnet run --project Tools/StrengthTesterHarness/StrengthTesterHarness.csproj --configuration Release` for production contact/scoring tests. The harness has no Unity doubles: it runs the actual engine-independent contact class. It does not execute the Unity adapter, XR device APIs, sound, geometry or Photon delivery.

Executed source/managed checks on September 30: 463 contact/scoring assertions; C# syntax; enabled-scene references; metadata/GUID pairing; existing project source contract checks; new prefab YAML/local/external references. Unity is unavailable in the implementation environment.

Pending checks in the repository's current **Unity 2022.3.62f3** editor:

1. Import with a clean console. Open `Hub_Base`, select the tester and inspect the cabinet, labels, pad and meter. Confirm floor contact, wall clearance, terminal access, ten spawn positions and nearby routes. Launch through Bootstrap and confirm the prop survives normal startup.
2. Try seated/standing and both hands. A deliberate front slap scores once, withdraw/re-slap scores again, a resting hand/jitter does not repeat, and a side/back miss or body-only approach does not score. Check the 5.5 m/s maximum feels achievable without uncomfortable swings; tune it if needed.
3. Confirm the pad depresses and returns, the meter rises, text fits the display, impact audio plays once, only the striking controller pulses and the high-score tone is comfortable.
4. On two clients, watch each other's hits in the Hub. Try near-simultaneous swings; displays should converge on the latest accepted timestamp/actor. Verify independent personal bests and no remote haptics. Put the other client in Level 1: it must receive no Hub impact presentation.
5. Pause/resume, lose/reacquire controller tracking, recenter, travel away/back, change rooms and join late. No phantom strike or replay; new visits/rooms begin without the earlier personal best. Test leaving during the pending peak tone.
6. Check Quest 2 and Quest 3 text/audio/interaction and performance. No headset or multiplayer outcome has yet been recorded.
