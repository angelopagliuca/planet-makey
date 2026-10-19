using PlanetMakey.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// Stands in for the booth hardware: a left click is a ball hit at the pointer, and the
    /// keys 1 to 4 choose the ball color (red, blue, yellow, green).
    /// </summary>
    public sealed class MouseHitSource : HitSource
    {
        [Tooltip("The ball color used until a color key is pressed.")]
        [SerializeField] private BallColor _selectedColor = BallColor.Red;

        [Header("Test skew")]
        [Tooltip("Distorts the click positions the way a badly aligned sensor would, so calibration can be tried at a desk.")]
        [SerializeField] private bool _skewEnabled;

        [Tooltip("Scale around the center of the screen.")]
        [SerializeField] private Vector2 _skewScale = new Vector2(0.8f, 0.9f);

        [Tooltip("Rotation around the center of the screen, in degrees.")]
        [SerializeField] private float _skewRotationDegrees = 10f;

        [Tooltip("Shift, as a fraction of the screen.")]
        [SerializeField] private Vector2 _skewOffset = new Vector2(0.1f, -0.05f);

        /// <summary>The color the next click will be reported with.</summary>
        public BallColor SelectedColor => _selectedColor;

        private void Update()
        {
            ReadColorKeys();
            ReadClick();
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
                _selectedColor = BallColor.Red;
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                _selectedColor = BallColor.Blue;
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                _selectedColor = BallColor.Yellow;
            }
            else if (keyboard.digit4Key.wasPressedThisFrame)
            {
                _selectedColor = BallColor.Green;
            }
        }

        private void ReadClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            // Pointer pixels have their origin at the bottom left, the same as sensor space,
            // so dividing by the screen size is the whole conversion.
            Vector2 pixels = mouse.position.ReadValue();
            Vector2 position = new Vector2(pixels.x / Screen.width, pixels.y / Screen.height);

            // In the Editor a click can land outside the Game view. That is not a wall hit.
            if (position.x < 0f || position.x > 1f || position.y < 0f || position.y > 1f)
            {
                return;
            }

            if (_skewEnabled)
            {
                position = ApplySkew(position);
            }

            RaiseHit(new BallHit(position, _selectedColor));
        }

        // The result may fall outside 0 to 1, as a misaligned sensor's would.
        private Vector2 ApplySkew(Vector2 position)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            Vector2 scaled = Vector2.Scale(position - center, _skewScale);

            float radians = _skewRotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 rotated = new Vector2(
                scaled.x * cos - scaled.y * sin,
                scaled.x * sin + scaled.y * cos);

            return rotated + center + _skewOffset;
        }
    }
}
