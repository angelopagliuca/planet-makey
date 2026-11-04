using System;
using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// Fades the scene between day and night as the round goes: dusk darkens it, the night stays
    /// dark and dawn brings the day back. Day is how the scene looks when the game starts; night is
    /// set here. It changes the sun's color and intensity, the ambient light and the skybox's
    /// exposure, and puts all of it back when it is disabled. These are placeholder looks made in code.
    /// </summary>
    /// <remarks>
    /// It works for a scene lit in real time, with ambient light from the skybox. A baked light does
    /// not change at run time. The skybox is changed on a copy of its material, never on the asset.
    /// </remarks>
    public sealed class DayNightView : MonoBehaviour
    {
        // The exposure property of Unity's skybox shaders (procedural, 6 sided, cubemap, panoramic).
        private const string ExposureProperty = "_Exposure";

        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("The main directional light, the sun. Its color and intensity at the start are the day values.")]
        [SerializeField] private Light _sun;

        [Header("Night look")]
        [Tooltip("The sun's color at night, for example a dim blue for the moon.")]
        [SerializeField] private Color _nightLightColor = new Color(0.35f, 0.45f, 0.8f, 1f);

        [Tooltip("The sun's intensity at night.")]
        [Min(0f)]
        [SerializeField] private float _nightLightIntensity = 0.15f;

        [Tooltip("The ambient light's intensity at night. By day it is whatever the scene has.")]
        [Min(0f)]
        [SerializeField] private float _nightAmbientIntensity = 0.2f;

        [Tooltip("The skybox's exposure at night. By day it is whatever the skybox material has. Ignored if the skybox has no exposure.")]
        [Min(0f)]
        [SerializeField] private float _nightSkyExposure = 0.15f;

        [Header("Debug")]
        [Tooltip("Write the day and night values it found, and each change of the round's state, to the Console. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // The day values, taken when this component is enabled, and put back when it is disabled.
        [NonSerialized] private Color _dayLightColor;
        [NonSerialized] private float _dayLightIntensity;
        [NonSerialized] private float _dayAmbientIntensity;
        [NonSerialized] private float _daySkyExposure;
        [NonSerialized] private Material _originalSkybox;
        [NonSerialized] private Material _skyboxCopy;

        // 0 is full day and 1 is full night. What was last applied.
        [NonSerialized] private float _appliedNight;
        [NonSerialized] private bool _applied;
        [NonSerialized] private bool _captured;
        [NonSerialized] private RoundState _lastLoggedState;
        [NonSerialized] private bool _hasLoggedState;

        private void OnEnable()
        {
            if (_round == null)
            {
                Debug.LogError("DayNightView needs a RoundController, so it will do nothing.", this);
                return;
            }

            _dayAmbientIntensity = RenderSettings.ambientIntensity;
            _originalSkybox = RenderSettings.skybox;

            if (_sun == null)
            {
                Debug.LogWarning("DayNightView has no sun light, so the light will not change with the time of day.", this);
            }
            else
            {
                _dayLightColor = _sun.color;
                _dayLightIntensity = _sun.intensity;
            }

            PrepareSkybox();

            _captured = true;
            _applied = false;
            _hasLoggedState = false;

            if (_debugLog)
            {
                Debug.Log("[DayNightView] Enabled. Day values: sun " + _dayLightColor + " at " + _dayLightIntensity.ToString("0.##")
                    + ", ambient " + _dayAmbientIntensity.ToString("0.##") + ", sky exposure "
                    + (_skyboxCopy != null ? _daySkyExposure.ToString("0.##") : "not used") + ".", this);
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // Leave the scene as it was found, so a Play session leaves nothing changed. The sun
            // can already be destroyed when the scene shuts down.
            if (_sun != null)
            {
                _sun.color = _dayLightColor;
                _sun.intensity = _dayLightIntensity;
            }

            RenderSettings.ambientIntensity = _dayAmbientIntensity;

            if (_skyboxCopy != null)
            {
                RenderSettings.skybox = _originalSkybox;
                Destroy(_skyboxCopy);
                _skyboxCopy = null;
            }
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            RoundState state = _round.State;
            float night = NightAmount(state, _round.StateProgress);

            if (_debugLog && (!_hasLoggedState || state != _lastLoggedState))
            {
                _hasLoggedState = true;
                _lastLoggedState = state;
                Debug.Log("[DayNightView] Round is " + state + ": night amount " + night.ToString("0.00") + ".", this);
            }

            // Nothing to do while the look has not moved.
            if (_applied && Mathf.Approximately(night, _appliedNight))
            {
                return;
            }

            Apply(night);
            _applied = true;
            _appliedNight = night;
        }

        // 0 is day and 1 is night. Dusk darkens, night stays dark, dawn brightens again.
        private static float NightAmount(RoundState state, float progress)
        {
            switch (state)
            {
                case RoundState.Dusk:
                    return Mathf.SmoothStep(0f, 1f, progress);
                case RoundState.Night:
                    return 1f;
                case RoundState.Dawn:
                    return Mathf.SmoothStep(1f, 0f, progress);
                default:
                    return 0f;
            }
        }

        private void Apply(float night)
        {
            if (_sun != null)
            {
                _sun.color = Color.Lerp(_dayLightColor, _nightLightColor, night);
                _sun.intensity = Mathf.Lerp(_dayLightIntensity, _nightLightIntensity, night);
            }

            RenderSettings.ambientIntensity = Mathf.Lerp(_dayAmbientIntensity, _nightAmbientIntensity, night);

            if (_skyboxCopy != null)
            {
                _skyboxCopy.SetFloat(ExposureProperty, Mathf.Lerp(_daySkyExposure, _nightSkyExposure, night));
            }
        }

        // The skybox's exposure is changed on a copy of its material, so the project's own material
        // asset is never edited.
        private void PrepareSkybox()
        {
            _skyboxCopy = null;

            if (_originalSkybox == null)
            {
                Debug.LogWarning("The scene has no skybox material, so the sky will not darken at night.", this);
                return;
            }

            if (!_originalSkybox.HasProperty(ExposureProperty))
            {
                Debug.LogWarning("The skybox material '" + _originalSkybox.name + "' (shader " + _originalSkybox.shader.name
                    + ") has no " + ExposureProperty + ", so the sky will not darken at night.", this);
                return;
            }

            _daySkyExposure = _originalSkybox.GetFloat(ExposureProperty);
            _skyboxCopy = new Material(_originalSkybox);
            _skyboxCopy.name = _originalSkybox.name + " (day/night copy)";
            RenderSettings.skybox = _skyboxCopy;
        }
    }
}
