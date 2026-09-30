using System;

namespace RunawayChimps.Toys.KnockBack
{
    public enum VentPhase { Idle, Recording, Replying, Cooldown }

    // Pure, bounded state. Both Unity tests and the managed harness execute this file.
    [Serializable]
    public sealed class VentSettings
    {
        public int maxTaps = 6;
        public float minimumInterval = 0.12f;
        public float quietInterval = 0.65f;
        public float maximumRecording = 3.5f;
        public float replyDelay = 0.75f;
        public float cooldown = 2f;
        public float extraKnockChance = 0.06f;
        public float heavyBangChance = 0.04f;
        public float extraKnockGap = 0.28f;

        public VentSettings Bounded()
        {
            var result = new VentSettings
            {
                maxTaps = Math.Max(1, Math.Min(VentModel.HardTapLimit, maxTaps)),
                minimumInterval = Clamp(minimumInterval, 0.1f, 0.3f, 0.12f),
                quietInterval = Clamp(quietInterval, 0.4f, 1.2f, 0.65f),
                maximumRecording = Clamp(maximumRecording, 1f, VentModel.RecordingLimit, 3.5f),
                replyDelay = Clamp(replyDelay, 0.4f, 2f, 0.75f),
                cooldown = Clamp(cooldown, 1f, 8f, 2f),
                extraKnockChance = Clamp(extraKnockChance, 0f, 0.15f, 0.06f),
                heavyBangChance = Clamp(heavyBangChance, 0f, 0.15f, 0.04f),
                extraKnockGap = Clamp(extraKnockGap, 0.15f, 0.8f, 0.28f)
            };
            // Variations remain uncommon even when Inspector values are edited.
            result.heavyBangChance = Math.Min(result.heavyBangChance, 0.2f - result.extraKnockChance);
            return result;
        }

        private static float Clamp(float value, float low, float high, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(low, Math.Min(high, value));
    }

    public sealed class VentReply
    {
        public readonly double Start;
        public readonly double CooldownUntil;
        public readonly float[] Offsets;
        public readonly int HeavyIndex;
        public VentReply(double start, double cooldownUntil, float[] offsets, int heavyIndex)
        { Start = start; CooldownUntil = cooldownUntil; Offsets = offsets; HeavyIndex = heavyIndex; }
        public double End => Start + Offsets[Offsets.Length - 1] + VentModel.AudioTail;
    }

    public sealed class VentModel
    {
        public const int HardTapLimit = 8;
        public const int ReplyLimit = HardTapLimit + 1;
        public const double AudioTail = 0.5d;
        public const float RecordingLimit = 5f;
        public const double MessageMaxAge = 0.75d;
        public const double FrameGapLimit = 0.75d;
        // A valid frame may cross the recording deadline before constructing the plan.
        // Receivers must allow that frame gap AND transport delay before timing out.
        public const double LatestPlanIssue = RecordingLimit + FrameGapLimit + 0.05d;
        public const double RecordingTimeout = LatestPlanIssue + MessageMaxAge + 0.1d;
        public VentPhase Phase { get; private set; }
        public int Owner { get; private set; }
        public int Count { get; private set; }
        public double Cycle { get; private set; }
        public VentReply Reply { get; private set; }
        public readonly VentSettings Settings;
        private readonly float[] offsets = new float[HardTapLimit];
        private double firstInput, lastInput, lastReceived;

        public VentModel(VentSettings settings) { Settings = (settings ?? new VentSettings()).Bounded(); }
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public bool TryTap(int actor, double inputTime, double now)
        {
            if (actor <= 0 || !Finite(inputTime) || !Finite(now) || inputTime > now + 0.1d ||
                now - inputTime > MessageMaxAge) return false;
            if (Phase == VentPhase.Idle)
            {
                Phase = VentPhase.Recording;
                Owner = actor;
                Cycle = now;
                firstInput = lastInput = inputTime;
                lastReceived = now;
                Count = 1;
                offsets[0] = 0f;
                return true;
            }
            if (Phase != VentPhase.Recording || actor != Owner || Count >= Settings.maxTaps ||
                now < lastReceived || now - lastReceived >= Settings.quietInterval ||
                now - Cycle >= Settings.maximumRecording ||
                inputTime - lastInput < Settings.minimumInterval ||
                inputTime - lastInput >= Settings.quietInterval ||
                inputTime - firstInput > Settings.maximumRecording) return false;
            offsets[Count++] = (float)(inputTime - firstInput);
            lastInput = inputTime;
            lastReceived = now;
            return true;
        }

        // Returns a plan exactly once. No queued sequences, no catch-up/replay on cancellation.
        public bool ReadyToReply(double now) => Phase == VentPhase.Recording && VentModel.Finite(now) &&
            (now - lastReceived >= Settings.quietInterval || now - Cycle >= Settings.maximumRecording);

        public VentReply Advance(double now, double variationRoll)
        {
            if (!Finite(now)) return null;
            if (ReadyToReply(now))
            {
                double roll = Finite(variationRoll) ? variationRoll : 1d;
                bool extra = roll >= 0d && roll < Settings.extraKnockChance;
                bool heavy = !extra && roll >= Settings.extraKnockChance &&
                    roll < Settings.extraKnockChance + Settings.heavyBangChance;
                var answer = new float[Count + (extra ? 1 : 0)];
                Array.Copy(offsets, answer, Count);
                if (extra) answer[Count] = answer[Count - 1] + Settings.extraKnockGap;
                double start = now + Settings.replyDelay;
                Reply = new VentReply(start, start + answer[answer.Length - 1] + AudioTail + Settings.cooldown,
                    answer, heavy ? Count - 1 : -1);
                Phase = VentPhase.Replying;
                return Reply;
            }
            if (Phase == VentPhase.Replying && now >= Reply.End) Phase = VentPhase.Cooldown;
            if (Phase == VentPhase.Cooldown && now >= Reply.CooldownUntil) Cancel();
            return null;
        }

        public void Cancel()
        {
            Phase = VentPhase.Idle;
            Owner = Count = 0;
            Cycle = firstInput = lastInput = lastReceived = 0d;
            Reply = null;
            Array.Clear(offsets, 0, offsets.Length);
        }
    }

    // One gate per physical hand, NOT per collider. A slow/resting contact consumes arming too.
    public sealed class VentContactGate
    {
        private double releasedAt = double.NaN;
        private bool armed;
        public void Reset() { armed = false; releasedAt = double.NaN; }
        public bool Sample(double now, bool validMotion, bool touching, bool released,
            float inwardSpeed, float minimumSpeed, float releaseSeconds)
        {
            if (!validMotion || !VentModel.Finite(now) || !VentModel.Finite(inwardSpeed))
            { Reset(); return false; }
            if (released)
            {
                if (double.IsNaN(releasedAt)) releasedAt = now;
                if (now - releasedAt >= releaseSeconds) armed = true;
                return false;
            }
            releasedAt = double.NaN;
            if (!touching) return false; // hysteresis band retains, but cannot create, arming
            bool accept = armed && inwardSpeed >= minimumSpeed;
            armed = false;
            return accept;
        }
    }

    // Shared receiver guard for live-only events. A local room/sector/controller generation
    // raises the time floor, including A -> B -> A. No per-actor history grows over time.
    public sealed class VentDeliveryGate
    {
        public double Floor { get; private set; } = double.PositiveInfinity;
        public double Cycle { get; private set; } = double.NegativeInfinity;
        public int Owner { get; private set; }
        public int LastTap { get; private set; } = -1;
        public bool Sealed { get; private set; }
        private double lastIssued = double.NegativeInfinity;
        private bool strictFloor = true;
        public void Reset(double now)
        {
            Floor = now;
            strictFloor = true;
            Cycle = double.NegativeInfinity;
            lastIssued = double.NegativeInfinity;
            Owner = 0;
            LastTap = -1;
            Sealed = false;
        }
        // Photon timestamps have millisecond resolution. Resetting at the old cycle's
        // exact timestamp must retire it too, not admit an equal-timestamp replay.
        public bool AcceptsInput(double time) => VentModel.Finite(time) &&
            (strictFloor ? time > Floor : time >= Floor);

        public bool Header(double cycle, double issued, double now, int owner)
        {
            if (owner <= 0 || !VentModel.Finite(cycle) || !VentModel.Finite(issued) || !VentModel.Finite(now) ||
                !AcceptsInput(cycle) || cycle > issued || issued > now + 0.1d || now - issued > VentModel.MessageMaxAge ||
                cycle < Cycle || issued < lastIssued || issued - cycle > 23d) return false;
            if (cycle > Cycle)
            {
                Cycle = cycle;
                Owner = owner;
                LastTap = -1;
                Sealed = false;
            }
            if (owner != Owner) return false;
            lastIssued = issued;
            return true;
        }
        public bool Tap(int ordinal)
        {
            if (Sealed || ordinal <= LastTap || ordinal < 0 || ordinal >= VentModel.HardTapLimit) return false;
            LastTap = ordinal;
            return true;
        }
        public bool Seal() { if (Sealed) return false; Sealed = true; return true; }
        public void Retire(double now)
        {
            Reset(now);
            // Normal cooldown completion has an inclusive deadline. Its retired cycle
            // is already strictly older; a fresh tap exactly at that deadline is valid.
            strictFloor = false;
        }

        public static bool ValidReply(VentReply reply, double issued)
        {
            if (reply == null || reply.Offsets == null || reply.Offsets.Length == 0 ||
                reply.Offsets.Length > VentModel.ReplyLimit || !VentModel.Finite(reply.Start) ||
                !VentModel.Finite(reply.CooldownUntil) || !VentModel.Finite(issued) ||
                reply.Start < issued + 0.39d || reply.Start > issued + 2.01d ||
                reply.HeavyIndex < -1 || reply.HeavyIndex >= reply.Offsets.Length ||
                reply.Offsets[0] != 0f) return false;
            for (int i = 0; i < reply.Offsets.Length; i++)
                if (!VentModel.Finite(reply.Offsets[i]) || reply.Offsets[i] < 0 || reply.Offsets[i] > 5.81f ||
                    (i > 0 && reply.Offsets[i] - reply.Offsets[i - 1] < 0.099f)) return false;
            return reply.CooldownUntil >= reply.End + 0.99d && reply.CooldownUntil <= reply.End + 8.01d;
        }
    }
}
