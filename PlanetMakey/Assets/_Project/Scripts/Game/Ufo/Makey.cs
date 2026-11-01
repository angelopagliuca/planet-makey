using System;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// Makey the UFO as a thing in the scene: it flies in from a spawn point, can be hit by balls,
    /// and tells the rest of the game what happens through events. The rules are in
    /// <see cref="MakeyBrain"/>; how it all looks is up to <see cref="MakeyView"/>.
    /// </summary>
    /// <remarks>
    /// Put this on the object that carries the collider, on the layer the hit resolver can hit.
    /// The hit resolver finds it through <see cref="IBallTarget"/>. This object is moved, so the
    /// spawn and hover points must not be its children.
    /// </remarks>
    public sealed class Makey : MonoBehaviour, IBallTarget
    {
        [Header("Wiring")]
        [Tooltip("The timings that set how hard Makey is to beat.")]
        [SerializeField] private MakeyConfig _config;

        [Tooltip("Where Makey is parked while away, and where he flies in from. Put it off screen. Not a child of this object.")]
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("Where Makey hovers. Not a child of this object.")]
        [SerializeField] private Transform _hoverPoint;

        [Header("Behavior")]
        [Tooltip("Fly in as soon as the game starts. A round controller will take over this later.")]
        [SerializeField] private bool _enterOnEnable = true;

        [Header("Debug")]
        [Tooltip("Write what Makey is doing to the Console: how he is wired, every state change, every ball that reaches him, and a status line once a second. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Seconds between the status lines.
        private const float StatusLogInterval = 1f;

        [NonSerialized] private MakeyBrain _brain;
        [NonSerialized] private bool _enterPending;
        [NonSerialized] private float _nextStatusLogTime;

        /// <summary>Raised after every change of state, with the new state.</summary>
        public event Action<MakeyState> StateChanged;

        /// <summary>Raised when a new shield goes up, with its color.</summary>
        public event Action<BallColor> ShieldRaised;

        /// <summary>Raised when a ball of the right color breaks the shield, with that color.</summary>
        public event Action<BallColor> ShieldShattered;

        /// <summary>Raised when a ball of the wrong color hits the shield, with its color and where it hit.</summary>
        public event Action<BallColor, Vector3> BallDeflected;

        /// <summary>What Makey is doing now. Away while this component is disabled or not wired.</summary>
        public MakeyState State => _brain != null ? _brain.State : MakeyState.Away;

        /// <summary>How far through the current Entering, Dazed or Recovering state he is, 0 to 1.</summary>
        public float StateProgress => _brain != null ? _brain.StateProgress : 0f;

        /// <summary>The color of the latest shield.</summary>
        public BallColor ShieldColor => _brain != null ? _brain.ShieldColor : default;

        private void OnEnable()
        {
            if (_config == null || _spawnPoint == null || _hoverPoint == null)
            {
                Debug.LogError("Makey needs a config, a spawn point and a hover point, so it will do nothing.", this);
                return;
            }

            _brain = new MakeyBrain(
                _config.EnterSeconds,
                _config.DazeSeconds,
                _config.RecoverSeconds,
                new System.Random());
            _brain.StateChanged += OnBrainStateChanged;

            transform.position = _spawnPoint.position;

            // The first Enter waits for the first Update, so every other component has been
            // enabled and heard about the state changes.
            _enterPending = _enterOnEnable;

            CheckWiring();
            _nextStatusLogTime = 0f;
        }

        private void OnDisable()
        {
            if (_brain != null)
            {
                _brain.StateChanged -= OnBrainStateChanged;
            }

            _brain = null;
            _enterPending = false;
        }

        private void Update()
        {
            if (_brain == null)
            {
                return;
            }

            if (_enterPending)
            {
                _enterPending = false;
                Log("Entering as soon as the game starts (Enter On Enable is ticked).");
                _brain.Enter();
            }

            _brain.Tick(Time.deltaTime);
            UpdatePosition();
            LogStatus();
        }

        /// <summary>Starts flying in. Does nothing unless Makey is away.</summary>
        public void Enter()
        {
            if (_brain == null)
            {
                return;
            }

            if (_debugLog && _brain.State != MakeyState.Away)
            {
                Log("Enter() ignored: Makey is already " + _brain.State + ".");
            }

            _brain.Enter();
        }

        /// <inheritdoc />
        public void ReceiveBall(BallColor color, Vector3 worldPoint)
        {
            // A disabled component can still be found by the hit resolver.
            if (_brain == null || !isActiveAndEnabled)
            {
                if (_debugLog)
                {
                    Log("Ball " + color + " ignored: " + (_brain == null ? "Makey is not set up (see the error when the game started)." : "the Makey component is disabled."));
                }

                return;
            }

            MakeyState stateBefore = _brain.State;
            BallColor shield = _brain.ShieldColor;
            MakeyHitResult result = _brain.ReceiveBall(color);

            if (_debugLog)
            {
                Log("Ball " + color + " hit at " + worldPoint.ToString("F2") + " -> " + result + ". " + Explain(result, stateBefore, shield, color));
            }

            switch (result)
            {
                case MakeyHitResult.Shattered:
                    ShieldShattered?.Invoke(color);
                    break;

                case MakeyHitResult.Deflected:
                    BallDeflected?.Invoke(color, worldPoint);
                    break;
            }
        }

        private void OnBrainStateChanged(MakeyState state)
        {
            if (_debugLog)
            {
                Log(DescribeState(state));
            }

            StateChanged?.Invoke(state);

            if (state == MakeyState.Shielded)
            {
                ShieldRaised?.Invoke(_brain.ShieldColor);
            }
        }

        // Debug text for a state that was just entered.
        private string DescribeState(MakeyState state)
        {
            switch (state)
            {
                case MakeyState.Entering:
                    return "State -> Entering. Flying from " + _spawnPoint.position.ToString("F1") + " to " + _hoverPoint.position.ToString("F1")
                        + " over " + _config.EnterSeconds.ToString("0.0#") + " s. Balls are ignored until he arrives.";

                case MakeyState.Shielded:
                    return "State -> Shielded. Hovering at " + _hoverPoint.position.ToString("F1") + ". Shield color: " + _brain.ShieldColor
                        + ". A " + _brain.ShieldColor + " ball breaks it; any other color bounces off.";

                case MakeyState.Dazed:
                    return "State -> Dazed for " + _config.DazeSeconds.ToString("0.0#") + " s. The shield is broken; balls are ignored.";

                case MakeyState.Recovering:
                    return "State -> Recovering for " + _config.RecoverSeconds.ToString("0.0#")
                        + " s. Balls are still ignored; then a new shield goes up in a different color.";

                default:
                    return "State -> " + state + ".";
            }
        }

        private static string Explain(MakeyHitResult result, MakeyState stateBefore, BallColor shield, BallColor ball)
        {
            switch (result)
            {
                case MakeyHitResult.Shattered:
                    return "The ball matched the " + shield + " shield, so it breaks.";

                case MakeyHitResult.Deflected:
                    return "The shield is " + shield + " and the ball is " + ball + ", so it bounces off.";

                default:
                    return "Only a shielded Makey can be hurt, and he is " + stateBefore + ".";
            }
        }

        // Flags a setup that would leave Makey on screen but doing nothing visible or unhittable.
        // The warnings always show; the summary is part of the debug log.
        private void CheckWiring()
        {
            Collider ownCollider = GetComponent<Collider>();
            MakeyView view = GetComponent<MakeyView>();

            if (ownCollider == null)
            {
                Debug.LogWarning("Makey has no collider on this object, so balls cannot hit him.", this);
            }
            else if (ownCollider.isTrigger)
            {
                Debug.LogWarning("Makey's collider is a trigger, and the hit resolver ignores triggers, so balls cannot hit him.", this);
            }

            if (view == null)
            {
                Debug.LogWarning("There is no MakeyView on this object, so nothing will show the shield, the daze or the hits. Add one and wire it.", this);
            }

            if (!_debugLog)
            {
                return;
            }

            Log("Enabled. Config " + _config.name + ": enter " + _config.EnterSeconds.ToString("0.0#") + " s, daze "
                + _config.DazeSeconds.ToString("0.0#") + " s, recover " + _config.RecoverSeconds.ToString("0.0#") + " s. Spawn "
                + _spawnPoint.position.ToString("F1") + ", hover " + _hoverPoint.position.ToString("F1")
                + ". Layer " + LayerMask.LayerToName(gameObject.layer) + ". Collider "
                + (ownCollider == null ? "none" : ownCollider.GetType().Name + (ownCollider.isTrigger ? " (trigger)" : ""))
                + ". MakeyView " + (view == null ? "MISSING" : "present") + ". Enter On Enable " + _enterOnEnable + ".");
        }

        // One status line a second, so a quiet Makey can be told apart from a stuck one.
        private void LogStatus()
        {
            if (!_debugLog || Time.unscaledTime < _nextStatusLogTime)
            {
                return;
            }

            _nextStatusLogTime = Time.unscaledTime + StatusLogInterval;
            Log("Status: " + _brain.State + ", " + Mathf.RoundToInt(_brain.StateProgress * 100f) + "% through the state, shield "
                + _brain.ShieldColor + ", at " + transform.position.ToString("F1") + ".");
        }

        private void Log(string message)
        {
            if (_debugLog)
            {
                Debug.Log("[Makey] " + message, this);
            }
        }

        private void UpdatePosition()
        {
            switch (_brain.State)
            {
                case MakeyState.Away:
                    transform.position = _spawnPoint.position;
                    break;

                case MakeyState.Entering:
                    float eased = Mathf.SmoothStep(0f, 1f, _brain.StateProgress);
                    transform.position = Vector3.Lerp(_spawnPoint.position, _hoverPoint.position, eased);
                    break;

                default:
                    transform.position = _hoverPoint.position;
                    break;
            }
        }
    }
}
