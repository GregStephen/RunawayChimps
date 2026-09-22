using System;

// Runs the production HandImpactGate, not a rewritten simulation. Native Unity
// contacts, XR tracking, spatial audio and headset sound quality remain separate.
internal static class Program
{
    private const float Threshold = 0.8f;
    private const float Interval = 0.06f;
    private const float ReleaseTime = 0.02f;
    private static int assertions;

    private static void Check(bool condition, string description)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(description);
    }

    private static bool Observe(HandImpactGate gate, bool contact, float speed, float now,
        float threshold = Threshold, float interval = Interval, float release = ReleaseTime)
    {
        return gate.Observe(contact, speed, now, threshold, interval, release);
    }

    private static void Release(HandImpactGate gate, float now)
    {
        Check(!Observe(gate, false, 0f, now), "Leaving a surface played an impact");
        Check(!Observe(gate, false, 0f, now + ReleaseTime + 0.001f), "Airborne hand played an impact");
    }

    private static HandImpactGate Armed(float start = 0f)
    {
        var gate = new HandImpactGate();
        gate.Reset(start);
        Release(gate, start);
        return gate;
    }

    private static void StartupAndResting()
    {
        var gate = new HandImpactGate();
        Check(!Observe(gate, true, 20f, 0f), "An initial unobserved contact played");
        Check(!Observe(gate, true, 20f, 1f), "Waiting at startup armed a planted hand");
        Release(gate, 2f);
        Check(Observe(gate, true, 2f, 2.03f), "First intentional contact after release was missed");
        for (int frame = 1; frame <= 1000; frame++)
            Check(!Observe(gate, true, frame % 2 == 0 ? 0f : 20f, 2.03f + frame * 0.01f),
                "Resting, pressure changes or collider seams repeated an impact");
    }

    private static void GentleAndCooldownContactsAreConsumed()
    {
        foreach (float speed in new[] { 0f, 0.3f, float.NaN, float.PositiveInfinity, -2f })
        {
            HandImpactGate gate = Armed();
            Check(!Observe(gate, true, speed, 0.03f), "A gentle or invalid first contact played");
            Check(!Observe(gate, true, 3f, 0.05f), "A gentle/invalid contact played when pressure increased");
            Check(!Observe(gate, true, 3f, 5f), "A held contact played after waiting");
            Release(gate, 6f);
            Check(Observe(gate, true, 3f, 6.03f), "Release did not recover from gentle/invalid contact");
        }

        HandImpactGate limited = Armed();
        Check(Observe(limited, true, 3f, 0.03f), "Initial limited impact missed");
        Release(limited, 0.031f);
        Check(!Observe(limited, true, 3f, 0.055f), "Cooldown did not limit a fast impact");
        Check(!Observe(limited, true, 3f, 0.2f), "An impact delayed until cooldown expired while planted");
        Release(limited, 0.3f);
        Check(Observe(limited, true, 3f, 0.33f), "Cooldown-limited contact did not recover after release");
    }

    private static void BriefQueryHolesDoNotRearm()
    {
        foreach (float rate in new[] { 72f, 90f, 120f })
        {
            float dt = 1f / rate;
            HandImpactGate gate = Armed();
            Check(Observe(gate, true, 3f, 0.1f), "Initial headset-rate impact missed");
            for (int frame = 1; frame <= 500; frame++)
            {
                // A planted hand alternates between one missing-contact frame and
                // a colliding frame; this also represents mesh/collider seams.
                bool touching = frame % 2 == 0;
                Check(!Observe(gate, touching, touching ? 4f : 0f, 0.1f + dt * frame),
                    "Single-frame contact dropout rearmed at " + rate + " Hz");
            }
            Release(gate, 10f);
            Check(Observe(gate, true, 3f, 10.03f), "A real release was lost after query chatter");
        }
    }

    private static void RapidDistinctTapsAndIndependentHands()
    {
        var left = Armed();
        var right = Armed();
        for (int tap = 0; tap < 1000; tap++)
        {
            float at = 0.1f + tap * 0.12f;
            Check(Observe(left, true, 2f, at), "Left distinct tap missed");
            Check(Observe(right, true, 2f, at), "Right tap was limited by left hand");
            Check(!Observe(left, true, 5f, at + 0.01f), "Left continuous contact duplicated");
            Check(!Observe(right, true, 5f, at + 0.01f), "Right continuous contact duplicated");
            Release(left, at + 0.04f);
            Release(right, at + 0.04f);
        }
    }

    private static void ReleaseThresholdCanMatureAtNextContact()
    {
        foreach (float rate in new[] { 72f, 90f, 120f })
        {
            float dt = 1f / rate;
            HandImpactGate gate = Armed();
            Check(Observe(gate, true, 3f, 0.1f), "Initial threshold-crossing strike missed");
            int releaseSamples = Math.Max(2, (int)Math.Ceiling(ReleaseTime / dt));
            for (int sample = 0; sample < releaseSamples; sample++)
                Check(!Observe(gate, false, 0f, 0.3f + sample * dt), "Release sample played");
            Check(Observe(gate, true, 3f, 0.3f + releaseSamples * dt),
                "Release interval maturing at contact was lost at " + rate + " Hz");
        }

        HandImpactGate loneGap = Armed();
        Check(Observe(loneGap, true, 3f, 0.1f), "Initial lone-gap strike missed");
        Check(!Observe(loneGap, false, 0f, 0.3f), "Lone release observation played");
        Check(!Observe(loneGap, true, 3f, 0.4f), "One missing-contact frame rearmed after a frame stall");
    }

    private static void ResetAndClockValidation()
    {
        foreach (float resetTime in new[] { 0.5f, float.NaN, float.PositiveInfinity, -1f })
        {
            HandImpactGate gate = Armed();
            Check(Observe(gate, true, 3f, 0.03f), "Pre-reset strike missed");
            gate.Reset(resetTime);
            Check(!Observe(gate, true, 100f, 1f), "Teleport/reset played artificial movement");
            Check(!Observe(gate, true, 3f, 2f), "Reset armed a hand that remained planted");
            Release(gate, 3f);
            Check(Observe(gate, true, 3f, 3.03f), "Reset failed to recover after release");
        }

        foreach (float badTime in new[] { 0.01f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            HandImpactGate gate = Armed(1f);
            Check(!Observe(gate, true, 3f, badTime), "Invalid or backwards time played");
            Check(!Observe(gate, true, 3f, 2f), "Clock reset did not consume the contact");
            Release(gate, 3f);
            Check(Observe(gate, true, 3f, 3.03f), "Clock reset failed to recover");
        }
    }

    private static void ConfigurationAndBoundaries()
    {
        foreach (float invalid in new[] { -0.1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            for (int field = 0; field < 3; field++)
            {
                HandImpactGate gate = Armed();
                Check(!Observe(gate, true, 3f, 0.03f,
                    field == 0 ? invalid : Threshold,
                    field == 1 ? invalid : Interval,
                    field == 2 ? invalid : ReleaseTime), "Invalid configuration played");
                Check(!Observe(gate, true, 3f, 1f), "Fixing configuration played an already-held contact");
                Release(gate, 2f);
                Check(Observe(gate, true, 3f, 2.03f), "Valid configuration failed after release");
            }
        }

        HandImpactGate zero = new HandImpactGate();
        Check(!Observe(zero, false, 0f, 0f, 0f, 0f, 0f), "Zero-time release played");
        Check(!Observe(zero, true, 0f, 0f, 0f, 0f, 0f), "Stationary contact played at zero threshold");
        Check(!Observe(zero, false, 0f, 0.01f, 0f, 0f, 0f), "Zero-time rearm played");
        Check(Observe(zero, true, 0.0001f, 0.02f, 0f, 0f, 0f), "Positive speed at zero thresholds was lost");

        HandImpactGate exact = new HandImpactGate();
        Observe(exact, false, 0f, 0f, Threshold, 0.125f, 0.125f);
        Observe(exact, false, 0f, 0.125f, Threshold, 0.125f, 0.125f);
        Check(Observe(exact, true, Threshold, 0.25f, Threshold, 0.125f, 0.125f), "Exact impact threshold rejected");
        Observe(exact, false, 0f, 0.375f, Threshold, 0.125f, 0.125f);
        Observe(exact, false, 0f, 0.5f, Threshold, 0.125f, 0.125f);
        Check(Observe(exact, true, Threshold, 0.5f, Threshold, 0.25f, 0.125f), "Exact minimum interval rejected");
    }

    private static void Main()
    {
        StartupAndResting();
        GentleAndCooldownContactsAreConsumed();
        BriefQueryHolesDoNotRearm();
        RapidDistinctTapsAndIndependentHands();
        ReleaseThresholdCanMatureAtNextContact();
        ResetAndClockValidation();
        ConfigurationAndBoundaries();
        Console.WriteLine("PASS: " + assertions + " assertions against production HandImpactGate.");
        Console.WriteLine("MANAGED CONTACT STATE ONLY: Unity contacts, XR tracking, spatial audio, Photon and headset validation remain separate.");
    }
}
