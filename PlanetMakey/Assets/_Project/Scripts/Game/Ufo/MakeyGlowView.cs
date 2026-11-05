using System;
using UnityEngine;
using UnityEngine.Rendering;
using PlanetMakey.Game.Round;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// A soft glow on Makey's ship, so that he can be seen arriving and leaving in the dark. It is a flat
    /// Quad (it always faces the camera) that follows the ship, fades in as he flies in and out as he is
    /// gone, breathes slowly, brightens while the beam is on and flickers with the shield's late-lift
    /// strain. It is bright at night and faint by day. Put it on a Quad at the root of the scene, with no
    /// collider and a material that uses the PlanetMakey/Glow shader. It sets its own position and size
    /// every frame, so it must not sit under a scaled or rotated parent.
    /// </summary>
    public sealed class MakeyGlowView : MonoBehaviour
    {
        // The properties of the PlanetMakey/Glow shader.
        private const string ColorProperty = "_BaseColor";
        private const string IntensityProperty = "_Intensity";
        private const string FadeProperty = "_Fade";

        private const float TwoPi = 2f * Mathf.PI;

        [Header("Wiring")]
        [SerializeField] private Makey _makey;

        [Tooltip("Optional. The round, so the glow is bright at night and faint by day. Without it the glow is always at full strength.")]
        [SerializeField] private RoundController _round;

        [Tooltip("Optional. The part of the ship the glow sits on and follows, for example the model's pivot. Without it, Makey's own position.")]
        [SerializeField] private Transform _anchor;

        [Header("Shape")]
        [Tooltip("Where the glow sits relative to the anchor, in world units. Move it to the middle of the hull.")]
        [SerializeField] private Vector3 _worldOffset = Vector3.zero;

        [Tooltip("The width of the glow, in world units. Wider than the ship so it spills around it.")]
        [Min(0.01f)]
        [SerializeField] private float _width = 3.6f;

        [Tooltip("The height of the glow, in world units. Smaller than the width for a flat ship.")]
        [Min(0.01f)]
        [SerializeField] private float _height = 1.8f;

        [Header("Glow")]
        [Tooltip("The color of the glow. The beam's lime matches the beam; the shield keeps its own color.")]
        [SerializeField] private Color _color = new Color(0.71f, 0.95f, 0.23f, 1f);

        [Tooltip("How hard the glow glows at night. Values above 1 are brighter than the screen's white, so Bloom makes it glow.")]
        [Range(0f, 12f)]
        [SerializeField] private float _intensity = 2.5f;

        [Tooltip("How much of the glow is left in full day, from 0 (none) to 1 (as at night). A bright glow on a bright sky only looks like a smudge.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dayLevel = 0.15f;

        [Tooltip("Seconds the glow takes to fade in when Makey flies in, and out when he is gone.")]
        [Min(0f)]
        [SerializeField] private float _fadeSeconds = 0.5f;

        [Header("Life")]
        [Tooltip("Seconds for one slow swell of the glow's brightness. 0 for a steady glow.")]
        [Min(0f)]
        [SerializeField] private float _breathSeconds = 3f;

        [Tooltip("How far the glow dims at the low point of its swell, as a share of its brightness.")]
        [Range(0f, 1f)]
        [SerializeField] private float _breathDepth = 0.25f;

        [Tooltip("How much brighter the glow gets while the beam is on, as a share of its brightness.")]
        [Min(0f)]
        [SerializeField] private float _beamBoost = 0.6f;

        [Tooltip("Seconds the glow takes to change between its normal and its beam brightness.")]
        [Min(0f)]
        [SerializeField] private float _beamBlendSeconds = 0.3f;

        [Tooltip("The share of the lift (0 to 1) after which the glow flickers. It matches the shield's flicker.")]
        [Range(0f, 1f)]
        [SerializeField] private float _urgencyStart = 0.6f;

        [Tooltip("How much the glow dims at the worst of the flicker, as a share of its brightness.")]
        [Range(0f, 1f)]
        [SerializeField] private float _flickerDepth = 0.3f;

        [Tooltip("How fast the glow flickers at the very end of the lift, in flickers per second.")]
        [Min(0f)]
        [SerializeField] private float _flickerMaxPerSecond = 10f;

        [Header("Debug")]
        [Tooltip("Write to the Console when the glow turns on and off. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Hidden by turning its renderer off, so this component keeps running.
        [NonSerialized] private Renderer _renderer;
        [NonSerialized] private bool _rendererWasEnabled;
        [NonSerialized] private ShadowCastingMode _shadowModeWas;
        [NonSerialized] private bool _shown;
        [NonSerialized] private bool _captured;
        [NonSerialized] private MaterialPropertyBlock _block;

        // Presence and beam levels, 0 to 1, and the flicker's phase.
        [NonSerialized] private float _level;
        [NonSerialized] private float _beamLevel;
        [NonSerialized] private float _flickerPhase;

        // Where the object was when this component was enabled, put back when it is disabled.
        [NonSerialized] private Vector3 _restPosition;
        [NonSerialized] private Vector3 _restScale;

        private void OnEnable()
        {
            _renderer = GetComponent<Renderer>();

            if (_makey == null || _renderer == null)
            {
                Debug.LogError("MakeyGlowView needs a Makey, and a renderer on this object (a Quad), so it will do nothing.", this);
                return;
            }

            if (GetComponent<Collider>() != null)
            {
                Debug.LogWarning("The glow has a collider. Remove it, or balls aimed at Makey can hit the glow instead.", this);
            }

            if (transform.parent != null)
            {
                Debug.LogWarning("The glow has a parent. Its size is set in world terms, so a parent that is scaled or turned will distort it.", this);
            }

            Material material = _renderer.sharedMaterial;
            if (material == null || !material.HasProperty(IntensityProperty))
            {
                Debug.LogWarning("The glow's material" + (material != null ? " '" + material.name + "' (shader " + material.shader.name + ")" : "")
                    + " has no " + IntensityProperty + ", so the glow will not show as intended. Give it the shader 'PlanetMakey/Glow'.", this);
            }

            _restPosition = transform.position;
            _restScale = transform.localScale;
            _rendererWasEnabled = _renderer.enabled;
            _shadowModeWas = _renderer.shadowCastingMode;
            _renderer.enabled = false;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;

            _shown = false;
            _level = 0f;
            _beamLevel = 0f;
            _flickerPhase = 0f;
            _captured = true;

            if (_debugLog)
            {
                Debug.Log("[MakeyGlowView] Enabled. Watching " + _makey.name + ", round " + (_round != null ? "wired" : "not wired (full strength)") + ".", this);
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // Leave the glow as it was found, so a Play session leaves nothing changed.
            transform.position = _restPosition;
            transform.localScale = _restScale;

            if (_renderer != null)
            {
                _renderer.SetPropertyBlock(null);
                _renderer.shadowCastingMode = _shadowModeWas;
                _renderer.enabled = _rendererWasEnabled;
            }
        }

        // After Makey and his view have moved, so the glow never trails a frame behind.
        private void LateUpdate()
        {
            if (!_captured)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            MakeyState state = _makey.State;
            AbductionPhase phase = _makey.AbductionPhase;

            // The glow is there whenever Makey is on the stage, which is also while he flies in and off.
            _level = BeamShape.MoveLevel(_level, state != MakeyState.Away ? 1f : 0f, deltaTime, _fadeSeconds);

            bool beamOn = _makey.TargetCowIndex >= 0 && (phase == AbductionPhase.LoweringBeam || phase == AbductionPhase.Lifting);
            _beamLevel = BeamShape.MoveLevel(_beamLevel, beamOn ? 1f : 0f, deltaTime, _beamBlendSeconds);

            float fade = Mathf.SmoothStep(0f, 1f, _level);
            if (fade <= 0.001f)
            {
                Show(false);
                return;
            }

            // Bright at night, faint by day.
            float night = _round != null ? DayNightCurve.NightAmount(_round.State, _round.StateProgress) : 1f;
            float daylight = Mathf.Lerp(_dayLevel, 1f, night);

            // The flicker is a phase that advances at the current rate, so a rising rate never makes it jump.
            float urgency = phase == AbductionPhase.Lifting ? BeamShape.Urgency(_makey.AbductionProgress, _urgencyStart) : 0f;
            _flickerPhase = Mathf.Repeat(_flickerPhase + TwoPi * Mathf.Lerp(0f, _flickerMaxPerSecond, urgency) * deltaTime, TwoPi);
            float flicker = 1f - _flickerDepth * urgency * (0.5f + 0.5f * Mathf.Sin(_flickerPhase));

            float breath = BeamShape.Breath(Time.time, _breathSeconds, _breathDepth);
            float brightness = _intensity * daylight * breath * flicker * (1f + _beamBoost * _beamLevel);

            Transform anchor = _anchor != null ? _anchor : _makey.transform;
            transform.position = anchor.position + _worldOffset;
            transform.localScale = new Vector3(_width, _height, 1f);

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.SetColor(ColorProperty, _color);
            _block.SetFloat(IntensityProperty, brightness);
            _block.SetFloat(FadeProperty, fade);
            _renderer.SetPropertyBlock(_block);

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
                Debug.Log("[MakeyGlowView] Glow " + (shown ? "on" : "off") + ".", this);
            }
        }
    }
}
