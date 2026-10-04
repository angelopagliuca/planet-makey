using UnityEngine;

namespace PlanetMakey.Core
{
    /// <summary>
    /// One ball impact on the wall, as reported by a hit source.
    /// </summary>
    public readonly struct BallHit
    {
        public BallHit(Vector2 position, BallColor color)
        {
            Position = position;
            Color = color;
        }

        /// <summary>
        /// Where the ball hit, in sensor space: 0 to 1 on both axes, with the origin at the
        /// bottom left as the player sees the wall. This is not yet lined up with the projected
        /// picture; calibration does that later in the pipeline.
        /// </summary>
        public Vector2 Position { get; }

        /// <summary>The color of the ball that hit.</summary>
        public BallColor Color { get; }
    }
}
