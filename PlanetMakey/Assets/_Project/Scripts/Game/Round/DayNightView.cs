using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// Fades the scene between day and night as the round goes: dusk darkens it with a warm glow, the
    /// night stays dark but readable, and dawn brings the day back. Day is how the scene looks when the
    /// game starts; night is set here. It drives the sky (the gradient sky shader: night amount, glow,
    /// sun and moon), turns the sun into a cool moon light, sets the ambient light from three colors,
    /// can fade in a distance fog and a night color grade, and puts all of it back when it is disabled.
    /// </summary>
    /// <remarks>
    /// It works for a scene lit in real time. A baked light does not change at run time. The sky is
    /// changed on a copy of its material, never on the asset. If the scene's sky is not the gradient sky
    /// (it has no night amount), the old look is used: the skybox's exposure and the ambient intensity.
    /// </remarks>
    public sealed class DayNightView : MonoBehaviour
    {
        // The properties of the gradient sky shader (PlanetMakey/Gradient Sky).
        private const string NightProperty = "_Night";
        private const string WarmthProperty = "_Warmth";
        private const string SunDirectionProperty = "_SunDirection";
        private const string MoonDirectionProperty = "_MoonDirection";
        private const string DayHorizonProperty = "_DayHorizon";
        private const string NightHorizonProperty = "_NightHorizon";
        private const string WarmColorProperty = "_WarmColor";
        private const string WarmStrengthProperty = "_WarmStrength";

        // The exposure property of Unity's skybox shaders (procedural, 6 sided, cubemap, panoramic).
        private const string ExposureProperty = "_Exposure";

        private enum SkyMode
        {
            None,
            Gradient,
            Exposure
        }

        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("The main directional light, the sun. Its color, intensity and direction at the start are the day values.")]
        [SerializeField] private Light _sun;

        [Tooltip("Optional. A global Volume with the night color grade (for example a bluer, slightly less saturated look). Its weight goes from 0 by day to 1 at night.")]
        [SerializeField] private Volume _nightGrade;

        [Header("Night light")]
        [Tooltip("The light's color at night: the moon. A cool blue.")]
        [SerializeField] private Color _nightLightColor = new Color(0.35f, 0.45f, 0.8f, 1f);

        [Tooltip("The light's intensity at night. By day it is whatever the sun has.")]
        [Min(0f)]
        [SerializeField] private float _nightLightIntensity = 0.9f;

        [Tooltip("The direction of the moon's light, as rotation angles. The default shines from behind the farm towards the camera, which outlines the cows and Makey. The light turns from the day direction to this during dusk.")]
        [SerializeField] private Vector3 _moonLightEuler = new Vector3(28f, 200f, 0f);

        [Header("Moon in the sky")]
        [Tooltip("Where the moon is, measured from straight ahead of the camera towards the right, in degrees. Only the sky's picture of the moon; the light uses Moon Light Euler.")]
        [SerializeField] private float _moonAzimuthDegrees = 15f;

        [Tooltip("How high the moon is above the horizon, in degrees. The camera sees about 22 degrees up, so stay below that to see it.")]
        [Range(0f, 80f)]
        [SerializeField] private float _moonElevationDegrees = 14f;

        [Header("Ambient light")]
        [Tooltip("Light the scene from three colors (sky, horizon, ground) instead of from the skybox. It changes smoothly with the time of day and keeps the night readable. Untick it to keep the scene's own ambient light and scale it with Night Ambient Intensity.")]
        [SerializeField] private bool _gradientAmbient = true;

        [SerializeField] private Color _dayAmbientSky = new Color(0.62f, 0.74f, 0.92f, 1f);
        [SerializeField] private Color _dayAmbientEquator = new Color(0.60f, 0.62f, 0.60f, 1f);
        [SerializeField] private Color _dayAmbientGround = new Color(0.38f, 0.34f, 0.28f, 1f);

        [Tooltip("The ambient light from above at night. It is the floor of how dark the night gets, so raise it if the scene is too dark.")]
        [SerializeField] private Color _nightAmbientSky = new Color(0.20f, 0.24f, 0.45f, 1f);

        [SerializeField] private Color _nightAmbientEquator = new Color(0.16f, 0.17f, 0.32f, 1f);
        [SerializeField] private Color _nightAmbientGround = new Color(0.08f, 0.07f, 0.12f, 1f);

        [Header("Fog")]
        [Tooltip("Add a soft distance fog tinted like the horizon, for depth. Needs the gradient sky. Not every shader in the scene honors fog.")]
        [SerializeField] private bool _useFog;

        [Tooltip("How thick the fog is (exponential squared). Small values, around 0.005, are a light haze.")]
        [Min(0f)]
        [SerializeField] private float _fogDensity = 0.006f;

        [Header("Old look (no gradient sky, or Gradient Ambient off)")]
        [Tooltip("The ambient light's intensity at night. Used only with Gradient Ambient off. By day it is whatever the scene has.")]
        [Min(0f)]
        [SerializeField] private float _nightAmbientIntensity = 0.2f;

        [Tooltip("The skybox's exposure at night. Used only when the sky is not the gradient sky. By day it is whatever the skybox material has.")]
        [Min(0f)]
        [SerializeField] private float _nightSkyExposure = 0.15f;

        [Header("Debug")]
        [Tooltip("Write the day and night values it found, and each change of the round's state, to the Console. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // The day values, taken when this component is enabled, and put back when it is disabled.
        [NonSerialized] private Color _dayLightColor;
        [NonSerialized] private float _dayLightIntensity;
        [NonSerialized] private Quaternion _dayLightRotation;
        [NonSerialized] private AmbientMode _dayAmbientMode;
        [NonSerialized] private float _dayAmbientIntensity;
        [NonSerialized] private Color _dayAmbientSkyWas;
        [NonSerialized] private Color _dayAmbientEquatorWas;
        [NonSerialized] private Color _dayAmbientGroundWas;
        [NonSerialized] private bool _dayFog;
        [NonSerialized] private FogMode _dayFogMode;
        [NonSerialized] private Color _dayFogColor;
        [NonSerialized] private float _dayFogDensity;
        [NonSerialized] private float _dayGradeWeight;
        [NonSerialized] private float _daySkyExposure;
        [NonSerialized] private Material _originalSkybox;
        [NonSerialized] private Material _skyboxCopy;
        [NonSerialized] private SkyMode _skyMode;

        // The gradient sky's own colors, read once, so the fog can be tinted like its horizon.
        [NonSerialized] private Color _skyDayHorizon;
        [NonSerialized] private Color _skyNightHorizon;
        [NonSerialized] private Color _skyWarmColor;
        [NonSerialized] private float _skyWarmStrength;

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

            _dayAmbientMode = RenderSettings.ambientMode;
            _dayAmbientIntensity = RenderSettings.ambientIntensity;
            _dayAmbientSkyWas = RenderSettings.ambientSkyColor;
            _dayAmbientEquatorWas = RenderSettings.ambientEquatorColor;
            _dayAmbientGroundWas = RenderSettings.ambientGroundColor;
            _dayFog = RenderSettings.fog;
            _dayFogMode = RenderSettings.fogMode;
            _dayFogColor = RenderSettings.fogColor;
            _dayFogDensity = RenderSettings.fogDensity;
            _originalSkybox = RenderSettings.skybox;

            if (_sun == null)
            {
                Debug.LogWarning("DayNightView has no sun light, so the light will not change with the time of day.", this);
            }
            else
            {
                _dayLightColor = _sun.color;
                _dayLightIntensity = _sun.intensity;
                _dayLightRotation = _sun.transform.rotation;
            }

            if (_nightGrade != null)
            {
                _dayGradeWeight = _nightGrade.weight;
            }

            PrepareSkybox();

            if (_useFog && _skyMode != SkyMode.Gradient)
            {
                Debug.LogWarning("Use Fog is ticked, but the sky is not the gradient sky, so the fog has no horizon color to match and is not used.", this);
            }

            _captured = true;
            _applied = false;
            _hasLoggedState = false;

            if (_debugLog)
            {
                Debug.Log("[DayNightView] Enabled. Sky: " + _skyMode + ". Day values: sun " + _dayLightColor + " at " + _dayLightIntensity.ToString("0.##")
                    + ", ambient " + _dayAmbientMode + " (intensity " + _dayAmbientIntensity.ToString("0.##") + "), gradient ambient " + _gradientAmbient
                    + ", fog " + _useFog + ", night grade " + (_nightGrade != null ? _nightGrade.name : "none") + ".", this);
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // Leave the scene as it was found, so a Play session leaves nothing changed. The sun and
            // the volume can already be destroyed when the scene shuts down.
            if (_sun != null)
            {
                _sun.color = _dayLightColor;
                _sun.intensity = _dayLightIntensity;
                _sun.transform.rotation = _dayLightRotation;
            }

            if (_nightGrade != null)
            {
                _nightGrade.weight = _dayGradeWeight;
            }

            RenderSettings.ambientMode = _dayAmbientMode;
            RenderSettings.ambientIntensity = _dayAmbientIntensity;
            RenderSettings.ambientSkyColor = _dayAmbientSkyWas;
            RenderSettings.ambientEquatorColor = _dayAmbientEquatorWas;
            RenderSettings.ambientGroundColor = _dayAmbientGroundWas;

            RenderSettings.fog = _dayFog;
            RenderSettings.fogMode = _dayFogMode;
            RenderSettings.fogColor = _dayFogColor;
            RenderSettings.fogDensity = _dayFogDensity;

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
            float night = DayNightCurve.NightAmount(state, _round.StateProgress);

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

        private void Apply(float night)
        {
            float warmth = DayNightCurve.Warmth(night);

            if (_sun != null)
            {
                _sun.color = Color.Lerp(_dayLightColor, _nightLightColor, night);
                _sun.intensity = Mathf.Lerp(_dayLightIntensity, _nightLightIntensity, night);
                _sun.transform.rotation = Quaternion.Slerp(_dayLightRotation, Quaternion.Euler(_moonLightEuler), night);
            }

            ApplyAmbient(night);
            ApplySky(night, warmth);
            ApplyFog(night);

            if (_nightGrade != null)
            {
                _nightGrade.weight = night;
            }
        }

        private void ApplyAmbient(float night)
        {
            if (_gradientAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.Lerp(_dayAmbientSky, _nightAmbientSky, night);
                RenderSettings.ambientEquatorColor = Color.Lerp(_dayAmbientEquator, _nightAmbientEquator, night);
                RenderSettings.ambientGroundColor = Color.Lerp(_dayAmbientGround, _nightAmbientGround, night);
                return;
            }

            RenderSettings.ambientIntensity = Mathf.Lerp(_dayAmbientIntensity, _nightAmbientIntensity, night);
        }

        private void ApplySky(float night, float warmth)
        {
            if (_skyboxCopy == null)
            {
                return;
            }

            if (_skyMode == SkyMode.Exposure)
            {
                _skyboxCopy.SetFloat(ExposureProperty, Mathf.Lerp(_daySkyExposure, _nightSkyExposure, night));
                return;
            }

            _skyboxCopy.SetFloat(NightProperty, night);
            _skyboxCopy.SetFloat(WarmthProperty, warmth);
            _skyboxCopy.SetVector(SunDirectionProperty, SunSkyDirection());
            _skyboxCopy.SetVector(MoonDirectionProperty, MoonSkyDirection());
        }

        // The fog takes the horizon's color, so the far land melts into the sky.
        private void ApplyFog(float night)
        {
            if (!_useFog || _skyMode != SkyMode.Gradient)
            {
                return;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fogColor = DayNightCurve.HorizonColor(_skyDayHorizon, _skyNightHorizon, _skyWarmColor, night, _skyWarmStrength);
        }

        // Towards the sun, which is the opposite of the way its light shines. Taken from the day direction.
        private Vector4 SunSkyDirection()
        {
            Vector3 towardsSun = _sun != null ? -(_dayLightRotation * Vector3.forward) : Vector3.up;
            return new Vector4(towardsSun.x, towardsSun.y, towardsSun.z, 0f);
        }

        // From the camera's straight-ahead (the +Z direction) turned to the right, then raised.
        private Vector4 MoonSkyDirection()
        {
            float azimuth = _moonAzimuthDegrees * Mathf.Deg2Rad;
            float elevation = _moonElevationDegrees * Mathf.Deg2Rad;
            float flat = Mathf.Cos(elevation);
            return new Vector4(Mathf.Sin(azimuth) * flat, Mathf.Sin(elevation), Mathf.Cos(azimuth) * flat, 0f);
        }

        // The sky is changed on a copy of its material, so the project's own material asset is never
        // edited. The gradient sky is driven by its night amount; any other skybox with an exposure
        // gets the old look.
        private void PrepareSkybox()
        {
            _skyboxCopy = null;
            _skyMode = SkyMode.None;

            if (_originalSkybox == null)
            {
                Debug.LogWarning("The scene has no skybox material, so the sky will not change at night.", this);
                return;
            }

            if (_originalSkybox.HasProperty(NightProperty))
            {
                _skyMode = SkyMode.Gradient;
                _skyDayHorizon = ReadColor(_originalSkybox, DayHorizonProperty);
                _skyNightHorizon = ReadColor(_originalSkybox, NightHorizonProperty);
                _skyWarmColor = ReadColor(_originalSkybox, WarmColorProperty);
                _skyWarmStrength = _originalSkybox.HasProperty(WarmStrengthProperty) ? _originalSkybox.GetFloat(WarmStrengthProperty) : 0f;
            }
            else if (_originalSkybox.HasProperty(ExposureProperty))
            {
                _skyMode = SkyMode.Exposure;
                _daySkyExposure = _originalSkybox.GetFloat(ExposureProperty);
                Debug.LogWarning("The skybox material '" + _originalSkybox.name + "' (shader " + _originalSkybox.shader.name
                    + ") is not the gradient sky, so only its exposure changes at night. Make a material from the shader 'PlanetMakey/Gradient Sky' and use it as the scene's skybox.", this);
            }
            else
            {
                Debug.LogWarning("The skybox material '" + _originalSkybox.name + "' (shader " + _originalSkybox.shader.name
                    + ") has neither a night amount nor an exposure, so the sky will not change at night.", this);
                return;
            }

            _skyboxCopy = new Material(_originalSkybox);
            _skyboxCopy.name = _originalSkybox.name + " (day/night copy)";
            RenderSettings.skybox = _skyboxCopy;
        }

        private static Color ReadColor(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetColor(property) : Color.black;
        }
    }
}
