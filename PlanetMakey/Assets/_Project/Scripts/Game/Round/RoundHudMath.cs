using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The plain math behind the round's on-screen display: when it is shown, which cow icons are lit, where
    /// the moon is on the night bar and how far it has turned into the sun. Kept apart from the views so it
    /// can be tested without a scene.
    /// </summary>
    public static class RoundHudMath
    {
        /// <summary>The display is shown from dusk to the end of dawn, and hidden while waiting for a round.</summary>
        public static bool IsRoundVisible(RoundState state)
        {
            return state != RoundState.Idle;
        }

        /// <summary>
        /// Whether a cow icon is lit. The icons are a count and not one per cow: with N cows safe the first N
        /// icons are lit, so the icons at the end fade as cows are lost.
        /// </summary>
        /// <param name="index">The icon's place in the row, from 0.</param>
        /// <param name="cowsSafe">How many cows are still safe.</param>
        public static bool IsCowLit(int index, int cowsSafe)
        {
            return index >= 0 && index < cowsSafe;
        }

        /// <summary>
        /// How far along the night bar the moon is, from 0 to 1: at the start through dusk, travelling through
        /// the night, and at the end through dawn. Zero while idle.
        /// </summary>
        /// <param name="state">The round's state.</param>
        /// <param name="stateProgress">How far through that state the round is, 0 to 1.</param>
        public static float NightBarProgress(RoundState state, float stateProgress)
        {
            switch (state)
            {
                case RoundState.Night:
                    return Mathf.Clamp01(stateProgress);
                case RoundState.Dawn:
                    return 1f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// How much of the moon has turned into the sun, from 0 (all moon) to 1 (all sun). It stays 0 until the
        /// moon is <paramref name="blendStart"/> of the way along, then eases to 1 as it arrives.
        /// </summary>
        /// <param name="progress">How far along the bar the moon is, 0 to 1.</param>
        /// <param name="blendStart">Where along the bar the change begins. At 1 or more it happens only at the very end.</param>
        public static float SunBlend(float progress, float blendStart)
        {
            if (blendStart >= 1f)
            {
                return progress >= 1f ? 1f : 0f;
            }

            float linear = Mathf.InverseLerp(Mathf.Max(0f, blendStart), 1f, Mathf.Clamp01(progress));
            return Mathf.SmoothStep(0f, 1f, linear);
        }

        /// <summary>
        /// Moves a value towards a target at a steady rate, so that it takes <paramref name="seconds"/> to cross
        /// the whole 0-to-1 range. With no duration it arrives at once.
        /// </summary>
        /// <param name="current">Where the value is now.</param>
        /// <param name="target">Where it is heading.</param>
        /// <param name="deltaTime">Seconds since the last step.</param>
        /// <param name="seconds">Seconds the whole range takes.</param>
        public static float Approach(float current, float target, float deltaTime, float seconds)
        {
            if (seconds <= 0f)
            {
                return target;
            }

            return Mathf.MoveTowards(current, target, deltaTime / seconds);
        }
    }
}
