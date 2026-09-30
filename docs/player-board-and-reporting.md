# Player board, mute and reporting

Last updated: 2026-09-30. Implementation branch: `feature/player-board-safety`, based on `main` `930a8f8`. Related release-safety work: [#43](https://github.com/GregStephen/RunawayChimps/issues/43).

## Decision and concept

**Confirmed by Greg:** add a Gorilla Tag-style room roster with individual mute/unmute and report controls. A rough visual concept is sufficient for the first implementation; final art/layout is not decided. This is a player list, not a score ranking.

**Prototype implementation:** a restrained dark facility screen labelled PLAYERS, with five large rows per page for the existing ten-player room. Each row shows a bounded plain-text nickname, room actor number, sector/reconnecting state and MUTE/UNMUTE plus REPORT. The local row says YOU and disables its actions. Actor numbers distinguish identical nicknames; account identifiers never appear on the board. Final art, placement and input ergonomics remain open to Greg's review.

`Assets/Resources/SocialSafety/PlayerBoard.prefab` contains real editable geometry, TMP labels, row bindings and trigger buttons. `PlayerBoardHub.prefab` contains one nested board with an authored offset of `(-1.68, 1.45, -3.10)`. The service installs this placement prefab under the Hub's existing `SpawnRoom` root when that scene loads (or when the service starts with the Hub already loaded). The board inherits the room's travel visibility and is destroyed with that scene. Repeated scene notifications do not create duplicates. The loader creates no geometry or labels in code.

For layout tuning, open `PlayerBoardHub.prefab` and move its nested board. To tune directly in the Hub before Play, drag the placement prefab under `SpawnRoom`, reset the wrapper's local transform, and move the nested board as desired. Save those scene overrides or apply them to the placement prefab. The loader respects a manually placed non-portable board and does not add another. No manual placement is required to run the feature. Missing anchor/assets emit one error per attempted installation while the portable board remains available. `Tools/PlayerBoard/build_player_board.py` explicitly regenerates the main board but preserves an existing placement prefab. Do not run it over intentional board customization without reviewing changes.

**September 30 publication correction:** Greg approved publishing an active, ready-for-review PR and reworking the implementation as needed. The original direct Hub scene instance was replaced with this authored placement prefab and scene lifecycle loader because the authenticated GitHub upload route could not transfer the full 30 MB scene. `Hub_Base.unity` is unchanged in the final branch; the in-game Hub board, portable board, muting and reporting are retained. This supersedes the earlier local-only publication hold. Merge and runtime acceptance remain separate.

Hold the left controller's **Y** button for 0.6 seconds to open/close a portable copy in front of you. **F4** does the same in the Editor/development builds. It is a stationary world-space panel, not a head-following overlay, and closes during travel/disconnection. Its colliders are triggers and cannot support/block locomotion. Both boards use the same local service; the copy is marked persistent across scene loads. The portable copy needs in-headset clearance/readability testing in tight vents; relocation is by closing and opening it while facing the desired direction.

## Implemented behavior

- The roster uses current Photon room players, not scene avatar discovery, and refreshes five times per second. Nickname, sector and join/leave/reconnect changes appear without rebuilding the board. Room/actor identity binds actions; nicknames never select the target.
- Local-hand/fingertip-only trigger buttons reject remote hands, local bodies and props. Contact must leave before another press; a short activation delay protects newly opened form controls. The final SEND REPORT button is spatially separate from the row REPORT buttons.
- Muting changes only the chosen remote actor's Photon Voice speaker on this client. `SectorAvatarVisibility` is the single speaker-mute owner, combining the existing source mute, sector filtering and user mute. Travel, cosmetics/roster refresh and a replacement speaker cannot erase the user mute; unmuting cannot bypass sector filtering. No RPC globally silences anyone.
- Mutes are **room-session local** in this first implementation: retained across sector travel, cleared when room membership ends or the account changes. They are not a saved account block list and do not prevent future matchmaking with the player. Persistent mute/block/avoid semantics remain follow-up work under #43.
- Reporting opens a separate form with no default reason: harassment/bullying, hate speech, inappropriate name, cheating/exploiting or other misconduct. Selecting a reason does not send. SEND REPORT is explicit; BACK sends nothing. A captured target can still be reported if they leave that room, but changing rooms invalidates the form, pending request and late callbacks. A changed advertised account ID requires reselection.
- Calls use the existing signed-in **PlayFab Client `ReportPlayer`** API. There is no embedded admin secret, webhook, email, voice recording, automatic ban or public accusation. The client authenticates the reporter via the existing PlayFab session.
- Single-flight submission, a three-second retry cooldown, duplicate suppression after confirmed submission in the room, a 20-second UI deadline, generic error/retry feedback and request-generation guards protect the flow. Timeout means receipt is unknown, not that the server rejected it. An explicit retry retains the form's request ID so duplicate reports can be recognized during review; the native API does not promise idempotent delivery.
- Report progress, failures and timeouts stay with the submitting form. Other open forms show a wait state during the request, and confirmed receipts are shared only for the reported account in the same room. The roster retains mute/room feedback; opening a different player's form cannot inherit a prior report's success. Confirmed forms show `SUBMITTED` and disable their reason/send controls.
- The UI timeout does not cancel the SDK request. A later positive receipt resolves the unknown status and suppresses duplicate submission if no new request or room/account change has superseded it. Older callbacks cannot finish a newer request or cross a room/account boundary.
- The pinned SDK exposes `SubmissionsRemaining` but no `Updated` acceptance flag. Official documentation describes a five-per-day limit. A positive remaining count shows “Report submitted”; zero/null shows an unconfirmed limit status, **never a claimed success**. The fifth report may have arrived; check it in PlayFab. This conservative edge case needs live-service acceptance before public release.

## Report destination and review

The native API feeds PlayFab's **Daily Abuse Reports History** and its `player_reported_as_abusive` PlayStream event. In the title's Game Manager, inspect the abuse report for the chosen day, then look up the reported player account to investigate. The event provides the reporter account and comment; the target is the player entity. A new dashboard/backend is not required for this prototype, but visibility/permissions and successful delivery must be verified in Greg's actual title.

**Owner:** Greg is the sole developer and intended reviewer; a response-time commitment is not set. During private acceptance, inspect each test submission, match reporter and reportee, distinguish the test accounts, and do not issue sanctions. Before public sessions, establish a manageable review cadence, access permissions and support/appeal procedure under #43. This PR does not close that issue.

**Data sent:** reporter account through PlayFab authentication; reported PlayFab account; reason; sanitized display-name snapshot (useful for inappropriate-name reports); room actor number; sector; client UTC time; build version; a random request ID; and an explicit `client-claimed` identity marker. Context is structured JSON to avoid nickname-delimiter injection. No room code, IP collection, device identifiers, voice content or free-form player text is added. PlayFab still performs its ordinary service logging.

**Access/retention boundary:** visible to the title's authorized operators, not other players. Microsoft documents 30-day availability for Daily Abuse History reports; this does not establish retention for every underlying event/service log. No extra report files or exports are created by the client. Confirm the title's actual retention/access settings and privacy disclosure before public launch; do not describe an unconfigured deletion policy as implemented.

### Identity limitation and public-release gate

The existing project does not bind Photon actors to server-verified PlayFab identities: `PhotonVRManager.Connect()` resets custom authentication, and development CustomID login remains part of startup. This feature publishes the signed-in PlayFab ID in a Photon `rcReportId` property so ordinary clients can target native reports. **That property is client-claimed, may be spoofed/removed by a modified client, and exposes an account identifier to room peers even though the UI hides it.** It is not proof of the offender's identity. All reports are advisory and must not drive automatic punishment.

Before treating reporting as authoritative for public sessions, complete the existing trusted-account/authentication work and bind server-validated PlayFab identity to Photon custom authentication (or a server-verified actor mapping). Then obtain report targets from that verified binding, assess identifier disclosure, test spoof attempts, and confirm backend review/retention. No dashboard authentication settings, account migration or platform-proof validation were changed here. Missing/invalid IDs show an honest unavailable-reporting state while muting remains usable. These are explicit release limitations, not completed public-safety acceptance.

## Validation

**Validated offline, 2026-09-30, final prefab-loader revision:** C# syntax across 150 first-party/test scripts; five enabled scenes; unique GUID/asset pairing across 1,897 metadata files; all existing source-contract validators; serialized board dependencies, every local reference, five rows, twenty button bindings, trigger/hand collision compatibility and exactly one nested board in the Hub placement prefab. The managed harness compiles all new C# plus the actual voice/sector integration and passes **70 assertions**, including duplicate names, per-actor mute, sector changes, replacement speakers, pre-existing mute, report authentication/identity failures, duplicate submissions, timeout/retry/stale callbacks, target departure, pagination, explicit report confirmation and local-hand filtering. New loader diagnostics cover duplicate callbacks, hidden/already-loaded Hub roots, manual placement precedence, portable-only presence, missing anchor/assets, unloaded scenes and unsubscribe behavior. Unity, scene lifecycle, Photon, PlayFab and input collaborators are diagnostic doubles; native scene cleanup still needs Unity acceptance.

Run:

```sh
python Tools/validate_source.py --syntax
python Tools/validate_repository_integrity.py
python Tools/validate_player_board.py
dotnet run --project Tools/SocialSafetyHarness/SocialSafetyHarness.csproj --configuration Release
```

These checks are configured in Source validation CI; the published PR carries the remote result. The original 59-assertion run preceded the Hub loader revision. The final 70-assertion run used direct .NET 8 Roslyn compilation/runtime execution because the `dotnet` CLI could not inspect its own process in this environment. CI uses the normal SDK command. The authoring generator also passes Python 3.11 grammar parsing, matching CI's pinned Python version.

**Validated offline, September 30 first review fix:** the managed harness passed **85 assertions**. The added switching-player regression failed against the original code, then passed after report feedback was scoped to its form/account. Fifteen additional assertions cover concurrent Hub/portable forms, pending and confirmed labels, callbacks after leaving a form, mute feedback isolation, failures, portable reopening, timeouts/retries and stale-room callbacks. Source syntax, serialized board dependencies, and repository integrity checks also passed. These remain diagnostic-double results; the runtime checks below are still pending.

**Validated offline, September 30 second self-review:** **89 assertions pass**, including four additional checks for an SDK success arriving after the UI deadline without a retry, duplicate suppression after that confirmation, and protection against a subsequent duplicate callback. The late-confirmation regression failed before the fix. The existing timeout/retry and stale-room guards, source syntax, prefab wiring and repository integrity checks still pass.

**Pending Unity 2022.3.62f3 and live-service acceptance:**

1. Import/compile without warnings/errors; inspect the board's font, material, placement, reachable heights and absence of blockers in Hub. Confirm one Hub copy at startup and after repeated travel/rollback; no board reveals during black loading, leaks into another sector, or duplicates a manually authored copy. Exercise all ten rows across two pages, duplicate/long nicknames and inactive/rejoining players.
2. Start both players through Bootstrap on the same branch. Test both local hands; remote hands, body/held props must do nothing; one sustained contact must produce one action. Open/close the portable board with Y in Hub, Level 1, a narrow vent, during travel, and after room changes; test seated/standing reach on Quest 2 and Quest 3. The old #64 gate wiring acceptance remains separate.
3. Hear two remote clients if available: mute A while B stays audible. A still hears you. Travel apart/reunite, recreate the voice speaker, unmute across sectors and rejoin/change rooms. Confirm no audio leakage or everyone-mute regression.
4. With two disposable test accounts and available daily quota, report each reason one at a time within the service limit. Verify target/reporter/context and request ID in Game Manager/PlayStream. Do not infer delivery from a button label. Specifically check the fifth/capped response semantics.
5. Cancel; disconnect before sending; fail the service; time out then retry; leave/rejoin while sending; leave as target after the form opens; alter/miss the advertised ID. Confirm no false success, wrong target or stuck controls. After reporting A, select B and confirm B shows no receipt. Open different targets on the Hub and portable boards before submitting; only the submitting form should show `SENDING...`, with `WAIT...` on the other, and no result should transfer to an unrelated target.
6. Complete the trusted-identity, operator review/privacy/retention, leave/block scope and broader public-release acceptance in #43. Keep #43 open until its full close rule is satisfied.

## Primary service references

Checked 2026-09-30:

- [Client ReportPlayer API](https://learn.microsoft.com/en-us/rest/api/playfab/client/account-management/report-player?view=playfab-rest): authenticated client request and daily-limit response.
- [Daily Abuse Reports History](https://learn.microsoft.com/en-us/xbox/playfab/data-analytics/learn-data/reports/daily-abuse-reports-history-report): review destination and documented report availability.
- [player_reported_as_abusive event](https://learn.microsoft.com/en-us/xbox/playfab/api-references/events/playeridentity/player-reported-as-abusive): reporter and comment fields.
