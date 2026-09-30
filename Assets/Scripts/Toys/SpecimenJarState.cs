using System;

namespace RunawayChimps.Toys
{
    public enum SpecimenMood { Idle, Interested, Recoiling, Watching }

    /// <summary>Pure local policy. No Unity timing, global RNG, colliders or network writes.</summary>
    public sealed class SpecimenJarState
    {
        public SpecimenMood Mood { get; private set; }
        public int Hand { get; private set; } = -1;
        public int ReactionCount { get; private set; }
        public int WatchCount { get; private set; }
        private float nextTap, recoilUntil, nextWatch, watchUntil, awaySince;
        private bool seen, awayAttempted;

        public void Reset(float now, float watchCooldown)
        {
            Mood = SpecimenMood.Idle;
            Hand = -1;
            ReactionCount = WatchCount = 0;
            nextTap = recoilUntil = watchUntil = now;
            nextWatch = now + Math.Max(0f, watchCooldown);
            awaySince = -1f;
            seen = awayAttempted = false;
        }

        public bool TryTap(float now, float cooldown, float recoilSeconds)
        {
            if (!Finite(now) || now < nextTap || now < recoilUntil) return false;
            nextTap = now + Math.Max(0.01f, cooldown);
            recoilUntil = now + Math.Max(0.01f, recoilSeconds);
            watchUntil = now;
            Mood = SpecimenMood.Recoiling;
            Hand = -1;
            ReactionCount++;
            return true;
        }

        // roll is supplied only by this jar's private RNG; a look-away episode rolls once.
        public void Step(float now, int nearestHand, bool clearlyVisible, bool definitelyAway,
            float awayDelay, float watchCooldown, float watchSeconds, float chance, float roll)
        {
            if (clearlyVisible)
            {
                seen = true;
                awaySince = -1f;
                awayAttempted = false;
            }
            else if (!definitelyAway)
            {
                awaySince = -1f;
            }
            else if (awaySince < 0f)
            {
                awaySince = now;
            }

            if (now < recoilUntil)
            {
                Mood = SpecimenMood.Recoiling;
                Hand = -1;
                return;
            }
            Hand = nearestHand == 0 || nearestHand == 1 ? nearestHand : -1;
            if (Hand >= 0)
            {
                watchUntil = now;
                Mood = SpecimenMood.Interested;
                return;
            }
            if (seen && definitelyAway && !clearlyVisible && !awayAttempted &&
                awaySince >= 0f && now - awaySince >= Math.Max(0.05f, awayDelay) && now >= nextWatch)
            {
                awayAttempted = true;
                nextWatch = now + Math.Max(0.1f, watchCooldown);
                if (roll < Math.Max(0f, Math.Min(1f, chance)))
                {
                    watchUntil = now + Math.Max(0.1f, watchSeconds);
                    WatchCount++;
                }
            }
            Mood = now < watchUntil ? SpecimenMood.Watching : SpecimenMood.Idle;
        }

        public static int Nearest(float leftDistance, float rightDistance, float radius)
        {
            bool left = Finite(leftDistance) && leftDistance >= 0f && leftDistance <= radius;
            bool right = Finite(rightDistance) && rightDistance >= 0f && rightDistance <= radius;
            if (!left) return right ? 1 : -1;
            return !right || leftDistance <= rightDistance ? 0 : 1;
        }

        // Multiplying a local point by this scale keeps its center in an ellipsoid.
        // The authoring bounds already reserve the specimen's full rotation/breath radius.
        public static float BoundsScale(float x, float y, float z, float ex, float ey, float ez)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z) ||
                !Finite(ex) || !Finite(ey) || !Finite(ez) || ex <= 0f || ey <= 0f || ez <= 0f) return 0f;
            double q = (double)x * x / ((double)ex * ex) +
                       (double)y * y / ((double)ey * ey) + (double)z * z / ((double)ez * ez);
            return q > 1d ? (float)(1d / Math.Sqrt(q)) : 1f;
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>One gate per anatomical hand, sampled once per frame, not per collider.</summary>
    public sealed class SpecimenTapGate
    {
        private bool armed;
        private float releasedFor;

        public void Reset() { armed = false; releasedFor = 0f; }

        public bool Sample(bool continuous, bool contact, bool fullyReleased, float dt,
            float inwardSpeed, float minimumSpeed, float rearmSeconds)
        {
            if (!continuous || !SpecimenJarState.Finite(dt) || dt <= 0f || dt > 0.1f ||
                !SpecimenJarState.Finite(inwardSpeed))
            {
                Reset();
                return false;
            }
            if (contact)
            {
                bool strike = armed && inwardSpeed >= minimumSpeed;
                // Even a slow/resting entry consumes the edge. Acceleration while touching
                // cannot manufacture a second tap, nor can expiry of the global cooldown.
                Reset();
                return strike;
            }
            if (fullyReleased)
            {
                releasedFor += dt;
                if (releasedFor >= Math.Max(0.02f, rearmSeconds)) armed = true;
            }
            else releasedFor = 0f;
            return false;
        }
    }
}
