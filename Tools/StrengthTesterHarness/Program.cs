using System;
using RunawayChimps.Hub;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static bool Sample(StrengthHitState state, float z, out int score,
        float x = 0f, float y = 0f, float speed = 3f, float dt = 0.02f)
        => state.Sample(x, y, z, dt, speed, 0.25f, 0.18f, 0.06f, 0.4f, 5.5f, out score);

    private static void Arm(StrengthHitState state, float x = 0f, float dt = 0.02f)
    {
        for (int i = 0; i < (int)Math.Ceiling(0.15f / dt) + 2; i++)
            Check(!Sample(state, 0.20f, out _, x: x, dt: dt), "Withdrawal cannot hit");
    }

    private static void Main()
    {
        var state = new StrengthHitState();
        Check(!Sample(state, 0f, out _), "Spawning in pad cannot hit");
        for (int i = 0; i < 100; i++) Check(!Sample(state, 0f, out _), "Resting palm cannot hit");
        Arm(state);
        Check(Sample(state, 0.03f, out int score) && score > 1 && score < 999, "Deliberate strike scores");
        for (int i = 0; i < 100; i++)
            Check(!Sample(state, i % 2 == 0 ? 0.065f : 0.055f, out _), "Contact jitter cannot repeat");
        Arm(state);
        Check(Sample(state, 0.03f, out _, speed: 5.5f), "Fresh swing after withdrawal scores");

        state.Reset(); Arm(state, x: 0.4f);
        Check(!Sample(state, -0.10f, out _, x: 0.4f), "Miss beside pad rejected");
        state.Reset(); Arm(state);
        Check(!Sample(state, -0.10f, out _, y: 0.5f), "Miss above pad rejected at swept intersection");
        state.Reset(); Arm(state);
        Check(Sample(state, -0.20f, out _), "Fast pass through thin pad detected");
        state.Reset();
        for (int i = 0; i < 10; i++) Sample(state, -0.1f, out _);
        Check(!Sample(state, 0.03f, out _), "Back face rejected");
        Check(!Sample(state, 0.1f, out _), "Withdrawing through front is not a hit");
        Check(!Sample(state, 0.03f, out _), "Insufficient withdrawal remains disarmed");
        state.Reset(); Arm(state);
        Check(!Sample(state, 0.03f, out _, speed: 0.1f), "Slow touch rejected");
        Check(!Sample(state, -0.05f, out _, speed: 5f), "Accelerating after touching cannot score");
        state.Reset(); Arm(state);
        Check(!Sample(state, 0.03f, out _, speed: 0f), "Rig-only motion rejected");
        state.Reset(); Arm(state);
        Check(!Sample(state, -1f, out _), "Teleport-sized displacement rejected");
        state.Reset(); Arm(state);
        Check(!Sample(state, 0.03f, out _, dt: 0.25f), "Frame stall cannot score");
        Check(!Sample(state, 0.02f, out _), "Frame-stall recovery requires fresh withdrawal");
        state.Reset(); Arm(state);state.Reset();
        Check(!Sample(state, 0.03f, out _), "Pause/travel reset cannot score stale swing");
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            state.Reset(); Arm(state);
            Check(!Sample(state, bad, out _), "Invalid pose rejected");
            state.Reset(); Arm(state);
            Check(!Sample(state, 0.03f, out _, speed: bad), "Invalid speed rejected");
        }
        state.Reset(); Arm(state);
        Check(!Sample(state, 0.03f, out _, speed: 16f), "Tracking-speed spike rejected");
        Check(StrengthHitState.Score(0.39f, 0.4f, 5.5f) == 0, "Below-threshold score");
        Check(StrengthHitState.Score(0.4f, 0.4f, 5.5f) == 1, "Minimum score");
        Check(StrengthHitState.Score(5.5f, 0.4f, 5.5f) == 999, "Maximum score");
        Check(StrengthHitState.Score(8f, 0.4f, 5.5f) == 999, "Score clamped");
        Check(StrengthHitState.Score(3f, 2f, 1f) == 0, "Invalid tuning rejected");

        foreach (int fps in new[] { 36, 72, 90, 120 })
        {
            float dt = 1f / fps;
            state.Reset(); Arm(state, dt: dt);
            int hits = 0, result = 0;
            for (float z = 0.20f - 3f * dt; z > -0.3f; z -= 3f * dt)
                if (Sample(state, z, out int value, speed: 3f, dt: dt)) { hits++; result = value; }
            Check(hits == 1, "One hit at " + fps + " FPS");
            Check(result == StrengthHitState.Score(3f, 0.4f, 5.5f), "Consistent score at " + fps + " FPS");
        }
        Console.WriteLine("PASS: " + assertions + " production contact/scoring assertions. Unity/XR/Photon execution remains separate.");
    }
}
