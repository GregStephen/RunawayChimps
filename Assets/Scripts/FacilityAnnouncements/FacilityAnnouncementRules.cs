using System;
using System.Collections.Generic;

namespace RunawayChimps.FacilityAnnouncements
{
    // Feature-local policy. Deliberately has no Unity/Photon dependencies so the same
    // selection, scheduling and receive guards can be exercised by the managed harness.
    public static class FacilityAnnouncementRules
    {
        public const string Protocol = "rc.facility.1";
        public const int MaxEntries = 32;
        public const int MaxPeers = 32;
        public const double MaxSpeechSeconds = 30;
        public const double MaxSequenceSeconds = 35;
        public const double StartLeadSeconds = 0.4;
        public const double MaxPacketAgeSeconds = 2;

        public struct Choice
        {
            public string Id;
            public int Weight;
            public bool Playable;
            public Choice(string id, int weight, bool playable)
            { Id = id; Weight = weight; Playable = playable; }
        }

        public struct Peer
        {
            public int Actor;
            public int Sector;
            public string Ticket;
            public Peer(int actor, int sector, string ticket)
            { Actor = actor; Sector = sector; Ticket = ticket; }
        }

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static double ClampFinite(double value, double min, double max, double fallback) =>
            Math.Max(min, Math.Min(max, Finite(value) ? value : fallback));

        // Photon.Time wraps at the unsigned 32-bit millisecond boundary. Retire the
        // local visit and rearm quietly; raw deadlines must never survive that wrap.
        public static bool ClockWrapped(double previous, double current) =>
            Finite(previous) && Finite(current) && previous - current > 2147483.648;

        public static bool ValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                    return false;
            return true;
        }

        public static bool ValidTicket(string ticket) => ticket != null &&
            ticket.Length == 32 && Guid.TryParseExact(ticket, "N", out _);

        public static int Elect(IList<Peer> peers, int sector)
        {
            if (peers == null || peers.Count > MaxPeers || sector < 1 || sector > 3) return 0;
            int actor = 0;
            for (int i = 0; i < peers.Count; ++i)
            {
                Peer p = peers[i];
                if (p.Sector == sector && p.Actor > 0 && ValidTicket(p.Ticket) &&
                    (actor == 0 || p.Actor < actor)) actor = p.Actor;
            }
            return actor;
        }

        public static int Select(IList<Choice> choices, string lastId, double randomUnit, bool allowSingleRepeat)
        {
            if (choices == null || choices.Count > MaxEntries || !Finite(randomUnit)) return -1;
            int total = 0;
            int usable = 0;
            int sole = -1;
            for (int i = 0; i < choices.Count; ++i)
            {
                if (!Usable(choices, i)) continue;
                ++usable;
                sole = i;
                if (choices[i].Id != lastId) total += Math.Min(10, choices[i].Weight);
            }
            // With one line, silence after its first play is the default, not a repeat loop.
            if (total == 0) return allowSingleRepeat && usable == 1 ? sole : -1;
            int ticket = Math.Min(total - 1, (int)(Math.Max(0, Math.Min(1, randomUnit)) * total));
            for (int i = 0; i < choices.Count; ++i)
            {
                if (!Usable(choices, i) || choices[i].Id == lastId) continue;
                ticket -= Math.Min(10, choices[i].Weight);
                if (ticket < 0) return i;
            }
            return -1;
        }

        private static bool Usable(IList<Choice> choices, int index)
        {
            Choice c = choices[index];
            if (!c.Playable || c.Weight <= 0 || !ValidId(c.Id)) return false;
            // Reject BOTH copies of a duplicate stable ID; never resolve one arbitrarily.
            for (int i = 0; i < choices.Count; ++i)
                if (i != index && choices[i].Id == c.Id) return false;
            return true;
        }

        public static double QuietSeconds(double min, double max, double unit, bool diagnostic)
        {
            double floor = diagnostic ? 5 : 60;
            if (!Finite(min)) min = diagnostic ? 8 : 180;
            if (!Finite(max)) max = diagnostic ? 12 : 300;
            if (!Finite(unit)) unit = 0.5;
            min = Math.Max(floor, Math.Min(1800, min));
            max = Math.Max(min, Math.Min(1800, max));
            return min + (max - min) * Math.Max(0, Math.Min(1, unit));
        }

        public enum PlaybackCompletion { Waiting, Finished, Interrupted }

        // AudioSource.isPlaying can begin and end between two rendered frames for
        // very short cues. Speech must actually have been observed playing before
        // it can complete normally or be accompanied by a caption.
        public static PlaybackCompletion CompletedClip(bool speech, bool observedPlaying, double elapsed, double length)
        {
            if (!Finite(elapsed) || !Finite(length) || elapsed < 0 || length <= 0)
                return PlaybackCompletion.Interrupted;
            if (!observedPlaying && elapsed < 0.3) return PlaybackCompletion.Waiting;
            if ((speech && !observedPlaying) || elapsed < length - Math.Min(0.05, length * 0.1))
                return PlaybackCompletion.Interrupted;
            return PlaybackCompletion.Finished;
        }

        public sealed class Schedule
        {
            public double Due { get; private set; } = double.PositiveInfinity;
            public bool IsDue(double now) => Finite(now) && now >= Due;
            public void Cancel() => Due = double.PositiveInfinity;
            public void Arm(double now, double quiet, double guard = 0)
            {
                Due = Finite(now) && Finite(quiet) && Finite(guard)
                    ? now + Math.Max(0, Math.Max(quiet, guard)) : double.PositiveInfinity;
            }
        }

        public sealed class ReceiveGate
        {
            public int Owner { get; private set; }
            public string OwnerTicket { get; private set; }
            public double BusyUntil { get; private set; }
            public int LastSerial { get; private set; }
            private double issuedFloor;
            private double lastIssued = double.NegativeInfinity;

            public void Reset(double now)
            {
                Owner = 0;
                OwnerTicket = null;
                LastSerial = 0;
                BusyUntil = 0;
                issuedFloor = now;
                lastIssued = double.NegativeInfinity;
            }

            public bool SetAuthority(int actor, string ticket, double now)
            {
                if (Owner == actor && OwnerTicket == ticket) return false;
                Owner = actor;
                OwnerTicket = ticket;
                LastSerial = 0;
                issuedFloor = now;
                // Preserve the active reservation across a handover, even if audio is cancelled.
                return true;
            }

            public bool TryAccept(int sender, string ticket, int serial, double issued, double duration, double now)
            {
                if (Owner <= 0 || sender != Owner || ticket != OwnerTicket || !ValidTicket(ticket) ||
                    serial <= LastSerial || serial <= 0 || !Finite(now) || !Finite(issued) || !Finite(duration) ||
                    duration <= 0 || duration > MaxSequenceSeconds || now < BusyUntil ||
                    issued < issuedFloor || issued <= lastIssued || issued > now + 0.25 ||
                    now - issued > MaxPacketAgeSeconds) return false;
                LastSerial = serial;
                lastIssued = issued;
                BusyUntil = Math.Max(now, issued + StartLeadSeconds) + duration + 0.5;
                return true;
            }
        }
    }
}
