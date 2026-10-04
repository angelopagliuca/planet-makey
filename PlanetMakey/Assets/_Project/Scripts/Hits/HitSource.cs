using System;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// Anything that produces ball hits: the booth hardware, or the mouse at a desk. Everything
    /// downstream listens to <see cref="HitReceived"/> and does not care which one it is.
    /// </summary>
    /// <remarks>
    /// This is an abstract MonoBehaviour and not an interface because Unity cannot show an
    /// interface reference in the Inspector, and sources are wired in the scene.
    /// </remarks>
    public abstract class HitSource : MonoBehaviour
    {
        /// <summary>Raised on the main thread once for every ball hit.</summary>
        public event Action<BallHit> HitReceived;

        /// <summary>Call from a subclass, on the main thread, to report a hit.</summary>
        protected void RaiseHit(BallHit hit)
        {
            HitReceived?.Invoke(hit);
        }
    }
}
