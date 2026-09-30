# Facility announcement adapter diagnostics

Run `dotnet run --project Tools/FacilityAnnouncementsAdapterHarness --configuration Release`
from the repository root with the .NET 8 SDK. No Unity installation or NuGet packages
are used. The existing policy harness remains separate.

The project links the actual five runtime feature files and `SectorPresence.cs`.
It does not rewrite the director, speaker, collection or caption logic into a test model.
`Doubles.cs` supplies instrumented Unity, Photon and rig collaborators. Tests explicitly
advance clocks, deliver packets, change membership, call lifecycle methods and report
native-source playback state. Properties are applied synchronously by the doubles;
real Photon delivery/acknowledgement ordering is **not** simulated.

Coverage includes accepted and failed sends/recovery, fresh send-time elections, A-B-A
handover, room/sector/scene/pause/focus/disconnect cancellation, timestamp rollover,
late/stale/duplicate messages, occupied/expired slots, delayed cue/speech transitions,
source replacement, nearest-speaker selection, disabled or replaced rigs, disabled
captions, invalid content and nonfinite local settings.

A pass establishes managed adapter/control-flow behavior only. It does not establish
Unity API binding/import, real callback ordering, destroyed-object semantics, native
AudioSource timing/output, XR rendering, live Photon transport or headset acceptance.
Editor adapters are inspected separately and are not linked into this harness.
