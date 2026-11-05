using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The shape of the day and night through a round, as plain math: how dark it is at each point of
    /// each state, how warm the sky is at dusk and dawn, and the color of the horizon. Kept apart from
    /// <see cref="DayNightView"/> so it can be tested without a scene.
    /// </summary>
    public static class DayNightCurve
    {
        /// <summary>
        /// How dark it is, from 0 (full day) to 1 (full night). Dusk darkens, night stays dark and dawn
        /// brightens again; between rounds it is day.
        /// </summary>
        /// <param name="state">The round's state.</param>
        /// <param name="progress">How far through the state, 0 to 1.</param>
        public static float NightAmount(RoundState state, float progress)
        {
            switch (state)
            {
                case RoundState.Dusk:
                    return Mathf.SmoothStep(0f, 1f, progress);
                case RoundState.Night:
                    return 1f;
                case RoundState.Dawn:
                    return Mathf.SmoothStep(1f, 0f, progress);
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// How much of the sunset (or sunrise) glow is shown, from 0 to 1. It is 0 in full day and in full
        /// night and peaks halfway through the change, so one curve serves dusk and dawn alike.
        /// </summary>
        /// <param name="night">How dark it is, from <see cref="NightAmount"/>.</param>
        public static float Warmth(float night)
        {
            float clamped = Mathf.Clamp01(night);
            return 4f * clamped * (1f - clamped);
        }

        /// <summary>
        /// The color of the sky at the horizon: the day color turning into the night color, with the warm
        /// glow laid over it at dusk and dawn. The sky shader does the same sum, so this can color the
        /// fog to match.
        /// </summary>
        /// <param name="day">The horizon color in full day.</param>
        /// <param name="nightColor">The horizon color in full night.</param>
        /// <param name="warm">The color of the glow.</param>
        /// <param name="night">How dark it is, from <see cref="NightAmount"/>.</param>
        /// <param name="warmStrength">How much of the horizon the glow covers at its peak, 0 to 1.</param>
        public static Color HorizonColor(Color day, Color nightColor, Color warm, float night, float warmStrength)
        {
            Color baseColor = Color.Lerp(day, nightColor, Mathf.Clamp01(night));
            float glow = Mathf.Clamp01(Warmth(night) * warmStrength);
            return Color.Lerp(baseColor, warm, glow);
        }
    }
}
