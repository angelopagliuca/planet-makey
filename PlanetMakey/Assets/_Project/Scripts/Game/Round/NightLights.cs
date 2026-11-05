using System;
using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// Small lamps that come on at dusk and go off at dawn, for a little ambience: the tractor's
    /// headlights, a lit window. Each light's intensity in the Inspector is how bright it is at night.
    /// By day the lights are off; during dusk and dawn they ease on and off with the sky, following the
    /// same night amount as <see cref="DayNightView"/>.
    /// </summary>
    /// <remarks>
    /// The intensities are read when this component is enabled and put back when it is disabled. While the
    /// lamps are at full brightness the intensities are read again every frame, so a light can be tuned live
    /// in Play mode. During dusk and dawn the component sets the intensity itself. Untick a light's own
    /// Enabled box to leave that lamp out. Every lamp counts towards the pipeline's limit of additional lights
    /// per object, which is 4 in the PC render pipeline asset, and the tractor beam's light is one of them.
    /// </remarks>
    public sealed class NightLights : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("The lamps. Each Light's intensity is how bright it is at night. Their position, color, range and angle are set on the lights themselves.")]
        [SerializeField] private Light[] _lights;

        [Header("Timing")]
        [Tooltip("How dark it has to be before the lamps start to come on, from 0 (the first moment of dusk) to 1 (only at full night). They go off again at the same point in dawn.")]
        [Range(0f, 1f)]
        [SerializeField] private float _turnOnAt = 0.4f;

        [Header("Debug")]
        [Tooltip("Write to the Console when the lamps come on and go off, and what the lights hold. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // The lights as they were when this component was enabled, put back when it is disabled.
        [NonSerialized] private float[] _nightIntensity;
        [NonSerialized] private bool[] _wasEnabled;
        [NonSerialized] private bool _captured;

        // What was last applied.
        [NonSerialized] private bool _applied;
        [NonSerialized] private float _appliedLevel;
        [NonSerialized] private bool _atFull;

        private void OnEnable()
        {
            if (_round == null)
            {
                Debug.LogError("NightLights needs the Round Controller, so it will do nothing.", this);
                return;
            }

            if (_lights == null || _lights.Length == 0)
            {
                Debug.LogWarning("NightLights has no lights, so it will do nothing. Add them to the Lights list.", this);
                return;
            }

            _nightIntensity = new float[_lights.Length];
            _wasEnabled = new bool[_lights.Length];

            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = _lights[i];
                if (light == null)
                {
                    Debug.LogWarning("NightLights: Lights element " + i + " is empty, so it is skipped.", this);
                    continue;
                }

                _nightIntensity[i] = light.intensity;
                _wasEnabled[i] = light.enabled;

                if (light.intensity <= 0f)
                {
                    Debug.LogWarning("NightLights: the light '" + light.name + "' has intensity 0, so it will never light up. Set its night intensity.", light);
                }
            }

            _applied = false;
            _atFull = false;
            _captured = true;

            if (_debugLog)
            {
                Debug.Log("[NightLights] Enabled with " + _lights.Length + " lights, coming on at night amount " + _turnOnAt.ToString("0.00") + ".", this);
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // Leave the lights as they were found, so a Play session leaves nothing changed.
            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = _lights[i];
                if (light == null)
                {
                    continue;
                }

                light.intensity = _nightIntensity[i];
                light.enabled = _wasEnabled[i];
            }
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            float night = DayNightCurve.NightAmount(_round.State, _round.StateProgress);
            float level = DayNightCurve.LampLevel(night, _turnOnAt);

            if (level >= 1f)
            {
                if (_atFull)
                {
                    // Fully lit: the lights are left alone and read again, so they can be tuned in Play mode.
                    ReadIntensities();
                    return;
                }

                _atFull = true;
                Apply(1f);
                return;
            }

            _atFull = false;

            // Nothing to do while the level has not moved.
            if (_applied && Mathf.Approximately(level, _appliedLevel))
            {
                return;
            }

            Apply(level);
        }

        private void ReadIntensities()
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = _lights[i];
                if (light != null)
                {
                    _nightIntensity[i] = light.intensity;
                }
            }
        }

        private void Apply(float level)
        {
            bool wasOn = _applied && _appliedLevel > 0f;
            bool isOn = level > 0f;

            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = _lights[i];
                if (light == null)
                {
                    continue;
                }

                light.intensity = _nightIntensity[i] * level;
                light.enabled = _wasEnabled[i] && isOn;
            }

            _applied = true;
            _appliedLevel = level;

            if (_debugLog && wasOn != isOn)
            {
                Debug.Log("[NightLights] Lamps " + (isOn ? "on" : "off") + ".", this);
            }
        }
    }
}
