using System;

namespace RunawayChimps.Hub
{
    // Engine-independent contact gate. Coordinates are metres in the pad's frame;
    // +Z faces the player. A hand must withdraw before each front-face strike.
    public sealed class StrengthHitState
    {
        private bool sampled;
        private bool armed;
        private float previousX, previousY, previousZ, clearTime;

        public void Reset()
        {
            sampled = armed = false;
            clearTime = 0f;
        }

        public bool Sample(float x, float y, float z, float deltaTime, float intoSpeed,
            float halfWidth, float halfHeight, float handRadius, float minimumSpeed,
            float maximumSpeed, out int score)
        {
            score = 0;
            if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(deltaTime) ||
                !Finite(intoSpeed) || deltaTime < 0.001f || deltaTime > 0.1f ||
                !Finite(halfWidth) || !Finite(halfHeight) || !Finite(handRadius) ||
                halfWidth <= 0f || halfHeight <= 0f || handRadius < 0f ||
                !Finite(minimumSpeed) || !Finite(maximumSpeed) ||
                minimumSpeed <= 0f || maximumSpeed <= minimumSpeed)
            {
                Reset();
                return false;
            }

            float dx = x - previousX, dy = y - previousY, dz = z - previousZ;
            bool continuous = sampled && dx * dx + dy * dy + dz * dz <= 0.36f;
            bool crossing = continuous && previousZ > handRadius && z <= handRadius;
            float fraction = crossing ? (previousZ - handRadius) / (previousZ - z) : 0f;
            float hitX = previousX + dx * fraction;
            float hitY = previousY + dy * fraction;
            previousX = x;
            previousY = y;
            previousZ = z;
            sampled = true;

            if (!continuous)
            {
                armed = false;
                clearTime = 0f;
                return false;
            }

            // Hysteresis and withdrawal time prevent contact jitter, lingering palms,
            // and a back-to-front pass from becoming additional hits.
            if (z > handRadius + 0.10f)
            {
                clearTime += deltaTime;
                if (clearTime >= 0.10f) armed = true;
            }
            else clearTime = 0f;

            if (!crossing) return false;
            bool wasArmed = armed;
            armed = false;
            if (!wasArmed || Math.Abs(hitX) > halfWidth || Math.Abs(hitY) > halfHeight ||
                intoSpeed < minimumSpeed || intoSpeed > 15f)
                return false;

            score = Score(intoSpeed, minimumSpeed, maximumSpeed);
            return true;
        }

        public static int Score(float speed, float minimumSpeed, float maximumSpeed)
        {
            if (!Finite(speed) || !Finite(minimumSpeed) || !Finite(maximumSpeed) ||
                minimumSpeed <= 0f || maximumSpeed <= minimumSpeed || speed < minimumSpeed)
                return 0;
            float t = Math.Max(0f, Math.Min(1f, (speed - minimumSpeed) / (maximumSpeed - minimumSpeed)));
            return 1 + (int)Math.Round(t * 998f);
        }

        public static string Assessment(int score)
        {
            if (score >= 900) return "PLEASE NOTIFY SECURITY";
            if (score >= 650) return "RESTRAINTS NO LONGER\nCONSIDERED EFFECTIVE";
            if (score >= 300) return "HANDLER ASSISTANCE\nRECOMMENDED";
            return "SUBJECT REQUIRES\nADDITIONAL ENRICHMENT";
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
