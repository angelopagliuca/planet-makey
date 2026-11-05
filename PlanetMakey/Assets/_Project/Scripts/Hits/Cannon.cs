using System;
using PlanetMakey.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// The cannon: remembers the color of the ball that was loaded most recently. The cannon's
    /// color sensor reports it with a <c>LOAD</c> message (see PROTOCOL.md), and the keys 1 to 4
    /// do the same by hand. A hit that arrives without a color, from the wall sensor or from the
    /// mouse, is reported with this color.
    /// </summary>
    /// <remarks>
    /// Firing does not empty the cannon: the color stays until the next ball is loaded, so a hit
    /// that is reported late, or twice, still has a color. Before the first ball is loaded the
    /// cannon is empty and such hits are ignored.
    /// </remarks>
    public sealed class Cannon : MonoBehaviour
    {
        [Tooltip("Keys 1 to 4 load a red, blue, yellow or green ball. A backup for the cannon's color sensor, and the way to load a ball at a desk.")]
        [SerializeField] private bool _keysEnabled = true;

        [NonSerialized] private bool _hasBall;
        [NonSerialized] private BallColor _loadedColor;
        [NonSerialized] private float _loadedTime;

        /// <summary>Raised on the main thread each time a ball is loaded, even one of the same color.</summary>
        public event Action<BallColor> Loaded;

        /// <summary>False until the first ball is loaded.</summary>
        public bool HasBall => _hasBall;

        /// <summary>The color of the ball loaded most recently. Only meaningful while <see cref="HasBall"/> is true.</summary>
        public BallColor LoadedColor => _loadedColor;

        /// <summary>Seconds since the latest ball was loaded, in unscaled time. -1 while the cannon is empty.</summary>
        public float SecondsSinceLoaded => _hasBall ? Time.unscaledTime - _loadedTime : -1f;

        /// <summary>Loads a ball of this color, replacing the one loaded before.</summary>
        public void Load(BallColor color)
        {
            _hasBall = true;
            _loadedColor = color;
            _loadedTime = Time.unscaledTime;
            Loaded?.Invoke(color);
        }

        private void Update()
        {
            if (_keysEnabled)
            {
                ReadColorKeys();
            }
        }

        private void ReadColorKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Load(BallColor.Red);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                Load(BallColor.Blue);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                Load(BallColor.Yellow);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame)
            {
                Load(BallColor.Green);
            }
        }
    }
}
