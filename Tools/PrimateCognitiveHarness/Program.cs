using System;
using System.Linq;
using RunawayChimps.Toys.PrimateCognitive;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string name)
    {
        assertions++;
        if (!condition) throw new Exception("FAILED: " + name);
    }
    private static void Advance(CognitiveGame game, ref double now, double duration)
    {
        double end = now + duration;
        while (now < end)
        {
            now += .05;
            if (game.Owner != 0) game.Heartbeat(game.Owner, now);
            game.Tick(now, true);
        }
    }
    private static void InputPhase(CognitiveGame game, ref double now)
    {
        int limit = 1000;
        while (game.Phase != CognitivePhase.Input && limit-- > 0) Advance(game, ref now, .05);
        Check(limit > 0, "demonstration eventually enters input");
    }
    private static bool Press(CognitiveGame game, int button, double now) =>
        game.Press(game.Owner, button, game.Session, game.PhaseToken, now);

    private static void Sequences()
    {
        for (uint seed = 1; seed <= 128; seed++)
        {
            var game = new CognitiveGame(16, seed);
            double now = 0;
            Check(game.Phase == CognitivePhase.Idle && game.Owner == 0, "initial idle");
            Check(game.Start(7, 0, 0, now), "acquire");
            Check(game.Round == 1, "one initial step");
            Check(!game.Press(7, 0, game.Session, game.PhaseToken, now), "demo rejects input");
            byte[] previous = Array.Empty<byte>();
            for (int round = 1; round <= 16; round++)
            {
                InputPhase(game, ref now);
                var sequence = game.Snapshot().Sequence;
                Check(sequence.Length == round, "extends by one");
                Check(sequence.Take(previous.Length).SequenceEqual(previous), "preserves entire prefix");
                Check(!game.Press(8, sequence[0], game.Session, game.PhaseToken, now), "spectator rejected");
                Check(!game.Start(8, game.Session, game.PhaseToken, now), "spectator cannot restart");
                Check(!game.Press(7, -1, game.Session, game.PhaseToken, now), "invalid pad");
                Check(!game.Press(7, sequence[0], game.Session - 1, game.PhaseToken, now), "old session");
                Check(!game.Press(7, sequence[0], game.Session, game.PhaseToken - 1, now), "old phase");
                for (int i = 0; i < sequence.Length; i++)
                {
                    Check(Press(game, sequence[i], now), "correct input");
                    Check(game.InputIndex == i + 1, "one edge advances once");
                }
                Check(game.Phase == (round == 16 ? CognitivePhase.Complete : CognitivePhase.Success), "round transition");
                var copy = game.Snapshot();
                Check(CognitiveSnapshot.TryRead(copy.Pack(), out var decoded) && decoded.Sequence.SequenceEqual(sequence), "packet roundtrip");
                if (round != 16) Advance(game, ref now, game.SuccessSeconds + .1);
                previous = sequence;
            }
            Check(game.Round == 16, "maximum bounded");
            Check(!Press(game, 0, now), "complete rejects input");
            int oldSession = game.Session;
            Check(game.Start(7, game.Session, game.PhaseToken, now), "restart completed");
            Check(game.Session == oldSession + 1 && game.Round == 1, "restart new generation");
            InputPhase(game, ref now);
            int wrong = (game.Snapshot().Sequence[0] + 1) % 4;
            Check(Press(game, wrong, now), "wrong input accepted as an attempt");
            Check(game.Phase == CognitivePhase.Failure && game.Round == 1, "failure preserves round reached");
            Check(game.Snapshot().Assessment.Contains("doorstop"), "facility assessment");
            Check(game.Start(7, game.Session, game.PhaseToken, now), "immediate failure restart");
        }
    }

    private static void Recovery()
    {
        var game = new CognitiveGame();
        double now = 0;
        game.Start(1, 0, 0, now);
        game.Tick(4.01, true);
        Check(game.Owner == 0 && game.Phase == CognitivePhase.Idle && game.Abandoned, "heartbeat lease expired");
        Check(game.Start(2, game.Session, game.PhaseToken, 4.1), "new operator after abandonment");
        game.Tick(4.2, false);
        Check(game.Owner == 0, "sector/departure releases");
        game.Start(2, game.Session, game.PhaseToken, 5);
        game.Tick(4.9, true);
        Check(game.Owner == 0, "clock regression releases");
        now = 10;
        game.Start(2, game.Session, game.PhaseToken, now);
        InputPhase(game, ref now);
        Advance(game, ref now, game.InputTimeoutSeconds + .1);
        Check(game.Owner == 0 && game.Abandoned, "input inactivity despite heartbeat releases");
        game.Start(2, game.Session, game.PhaseToken, now);
        InputPhase(game, ref now);
        Press(game, (game.Snapshot().Sequence[0] + 1) % 4, now);
        Advance(game, ref now, game.ResultSeconds + .1);
        Check(game.Owner == 0 && !game.Abandoned, "results release for another player");
        Check(!game.Start(0, game.Session, game.PhaseToken, now), "actor zero invalid");
        Check(!game.Start(1, game.Session, game.PhaseToken, double.NaN), "NaN start invalid");
        Check(new CognitiveGame(999).Maximum == 16 && new CognitiveGame(-1).Maximum == 1, "length clamps");
        var malformedSettings = new CognitiveGame(flash: double.NaN, step: double.PositiveInfinity);
        Check(CognitiveGame.Finite(malformedSettings.StepSeconds) && malformedSettings.StepSeconds > malformedSettings.FlashSeconds, "finite pacing with visible gap");
        var single = new CognitiveGame(1);
        single.Start(1, 0, 0, now);
        InputPhase(single, ref now);
        Check(Press(single, single.Snapshot().Sequence[0], now) && single.Phase == CognitivePhase.Complete, "one-step maximum");
        Check(!single.Heartbeat(99, now), "spectator cannot extend lease");
        Check(!single.Start(1, single.Session - 1, single.PhaseToken, now), "duplicate start token");
    }

    private static void Contacts()
    {
        var gate = new CognitiveContactGate();
        gate.Reset(0);
        Check(!gate.Enter(1, 0, false), "initial overlap is not a press");
        gate.Tick(1);
        Check(!gate.Enter(1, 1), "rest never repeats");
        gate.Exit(1, 1);
        gate.Tick(1.09);
        Check(gate.Enter(1, 1.09), "fresh edge after release");
        Check(!gate.Enter(1, 1.1), "duplicate callback");
        Check(!gate.Enter(2, 1.1), "duplicate hand collider");
        gate.Exit(1, 1.2);
        gate.Tick(2);
        Check(!gate.Enter(3, 2), "second hand while occupied");
        gate.RequireRelease(2);
        Check(gate.Count == 2, "state change preserves contacts");
        gate.Tick(3);
        Check(!gate.Enter(2, 3), "held across input transition");
        gate.Exit(2, 3); gate.Exit(3, 3);
        gate.Tick(3.01);
        Check(!gate.Enter(1, 3.01), "release debounce");
        gate.Exit(1, 3.02); gate.Tick(3.11);
        Check(gate.Enter(1, 3.11), "debounce recovery");
        gate.Reset(4); gate.Tick(4.2);
        Check(!gate.Enter(1, 4.2, false), "trigger stay never invents an edge");
        gate.Exit(1, 4.3); gate.Tick(4.4);
        Check(gate.Enter(1, 4.4), "enabled again and fresh contact");
        // Full integration of gate and actual game transition, not an alternate implementation.
        var game = new CognitiveGame();
        double now = 5;
        game.Start(1, 0, 0, now);
        gate.Reset(now); gate.Tick(now + .1);
        Check(gate.Enter(20, now + .1), "physical edge during demo");
        Check(!Press(game, game.Snapshot().Sequence[0], now + .1), "demo edge does not count");
        InputPhase(game, ref now);
        gate.RequireRelease(now);
        Check(!gate.Enter(20, now), "same contact after demo not accepted");
        Check(game.InputIndex == 0, "still awaiting first input");
        gate.Exit(20, now); gate.Tick(now + .1);
        Check(gate.Enter(20, now + .1) && Press(game, game.Snapshot().Sequence[0], now + .1), "fresh input accepted");
    }

    private static void Protocol()
    {
        var game = new CognitiveGame();
        var idle = game.Snapshot();
        var replica = new CognitiveReplica();
        replica.Elect(1, "nonce-a");
        Check(!replica.Accept(1, "old-a", "", idle), "old broadcast before handshake");
        Check(!replica.Accept(2, "a", "nonce-a", idle), "foreign authority");
        Check(replica.Accept(1, "a", "nonce-a", idle), "initial current authority");
        Check(!replica.Accept(1, "a", "", idle), "duplicate revision");
        game.Start(7, 0, 0, 0);
        Check(replica.Accept(1, "a", "", game.Snapshot()), "new state");
        replica.Elect(2, "nonce-b");
        Check(!replica.Accept(1, "a", "", game.Snapshot()), "departed authority");
        Check(replica.Accept(2, "b", "nonce-b", idle), "B resets machine");
        replica.Elect(1, "nonce-new-a");
        Check(!replica.Accept(1, "a", "nonce-a", game.Snapshot()), "A-B-A stale reply");
        Check(replica.Accept(1, "new-a", "nonce-new-a", idle), "A fresh term");
        replica.RequestSync("restart-check");
        Check(replica.Accept(1, "re-enabled-a", "restart-check", idle), "same actor component re-enable recovery");
        Check(!replica.Accept(1, "new-a", "restart-check", game.Snapshot()), "consumed nonce cannot resurrect old term");
        replica.Reset();
        Check(!replica.Ready && replica.Authority == 0, "room identity reset");
        Check(!CognitiveSnapshot.TryRead(null, out _), "null snapshot");
        Check(!CognitiveSnapshot.TryRead(new object[0], out _), "empty snapshot");
        object[] packet = game.Snapshot().Pack();
        packet[5] = new byte[17];
        Check(!CognitiveSnapshot.TryRead(packet, out _), "oversized sequence");
        packet = game.Snapshot().Pack(); packet[5] = new byte[] { 4 };
        Check(!CognitiveSnapshot.TryRead(packet, out _), "invalid symbol");
        packet = game.Snapshot().Pack(); packet[10] = double.NaN;
        Check(!CognitiveSnapshot.TryRead(packet, out _), "nonfinite timestamp");
        packet = idle.Pack(); packet[0] = 9;
        Check(!CognitiveSnapshot.TryRead(packet, out _), "idle occupancy impossible");
        packet = game.Snapshot().Pack(); packet[4] = (int)CognitivePhase.Complete;
        Check(!CognitiveSnapshot.TryRead(packet, out _), "impossible completion");
    }

    private static void DelayedHandshake()
    {
        // Retries every .5 seconds must not invalidate a .75+ second reply.
        var replica = new CognitiveReplica();
        var idle = new CognitiveGame().Snapshot();
        replica.Elect(1, "initial-request");
        string inFlight = replica.RequestNonce;
        for (int retry = 0; retry < 20; retry++)
            replica.RequestSync("retry-" + retry);
        Check(replica.Accept(1, "current-term", inFlight, idle), "delayed initial reply survives retries");
        Check(replica.RequestNonce == "", "accepted reply consumes outstanding request");
        replica.RequestSync("restart-request");
        inFlight = replica.RequestNonce;
        replica.RequestSync("retry-after-restart");
        Check(replica.Accept(1, "restarted-term", inFlight, idle), "delayed same-actor recovery survives retries");
        Check(!replica.Accept(1, "current-term", inFlight, idle), "consumed nonce cannot revive old controller term");
        replica.RequestSync("same-state");
        Check(!replica.Accept(1, "restarted-term", "same-state", idle) && replica.RequestNonce == "",
            "unchanged revision still acknowledges synchronization");
        replica.RequestSync("before-election");
        replica.Elect(2, "after-election");
        Check(!replica.Accept(1, "restarted-term", "before-election", idle), "pending reply from departed controller rejected");
        Check(replica.Accept(2, "new-controller", "after-election", idle), "new controller gets independent request");
    }

    private static void Main()
    {
        Sequences(); Recovery(); Contacts(); Protocol(); DelayedHandshake();
        Console.WriteLine("PASS: " + assertions + " assertions against production cognitive rules, contact gate and replica protocol.");
        Console.WriteLine("No Unity engine, native physics, live Photon or headset execution is implied.");
    }
}
