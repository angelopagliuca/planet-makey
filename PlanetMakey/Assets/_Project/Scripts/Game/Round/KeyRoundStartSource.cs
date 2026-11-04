using UnityEngine;
using UnityEngine.InputSystem;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The operator's way to start a round at a desk or at the booth keyboard: one key.
    /// </summary>
    public sealed class KeyRoundStartSource : RoundStartSource
    {
        [Tooltip("The key that asks for a round to start. It does nothing while a round is running.")]
        [SerializeField] private Key _startKey = Key.Space;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _startKey == Key.None)
            {
                return;
            }

            if (keyboard[_startKey].wasPressedThisFrame)
            {
                RaiseStartRequested();
            }
        }
    }
}
