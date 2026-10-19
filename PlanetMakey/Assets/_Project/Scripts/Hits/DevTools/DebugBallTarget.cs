using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Hits.DevTools
{
    /// <summary>
    /// A stand-in target for testing the hit pipeline: it flashes in the color of the ball that
    /// hit it. Put it on an object that has a collider and a renderer.
    /// </summary>
    public sealed class DebugBallTarget : MonoBehaviour, IBallTarget
    {
        // The color property of the URP Lit shader.
        private const string BaseColorProperty = "_BaseColor";

        [Tooltip("The renderer to flash. Filled in automatically when the component is added.")]
        [SerializeField] private Renderer _renderer;

        [Tooltip("Display colors for the ball colors.")]
        [SerializeField] private BallColorPalette _palette;

        [Tooltip("Seconds the flash is held before the object returns to its normal color.")]
        [SerializeField] private float _flashSeconds = 0.5f;

        // A property block overrides the color without creating or editing a material.
        private MaterialPropertyBlock _block;
        private float _flashEndTime;
        private bool _flashing;

        private void Reset()
        {
            _renderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable()
        {
            // Start clean, in case a flash was left over from an earlier Play session.
            ClearFlash();

            if (_renderer == null)
            {
                Debug.LogWarning("DebugBallTarget has no renderer, so it cannot flash.", this);
            }

            if (_palette == null)
            {
                Debug.LogWarning("DebugBallTarget has no palette, so it will flash white.", this);
            }
        }

        private void OnDisable()
        {
            ClearFlash();
        }

        private void Update()
        {
            if (_flashing && Time.time >= _flashEndTime)
            {
                ClearFlash();
            }
        }

        /// <inheritdoc />
        public void ReceiveBall(BallColor color, Vector3 worldPoint)
        {
            // A disabled component can still be found by the resolver, but it has no Update to
            // end the flash, so the color would stay on the renderer.
            if (!isActiveAndEnabled || _renderer == null)
            {
                return;
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorProperty, GetFlashColor(color));
            _renderer.SetPropertyBlock(_block);

            _flashEndTime = Time.time + _flashSeconds;
            _flashing = true;
        }

        private void ClearFlash()
        {
            _flashing = false;

            if (_renderer != null)
            {
                // Passing null removes the override, so the material's own color shows again.
                _renderer.SetPropertyBlock(null);
            }
        }

        private Color GetFlashColor(BallColor color)
        {
            if (_palette == null)
            {
                return Color.white;
            }

            return _palette.GetColor(color);
        }
    }
}
