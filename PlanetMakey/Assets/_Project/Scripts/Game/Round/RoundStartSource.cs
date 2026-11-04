using System;
using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// Anything that can ask for a round to start: an operator key now, a hardware button or a
    /// network message later. The <see cref="RoundController"/> listens to
    /// <see cref="StartRequested"/> and does not care which one it is.
    /// </summary>
    /// <remarks>
    /// This is an abstract MonoBehaviour and not an interface because Unity cannot show an
    /// interface reference in the Inspector, and sources are wired in the scene.
    /// </remarks>
    public abstract class RoundStartSource : MonoBehaviour
    {
        /// <summary>Raised on the main thread each time a start is asked for.</summary>
        public event Action StartRequested;

        /// <summary>Call from a subclass, on the main thread, to ask for a round to start.</summary>
        protected void RaiseStartRequested()
        {
            StartRequested?.Invoke();
        }
    }
}
