using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// What one ball hit turned out to land on, as reported by <see cref="HitResolver"/>.
    /// </summary>
    public readonly struct ResolvedHit
    {
        public ResolvedHit(BallHit hit, Collider collider, IBallTarget target, Vector3 worldPoint)
        {
            Hit = hit;
            Collider = collider;
            Target = target;
            WorldPoint = worldPoint;
        }

        /// <summary>The hit as it came from the hit source.</summary>
        public BallHit Hit { get; }

        /// <summary>The collider the ray hit, or null if it hit nothing.</summary>
        public Collider Collider { get; }

        /// <summary>The target on that collider, or null if the collider has none.</summary>
        public IBallTarget Target { get; }

        /// <summary>Where the ray hit. Only meaningful when <see cref="Collider"/> is not null.</summary>
        public Vector3 WorldPoint { get; }

        /// <summary>True if a target was found and told about the ball.</summary>
        public bool HitTarget => Target != null;
    }
}
