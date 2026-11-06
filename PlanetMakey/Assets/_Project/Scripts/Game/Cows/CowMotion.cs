using UnityEngine;

namespace PlanetMakey.Game.Cows
{
    /// <summary>
    /// The small pieces of math behind the cows' life: the idle hop, the shrink at the end of the lift and the
    /// pop of the big eyes. Kept apart from <see cref="CowHerdView"/> and <see cref="CowEyesView"/> so they can be
    /// tested without a scene.
    /// </summary>
    public static class CowMotion
    {
        /// <summary>
        /// How high a cow is during a hop, from 0 on the ground to 1 at the top: a plain arc.
        /// </summary>
        /// <param name="t">How far through the hop, 0 to 1.</param>
        public static float HopHeight(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return 4f * clamped * (1f - clamped);
        }

        /// <summary>
        /// How tall a cow is drawn during a hop, as a multiple of her normal height: stretched while she leaves the
        /// ground and squashed as she lands, and back to 1 at both ends of the hop.
        /// </summary>
        /// <param name="t">How far through the hop, 0 to 1.</param>
        /// <param name="amount">How strong the effect is. 0.1 is 10% taller at most.</param>
        public static float HopStretch(float t, float amount)
        {
            float clamped = Mathf.Clamp01(t);
            float stretch = clamped < 0.5f ? Mathf.Sin(Mathf.PI * clamped / 0.5f) : 0f;
            float squash = Mathf.Sin(Mathf.PI * Mathf.Clamp01((clamped - 0.8f) / 0.2f));
            return 1f + amount * (stretch - 1.4f * squash);
        }

        /// <summary>
        /// How much of a hop's turn is done, from 0 to 1: the cow starts turning as she leaves the ground and has
        /// finished a moment before she lands, easing in and out.
        /// </summary>
        /// <param name="t">How far through the hop, 0 to 1.</param>
        public static float HopTurn(float t)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / HopTurnDoneAt));
        }

        // The point of the hop, 0 to 1, by which the turn is finished.
        private const float HopTurnDoneAt = 0.85f;

        /// <summary>
        /// A quick, smooth jitter for a shaking cow: three numbers, each wandering between -1 and 1, that change a
        /// little from one moment to the next and differ for each seed, so two cows never shake in step.
        /// </summary>
        /// <param name="time">A running time, in units of one wander: raise it faster for a faster shake. Zero or more.</param>
        /// <param name="seed">Tells one cow's shake from another's.</param>
        public static Vector3 Shake(float time, float seed)
        {
            float t = Mathf.Max(0f, time);
            return new Vector3(
                Mathf.PerlinNoise(t, seed) * 2f - 1f,
                Mathf.PerlinNoise(t, seed + 17.3f) * 2f - 1f,
                Mathf.PerlinNoise(t, seed + 41.7f) * 2f - 1f);
        }

        /// <summary>
        /// How big the lifted cow is drawn, as a multiple of her size: 1 until the lift is <paramref name="start"/>
        /// of the way up, then easing down to <paramref name="endScale"/> as she reaches the ship.
        /// </summary>
        /// <param name="progress">How far through the lift, 0 to 1.</param>
        /// <param name="start">Where the shrink begins, 0 to 1. At 1 or more it never happens.</param>
        /// <param name="endScale">The size at the top, as a multiple of her size.</param>
        public static float EndShrink(float progress, float start, float endScale)
        {
            if (start >= 1f)
            {
                return 1f;
            }

            float linear = Mathf.InverseLerp(Mathf.Max(0f, start), 1f, Mathf.Clamp01(progress));
            return Mathf.Lerp(1f, endScale, Mathf.SmoothStep(0f, 1f, linear));
        }

        /// <summary>
        /// The size of something that pops up: 0 to 1, but swelling past 1 on the way and settling back, like a
        /// spring. It is 0 at the start and exactly 1 at the end.
        /// </summary>
        /// <param name="t">How far through the pop, 0 to 1.</param>
        /// <param name="overshoot">How far it swells past full size. 0 for none; about 1.7 swells it by 10%.</param>
        public static float PopScale(float t, float overshoot)
        {
            float clamped = Mathf.Clamp01(t);
            float c1 = Mathf.Max(0f, overshoot);
            float c3 = c1 + 1f;
            float shifted = clamped - 1f;
            return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
        }
    }
}
