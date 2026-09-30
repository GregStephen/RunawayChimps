using System;
using System.Collections.Generic;
using RunawayChimps.FacilityAnnouncements;
using Rules = RunawayChimps.FacilityAnnouncements.FacilityAnnouncementRules;

internal static class Program
{
    private static int assertions;
    private static readonly string A = new Guid("11111111-1111-1111-1111-111111111111").ToString("N");
    private static readonly string B = new Guid("22222222-2222-2222-2222-222222222222").ToString("N");
    private static void Check(bool condition, string message)
    {
        ++assertions;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Main()
    {
        Selection();
        Scheduling();
        PlaybackCompletion();
        AuthorityAndCancellation();
        Console.WriteLine("PASS: " + assertions + " production-policy assertions. Unity, native audio, Photon transport and XR are NOT exercised.");
    }
    private static void Selection()
    {
        Check(Rules.Select(null, null, 0, false) == -1, "null collection");
        Check(Rules.Select(new List<Rules.Choice>(), null, 0, false) == -1, "empty collection");
        var choices = new List<Rules.Choice>
        {
            new Rules.Choice("a", 1, true), new Rules.Choice("b", 3, true),
            new Rules.Choice("pending", 10, false)
        };
        Check(Rules.Select(choices, null, 0, false) == 0, "first weighted boundary");
        Check(Rules.Select(choices, null, 0.25, false) == 1, "second weighted boundary");
        Check(Rules.Select(choices, null, 1, false) == 1, "inclusive random endpoint");
        Check(Rules.Select(choices, null, double.NaN, false) == -1, "NaN random");
        string previous = null;
        var random = new Random(839);
        for (int i = 0; i < 10000; ++i)
        {
            int index = Rules.Select(choices, previous, random.NextDouble(), false);
            Check(index >= 0 && index < 2, "never select pending speech");
            Check(choices[index].Id != previous, "no immediate repeat");
            previous = choices[index].Id;
        }
        choices.RemoveAt(1);
        Check(Rules.Select(choices, null, 0.5, false) == 0, "single first play");
        Check(Rules.Select(choices, "a", 0.5, false) == -1, "single then silence");
        Check(Rules.Select(choices, "a", 0.5, true) == 0, "explicit single repeat opt-in");
        choices.Add(new Rules.Choice("a", 1, true));
        Check(Rules.Select(choices, null, 0.5, true) == -1, "both duplicate IDs rejected");
        choices[2] = new Rules.Choice("a", 1, false);
        Check(Rules.Select(choices, null, 0.5, true) == -1, "pending duplicate also ambiguous");
        Check(!Rules.ValidId("../bad") && !Rules.ValidId(new string('a', 65)) && Rules.ValidId("facility-01"), "bounded IDs");
        Check(Rules.Select(new Rules.Choice[33], null, 0, false) == -1, "bounded collection");
    }
    private static void PlaybackCompletion()
    {
        Check(Rules.CompletedClip(false, false, 0.32, 0.22) == Rules.PlaybackCompletion.Finished,
            "short cue completed between frames must not discard selected speech");
        Check(Rules.CompletedClip(true, false, 0.32, 5) == Rules.PlaybackCompletion.Interrupted,
            "unobserved speech cannot create caption-only playback");
        Check(Rules.CompletedClip(true, false, 0.1, 5) == Rules.PlaybackCompletion.Waiting,
            "bounded initial source-start grace");
        Check(Rules.CompletedClip(true, true, 2, 5) == Rules.PlaybackCompletion.Interrupted,
            "externally interrupted speech cancels instead of completing");
        Check(Rules.CompletedClip(false, true, 0.1, 0.22) == Rules.PlaybackCompletion.Interrupted,
            "externally interrupted cue cancels");
        Check(Rules.CompletedClip(true, true, 5, 5) == Rules.PlaybackCompletion.Finished,
            "observed natural speech completion");
        Check(Rules.CompletedClip(true, true, double.NaN, 5) == Rules.PlaybackCompletion.Interrupted,
            "invalid playback clock fails closed");
        Check(Rules.CompletedClip(false, false, 1, 0) == Rules.PlaybackCompletion.Interrupted,
            "empty cue cannot advance a caption");
    }
    private static void Scheduling()
    {
        var schedule = new Rules.Schedule();
        Check(!schedule.IsDue(999), "silence until armed");
        schedule.Arm(10, 180);
        Check(!schedule.IsDue(189.999) && schedule.IsDue(190), "quiet interval exact boundary");
        schedule.Arm(20, 8, 36);
        Check(!schedule.IsDue(55.999) && schedule.IsDue(56), "handover guard wins in diagnostic mode");
        schedule.Cancel();
        Check(!schedule.IsDue(100000), "cancel cannot catch up");
        schedule.Arm(1000, 180);
        Check(!schedule.IsDue(1001) && schedule.IsDue(1180), "resume uses fresh deadline");
        schedule.Arm(double.NaN, 1);
        Check(!schedule.IsDue(10000), "invalid clock fails silent");
        Check(Rules.QuietSeconds(1, 2, 0, false) == 60, "normal hard floor");
        Check(Rules.QuietSeconds(8, 12, 0, true) == 8 && Rules.QuietSeconds(8, 12, 1, true) == 12, "diagnostic bounds");
        Check(Rules.QuietSeconds(double.NaN, double.NaN, double.NaN, false) == 240, "invalid settings safe defaults");
        Check(Rules.QuietSeconds(300, 180, 0.5, false) == 300, "reversed interval corrected");
    }
    private static void AuthorityAndCancellation()
    {
        var peers = new List<Rules.Peer> { new Rules.Peer(1, 2, A), new Rules.Peer(8, 1, B), new Rules.Peer(4, 1, A) };
        Check(Rules.Elect(peers, 1) == 4, "master elsewhere does not own Hub");
        peers.Reverse();
        Check(Rules.Elect(peers, 1) == 4, "simultaneous initialization order independent");
        peers.RemoveAt(0);
        Check(Rules.Elect(peers, 1) == 8, "sector departure handover");
        Check(Rules.Elect(null, 1) == 0 && Rules.Elect(peers, 0) == 0, "no eligible authority");
        Check(Rules.Elect(new[] { new Rules.Peer(1, 1, "stale") }, 1) == 0, "unready actor excluded");
        var gate = new Rules.ReceiveGate();
        gate.Reset(10);
        Check(gate.SetAuthority(4, A, 10), "first authority");
        Check(!gate.TryAccept(8, B, 1, 11, 5, 11), "wrong authority");
        Check(!gate.TryAccept(4, B, 1, 11, 5, 11), "wrong readiness ticket");
        Check(!gate.TryAccept(4, A, 1, 9, 5, 11), "pre-visit packet");
        Check(gate.TryAccept(4, A, 1, 11, 5, 11.1), "valid event");
        Check(!gate.TryAccept(4, A, 1, 11, 5, 11.2), "duplicate event");
        Check(!gate.TryAccept(4, A, 2, 12, 5, 12), "no overlapping announcements");
        Check(gate.SetAuthority(8, B, 13), "handover changes owner");
        Check(!gate.TryAccept(8, B, 1, 14, 5, 14), "handover retains active slot reservation");
        Check(gate.TryAccept(8, B, 1, 20, 5, 20), "new owner after old slot expires");
        Check(gate.SetAuthority(4, A, 30), "A-B-A ownership");
        Check(!gate.TryAccept(4, A, 99, 29.9, 5, 30), "late prior-term A event rejected despite higher serial");
        Check(gate.TryAccept(4, A, 2, 31, 5, 31), "fresh A term works");
        gate.Reset(100);
        gate.SetAuthority(4, B, 100);
        Check(!gate.TryAccept(4, A, 99, 101, 5, 101), "new room/visit ticket rejects old content");
        Check(!gate.TryAccept(4, B, 1, 101, 5, 104), "late arrival receives no backlog");
        Check(!gate.TryAccept(4, B, 1, 105, 5, 101), "future packet");
        Check(!gate.TryAccept(4, B, 1, double.NaN, 5, 101), "invalid time");
        Check(!gate.TryAccept(4, B, 1, 101, 36, 101), "unbounded clip rejected");
        Check(!gate.TryAccept(4, B, 0, 101, 5, 101), "invalid serial");
        gate.SetAuthority(0, null, 102);
        Check(!gate.TryAccept(4, B, 1, 103, 5, 103), "pause/disable cancellation");
    }
}
