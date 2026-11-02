using System;
using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// The tractor beam: a placeholder cylinder that stretches from Makey's underside down to the
    /// cow he is abducting, and follows it up. Put it on a Cylinder at the root of the scene, with
    /// no collider and a transparent material. It sets its own position, rotation and size every
    /// frame, so it must not sit under a scaled or rotated parent.
    /// </summary>
    public sealed class BeamView : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Makey _makey;

        [Header("Look")]
        [Tooltip("How wide the beam is, in meters.")]
        [Min(0.01f)]
        [SerializeField] private float _width = 0.5f;

        [Header("Debug")]
        [Tooltip("Write what the beam does to the Console. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Hidden by turning its renderer off, so this component keeps running.
        [NonSerialized] private Renderer _renderer;
        [NonSerialized] private bool _rendererWasEnabled;
        [NonSerialized] private bool _shown;
        [NonSerialized] private bool _captured;

        // Where the beam object was when this component was enabled, put back when it is disabled.
        [NonSerialized] private Vector3 _restPosition;
        [NonSerialized] private Quaternion _restRotation;
        [NonSerialized] private Vector3 _restScale;

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();

            if (_makey == null || _renderer == null)
            {
                Debug.LogError("BeamView needs a Makey, and a renderer on this object (a Cylinder), so it will do nothing.", this);
                return;
            }

            if (GetComponent<Collider>() != null)
            {
                Debug.LogWarning("The beam has a collider. Remove it, or balls aimed at Makey can hit the beam instead.", this);
            }

            if (transform.parent != null)
            {
                Debug.LogWarning("The beam has a parent. Its size is set in world terms, so a parent that is scaled or turned will distort it.", this);
            }

            _restPosition = transform.position;
            _restRotation = transform.rotation;
            _restScale = transform.localScale;
            _rendererWasEnabled = _renderer.enabled;
            _renderer.enabled = false;
            _shown = false;
            _captured = true;

            if (_debugLog)
            {
                Debug.Log("[BeamView] Enabled. Watching " + _makey.name + ", width " + _width.ToString("0.0#") + " m.", this);
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // Leave the beam as it was found, so a Play session leaves nothing changed.
            transform.position = _restPosition;
            transform.rotation = _restRotation;
            transform.localScale = _restScale;

            if (_renderer != null)
            {
                _renderer.enabled = _rendererWasEnabled;
            }
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            AbductionPhase phase = _makey.AbductionPhase;
            if (_makey.TargetCowIndex < 0 || (phase != AbductionPhase.LoweringBeam && phase != AbductionPhase.Lifting))
            {
                Show(false);
                return;
            }

            // The beam reaches down to the cow, then follows it up.
            Vector3 top = _makey.BeamOrigin;
            Vector3 bottom = phase == AbductionPhase.LoweringBeam
                ? Vector3.Lerp(top, _makey.TargetCowPosition, _makey.AbductionProgress)
                : _makey.TargetCowPosition;

            Vector3 along = top - bottom;
            float length = along.magnitude;
            if (length < 0.001f)
            {
                Show(false);
                return;
            }

            // A Unity cylinder is 2 units tall, hence half the length for the height.
            transform.position = (top + bottom) * 0.5f;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, along / length);
            transform.localScale = new Vector3(_width, length * 0.5f, _width);
            Show(true);
        }

        private void Show(bool shown)
        {
            if (shown == _shown)
            {
                return;
            }

            _shown = shown;
            _renderer.enabled = shown;

            if (_debugLog)
            {
                Debug.Log("[BeamView] Beam " + (shown ? "on" : "off") + ".", this);
            }
        }
    }
}
