using System;
using PlanetMakey.Core;
using PlanetMakey.Game.Cows;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Tooltip("Where Makey hovers when he arrives, before he picks his first cow. Not a child of this object. Over a cow his height comes from the config's hover height range.")]
        [SerializeField] private Transform _hoverPoint;

        [Tooltip("The cows Makey abducts. Leave it empty and he only guards his shield.")]
        [SerializeField] private CowHerd _cowHerd;

        [Tooltip("Where the beam starts and where a lifted cow ends up: a child of this object at the underside of the UFO.")]
        [SerializeField] private Transform _beamOrigin;

        [Tooltip("The camera the players see. It keeps Makey's hover height inside the config's flight band, and lets the game warn at the start about a cow, or Makey above it, that is off screen. Without it the band is not applied.")]
        [FormerlySerializedAs("_checkCamera")]
        [SerializeField] private Camera _camera;

        [Header("Behavior")]
        [Tooltip("Fly in as soon as the game starts. Untick it when a RoundController sends Makey in at the start of the night.")]
        [SerializeField] private bool _enterOnEnable = true;

        [Tooltip("Tick this when the model's animations fly the ship in and out (the MakeyView has a Model Animator). Makey then appears at the hover point at once, stays there while he leaves, and the animations supply the flights. Untick it for the plain glide from and to the spawn point.")]
        [SerializeField] private bool _introFliesIn;

        [Header("Debug")]
        [Tooltip("Write what Makey is doing to the Console: how he is wired, every state change, every ball that reaches him, and a status line once a second. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Seconds between the status lines.
        private const float StatusLogInterval = 1f;

        [NonSerialized] private MakeyBrain _brain;
        [NonSerialized] private AbductionBrain _abduction;
        [NonSerialized] private bool _enterPending;
        [NonSerialized] private float _nextStatusLogTime;

        // Where Makey hovers while he is not flying in or gliding over a cow, and the two ends of
        // the glide to the cow he picked.
        [NonSerialized] private Vector3 _anchor;
        [NonSerialized] private Vector3 _approachFrom;
        [NonSerialized] private Vector3 _approachTo;

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

        /// <summary>Raised when Makey picks a cow, with its number.</summary>
        public event Action<int> CowTargeted;

        /// <summary>Raised when a cow is lost, with its number.</summary>
        public event Action<int> CowAbsorbed;

        /// <summary>
        /// Raised when an attempt ends with a cow picked: the cow's number, and whether it had
        /// already started to rise.
        /// </summary>
        public event Action<int, bool> CowDropped;

        /// <summary>Raised when Makey looks for a cow and every cow is lost.</summary>
        public event Action HerdEmptied;

        /// <summary>The herd Makey abducts from, or null if none is wired. The cow view reads it from here.</summary>
        public CowHerd CowHerd => _cowHerd;

        /// <summary>The camera the players see, or null if none is wired. The view reads it from here, so it is wired in one place.</summary>
        public Camera ViewCamera => _camera;

        /// <summary>True if Makey flies in by himself when the game starts, so a round controller is not in charge of him.</summary>
        public bool EntersOnEnable => _enterOnEnable;

        /// <summary>How long he takes to fly off screen, in seconds. 0 if no config is wired. The round controller checks it against the dawn time.</summary>
        public float LeaveSeconds => _config != null ? _config.LeaveSeconds : 0f;

        /// <summary>Where Makey is in an attempt to abduct a cow. Idle when there is none.</summary>
        public AbductionPhase AbductionPhase => _abduction != null ? _abduction.Phase : AbductionPhase.Idle;

        /// <summary>How far through the current abduction phase he is, 0 to 1.</summary>
        public float AbductionProgress => _abduction != null ? _abduction.PhaseProgress : 0f;

        /// <summary>The number of the cow he is abducting, or -1 when there is none.</summary>
        public int TargetCowIndex => _abduction != null ? _abduction.TargetIndex : -1;

        /// <summary>Where the beam starts. Makey's own position if no beam origin is set.</summary>
        public Vector3 BeamOrigin => _beamOrigin != null ? _beamOrigin.position : transform.position;

        /// <summary>
        /// Where the cow he is abducting is now, in world space: on the ground until the lift
        /// starts, then rising to <see cref="BeamOrigin"/>. Only meaningful when
        /// <see cref="TargetCowIndex"/> is not -1. The cow and the beam both read this, so they agree.
        /// </summary>
        public Vector3 TargetCowPosition
        {
            get
            {
                int index = TargetCowIndex;
                if (index < 0 || _cowHerd == null)
                {
                    return transform.position;
                }

                Vector3 rest = _cowHerd.RestPosition(index);
                if (_abduction.Phase != AbductionPhase.Lifting)
                {
                    return rest;
                }

                return Vector3.Lerp(rest, BeamOrigin, _abduction.PhaseProgress);
            }
        }

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
                _config.LeaveSeconds,
                new System.Random());
            _brain.StateChanged += OnBrainStateChanged;

            if (_cowHerd != null)
            {
                _abduction = new AbductionBrain(
                    _cowHerd.Rules,
                    _config.ApproachSeconds,
                    _config.BeamSeconds,
                    _config.LiftSeconds,
                    _config.CelebrateSeconds,
                    new System.Random());
                _abduction.PhaseChanged += OnAbductionPhaseChanged;
                _abduction.CowTargeted += OnCowTargeted;
                _abduction.CowDropped += OnCowDropped;
                _abduction.CowAbsorbed += OnCowAbsorbed;
                _abduction.HerdEmpty += OnHerdEmpty;
                _cowHerd.HerdReset += OnHerdReset;
            }

            transform.position = _spawnPoint.position;
            _anchor = _hoverPoint.position;

            // The first Enter waits for the first Update, so every other component has been
            // enabled and heard about the state changes.
            _enterPending = _enterOnEnable;

            CheckWiring();
            _nextStatusLogTime = 0f;
        }

        private void OnDisable()
        {
            // A cow that was being abducted goes back to the herd.
            if (_abduction != null)
            {
                _abduction.Abort();
                _abduction.PhaseChanged -= OnAbductionPhaseChanged;
                _abduction.CowTargeted -= OnCowTargeted;
                _abduction.CowDropped -= OnCowDropped;
                _abduction.CowAbsorbed -= OnCowAbsorbed;
                _abduction.HerdEmpty -= OnHerdEmpty;
            }

            if (_cowHerd != null)
            {
                _cowHerd.HerdReset -= OnHerdReset;
            }

            if (_brain != null)
            {
                _brain.StateChanged -= OnBrainStateChanged;
            }

            _abduction = null;
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

            if (_abduction != null)
            {
                _abduction.Tick(Time.deltaTime);
            }

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

        /// <summary>
        /// Sends Makey off screen at the end of a round, from any state. An abduction in progress
        /// is aborted and its cow goes back to the herd. Does nothing if he is Away or already leaving.
        /// </summary>
        public void Leave()
        {
            if (_brain == null)
            {
                return;
            }

            if (_debugLog && (_brain.State == MakeyState.Away || _brain.State == MakeyState.Leaving))
            {
                Log("Leave() ignored: Makey is already " + _brain.State + ".");
            }

            _brain.Leave();
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

            // A shield that is up means he hunts a cow. A shield that breaks ends the hunt.
            if (_abduction == null)
            {
                return;
            }

            if (state == MakeyState.Shielded)
            {
                _abduction.Begin();
            }
            else if (state == MakeyState.Dazed || state == MakeyState.Leaving || state == MakeyState.Away)
            {
                _abduction.Abort();
            }
        }

        private void OnAbductionPhaseChanged(AbductionPhase phase)
        {
            // The glide is over: stay exactly above the cow.
            if (phase == AbductionPhase.LoweringBeam)
            {
                _anchor = _approachTo;
            }

            if (_debugLog)
            {
                Log(DescribePhase(phase));
            }
        }

        private void OnCowTargeted(int index)
        {
            _approachFrom = transform.position;

            // Straight above the cow in x and z, so cows may stand anywhere in 3D, at a random height
            // above it (kept inside the flight band, so he stays on screen).
            Vector3 rest = _cowHerd.RestPosition(index);
            float height = ChooseHoverHeight(rest);
            _approachTo = new Vector3(rest.x, height, rest.z);

            if (_debugLog)
            {
                Log("Picked cow " + index + " standing at " + rest.ToString("F1") + ". Hovering " + (height - rest.y).ToString("0.0#")
                    + " above it (range for this cow's distance " + _config.HoverHeightMin.ToString("0.0#") + " to " + HighestHoverAbove(rest).ToString("0.0#")
                    + (_camera != null ? ", then kept in the flight band" : ", no camera so no flight band") + "). Gliding from "
                    + _approachFrom.ToString("F1") + " to " + _approachTo.ToString("F1") + ".");
            }

            CowTargeted?.Invoke(index);
        }

        private void OnCowDropped(int index, bool wasLifted)
        {
            if (_debugLog)
            {
                Log("Cow " + index + " dropped " + (wasLifted ? "from the beam" : "before it left the ground") + ": the shield broke, so the attempt is over.");
            }

            CowDropped?.Invoke(index, wasLifted);
        }

        private void OnCowAbsorbed(int index)
        {
            if (_debugLog)
            {
                Log("Cow " + index + " absorbed. " + _cowHerd.Rules.RemainingCount + " of " + _cowHerd.Rules.Count + " cows are still safe.");
            }

            CowAbsorbed?.Invoke(index);
        }

        private void OnHerdEmpty()
        {
            Log("Every cow is lost: nothing left to abduct.");
            HerdEmptied?.Invoke();
        }

        // The herd was reset (a new round, or the context menu): end any attempt, and hunt again
        // if the shield is up.
        private void OnHerdReset()
        {
            if (_abduction == null)
            {
                return;
            }

            _abduction.Abort();

            if (_brain != null && _brain.State == MakeyState.Shielded)
            {
                _abduction.Begin();
            }
        }

        private static string DescribePhase(AbductionPhase phase)
        {
            switch (phase)
            {
                case AbductionPhase.Approaching:
                    return "Abduction -> Approaching. Gliding over the cow. The shield can be broken now.";
                case AbductionPhase.LoweringBeam:
                    return "Abduction -> LoweringBeam. Hovering over the cow while the beam reaches down.";
                case AbductionPhase.Lifting:
                    return "Abduction -> Lifting. The cow is rising: break the shield before it arrives.";
                case AbductionPhase.Celebrating:
                    return "Abduction -> Celebrating. The cow is gone and Makey celebrates.";
                default:
                    return "Abduction -> Idle.";
            }
        }

        // Debug text for a state that was just entered.
        private string DescribeState(MakeyState state)
        {
            switch (state)
            {
                case MakeyState.Entering:
                    return (_introFliesIn
                            ? "State -> Entering. At the hover point " + _hoverPoint.position.ToString("F1") + "; the intro animation flies the ship in"
                            : "State -> Entering. Gliding from " + _spawnPoint.position.ToString("F1") + " to " + _hoverPoint.position.ToString("F1"))
                        + " over " + _config.EnterSeconds.ToString("0.0#") + " s. Balls are ignored until he arrives.";

                case MakeyState.Shielded:
                    return "State -> Shielded. Hovering at " + _hoverPoint.position.ToString("F1") + ". Shield color: " + _brain.ShieldColor
                        + ". A " + _brain.ShieldColor + " ball breaks it; any other color bounces off.";

                case MakeyState.Dazed:
                    return "State -> Dazed for " + _config.DazeSeconds.ToString("0.0#") + " s. The shield is broken; balls are ignored.";

                case MakeyState.Recovering:
                    return "State -> Recovering for " + _config.RecoverSeconds.ToString("0.0#")
                        + " s. Balls are still ignored; then a new shield goes up in a different color.";

                case MakeyState.Leaving:
                    return (_introFliesIn
                            ? "State -> Leaving. Staying at the hover point " + _hoverPoint.position.ToString("F1") + "; the escape animation flies the ship off"
                            : "State -> Leaving. Gliding from the hover point to " + _spawnPoint.position.ToString("F1"))
                        + " over " + _config.LeaveSeconds.ToString("0.0#") + " s. Balls are ignored; any abduction is aborted.";

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

            if (view != null)
            {
                if (_introFliesIn && !view.HasIntroAnimator)
                {
                    Debug.LogWarning("Intro Flies In is ticked, but the MakeyView has no Model Animator, so Makey will just appear at the hover point. Wire the Animator in, or untick it.", this);
                }
                else if (!_introFliesIn && view.HasIntroAnimator)
                {
                    Debug.LogWarning("The MakeyView has a Model Animator, but Intro Flies In is not ticked, so the glide and the intro animation would both move him. Tick it.", this);
                }
            }

            if (_camera == null)
            {
                Debug.LogWarning("Makey has no camera, so the flight band is not applied: his hover height over a cow is only the random range from the config, and he can end up off screen.", this);
            }

            if (_cowHerd == null)
            {
                Debug.LogWarning("Makey has no CowHerd, so he will not abduct anything. Wire the herd in.", this);
            }
            else
            {
                CheckCowPlacement();
            }

            if (_beamOrigin == null)
            {
                Debug.LogWarning("Makey has no beam origin, so the beam starts, and lifted cows end up, in the middle of him. Add a child at the underside of the UFO.", this);
            }
            else if (!_beamOrigin.IsChildOf(transform))
            {
                Debug.LogWarning("The beam origin is not a child of Makey, so it will not move with him.", this);
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

        // With a camera wired in, warns about a cow that cannot be seen, and about Makey flying off
        // screen above it. Both would be invisible and could not be hit.
        private void CheckCowPlacement()
        {
            if (_camera == null)
            {
                return;
            }

            for (int i = 0; i < _cowHerd.Count; i++)
            {
                Transform cow = _cowHerd.GetCow(i);
                if (cow == null)
                {
                    continue;
                }

                Vector3 rest = _cowHerd.RestPosition(i);
                if (!IsOnScreen(rest))
                {
                    Debug.LogWarning("Cow " + i + " ('" + cow.name + "') at " + rest.ToString("F1") + " is outside the camera's view.", this);
                }

                // Makey at the lowest and the highest hover height over this cow, after the flight band.
                Vector3 lowest = new Vector3(rest.x, ClampToFlightBand(rest, rest.y + _config.HoverHeightMin), rest.z);
                Vector3 highest = new Vector3(rest.x, ClampToFlightBand(rest, rest.y + HighestHoverAbove(rest)), rest.z);
                if (!IsOnScreen(lowest) || !IsOnScreen(highest))
                {
                    Debug.LogWarning("Makey would be outside the camera's view above cow " + i + " ('" + cow.name + "'), between "
                        + lowest.ToString("F1") + " and " + highest.ToString("F1") + ". Move the cow, or narrow the hover height range or the flight band.", this);
                }
            }
        }

        private bool IsOnScreen(Vector3 point)
        {
            Vector3 view = _camera.WorldToViewportPoint(point);
            return view.z > 0f && view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f;
        }

        // The world height Makey hovers at over a cow: a random height above the cow's resting
        // position, inside the flight band so he stays on screen and clear of the HUD.
        private float ChooseHoverHeight(Vector3 rest)
        {
            float above = UnityEngine.Random.Range(_config.HoverHeightMin, HighestHoverAbove(rest));
            return ClampToFlightBand(rest, rest.y + above);
        }

        // The top of the hover height range over this cow. Over a near cow only part of the range
        // is used, because the same world height looks higher on screen there; the minimum is kept.
        // With no camera the whole range is used.
        private float HighestHoverAbove(Vector3 rest)
        {
            float min = _config.HoverHeightMin;
            float range = _config.HoverHeightMax - min;

            if (_camera == null)
            {
                return min + range;
            }

            Transform cameraTransform = _camera.transform;
            float depth = Vector3.Dot(rest - cameraTransform.position, cameraTransform.forward);
            float scale = HoverHeightSolver.RangeScale(depth, _config.NearDistance, _config.FarDistance, _config.NearHeightScale);
            return min + range * scale;
        }

        // Pulls a height into the flight band for the line above this cow. The band is on the
        // screen and the heights are in the world, so the camera turns one into the other. With no
        // camera, an orthographic one, or a line the camera cannot place, the height is left alone.
        private float ClampToFlightBand(Vector3 rest, float height)
        {
            if (_camera == null || _camera.orthographic)
            {
                return height;
            }

            Transform cameraTransform = _camera.transform;
            bool lowFound = HoverHeightSolver.TryHeightAtViewportY(
                cameraTransform.position, cameraTransform.forward, cameraTransform.up,
                _camera.fieldOfView, rest, _config.FlightBandMinY, out float bandLow);
            bool highFound = HoverHeightSolver.TryHeightAtViewportY(
                cameraTransform.position, cameraTransform.forward, cameraTransform.up,
                _camera.fieldOfView, rest, _config.FlightBandMaxY, out float bandHigh);

            if (!lowFound || !highFound)
            {
                return height;
            }

            return Mathf.Clamp(height, Mathf.Min(bandLow, bandHigh), Mathf.Max(bandLow, bandHigh));
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
                + _brain.ShieldColor + ", at " + transform.position.ToString("F1") + ". Abduction: " + AbductionPhase
                + (TargetCowIndex >= 0 ? " of cow " + TargetCowIndex + ", " + Mathf.RoundToInt(AbductionProgress * 100f) + "% through the phase" : "")
                + (_cowHerd != null ? ". Cows safe: " + _cowHerd.Rules.RemainingCount + " of " + _cowHerd.Rules.Count : "") + ".");
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
                {
                    transform.position = _spawnPoint.position;
                    _anchor = _hoverPoint.position;
                    break;
                }

                case MakeyState.Entering:
                {
                    if (_introFliesIn)
                    {
                        // The animation moves the ship, so only the collider's place is set here.
                        transform.position = _hoverPoint.position;
                    }
                    else
                    {
                        float flyIn = Mathf.SmoothStep(0f, 1f, _brain.StateProgress);
                        transform.position = Vector3.Lerp(_spawnPoint.position, _hoverPoint.position, flyIn);
                    }

                    _anchor = _hoverPoint.position;
                    break;
                }

                case MakeyState.Leaving:
                {
                    // With the escape animation he stays put and the animation supplies the flight.
                    // Otherwise he glides from where he was to the spawn point. The anchor is kept,
                    // so the glide starts from the same place for the whole state.
                    if (_introFliesIn)
                    {
                        transform.position = _anchor;
                    }
                    else
                    {
                        float flyOut = Mathf.SmoothStep(0f, 1f, _brain.StateProgress);
                        transform.position = Vector3.Lerp(_anchor, _spawnPoint.position, flyOut);
                    }

                    break;
                }

                default:
                {
                    // He stays where he is, except while he glides over the cow he picked. If the
                    // shield breaks mid-glide he simply stops there.
                    if (_abduction != null && _abduction.Phase == AbductionPhase.Approaching)
                    {
                        float glide = Mathf.SmoothStep(0f, 1f, _abduction.PhaseProgress);
                        _anchor = Vector3.Lerp(_approachFrom, _approachTo, glide);
                    }

                    transform.position = _anchor;
                    break;
                }
            }
        }
    }
}
