using UnityEngine;

namespace PlanetMakey.Core
{
    /// <summary>
    /// Anything a ball can hit in the scene: Makey, or a test cube. The hit resolver finds it on
    /// the collider the ray hit, or on one of that collider's parents, so no wiring is needed.
    /// </summary>
    public interface IBallTarget
    {
        /// <summary>
        /// Called on the main thread once for every ball that hits this target.
        /// </summary>
        /// <param name="color">The color of the ball.</param>
        /// <param name="worldPoint">Where the ray hit, for effects such as sparks.</param>
        void ReceiveBall(BallColor color, Vector3 worldPoint);
    }
}
