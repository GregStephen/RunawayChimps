using System;
using System.Collections.Generic;

namespace RunawayChimps.Toys.PrimateCognitive
{
    public enum CognitivePhase { Idle, Demonstrating, Input, Success, Failure, Complete }

    // Pure, authority-owned rules. Unity, physical contacts and Photon all use this model.
    public sealed class CognitiveGame
    {
        public const int HardMaximum = 16;
        public readonly int Maximum;
        public readonly double LeadSeconds, StepSeconds, FlashSeconds, SuccessSeconds;
        public readonly double LeaseSeconds, InputTimeoutSeconds, ResultSeconds;
        public CognitivePhase Phase { get; private set; }
        public int Owner { get; private set; }
        public int Session { get; private set; }
        public int PhaseToken { get; private set; }
        public int Revision { get; private set; }
        public int InputIndex { get; private set; }
        public int Cue { get; private set; } = -1;
        public int CueSerial { get; private set; }
        public double CueAt { get; private set; }
        public bool Abandoned { get; private set; }
        public int Round => sequence.Count;
        private readonly List<byte> sequence = new List<byte>(HardMaximum);
        private uint random;
        private double nextAt, lastHeartbeat, lastInput, phaseAt;
        private int demonstrationIndex;

        public CognitiveGame(int maximum = 8, uint seed = 1, double lead = .7,
            double step = .65, double flash = .32, double success = 1.1,
            double lease = 4, double inputTimeout = 20, double result = 10)
        {
            Maximum = Math.Max(1, Math.Min(HardMaximum, maximum));
            random = seed == 0 ? 1u : seed;
            FlashSeconds = Bounded(flash, .15, .8, .32);
            StepSeconds = Bounded(step, FlashSeconds + .15, 2, .65);
            LeadSeconds = Bounded(lead, .3, 3, .7);
            SuccessSeconds = Bounded(success, .5, 4, 1.1);
            LeaseSeconds = Bounded(lease, 3, 15, 4);
            InputTimeoutSeconds = Bounded(inputTimeout, 5, 90, 20);
            ResultSeconds = Bounded(result, 3, 30, 10);
        }

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static double Bounded(double value, double min, double max, double fallback) =>
            Math.Max(min, Math.Min(max, Finite(value) ? value : fallback));

        public bool Start(int actor, int expectedSession, int expectedPhase, double now)
        {
            if (!Finite(now) || actor <= 0 || (Owner != 0 && Owner != actor) ||
                expectedSession != Session || expectedPhase != PhaseToken) return false;
            Owner = actor;
            Session++;
            sequence.Clear();
            AppendStep();
            InputIndex = 0;
            Abandoned = false;
            lastHeartbeat = lastInput = now;
            Demonstrate(now);
            return true;
        }

        public bool Heartbeat(int actor, double now)
        {
            if (actor != Owner || Owner == 0 || !Finite(now)) return false;
            lastHeartbeat = now;
            return true;
        }

        public bool Press(int actor, int button, int session, int phase, double now)
        {
            if (!Finite(now) || Owner == 0 || actor != Owner || Phase != CognitivePhase.Input ||
                session != Session || phase != PhaseToken || button < 0 || button > 3) return false;
            // The adapter also ticks before commands. Keep the deadline guard in the rules too.
            if (now - lastHeartbeat >= LeaseSeconds || now - lastInput >= InputTimeoutSeconds)
            {
                Release(true);
                return false;
            }
            lastInput = now;
            if (sequence[InputIndex] != button)
            {
                ChangePhase(CognitivePhase.Failure, now);
                Signal(5, now);
                return true;
            }
            Signal(button, now);
            InputIndex++;
            Revision++;
            if (InputIndex == sequence.Count)
                ChangePhase(sequence.Count == Maximum ? CognitivePhase.Complete : CognitivePhase.Success, now);
            return true;
        }

        public bool Release(bool abandoned)
        {
            if (Owner == 0 && Phase == CognitivePhase.Idle) return false;
            Owner = 0;
            Phase = CognitivePhase.Idle;
            sequence.Clear();
            InputIndex = 0;
            Cue = -1;
            Abandoned = abandoned;
            PhaseToken++;
            Revision++;
            return true;
        }

        public void Tick(double now, bool ownerStillPresent)
        {
            if (Owner == 0 || !Finite(now)) return;
            if (!ownerStillPresent || now < lastHeartbeat || now - lastHeartbeat >= LeaseSeconds ||
                (Phase == CognitivePhase.Input && now - lastInput >= InputTimeoutSeconds))
            {
                Release(true);
                return;
            }
            if (Phase == CognitivePhase.Failure || Phase == CognitivePhase.Complete)
            {
                if (now - phaseAt >= ResultSeconds) Release(false);
                return;
            }
            if (Phase == CognitivePhase.Success && now - phaseAt >= SuccessSeconds)
            {
                AppendStep();
                Demonstrate(now);
                return;
            }
            if (Phase != CognitivePhase.Demonstrating || now < nextAt) return;
            // At most one note per tick: a stalled frame never crams several cues together.
            if (demonstrationIndex < sequence.Count)
            {
                Signal(sequence[demonstrationIndex++], now);
                nextAt = now + StepSeconds;
            }
            else
            {
                InputIndex = 0;
                lastInput = now;
                ChangePhase(CognitivePhase.Input, now);
            }
        }

        private void Demonstrate(double now)
        {
            demonstrationIndex = 0;
            InputIndex = 0;
            Cue = -1;
            nextAt = now + LeadSeconds;
            ChangePhase(CognitivePhase.Demonstrating, now);
        }

        private void AppendStep()
        {
            if (sequence.Count >= Maximum) throw new InvalidOperationException("Cognitive sequence is bounded.");
            // Deterministic across Unity/.NET, but consecutive repeats remain valid memory tasks.
            random ^= random << 13;
            random ^= random >> 17;
            random ^= random << 5;
            sequence.Add((byte)(random % 4));
        }

        private void ChangePhase(CognitivePhase phase, double now)
        {
            Phase = phase;
            phaseAt = now;
            PhaseToken++;
            Revision++;
        }

        private void Signal(int cue, double now)
        {
            Cue = cue;
            CueAt = now;
            CueSerial++;
            Revision++;
        }

        public CognitiveSnapshot Snapshot() => new CognitiveSnapshot(Owner, Session, PhaseToken,
            Revision, Phase, sequence.ToArray(), InputIndex, Maximum, Cue, CueSerial, CueAt,
            FlashSeconds, Abandoned);
    }

    public sealed class CognitiveSnapshot
    {
        public readonly int Owner, Session, PhaseToken, Revision, InputIndex, Maximum, Cue, CueSerial;
        public readonly CognitivePhase Phase;
        public readonly byte[] Sequence;
        public readonly double CueAt, FlashSeconds;
        public readonly bool Abandoned;
        public int Round => Sequence.Length;

        public CognitiveSnapshot(int owner, int session, int phaseToken, int revision,
            CognitivePhase phase, byte[] sequence, int inputIndex, int maximum, int cue,
            int cueSerial, double cueAt, double flashSeconds, bool abandoned)
        {
            Owner = owner; Session = session; PhaseToken = phaseToken; Revision = revision;
            Phase = phase; Sequence = sequence; InputIndex = inputIndex; Maximum = maximum;
            Cue = cue; CueSerial = cueSerial; CueAt = cueAt; FlashSeconds = flashSeconds;
            Abandoned = abandoned;
        }

        public object[] Pack() => new object[] { Owner, Session, PhaseToken, Revision, (int)Phase,
            Sequence, InputIndex, Maximum, Cue, CueSerial, CueAt, FlashSeconds, Abandoned };

        public static bool TryRead(object value, out CognitiveSnapshot snapshot)
        {
            snapshot = null;
            if (!(value is object[] d) || d.Length != 13 || !(d[0] is int owner) || owner < 0 ||
                !(d[1] is int session) || session < 0 || !(d[2] is int token) || token < 0 ||
                !(d[3] is int revision) || revision < 0 || !(d[4] is int phase) || phase < 0 || phase > 5 ||
                !(d[5] is byte[] seq) || seq.Length > CognitiveGame.HardMaximum ||
                !(d[6] is int input) || input < 0 || input > seq.Length ||
                !(d[7] is int max) || max < 1 || max > CognitiveGame.HardMaximum || seq.Length > max ||
                !(d[8] is int cue) || cue < -1 || cue > 5 || !(d[9] is int serial) || serial < 0 ||
                !(d[10] is double at) || !CognitiveGame.Finite(at) ||
                !(d[11] is double flash) || !CognitiveGame.Finite(flash) || flash < .15 || flash > .8 ||
                !(d[12] is bool abandoned)) return false;
            if ((phase == 0) != (owner == 0) || (phase == 0 ? seq.Length != 0 : seq.Length == 0)) return false;
            if (phase == (int)CognitivePhase.Input && input >= seq.Length) return false;
            if ((phase == (int)CognitivePhase.Success || phase == (int)CognitivePhase.Complete) && input != seq.Length) return false;
            if (phase == (int)CognitivePhase.Complete && seq.Length != max) return false;
            foreach (byte button in seq) if (button > 3) return false;
            snapshot = new CognitiveSnapshot(owner, session, token, revision, (CognitivePhase)phase,
                (byte[])seq.Clone(), input, max, cue, serial, at, flash, abandoned);
            return true;
        }

        public string Assessment
        {
            get
            {
                switch (Phase)
                {
                    case CognitivePhase.Demonstrating: return "OBSERVE. Do not improvise.";
                    case CognitivePhase.Input: return "REPEAT THE SEQUENCE";
                    case CognitivePhase.Success: return Round < 3 ? "Basic motor function detected." : "Performance exceeds\nfacility management.";
                    case CognitivePhase.Failure: return "A doorstop has outperformed you.";
                    case CognitivePhase.Complete: return "Please remain available\nfor further testing.";
                    default: return Abandoned ? "Subject absent. Typical." : "Voluntary testing. Mandatory judgement.";
                }
            }
        }
    }
}
