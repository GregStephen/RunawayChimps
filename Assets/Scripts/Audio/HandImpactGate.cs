/// <summary>
/// Per-hand contact state. Playback is possible only on the first contact after
/// a stable release, so changing pressure or crossing collider seams cannot
/// produce another impact while the hand remains planted.
/// This class deliberately has no Unity dependency; its transitions are tested
/// against the same production source by the managed regression harness.
/// </summary>
public sealed class HandImpactGate
{
    private bool armed;
    private bool hasClock;
    private bool hasImpact;
    private bool observingRelease;
    private int releaseSamples;
    private float lastObservation;
    private float releaseStarted;
    private float lastImpact;

    public void Reset(float now)
    {
        armed = false;
        observingRelease = false;
        releaseSamples = 0;
        hasImpact = false;
        hasClock = Finite(now) && now >= 0f;
        lastObservation = hasClock ? now : 0f;
    }

    /// <param name="speed">Speed into the contact normal, without artificial solver gravity.</param>
    public bool Observe(bool touching, float speed, float now, float minImpact, float interval, float rearmTime)
    {
        if (!Finite(now) || now < 0f || (hasClock && now < lastObservation))
        {
            Reset(now);
            return false;
        }

        hasClock = true;
        lastObservation = now;

        // An invalid sample cannot arm a hand or defer a strike until later in
        // the same contact. The next valid release must establish a fresh start.
        if (!Finite(speed) || speed < 0f || !Finite(minImpact) || minImpact < 0f ||
            !Finite(interval) || interval < 0f || !Finite(rearmTime) || rearmTime < 0f)
        {
            armed = false;
            observingRelease = false;
            releaseSamples = 0;
            return false;
        }

        if (!touching)
        {
            if (!observingRelease)
            {
                observingRelease = true;
                releaseStarted = now;
                releaseSamples = 1;
            }
            else if (releaseSamples < 2)
                releaseSamples++;
            if (ReleaseComplete(now, rearmTime))
                armed = true;
            return false;
        }

        // Include the elapsed interval up to this contact. Requiring a second
        // released observation still rejects a lone missing-contact frame.
        bool freshContact = armed || ReleaseComplete(now, rearmTime);
        observingRelease = false;
        releaseSamples = 0;
        armed = false;

        // Consume every first contact, including a gentle touch or a strike
        // inside the minimum interval. Neither may play later while resting.
        if (!freshContact || speed <= 0f || speed < minImpact ||
            (hasImpact && now - lastImpact < interval))
            return false;

        hasImpact = true;
        lastImpact = now;
        return true;
    }

    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private bool ReleaseComplete(float now, float rearmTime)
    {
        return observingRelease && (rearmTime == 0f || releaseSamples >= 2) &&
               now - releaseStarted >= rearmTime;
    }
}
