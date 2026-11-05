using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// The tractor beam: a glowing cone of light that stretches from Makey's underside down to the cow
    /// he is abducting, flares out at the ground, and follows the cow up. It also shines a green spot
    /// light from the ship onto the cow and the ground, grows brighter and whiter as the cow nears the
    /// ship, sputters off when the shield breaks and flashes when the cow is taken. Put it on a Cylinder
    /// at the root of the scene, with no collider and a material that uses the PlanetMakey/Beam shader.
    /// It sets its own position, rotation and size every frame, so it must not sit under a scaled or
    /// rotated parent.
    /// An optional second cylinder, the halo, is a wider and softer column of light that stays where the
    /// cow stood and surrounds her the whole way up, while the beam inside it follows her. It follows the
    /// same rules as the beam.
    /// </summary>
    public sealed class BeamView : MonoBehaviour
    {
        // The properties of the PlanetMakey/Beam shader.
        private const string ColorProperty = "_BaseColor";
        private const string IntensityProperty = "_Intensity";
        private const string FadeProperty = "_Fade";
        private const string UrgencyProperty = "_Urgency";
        private const string PhaseProperty = "_Phase";
        private const string LengthProperty = "_Length";
        private const string TopFlareProperty = "_TopFlare";
        private const string BottomFlareProperty = "_BottomFlare";
        private const string FlarePowerProperty = "_FlarePower";

        // The rise phase wraps only after many hours, so its one-frame jump is never seen in a day at the booth.
        private const float PhaseWrap = 100000f;

        // How fast the pulse of an urgent lift beats, in beats per second, at the start and at the end.
        private const float CalmPulsePerSecond = 1f;
        private const float UrgentPulsePerSecond = 3f;

        [Header("Wiring")]
        [SerializeField] private Makey _makey;

        [Header("Shape")]
        [Tooltip("How wide the beam is before it is flared, in meters. The width at the ship and at the ground are multiples of this.")]
        [Min(0.01f)]
        [SerializeField] private float _width = 0.5f;

        [Tooltip("The width at the ship, as a multiple of Width.")]
        [Min(0.05f)]
        [SerializeField] private float _topFlare = 0.8f;

        [Tooltip("The width where the beam meets the ground, as a multiple of Width. This is the flare.")]
        [Min(0.5f)]
        [SerializeField] private float _bottomFlare = 5f;

        [Tooltip("How late the flare starts. 1 is a straight cone; 2 or more keeps the beam slim for most of its length and spreads it into a skirt at the ground.")]
        [Min(0.5f)]
        [SerializeField] private float _flarePower = 2.2f;

        [Tooltip("How far below the cow's position the beam reaches, in meters, so its flared foot ends at her feet and not at her middle. The terrain hides any part of the beam under the ground.")]
        [Min(0f)]
        [SerializeField] private float _reachBelowCow = 0.5f;

        [Header("Halo")]
        [Tooltip("A wider, softer cylinder of light that stays where the cow stood and fades in and out around her while the beam lifts her. Leave empty for no halo. It needs its own cylinder at the root of the scene, with no collider and its own material that uses the PlanetMakey/Beam shader with Streak Strength and Ring Strength at 0.")]
        [SerializeField] private Renderer _halo;

        [Tooltip("How wide the halo is before it is flared, in meters.")]
        [Min(0.01f)]
        [SerializeField] private float _haloWidth = 1.6f;

        [Tooltip("The halo's width at the ship, as a multiple of Halo Width.")]
        [Min(0.05f)]
        [SerializeField] private float _haloTopFlare = 0.6f;

        [Tooltip("The halo's width at the ground, as a multiple of Halo Width.")]
        [Min(0.5f)]
        [SerializeField] private float _haloBottomFlare = 2.2f;

        [Tooltip("How late the halo's flare starts. 1 is a straight cone.")]
        [Min(0.5f)]
        [SerializeField] private float _haloFlarePower = 1.4f;

        [Tooltip("How bright the halo is. Keep it below the beam's intensity, or the two add up to a blown-out middle.")]
        [Range(0f, 12f)]
        [SerializeField] private float _haloIntensity = 1.2f;

        [Tooltip("Seconds the halo takes to fade in when the beam comes down.")]
        [Min(0f)]
        [SerializeField] private float _haloFadeInSeconds = 0.6f;

        [Tooltip("Seconds the halo takes to fade out when the cow is taken. When the shield breaks it goes with the beam instead.")]
        [Min(0f)]
        [SerializeField] private float _haloFadeOutSeconds = 0.5f;

        [Tooltip("Seconds for one slow swell of the halo's brightness. 0 for a steady glow.")]
        [Min(0f)]
        [SerializeField] private float _haloBreathSeconds = 2.5f;

        [Tooltip("How far the halo dims at the low point of its swell, as a share of its brightness.")]
        [Range(0f, 1f)]
        [SerializeField] private float _haloBreathDepth = 0.35f;

        [Header("Glow")]
        [Tooltip("The color of the beam and of its light.")]
        [SerializeField] private Color _color = new Color(0.71f, 0.95f, 0.23f, 1f);

        [Tooltip("How hard the beam glows. Values above 1 are brighter than the screen's white, so Bloom makes it glow.")]
        [Range(0f, 12f)]
        [SerializeField] private float _intensity = 3f;

        [Header("Motion")]
        [Tooltip("How fast the rings and streaks rise up the beam at the start of the lift, in world units per second.")]
        [Min(0f)]
        [SerializeField] private float _riseSpeed = 2f;

        [Tooltip("How fast they rise when the cow is almost at the ship.")]
        [Min(0f)]
        [SerializeField] private float _urgentRiseSpeed = 7f;

        [Tooltip("The share of the lift (0 to 1) after which the beam grows urgent: faster, pulsing and whiter. It matches the shield's flicker.")]
        [Range(0f, 1f)]
        [SerializeField] private float _urgencyStart = 0.6f;

        [Tooltip("How much the beam's brightness pulses at the worst of the urgency, as a share of its brightness.")]
        [Range(0f, 1f)]
        [SerializeField] private float _pulseDepth = 0.35f;

        [Header("Ending")]
        [Tooltip("Seconds the beam sputters off after the shield breaks and the cow is dropped.")]
        [Min(0f)]
        [SerializeField] private float _dropFadeSeconds = 0.2f;

        [Tooltip("Seconds of the bright flash at the ship when the cow is taken.")]
        [Min(0f)]
        [SerializeField] private float _absorbFlashSeconds = 0.3f;

        [Header("Light on the world")]
        [Tooltip("Shine a spot light from the ship down the beam, so the cow and the ground around it are lit by it. It is created when the game starts.")]
        [SerializeField] private bool _useLight = true;

        [Tooltip("The light's color. It is usually the beam's color.")]
        [SerializeField] private Color _lightColor = new Color(0.71f, 0.95f, 0.23f, 1f);

        [Tooltip("How bright the light is. A realtime spot light of about 25 lights the ground a few meters away clearly.")]
        [Min(0f)]
        [SerializeField] private float _lightIntensity = 25f;

        [Tooltip("How many times brighter the light flashes when the cow is taken.")]
        [Min(1f)]
        [SerializeField] private float _lightFlashBoost = 3f;

        [Header("Debug")]
        [Tooltip("Write what the beam does to the Console. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Hidden by turning its renderer off, so this component keeps running.
        [NonSerialized] private Renderer _renderer;
        [NonSerialized] private bool _rendererWasEnabled;
        [NonSerialized] private ShadowCastingMode _shadowModeWas;
        [NonSerialized] private bool _shown;
        [NonSerialized] private bool _captured;
        [NonSerialized] private MaterialPropertyBlock _block;
        [NonSerialized] private Light _light;

        // The state of the ending effects and of the rising pattern.
        [NonSerialized] private float _risePhase;
        [NonSerialized] private float _pulsePhase;
        [NonSerialized] private float _dropLeft;
        [NonSerialized] private float _flashLeft;
        [NonSerialized] private Vector3 _lastBottom;

        // The halo: the same kind of state as the beam's. Its ends are kept so it can fade out where it was.
        [NonSerialized] private bool _haloReady;
        [NonSerialized] private bool _haloShown;
        [NonSerialized] private bool _haloWasEnabled;
        [NonSerialized] private ShadowCastingMode _haloShadowModeWas;
        [NonSerialized] private MaterialPropertyBlock _haloBlock;
        [NonSerialized] private float _haloLevel;
        [NonSerialized] private bool _haloHasCow;
        [NonSerialized] private Vector3 _haloTop;
        [NonSerialized] private Vector3 _haloGround;
        [NonSerialized] private Vector3 _haloRestPosition;
        [NonSerialized] private Quaternion _haloRestRotation;
        [NonSerialized] private Vector3 _haloRestScale;

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

            Material material = _renderer.sharedMaterial;
            if (material == null || !material.HasProperty(IntensityProperty))
            {
                Debug.LogWarning("The beam's material" + (material != null ? " '" + material.name + "' (shader " + material.shader.name + ")" : "")
                    + " has no " + IntensityProperty + ", so the beam will not glow or flare. Give it the shader 'PlanetMakey/Beam'.", this);
            }

            _restPosition = transform.position;
            _restRotation = transform.rotation;
            _restScale = transform.localScale;
            _rendererWasEnabled = _renderer.enabled;
            _shadowModeWas = _renderer.shadowCastingMode;
            _renderer.enabled = false;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;

            // The skirt is widened by the shader, so the renderer's box is widened to match, or the
            // skirt would be culled when the beam is near the edge of the picture.
            float widest = Mathf.Max(_topFlare, _bottomFlare) * 1.1f;
            _renderer.localBounds = new Bounds(Vector3.zero, new Vector3(widest, 2f, widest));
            _shown = false;
            _captured = true;

            _risePhase = 0f;
            _pulsePhase = 0f;
            _dropLeft = 0f;
            _flashLeft = 0f;

            SetUpHalo();

            if (_useLight)
            {
                CreateLight();
            }

            _makey.CowDropped += OnCowDropped;
            _makey.CowAbsorbed += OnCowAbsorbed;

            if (_debugLog)
            {
                Debug.Log("[BeamView] Enabled. Watching " + _makey.name + ", width " + _width.ToString("0.0#") + " m, flaring to x"
                    + _bottomFlare.ToString("0.0#") + " at the ground, light " + (_light != null ? "on" : "off") + ".", this);
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // Any of these can already be destroyed when the scene shuts down.
            if (_makey != null)
            {
                _makey.CowDropped -= OnCowDropped;
                _makey.CowAbsorbed -= OnCowAbsorbed;
            }

            if (_light != null)
            {
                Destroy(_light.gameObject);
                _light = null;
            }

            // Leave the beam as it was found, so a Play session leaves nothing changed.
            transform.position = _restPosition;
            transform.rotation = _restRotation;
            transform.localScale = _restScale;

            if (_renderer != null)
            {
                _renderer.SetPropertyBlock(null);
                _renderer.ResetLocalBounds();
                _renderer.shadowCastingMode = _shadowModeWas;
                _renderer.enabled = _rendererWasEnabled;
            }

            TearDownHalo();
        }

        // Prepares the halo cylinder, if one is wired: hidden, with a box wide enough for its flare.
        private void SetUpHalo()
        {
            _haloReady = false;
            _haloShown = false;
            _haloLevel = 0f;
            _haloHasCow = false;

            if (_halo == null)
            {
                return;
            }

            if (_halo == _renderer)
            {
                Debug.LogWarning("The halo is the same renderer as the beam. Give it a cylinder of its own, or leave it empty.", this);
                return;
            }

            if (_halo.GetComponent<Collider>() != null)
            {
                Debug.LogWarning("The halo has a collider. Remove it, or balls aimed at Makey can hit the halo instead.", this);
            }

            if (_halo.transform.parent != null)
            {
                Debug.LogWarning("The halo has a parent. Its size is set in world terms, so a parent that is scaled or turned will distort it.", this);
            }

            Material material = _halo.sharedMaterial;
            if (material == null || !material.HasProperty(IntensityProperty))
            {
                Debug.LogWarning("The halo's material" + (material != null ? " '" + material.name + "' (shader " + material.shader.name + ")" : "")
                    + " has no " + IntensityProperty + ", so the halo will not glow or flare. Give it the shader 'PlanetMakey/Beam'.", this);
            }

            Transform haloTransform = _halo.transform;
            _haloRestPosition = haloTransform.position;
            _haloRestRotation = haloTransform.rotation;
            _haloRestScale = haloTransform.localScale;
            _haloWasEnabled = _halo.enabled;
            _haloShadowModeWas = _halo.shadowCastingMode;
            _halo.enabled = false;
            _halo.shadowCastingMode = ShadowCastingMode.Off;

            float widest = Mathf.Max(_haloTopFlare, _haloBottomFlare) * 1.1f;
            _halo.localBounds = new Bounds(Vector3.zero, new Vector3(widest, 2f, widest));
            _haloReady = true;
        }

        // Puts the halo cylinder back as it was found.
        private void TearDownHalo()
        {
            if (!_haloReady)
            {
                return;
            }

            _haloReady = false;

            if (_halo == null)
            {
                return;
            }

            Transform haloTransform = _halo.transform;
            haloTransform.position = _haloRestPosition;
            haloTransform.rotation = _haloRestRotation;
            haloTransform.localScale = _haloRestScale;
            _halo.SetPropertyBlock(null);
            _halo.ResetLocalBounds();
            _halo.shadowCastingMode = _haloShadowModeWas;
            _halo.enabled = _haloWasEnabled;
        }

        // After Makey's Update has moved him, so the beam never trails a frame behind.
        private void LateUpdate()
        {
            if (!_captured)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _dropLeft = Mathf.Max(0f, _dropLeft - deltaTime);
            _flashLeft = Mathf.Max(0f, _flashLeft - deltaTime);

            AbductionPhase phase = _makey.AbductionPhase;
            Vector3 top = _makey.BeamOrigin;
            bool active = _makey.TargetCowIndex >= 0 && (phase == AbductionPhase.LoweringBeam || phase == AbductionPhase.Lifting);

            if (active)
            {
                // The beam reaches down to the cow's feet, then follows her up.
                Vector3 cowFeet = _makey.TargetCowPosition + Vector3.down * _reachBelowCow;
                Vector3 bottom = phase == AbductionPhase.LoweringBeam
                    ? Vector3.Lerp(top, cowFeet, _makey.AbductionProgress)
                    : cowFeet;
                _lastBottom = bottom;

                // The halo is fixed to the spot where the cow stood, taken as the beam starts to come down.
                if (!_haloHasCow)
                {
                    _haloHasCow = true;
                    _haloGround = cowFeet;
                    _haloLevel = 0f;
                }

                _haloTop = top;
                UpdateHalo(true, _haloFadeInSeconds, 1f, deltaTime);

                float urgency = phase == AbductionPhase.Lifting ? BeamShape.Urgency(_makey.AbductionProgress, _urgencyStart) : 0f;
                DrawBeam(top, bottom, 1f, urgency, deltaTime);
                return;
            }

            _haloHasCow = false;

            if (_dropLeft > 0f)
            {
                // The shield broke: the beam, still where it was, sputters out, and the halo with it.
                float fade = BeamShape.FadeOut(_dropLeft, _dropFadeSeconds);
                float sputter = Mathf.Sin(Time.time * 80f) > -0.2f ? 1f : 0.3f;
                UpdateHalo(false, _dropFadeSeconds, sputter, deltaTime);
                DrawBeam(top, _lastBottom, fade * sputter, 0f, deltaTime);
                return;
            }

            // The cow was taken: the beam is gone at once, and the halo fades away where it stood.
            UpdateHalo(false, _haloFadeOutSeconds, 1f, deltaTime);
            Show(false);
            UpdateLight(false, top, top + Vector3.down, 1f, 0f);
        }

        // Fades the halo towards on or off and shapes it between the ship and the cow's starting spot.
        // "sputter" dims it further, to flicker with the beam.
        private void UpdateHalo(bool wanted, float stepSeconds, float sputter, float deltaTime)
        {
            if (!_haloReady)
            {
                return;
            }

            _haloLevel = BeamShape.MoveLevel(_haloLevel, wanted ? 1f : 0f, deltaTime, stepSeconds);
            float fade = Mathf.SmoothStep(0f, 1f, _haloLevel) * sputter;

            Vector3 along = _haloTop - _haloGround;
            float length = along.magnitude;
            if (fade <= 0.001f || length < 0.001f)
            {
                ShowHalo(false);
                return;
            }

            Transform haloTransform = _halo.transform;
            haloTransform.position = (_haloTop + _haloGround) * 0.5f;
            haloTransform.rotation = Quaternion.FromToRotation(Vector3.up, along / length);
            haloTransform.localScale = new Vector3(_haloWidth, length * 0.5f, _haloWidth);

            if (_haloBlock == null)
            {
                _haloBlock = new MaterialPropertyBlock();
            }

            float breath = BeamShape.Breath(Time.time, _haloBreathSeconds, _haloBreathDepth);
            _haloBlock.SetColor(ColorProperty, _color);
            _haloBlock.SetFloat(IntensityProperty, _haloIntensity * breath);
            _haloBlock.SetFloat(FadeProperty, fade);
            _haloBlock.SetFloat(UrgencyProperty, 0f);
            _haloBlock.SetFloat(PhaseProperty, 0f);
            _haloBlock.SetFloat(LengthProperty, length);
            _haloBlock.SetFloat(TopFlareProperty, _haloTopFlare);
            _haloBlock.SetFloat(BottomFlareProperty, _haloBottomFlare);
            _haloBlock.SetFloat(FlarePowerProperty, _haloFlarePower);
            _halo.SetPropertyBlock(_haloBlock);

            ShowHalo(true);
        }

        private void ShowHalo(bool shown)
        {
            if (shown == _haloShown)
            {
                return;
            }

            _haloShown = shown;
            _halo.enabled = shown;

            if (_debugLog)
            {
                Debug.Log("[BeamView] Halo " + (shown ? "on" : "off") + ".", this);
            }
        }

        // Shapes and lights the beam between two points. Returns false, and hides it, if they are too close to see.
        private bool DrawBeam(Vector3 top, Vector3 bottom, float fade, float urgency, float deltaTime)
        {
            Vector3 along = top - bottom;
            float length = along.magnitude;
            if (length < 0.001f)
            {
                Show(false);
                UpdateLight(false, top, bottom, length, 0f);
                return false;
            }

            // A Unity cylinder is 2 units tall, hence half the length for the height.
            transform.position = (top + bottom) * 0.5f;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, along / length);
            transform.localScale = new Vector3(_width, length * 0.5f, _width);

            // The pattern rises faster as the lift runs out, and the beam pulses harder.
            float riseSpeed = Mathf.Lerp(_riseSpeed, _urgentRiseSpeed, urgency);
            _risePhase = Mathf.Repeat(_risePhase + riseSpeed * deltaTime, PhaseWrap);

            // The pulse is a phase that advances at the current beat rate, so a rising rate speeds it up
            // smoothly and never makes it jump.
            _pulsePhase = Mathf.Repeat(_pulsePhase + 2f * Mathf.PI * Mathf.Lerp(CalmPulsePerSecond, UrgentPulsePerSecond, urgency) * deltaTime, 2f * Mathf.PI);
            float pulse = 1f + _pulseDepth * urgency * Mathf.Sin(_pulsePhase);

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.SetColor(ColorProperty, _color);
            _block.SetFloat(IntensityProperty, _intensity * pulse);
            _block.SetFloat(FadeProperty, fade);
            _block.SetFloat(UrgencyProperty, urgency);
            _block.SetFloat(PhaseProperty, _risePhase);
            _block.SetFloat(LengthProperty, length);
            _block.SetFloat(TopFlareProperty, _topFlare);
            _block.SetFloat(BottomFlareProperty, _bottomFlare);
            _block.SetFloat(FlarePowerProperty, _flarePower);
            _renderer.SetPropertyBlock(_block);

            Show(true);
            UpdateLight(true, top, bottom, length, fade * pulse);
            return true;
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

        private void CreateLight()
        {
            GameObject lightObject = new GameObject("Beam light (runtime)");
            lightObject.hideFlags = HideFlags.DontSave;
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.shadows = LightShadows.None;
            _light.color = _lightColor;
            _light.enabled = false;
        }

        // The spot light shines from the ship down the beam. While the cow is taken it flashes at the
        // ship even though the beam is gone.
        private void UpdateLight(bool beamOn, Vector3 top, Vector3 bottom, float length, float strength)
        {
            if (_light == null)
            {
                return;
            }

            float flash = _absorbFlashSeconds > 0f ? _flashLeft / _absorbFlashSeconds : 0f;
            bool on = beamOn || flash > 0f;
            if (!on)
            {
                _light.enabled = false;
                return;
            }

            Vector3 direction = bottom - top;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.down;
            }

            float groundRadius = 0.5f * _width * _bottomFlare;
            float spotLength = beamOn ? length : 3f;
            _light.transform.position = top;
            _light.transform.rotation = Quaternion.LookRotation(direction.normalized);
            _light.color = _lightColor;
            _light.range = spotLength * 1.5f + 2f;
            _light.spotAngle = BeamShape.SpotAngleDegrees(groundRadius, spotLength, 1.3f, 25f, 130f);
            _light.innerSpotAngle = _light.spotAngle * 0.4f;
            _light.intensity = _lightIntensity * (beamOn ? strength : 0f) + _lightIntensity * _lightFlashBoost * flash;
            _light.enabled = true;
        }

        private void OnCowDropped(int index, bool wasLifted)
        {
            // Only a beam that is showing needs to sputter out.
            if (_shown)
            {
                _dropLeft = _dropFadeSeconds;
            }
        }

        private void OnCowAbsorbed(int index)
        {
            _flashLeft = _absorbFlashSeconds;
        }
    }
}
