using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// Small pieces of math behind the tractor beam's look, kept apart from <see cref="BeamView"/> so
    /// they can be tested without a scene.
    /// </summary>
    public static class BeamShape
    {
        /// <summary>
        /// How wide the beam is at a point along it, as a multiple of its base width. It narrows to
        /// <paramref name="top"/> at the ship and flares out to <paramref name="bottom"/> at the ground, and a
        /// power above 1 keeps it slim for most of the way and spreads it at the end, like a skirt. The
        /// beam shader does the same sum, so the glow and the light can be sized to match.
        /// </summary>
        /// <param name="along">How far down the beam, from 0 at the ship to 1 at the ground.</param>
        /// <param name="top">The width factor at the ship.</param>
        /// <param name="bottom">The width factor at the ground.</param>
        /// <param name="power">How late the flare starts. 1 is a straight cone; 2 or more is a skirt.</param>
        public static float WidthFactor(float along, float top, float bottom, float power)
        {
            float clamped = Mathf.Clamp01(along);
            float shaped = Mathf.Pow(clamped, Mathf.Max(0.01f, power));
            return Mathf.Lerp(top, bottom, shaped);
        }

        /// <summary>
        /// How urgent the abduction is, from 0 to 1: 0 until <paramref name="start"/> of the lift is done, then
        /// growing steadily to 1 as the cow reaches the ship.
        /// </summary>
        /// <param name="progress">How far through the lift, 0 to 1.</param>
        /// <param name="start">The share of the lift after which it grows. At 1 or more it never does.</param>
        public static float Urgency(float progress, float start)
        {
            if (start >= 1f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.InverseLerp(start, 1f, progress));
        }

        /// <summary>
        /// A fade-out that runs from 1 down to 0 over its duration, from the seconds still left. A duration of
        /// zero or less means no fade at all, so it is 0 at once.
        /// </summary>
        public static float FadeOut(float secondsLeft, float duration)
        {
            if (duration <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(secondsLeft / duration);
        }

        /// <summary>
        /// A slow swell for the halo's brightness: a multiplier that moves smoothly between 1 and
        /// <c>1 - depth</c> once every <paramref name="periodSeconds"/>. A period of zero or less, or a
        /// depth of zero, gives a steady 1.
        /// </summary>
        /// <param name="timeSeconds">The time, in seconds.</param>
        /// <param name="periodSeconds">Seconds for one full swell.</param>
        /// <param name="depth">How far the brightness dips, 0 to 1 (clamped).</param>
        public static float Breath(float timeSeconds, float periodSeconds, float depth)
        {
            if (periodSeconds <= 0f)
            {
                return 1f;
            }

            // 0 at the start of each period, 1 half way through.
            float wave = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * timeSeconds / periodSeconds);
            return 1f - Mathf.Clamp01(depth) * wave;
        }

        /// <summary>
        /// Moves a 0-to-1 level towards a target at a steady rate, so that it takes
        /// <paramref name="seconds"/> to cross the whole range. With no duration it arrives at once.
        /// </summary>
        /// <param name="level">Where the level is now.</param>
        /// <param name="target">Where it is heading, normally 0 or 1.</param>
        /// <param name="deltaTime">Seconds since the last step.</param>
        /// <param name="seconds">Seconds the whole range takes.</param>
        public static float MoveLevel(float level, float target, float deltaTime, float seconds)
        {
            if (seconds <= 0f)
            {
                return target;
            }

            return Mathf.MoveTowards(level, target, deltaTime / seconds);
        }

        /// <summary>
        /// The full opening angle, in degrees, of a spot light at the ship that just covers the beam's
        /// flared foot, with some margin.
        /// </summary>
        /// <param name="groundRadius">The beam's radius at the ground, in world units.</param>
        /// <param name="length">The beam's length, in world units. Very short beams are treated as half a unit.</param>
        /// <param name="margin">How much wider than the foot the light reaches, as a multiple (1.3 is a third wider).</param>
        /// <param name="minDegrees">The smallest angle returned.</param>
        /// <param name="maxDegrees">The largest angle returned.</param>
        public static float SpotAngleDegrees(float groundRadius, float length, float margin, float minDegrees, float maxDegrees)
        {
            float distance = Mathf.Max(0.5f, length);
            float angle = 2f * Mathf.Atan2(Mathf.Max(0f, groundRadius) * margin, distance) * Mathf.Rad2Deg;
            return Mathf.Clamp(angle, minDegrees, maxDegrees);
        }
    }
}
