using System;
using System.Collections.Generic;

namespace RunawayChimps.Toys.PrimateCognitive
{
    // Aggregate all colliders of either local hand. Phase changes never clear occupancy.
    public sealed class CognitiveContactGate
    {
        private readonly HashSet<int> contacts = new HashSet<int>();
        private readonly double releaseSeconds;
        private bool armed;
        private double clearSince;
        public int Count => contacts.Count;

        public CognitiveContactGate(double release = .08)
        {
            releaseSeconds = Math.Max(.03, Math.Min(.5, CognitiveGame.Finite(release) ? release : .08));
        }

        public void Tick(double now)
        {
            if (contacts.Count == 0 && now - clearSince >= releaseSeconds) armed = true;
        }

        public bool Enter(int colliderId, double now, bool freshEnter = true)
        {
            if (!contacts.Add(colliderId)) return false;
            bool fire = freshEnter && contacts.Count == 1 && armed;
            armed = false;
            return fire;
        }

        public void Exit(int colliderId, double now)
        {
            if (contacts.Remove(colliderId) && contacts.Count == 0) clearSince = now;
        }

        public void RequireRelease(double now)
        {
            armed = false;
            if (contacts.Count == 0) clearSince = now;
        }

        public void Reset(double now)
        {
            contacts.Clear();
            RequireRelease(now);
        }
    }

    // A fresh, echoed nonce authenticates a controller term, including A -> B -> A.
    // This is stale-message protection, not a trusted-server anti-cheat boundary.
    public sealed class CognitiveReplica
    {
        public int Authority { get; private set; }
        public string Epoch { get; private set; } = "";
        public string RequestNonce { get; private set; } = "";
        public CognitiveSnapshot State { get; private set; }
        public bool Ready => State != null && Epoch.Length != 0;

        public bool Elect(int actor, string nonce)
        {
            if (Authority == actor) return false;
            Authority = actor;
            Epoch = "";
            RequestNonce = nonce;
            State = null;
            return true;
        }

        public void RequestSync(string nonce)
        {
            // Retransmit the outstanding challenge until a reply consumes it.
            // Replacing it every retry starves peers whose RTT exceeds the retry
            // interval. Election/room reset still establishes a fresh challenge.
            if (string.IsNullOrEmpty(RequestNonce)) RequestNonce = nonce;
        }

        public void Reset()
        {
            Authority = 0; Epoch = ""; RequestNonce = ""; State = null;
        }

        public bool Accept(int sender, string epoch, string echo, CognitiveSnapshot next)
        {
            if (sender == 0 || sender != Authority || next == null || string.IsNullOrEmpty(epoch) || epoch.Length > 64) return false;
            bool handshake = !string.IsNullOrEmpty(RequestNonce) && echo == RequestNonce;
            if ((!Ready || Epoch != epoch) && !handshake) return false;
            // Consume even a no-change sync reply: it cannot later resurrect an older term.
            if (handshake) RequestNonce = "";
            if (Ready && Epoch == epoch && next.Revision <= State.Revision) return false;
            Epoch = epoch;
            State = next;
            return true;
        }
    }
}
