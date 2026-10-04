using UnityEngine;

namespace PlanetMakey.Core
{
    /// <summary>
    /// Maps each <see cref="BallColor"/> to the color drawn on screen. It is an asset so the
    /// colors can be tuned against the projector without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "BallColorPalette", menuName = "Planet Makey/Ball Color Palette")]
    public sealed class BallColorPalette : ScriptableObject
    {
        [SerializeField] private Color _red = Color.red;
        [SerializeField] private Color _blue = Color.blue;
        [SerializeField] private Color _yellow = Color.yellow;
        [SerializeField] private Color _green = Color.green;

        [Tooltip("Shown for a ball color this palette does not know. It should look wrong on purpose.")]
        [SerializeField] private Color _unknown = Color.magenta;

        /// <summary>Returns the display color for a ball color.</summary>
        public Color GetColor(BallColor ballColor)
        {
            switch (ballColor)
            {
                case BallColor.Red:
                    return _red;
                case BallColor.Blue:
                    return _blue;
                case BallColor.Yellow:
                    return _yellow;
                case BallColor.Green:
                    return _green;
                default:
                    // A color added to the enum but not here should be obvious on screen,
                    // not crash the booth.
                    return _unknown;
            }
        }
    }
}
