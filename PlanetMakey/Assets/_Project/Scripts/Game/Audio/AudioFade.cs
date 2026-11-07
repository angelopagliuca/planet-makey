using UnityEngine;

namespace PlanetMakey.Game.Audio
{
    /// <summary>
    /// The plain math behind the music's fades. Kept apart from <see cref="MusicPlayer"/> so it can be tested
    /// without a scene.
    /// </summary>
    public static class AudioFade
    {
        /// <summary>
        /// Moves a fade level from 0 (silent) to 1 (full) towards a target at a steady rate, so that the whole
        /// range takes <paramref name="seconds"/>. With no duration it arrives at once.
        /// </summary>
        /// <param name="level">Where the level is now, 0 to 1.</param>
        /// <param name="target">Where it is heading, normally 0 or 1.</param>
        /// <param name="deltaTime">Seconds since the last step.</param>
        /// <param name="seconds">Seconds a fade from silent to full (or back) takes.</param>
        public static float Approach(float level, float target, float deltaTime, float seconds)
        {
            if (seconds <= 0f)
            {
                return target;
            }

            return Mathf.MoveTowards(level, target, deltaTime / seconds);
        }

        /// <summary>
        /// The volume (0 to 1) for a fade level: a gentle S-curve, so a fade starts and ends softly and does not
        /// sound like a straight ramp.
        /// </summary>
        /// <param name="level">The fade level, 0 to 1.</param>
        public static float Gain(float level)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(level));
        }

        /// <summary>
        /// True if a round and its music differ in length by more than a tolerance, so the music would be cut
        /// off or would end early. A negative length is treated as 0.
        /// </summary>
        /// <param name="roundSeconds">The length of the whole round, from the start of dusk to the end of dawn.</param>
        /// <param name="clipSeconds">The length of the music clip.</param>
        /// <param name="toleranceSeconds">How much they may differ before it is worth a warning.</param>
        public static bool LengthsDiffer(float roundSeconds, float clipSeconds, float toleranceSeconds)
        {
            float difference = Mathf.Abs(Mathf.Max(0f, roundSeconds) - Mathf.Max(0f, clipSeconds));
            return difference > Mathf.Max(0f, toleranceSeconds);
        }
    }
}
