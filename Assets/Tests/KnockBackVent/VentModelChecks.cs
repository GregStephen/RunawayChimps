using System;
using RunawayChimps.Toys.KnockBack;

namespace RunawayChimps.Tests.KnockBack
{
    // No Unity or NUnit dependency here: this exact suite runs in the Editor and managed CI.
    public static class VentModelChecks
    {
        public static readonly string[] Cases = {
            "Timing", "QuietDeadline", "Bounded", "SimultaneousUsers", "Variations", "Cooldown",
            "CancelEveryPhase", "FreshRelease", "RestingContact", "CompoundContact", "TrackingReset",
            "DeliveryOrdering", "ContextCancellation", "SameTickRetirement", "InclusiveCooldownFloor", "MalformedReply", "InvalidInput", "Stress"
        };
        public static int Assertions { get; private set; }
        private static void Require(bool value, string reason)
        {
            Assertions++;
            if (!value) throw new InvalidOperationException(reason);
        }
        private static void Near(double value, double expected, string reason) => Require(Math.Abs(value - expected) < 0.00001, reason);
        private static VentModel Recorded()
        {
            var model = new VentModel(new VentSettings());
            Require(model.TryTap(1, 10, 10.05), "first tap");
            Require(model.TryTap(1, 10.22, 10.27), "second tap");
            Require(model.TryTap(1, 10.58, 10.61), "third tap with different transit latency");
            return model;
        }
        private static bool Sample(VentContactGate gate, double now, bool contact, bool released, float speed = 0.8f, bool valid = true) =>
            gate.Sample(now, valid, contact, released, speed, 0.3f, 0.07f);
        private static void Arm(VentContactGate gate, double start)
        { Sample(gate, start, false, true); Sample(gate, start + 0.08, false, true); }

        public static void Run(string name)
        {
            switch (name)
            {
                case "Timing":
                {
                    var model = Recorded();
                    Require(model.Phase == VentPhase.Recording, "recording");
                    Require(model.Advance(11.25, 0.9) == null, "quiet interval not elapsed");
                    var plan = model.Advance(11.27, 0.9);
                    Require(plan != null && plan.Offsets.Length == 3 && plan.HeavyIndex == -1, "basic exact repeat");
                    Near(plan.Start, 12.02, "configured reply delay");
                    Near(plan.Offsets[1], 0.22, "tap interval preserved, not packet interval");
                    Near(plan.Offsets[2], 0.58, "longer interval preserved");
                    Require(model.Advance(11.28, 0) == null, "plan emitted once; second roll does not change it");
                    Require(plan.Offsets.Length == 3, "immutable chosen variation");
                    Require(!model.TryTap(1, 11.3, 11.3), "no queue while replying");
                    break;
                }
                case "QuietDeadline":
                {
                    var model = new VentModel(new VentSettings { quietInterval = 0.5f });
                    Require(model.TryTap(1, 1, 1), "begin");
                    Require(!model.TryTap(1, 1.5, 1.5), "tap at quiet deadline must not revive sequence before Advance");
                    Require(model.Advance(1.5, 0.9) != null, "reply at deadline");
                    break;
                }
                case "Bounded":
                {
                    var model = new VentModel(new VentSettings { maxTaps = 1000 });
                    for (int i = 0; i < 8; i++) Require(model.TryTap(1, 1 + i * 0.2, 1 + i * 0.2), "bounded accepted tap");
                    for (int i = 0; i < 1000; i++) Require(!model.TryTap(1, 2.6, 2.6), "overflow rejected");
                    Require(model.Count == 8, "hard eight tap cap");
                    Require(model.Advance(3.1, 0).Offsets.Length == 9, "one extra only");
                    var shortModel = new VentModel(new VentSettings { maximumRecording = 1f, maxTaps = 8 });
                    for (int i = 0; i < 5; i++) Require(shortModel.TryTap(1, 4 + i * 0.2, 4 + i * 0.2), "max duration input");
                    Require(!shortModel.TryTap(1, 5, 5), "maximum duration before Update");
                    Require(shortModel.Advance(5, 0.99) != null, "maximum recording closes without waiting indefinitely");
                    break;
                }
                case "SimultaneousUsers":
                {
                    var model = new VentModel(new VentSettings());
                    Require(model.TryTap(2, 1, 1), "first arrival owns recording");
                    Require(!model.TryTap(1, 1, 1), "simultaneous contender ignored");
                    Require(!model.TryTap(1, 1.3, 1.3), "other player's later taps not mixed");
                    Require(model.TryTap(2, 1.3, 1.3), "first player's rhythm continues");
                    Require(model.Owner == 2 && model.Count == 2, "one owner");
                    break;
                }
                case "Variations":
                {
                    var basic = Recorded().Advance(11.3, 0.9);
                    var extra = Recorded().Advance(11.3, 0.01);
                    var heavy = Recorded().Advance(11.3, 0.08);
                    Require(basic.Offsets.Length == 3 && basic.HeavyIndex == -1, "basic");
                    Require(extra.Offsets.Length == 4 && extra.HeavyIndex == -1, "extra only");
                    Near(extra.Offsets[3] - extra.Offsets[2], 0.28, "extra configured gap");
                    Require(heavy.Offsets.Length == 3 && heavy.HeavyIndex == 2, "single final heavy replacement");
                    var settings = new VentSettings { extraKnockChance = 1, heavyBangChance = 1 }.Bounded();
                    Require(settings.extraKnockChance + settings.heavyBangChance <= 0.20001, "variants capped uncommon");
                    var noVariation = new VentModel(new VentSettings { extraKnockChance = 0, heavyBangChance = 0 });
                    noVariation.TryTap(1, 1, 1);
                    Require(noVariation.Advance(2, 0).HeavyIndex == -1, "zero chances mean exact repeat");
                    break;
                }
                case "Cooldown":
                {
                    var model = Recorded();
                    var plan = model.Advance(11.3, 0.9);
                    model.Advance(plan.End, 0.9);
                    Require(model.Phase == VentPhase.Cooldown, "cooldown starts after audio tail");
                    Require(!model.TryTap(1, plan.End + 0.1, plan.End + 0.1), "cooldown input discarded, not queued");
                    model.Advance(plan.CooldownUntil - 0.001, 0.9);
                    Require(model.Phase == VentPhase.Cooldown, "full cooldown");
                    model.Advance(plan.CooldownUntil, 0.9);
                    Require(model.Phase == VentPhase.Idle && model.Owner == 0 && model.Count == 0, "returns clean idle");
                    Require(model.TryTap(2, plan.CooldownUntil, plan.CooldownUntil), "new owner after cooldown");
                    break;
                }
                case "CancelEveryPhase":
                {
                    for (int phase = 0; phase < 4; phase++)
                    {
                        var model = new VentModel(new VentSettings());
                        if (phase > 0) model.TryTap(1, 1, 1);
                        VentReply plan = phase > 1 ? model.Advance(2, 0.01) : null;
                        if (phase > 2) model.Advance(plan.End, 0.9);
                        Require((int)model.Phase == phase, "exercise each phase");
                        model.Cancel();
                        model.Cancel();
                        Require(model.Phase == VentPhase.Idle && model.Owner == 0 && model.Count == 0 && model.Reply == null, "idempotent complete cancellation");
                        Require(model.Advance(100, 0) == null, "cancellation cannot create delayed reply");
                        Require(model.TryTap(2, 101, 101), "new session independent of old phase");
                    }
                    break;
                }
                case "FreshRelease":
                {
                    var gate = new VentContactGate();
                    Require(!Sample(gate, 0, true, false), "initial overlap not a tap");
                    Arm(gate, 1);
                    Require(Sample(gate, 1.1, true, false), "released and moving inward");
                    Require(!Sample(gate, 1.2, true, false), "continuous contact ignored");
                    Sample(gate, 1.21, false, true);
                    Require(!Sample(gate, 1.23, true, false), "brief release jitter ignored");
                    Arm(gate, 2);
                    Require(Sample(gate, 2.1, true, false), "genuine fresh tap");
                    break;
                }
                case "RestingContact":
                {
                    var gate = new VentContactGate();
                    Arm(gate, 1);
                    Require(!Sample(gate, 1.1, true, false, 0.05f), "slow resting contact silent");
                    for (int i = 0; i < 100; i++) Require(!Sample(gate, 1.2 + i * 0.1, true, false, 2f), "speed while already touching cannot rearm");
                    break;
                }
                case "CompoundContact":
                {
                    var gate = new VentContactGate();
                    Arm(gate, 1);
                    Require(Sample(gate, 1.1, true, false), "first hand observation");
                    for (int collider = 0; collider < 8; collider++) Require(!Sample(gate, 1.1, true, false), "multiple observations of one hand do not multiply taps");
                    break;
                }
                case "TrackingReset":
                {
                    var gate = new VentContactGate();
                    Arm(gate, 1);
                    Require(!Sample(gate, 1.1, true, false, 4f, false), "teleport/tracking/gap rejection");
                    Require(!Sample(gate, 1.2, true, false, 1f), "recovered tracking still requires release");
                    Arm(gate, 2);
                    gate.Reset();
                    Require(!Sample(gate, 2.1, true, false), "recenter clears arming");
                    Arm(gate, 3);
                    Require(Sample(gate, 3.1, true, false), "fresh post-reset tap");
                    break;
                }
                case "DeliveryOrdering":
                {
                    var gate = new VentDeliveryGate();
                    gate.Reset(1);
                    Require(gate.Header(2, 2, 2.1, 3) && gate.Tap(0), "live accepted tap");
                    Require(!gate.Tap(0) && !gate.Tap(-1) && !gate.Tap(8), "duplicate and invalid ordinals");
                    Require(!gate.Header(2, 2.1, 2.2, 4), "owner cannot change within cycle");
                    Require(gate.Header(2, 2.2, 2.3, 3) && gate.Tap(1), "next tap");
                    Require(gate.Seal() && !gate.Seal() && !gate.Tap(2), "one sealed reply; late tap cannot reopen it");
                    Require(!gate.Header(1.9, 2.4, 2.5, 3), "old cycle rejected");
                    Require(!gate.Header(3, 4, 3, 3), "future packet rejected");
                    Require(!gate.Header(2, 2.2, 4, 3), "stale packet rejected");
                    break;
                }
                case "ContextCancellation":
                {
                    var gate = new VentDeliveryGate();
                    gate.Reset(1);
                    Require(gate.Header(2, 2, 2, 1), "A term");
                    gate.Reset(3); // room, sector, authority A -> B
                    Require(!gate.Header(2, 3.1, 3.1, 1), "old term cannot resume even with a recent issued time");
                    gate.Reset(4); // B -> A
                    Require(!gate.Header(2, 4.1, 4.1, 1), "A return cannot replay its first term");
                    Require(gate.Header(4.1, 4.1, 4.2, 1), "fresh new term");
                    var lateArrival = new VentDeliveryGate();
                    lateArrival.Reset(5);
                    Require(!lateArrival.Header(4.1, 5.1, 5.1, 1), "late arrival receives no old cycle");
                    gate.Retire(6);
                    Require(!gate.Header(4.1, 6.1, 6.1, 1), "completed cycle not replayed");
                    break;
                }
                case "SameTickRetirement":
                {
                    var gate = new VentDeliveryGate();
                    gate.Reset(9);
                    Require(gate.Header(10, 10, 10, 1) && gate.Tap(0), "original live tap");
                    gate.Reset(10);
                    Require(!gate.Header(10, 10, 10, 1), "equal-timestamp retired cycle cannot reappear");
                    Require(!gate.AcceptsInput(10), "queued equal-timestamp request cannot seed another authority term");
                    Require(gate.AcceptsInput(10.001) && gate.Header(10.001, 10.001, 10.001, 2), "fresh millisecond admits new owner");
                    Require(!gate.AcceptsInput(double.NaN) && !gate.AcceptsInput(double.PositiveInfinity), "input floor fails closed on nonfinite data");
                    break;
                }
                case "InclusiveCooldownFloor":
                {
                    var gate = new VentDeliveryGate();
                    gate.Reset(9);
                    Require(gate.Header(10, 10, 10, 1), "original cycle");
                    gate.Retire(15);
                    Require(!gate.AcceptsInput(14.999), "cooldown input is discarded");
                    Require(gate.AcceptsInput(15) && gate.Header(15, 15, 15, 2), "fresh tap at exact cooldown deadline works");
                    Require(!gate.Header(10, 15, 15, 1), "inclusive deadline does not revive older cycle");
                    break;
                }
                case "MalformedReply":
                {
                    var good = new VentReply(2, 5, new float[] { 0, 0.5f }, -1);
                    Require(VentDeliveryGate.ValidReply(good, 1.25), "well formed reply");
                    Require(!VentDeliveryGate.ValidReply(new VentReply(2, 5, new float[0], -1), 1.25), "empty pattern");
                    Require(!VentDeliveryGate.ValidReply(new VentReply(2, 5, new float[10], -1), 1.25), "unbounded pattern");
                    Require(!VentDeliveryGate.ValidReply(new VentReply(2, 5, new float[] { 0, float.NaN }, -1), 1.25), "NaN interval");
                    Require(!VentDeliveryGate.ValidReply(new VentReply(2, 5, new float[] { 0, 0.05f }, -1), 1.25), "burst interval");
                    Require(!VentDeliveryGate.ValidReply(new VentReply(2, 5, new float[] { 0, 0.5f }, 2), 1.25), "invalid heavy index");
                    Require(!VentDeliveryGate.ValidReply(new VentReply(double.PositiveInfinity, 5, new float[] { 0 }, -1), 1.25), "invalid time");
                    Require(!VentDeliveryGate.ValidReply(new VentReply(2, double.PositiveInfinity, new float[] { 0 }, -1), 1.25), "infinite cooldown");
                    break;
                }
                case "InvalidInput":
                {
                    var model = new VentModel(new VentSettings { quietInterval = float.NaN });
                    Require(model.Settings.quietInterval == 0.65f, "invalid Inspector setting repaired");
                    Require(!model.TryTap(0, 1, 1), "invalid actor");
                    Require(!model.TryTap(1, double.NaN, 1), "invalid input timestamp");
                    Require(!model.TryTap(1, 2, 1), "future input");
                    Require(!model.TryTap(1, 1, 2), "old input");
                    Require(model.TryTap(1, 3, 3), "valid input");
                    Require(!model.TryTap(1, 3, 3.05), "duplicate request");
                    Require(!model.TryTap(1, 2.9, 3.1), "out of order request");
                    Require(model.Advance(double.NaN, 0) == null, "invalid clock does not fire");
                    break;
                }
                case "Stress":
                {
                    var rng = new Random(4242);
                    var model = new VentModel(new VentSettings());
                    double now = 100;
                    for (int run = 0; run < 500; run++)
                    {
                        Require(model.TryTap(1, now, now), "fresh stress cycle");
                        int count = rng.Next(1, 7);
                        double input = now;
                        for (int i = 1; i < count; i++)
                        {
                            input += 0.14 + rng.NextDouble() * 0.4;
                            Require(model.TryTap(1, input, input + 0.02), "stress tap");
                        }
                        var plan = model.Advance(input + 0.7, rng.NextDouble());
                        Require(plan != null && plan.Offsets.Length <= 7 && VentDeliveryGate.ValidReply(plan, input + 0.7), "bounded valid plan");
                        model.Cancel();
                        Require(model.Advance(plan.CooldownUntil, 0) == null, "cancel prevents old replies");
                        now = plan.CooldownUntil + 1;
                    }
                    break;
                }
                default: throw new ArgumentException("Unknown check: " + name);
            }
        }
    }
}
