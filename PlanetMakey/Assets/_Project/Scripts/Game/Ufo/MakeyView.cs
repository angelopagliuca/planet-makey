using System;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// How Makey looks while he plays: the colored shield, how it bursts, the flash and sparks
    /// when a wrong color hits, and the bob, spin and head shake of the model. It only reads
    /// from <see cref="Makey"/> and holds no game state. These are placeholder effects made in
    /// code, to be replaced as real animation arrives.
    /// </summary>
    public sealed class MakeyView : MonoBehaviour
    {
        // The color property of the URP Lit shader.
        private const string BaseColorProperty = "_BaseColor";

        [Header("Wiring")]
        [SerializeField] private Makey _makey;

        [Tooltip("Display colors for the ball colors.")]
        [SerializeField] private BallColorPalette _palette;

        [Tooltip("The object that holds the model. It is bobbed, spun and shaken, so the collider on the Makey object stays still.")]
        [SerializeField] private Transform _modelPivot;

        [Tooltip("The shield: a sphere around the model with no collider, using a transparent URP Lit material.")]
        [SerializeField] private Renderer _shield;

        [Tooltip("Optional. Switched on while Makey is dazed, for example a few small stars.")]
        [SerializeField] private GameObject _dazeEffect;

        [Tooltip("Optional. Gives a burst in the ball's color when a wrong color hits. Set Play On Awake off and Simulation Space to World.")]
        [SerializeField] private ParticleSystem _sparks;

        [Header("Shield")]
        [Tooltip("How see-through the shield is, from 0 (invisible) to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _shieldAlpha = 0.4f;

        [Tooltip("Seconds the shield takes to burst.")]
        [Min(0.01f)]
        [SerializeField] private float _shatterSeconds = 0.35f;

        [Tooltip("How many times its size the shield grows to while it bursts.")]
        [Min(1f)]
        [SerializeField] private float _shatterGrowth = 1.5f;

        [Tooltip("Seconds the shield glows in the ball's color after a wrong color hits.")]
        [Min(0.01f)]
        [SerializeField] private float _flashSeconds = 0.25f;

        [Tooltip("Particles in one burst of sparks.")]
        [Min(0)]
        [SerializeField] private int _sparkCount = 20;

        [Header("Motion")]
        [Tooltip("How far the model bobs up and down, in meters.")]
        [Min(0f)]
        [SerializeField] private float _bobHeight = 0.15f;

        [Tooltip("Bobs per second.")]
        [Min(0f)]
        [SerializeField] private float _bobsPerSecond = 0.6f;

        [Tooltip("Full turns made while dazed. Keep it a whole number so the model ends facing the front.")]
        [Min(0f)]
        [SerializeField] private float _dazeTurns = 3f;

        [Tooltip("How far the head swings to each side while recovering, in degrees.")]
        [Min(0f)]
        [SerializeField] private float _headShakeDegrees = 15f;

        [Tooltip("Swings from side to side while recovering.")]
        [Min(0f)]
        [SerializeField] private float _headShakeCycles = 4f;

        [Tooltip("Seconds the model rattles after a wrong color hits.")]
        [Min(0.01f)]
        [SerializeField] private float _rattleSeconds = 0.3f;

        [Tooltip("How far the model tilts to each side while it rattles, in degrees.")]
        [Min(0f)]
        [SerializeField] private float _rattleDegrees = 10f;

        [Tooltip("Tilts from side to side during one rattle.")]
        [Min(0f)]
        [SerializeField] private float _rattleCycles = 3f;

        [Header("Debug")]
        [Tooltip("Write what the view shows to the Console: how it is wired, every state it sees, every effect it starts, and a status line once a second. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Seconds between the status lines.
        private const float StatusLogInterval = 1f;

        // What the shield was last drawn as, for the status line.
        [NonSerialized] private bool _shieldShown;
        [NonSerialized] private Color _shieldDrawnColor;
        [NonSerialized] private float _shieldDrawnAlpha;
        [NonSerialized] private float _shieldDrawnSize;
        [NonSerialized] private bool _hasLoggedState;
        [NonSerialized] private MakeyState _lastLoggedState;
        [NonSerialized] private float _nextStatusLogTime;

        // Where things were when this component was enabled, put back when it is disabled.
        [NonSerialized] private Vector3 _pivotPosition;
        [NonSerialized] private Quaternion _pivotRotation;
        [NonSerialized] private Vector3 _shieldScale;
        [NonSerialized] private bool _shieldWasEnabled;
        [NonSerialized] private bool _captured;

        // A property block overrides the color without creating or editing a material.
        [NonSerialized] private MaterialPropertyBlock _block;

        // Counted down to zero by Update. Zero means the effect is not running.
        [NonSerialized] private float _shatterLeft;
        [NonSerialized] private Color _shatterColor;
        [NonSerialized] private float _flashLeft;
        [NonSerialized] private Color _flashColor;
        [NonSerialized] private float _rattleLeft;
        [NonSerialized] private bool _dazeShown;

        private void OnEnable()
        {
            if (_makey == null || _modelPivot == null || _shield == null)
            {
                Debug.LogError("MakeyView needs a Makey, a model pivot and a shield, so it will do nothing.", this);
                return;
            }

            if (_palette == null)
            {
                Debug.LogWarning("MakeyView has no palette, so the shield will be white.", this);
            }

            _pivotPosition = _modelPivot.localPosition;
            _pivotRotation = _modelPivot.localRotation;
            _shieldScale = _shield.transform.localScale;
            _shieldWasEnabled = _shield.enabled;
            _captured = true;

            _shatterLeft = 0f;
            _flashLeft = 0f;
            _rattleLeft = 0f;

            // Start with the stars off, whatever the scene was saved with.
            _dazeShown = false;
            if (_dazeEffect != null)
            {
                _dazeEffect.SetActive(false);
            }

            _shieldShown = false;
            _hasLoggedState = false;
            _nextStatusLogTime = 0f;

            _makey.ShieldShattered += OnShieldShattered;
            _makey.BallDeflected += OnBallDeflected;

            CheckWiring();
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
                _makey.ShieldShattered -= OnShieldShattered;
                _makey.BallDeflected -= OnBallDeflected;
            }

            // Leave everything as it was found, so a Play session leaves nothing behind.
            if (_modelPivot != null)
            {
                _modelPivot.localPosition = _pivotPosition;
                _modelPivot.localRotation = _pivotRotation;
            }

            if (_shield != null)
            {
                _shield.transform.localScale = _shieldScale;
                _shield.SetPropertyBlock(null);
                _shield.enabled = _shieldWasEnabled;
            }

            if (_dazeEffect != null)
            {
                _dazeEffect.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _shatterLeft = Mathf.Max(0f, _shatterLeft - deltaTime);
            _flashLeft = Mathf.Max(0f, _flashLeft - deltaTime);
            _rattleLeft = Mathf.Max(0f, _rattleLeft - deltaTime);

            MakeyState state = _makey.State;
            UpdateShield(state);
            UpdatePivot(state);
            UpdateDazeEffect(state);
            LogState(state);
        }

        private void OnShieldShattered(BallColor color)
        {
            _shatterLeft = _shatterSeconds;
            _shatterColor = GetDisplayColor(color);

            if (_debugLog)
            {
                Log("Shield broken by " + color + ": it bursts for " + _shatterSeconds.ToString("0.0#") + " s (grows to x"
                    + _shatterGrowth.ToString("0.0#") + " and fades).");
            }
        }

        private void OnBallDeflected(BallColor color, Vector3 worldPoint)
        {
            _flashLeft = _flashSeconds;
            _flashColor = GetDisplayColor(color);
            _rattleLeft = _rattleSeconds;

            if (_sparks == null)
            {
                if (_debugLog)
                {
                    Log("Wrong color " + color + ": the shield glows " + color + " for " + _flashSeconds.ToString("0.0#") + " s and the model rattles for "
                        + _rattleSeconds.ToString("0.0#") + " s. No sparks: the Sparks field is empty.");
                }

                return;
            }

            _sparks.transform.position = worldPoint;
            ParticleSystem.MainModule main = _sparks.main;
            main.startColor = _flashColor;
            _sparks.Emit(_sparkCount);

            if (_debugLog)
            {
                Log("Wrong color " + color + ": the shield glows " + color + " for " + _flashSeconds.ToString("0.0#") + " s, the model rattles for "
                    + _rattleSeconds.ToString("0.0#") + " s, and " + _sparkCount + " sparks were emitted at " + worldPoint.ToString("F2")
                    + " (particles alive now: " + _sparks.particleCount + ").");
            }
        }

        private void UpdateShield(MakeyState state)
        {
            if (state == MakeyState.Shielded)
            {
                // A glow in the ball's color fades back into the shield's own color.
                Color shieldColor = GetDisplayColor(_makey.ShieldColor);
                float glow = _flashSeconds > 0f ? _flashLeft / _flashSeconds : 0f;
                Color color = Color.Lerp(shieldColor, _flashColor, glow);
                float alpha = Mathf.Lerp(_shieldAlpha, 0.9f, glow);

                ShowShield(color, alpha, 1f);
                return;
            }

            if (_shatterLeft > 0f)
            {
                // Bursting: the shield grows and fades away.
                float burst = 1f - _shatterLeft / _shatterSeconds;
                ShowShield(_shatterColor, _shieldAlpha * (1f - burst), Mathf.Lerp(1f, _shatterGrowth, burst));
                return;
            }

            _shield.enabled = false;
            _shieldShown = false;
        }

        private void ShowShield(Color color, float alpha, float sizeFactor)
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.SetColor(BaseColorProperty, new Color(color.r, color.g, color.b, alpha));
            _shield.SetPropertyBlock(_block);
            _shield.transform.localScale = _shieldScale * sizeFactor;
            _shield.enabled = true;

            _shieldShown = true;
            _shieldDrawnColor = color;
            _shieldDrawnAlpha = alpha;
            _shieldDrawnSize = sizeFactor;
        }

        private void UpdatePivot(MakeyState state)
        {
            Vector3 position = _pivotPosition;
            Quaternion rotation = _pivotRotation;

            if (state != MakeyState.Away)
            {
                position.y += Mathf.Sin(Time.time * 2f * Mathf.PI * _bobsPerSecond) * _bobHeight;
            }

            if (state == MakeyState.Dazed)
            {
                // Fast at first and slowing to a stop, finishing on a whole number of turns.
                float progress = _makey.StateProgress;
                float eased = 1f - (1f - progress) * (1f - progress);
                rotation = _pivotRotation * Quaternion.Euler(0f, 360f * _dazeTurns * eased, 0f);
            }
            else if (state == MakeyState.Recovering)
            {
                // Swings from side to side, less and less.
                float progress = _makey.StateProgress;
                float swing = Mathf.Sin(progress * _headShakeCycles * 2f * Mathf.PI) * _headShakeDegrees * (1f - progress);
                rotation = _pivotRotation * Quaternion.Euler(0f, swing, 0f);
            }

            if (_rattleLeft > 0f)
            {
                // A quick tilt from side to side, dying away.
                float elapsed = 1f - _rattleLeft / _rattleSeconds;
                float tilt = Mathf.Sin(elapsed * _rattleCycles * 2f * Mathf.PI) * _rattleDegrees * (1f - elapsed);
                rotation *= Quaternion.Euler(0f, 0f, tilt);
            }

            _modelPivot.localPosition = position;
            _modelPivot.localRotation = rotation;
        }

        private void UpdateDazeEffect(MakeyState state)
        {
            if (_dazeEffect == null)
            {
                return;
            }

            bool dazed = state == MakeyState.Dazed;
            if (dazed == _dazeShown)
            {
                return;
            }

            _dazeShown = dazed;
            _dazeEffect.SetActive(dazed);
        }

        // Flags a setup that would leave the shield invisible or wrong. The warnings always show;
        // the summary is part of the debug log.
        private void CheckWiring()
        {
            Material material = _shield.sharedMaterial;
            string shaderName = material != null && material.shader != null ? material.shader.name : "none";

            if (material == null)
            {
                Debug.LogWarning("The shield renderer '" + _shield.name + "' has no material, so it will draw nothing.", this);
            }
            else if (!material.HasProperty(BaseColorProperty))
            {
                Debug.LogWarning("The shield material '" + material.name + "' (shader " + shaderName
                    + ") has no _BaseColor, so the tint will not show. Use a URP Lit material.", this);
            }
            else if (material.HasProperty("_Surface") && material.GetFloat("_Surface") < 0.5f)
            {
                Debug.LogWarning("The shield material '" + material.name + "' is Opaque, so the shield will be a solid ball. Set its Surface Type to Transparent.", this);
            }

            if (!_shield.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("The shield object '" + _shield.name + "' is inactive, so it can never show.", this);
            }

            if (_shield.GetComponent<Collider>() != null)
            {
                Debug.LogWarning("The shield object '" + _shield.name + "' has a collider. Remove it, or balls meant for Makey may stop at the shield.", this);
            }

            if (!_debugLog)
            {
                return;
            }

            Log("Enabled. Watching " + _makey.name + ". Model pivot '" + _modelPivot.name + "' at local " + _pivotPosition.ToString("F2")
                + ". Shield '" + _shield.name + "', material " + (material != null ? material.name : "none") + ", shader " + shaderName
                + ", scale " + _shieldScale.ToString("F2") + ". Palette " + (_palette != null ? _palette.name : "MISSING")
                + ". Daze stars " + (_dazeEffect != null ? _dazeEffect.name : "not set (no stars)")
                + ". Sparks " + (_sparks != null ? _sparks.name : "not set (no sparks)") + ".");
        }

        // Says what the view is doing, once for every state it sees and then once a second.
        private void LogState(MakeyState state)
        {
            if (!_debugLog)
            {
                return;
            }

            if (!_hasLoggedState || state != _lastLoggedState)
            {
                _hasLoggedState = true;
                _lastLoggedState = state;
                Log("Sees Makey " + state + ". " + DescribeLook(state));
            }

            if (Time.unscaledTime < _nextStatusLogTime)
            {
                return;
            }

            _nextStatusLogTime = Time.unscaledTime + StatusLogInterval;
            Log("Status: " + state + ". Shield " + (_shieldShown
                    ? "shown, color " + _shieldDrawnColor.ToString("F2") + ", alpha " + _shieldDrawnAlpha.ToString("0.00") + ", size x" + _shieldDrawnSize.ToString("0.00")
                    : "hidden")
                + " (renderer enabled " + _shield.enabled + "). Pivot at local " + _modelPivot.localPosition.ToString("F2")
                + ", turned " + _modelPivot.localEulerAngles.ToString("F0")
                + ". Effects left: burst " + _shatterLeft.ToString("0.00") + " s, glow " + _flashLeft.ToString("0.00")
                + " s, rattle " + _rattleLeft.ToString("0.00") + " s.");
        }

        private string DescribeLook(MakeyState state)
        {
            switch (state)
            {
                case MakeyState.Away:
                    return "Shield hidden, model not bobbing.";
                case MakeyState.Entering:
                    return "Shield hidden while he flies in; the model bobs.";
                case MakeyState.Shielded:
                    return "Showing a " + _makey.ShieldColor + " shield.";
                case MakeyState.Dazed:
                    return "Shield hidden (unless still bursting); the model spins " + _dazeTurns.ToString("0.#") + " turns"
                        + (_dazeEffect != null ? " and the stars are on." : " (no stars set).");
                case MakeyState.Recovering:
                    return "Shield hidden; the head swings side to side " + _headShakeCycles.ToString("0.#") + " times.";
                default:
                    return string.Empty;
            }
        }

        private void Log(string message)
        {
            if (_debugLog)
            {
                Debug.Log("[MakeyView] " + message, this);
            }
        }

        private Color GetDisplayColor(BallColor ballColor)
        {
            if (_palette == null)
            {
                return Color.white;
            }

            return _palette.GetColor(ballColor);
        }
    }
}
