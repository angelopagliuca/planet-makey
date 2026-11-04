using System;
using PlanetMakey.Game.Cows;
using PlanetMakey.Game.Ufo;
using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// Runs a round in the scene: it ticks the <see cref="RoundBrain"/>, starts a round when a
    /// start source asks for one, and does what each state needs. At dusk the herd is brought back,
    /// at night Makey flies in, and at dawn he leaves and the score is taken. The score is the
    /// number of cows not lost. The look of it all (HUD, light) is up to the views.
    /// </summary>
    /// <remarks>
    /// Untick Enter On Enable on <see cref="Makey"/>: this controller sends him in. The herd is
    /// taken from the Makey, so it is wired only once.
    /// </remarks>
    public sealed class RoundController : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("How long dusk, night and dawn last.")]
        [SerializeField] private RoundConfig _config;

        [Tooltip("Makey. He is sent in at night and out at dawn. The herd comes from him.")]
        [SerializeField] private Makey _makey;

        [Tooltip("Everything that can start a round. A request while a round is running is ignored.")]
        [SerializeField] private RoundStartSource[] _startSources;

        [Header("Debug")]
        [Tooltip("Write what the round does to the Console: every state, every start request, and a status line once a second. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Seconds between the status lines.
        private const float StatusLogInterval = 1f;

        [NonSerialized] private RoundBrain _brain;
        [NonSerialized] private CowHerd _cowHerd;
        [NonSerialized] private int _score;
        [NonSerialized] private float _nextStatusLogTime;

        /// <summary>Raised after every change of the round's state, with the new state.</summary>
        public event Action<RoundState> StateChanged;

        /// <summary>Where the round is. Idle while this component is disabled or not wired.</summary>
        public RoundState State => _brain != null ? _brain.State : RoundState.Idle;

        /// <summary>How far through Dusk, Night or Dawn the round is, 0 to 1.</summary>
        public float StateProgress => _brain != null ? _brain.StateProgress : 0f;

        /// <summary>Seconds of night left: the full length before the night, 0 after it.</summary>
        public float NightSecondsLeft => _brain != null ? _brain.NightSecondsLeft : 0f;

        /// <summary>How many cows are not lost right now. 0 if no herd is wired.</summary>
        public int CowsSafe => _cowHerd != null ? _cowHerd.Rules.RemainingCount : 0;

        /// <summary>How many cows the herd has. 0 if no herd is wired.</summary>
        public int CowsTotal => _cowHerd != null ? _cowHerd.Rules.Count : 0;

        /// <summary>
        /// The cows kept through the last night. Fixed when dawn begins and cleared when the next
        /// round starts, so it can be shown through dawn and the idle time after it.
        /// </summary>
        public int Score => _score;

        private void OnEnable()
        {
            if (_config == null || _makey == null)
            {
                Debug.LogError("RoundController needs a config and a Makey, so it will do nothing.", this);
                return;
            }

            _cowHerd = _makey.CowHerd;
            _score = 0;

            _brain = new RoundBrain(_config.DuskSeconds, _config.NightSeconds, _config.DawnSeconds);
            _brain.StateChanged += OnBrainStateChanged;

            if (_startSources != null)
            {
                for (int i = 0; i < _startSources.Length; i++)
                {
                    if (_startSources[i] != null)
                    {
                        _startSources[i].StartRequested += OnStartRequested;
                    }
                }
            }

            _makey.HerdEmptied += OnHerdEmptied;

            CheckWiring();
            _nextStatusLogTime = 0f;
        }

        private void OnDisable()
        {
            if (_startSources != null)
            {
                for (int i = 0; i < _startSources.Length; i++)
                {
                    if (_startSources[i] != null)
                    {
                        _startSources[i].StartRequested -= OnStartRequested;
                    }
                }
            }

            // Any of these can already be destroyed when the scene shuts down.
            if (_makey != null)
            {
                _makey.HerdEmptied -= OnHerdEmptied;
            }

            if (_brain != null)
            {
                _brain.StateChanged -= OnBrainStateChanged;
            }

            _brain = null;
            _cowHerd = null;
        }

        private void Update()
        {
            if (_brain == null)
            {
                return;
            }

            _brain.Tick(Time.deltaTime);
            LogStatus();
        }

        /// <summary>
        /// Starts a round, the same way a start source does. Returns false, and does nothing,
        /// unless the round is Idle and Makey is gone (after an abort he may still be flying off).
        /// </summary>
        public bool RequestStart()
        {
            if (_brain == null)
            {
                return false;
            }

            if (_brain.State == RoundState.Idle && _makey.State != MakeyState.Away)
            {
                Log("Start request ignored: Makey is still " + _makey.State + ". Try again when he is gone.");
                return false;
            }

            bool started = _brain.RequestStart();
            if (!started)
            {
                Log("Start request ignored: the round is " + _brain.State + ", not Idle.");
            }

            return started;
        }

        /// <summary>
        /// Cancels the round during dusk or night: Makey flies off, a lifted cow drops back, there
        /// is no score, and the round is Idle at once. Returns false, and does nothing, in Idle and
        /// at dawn.
        /// </summary>
        public bool Abort()
        {
            if (_brain == null)
            {
                return false;
            }

            if (!_brain.Abort())
            {
                Log("Abort ignored: the round is " + _brain.State + ", and only dusk or night can be aborted.");
                return false;
            }

            _score = 0;
            _makey.Leave();
            Log("Round aborted by the operator. Makey is sent off and there is no score.");
            return true;
        }

        private void OnStartRequested()
        {
            RequestStart();
        }

        // Every cow is lost: the night is over. The brain ignores this outside Night.
        private void OnHerdEmptied()
        {
            if (_brain == null || _brain.State != RoundState.Night)
            {
                return;
            }

            Log("Every cow is lost: the night ends early.");
            _brain.EndNightEarly();
        }

        private void OnBrainStateChanged(RoundState state)
        {
            switch (state)
            {
                case RoundState.Dusk:
                    // The herd comes back before anyone is told, so the views see a full herd.
                    _score = 0;
                    if (_cowHerd != null)
                    {
                        _cowHerd.ResetHerd();
                    }

                    break;

                case RoundState.Night:
                    _makey.Enter();
                    break;

                case RoundState.Dawn:
                    // A cow being lifted when time runs out is not lost: leaving drops it back.
                    // The score is taken first, and it counts that cow either way.
                    _score = CowsSafe;
                    _makey.Leave();
                    break;
            }

            if (_debugLog)
            {
                Log(DescribeState(state));
            }

            StateChanged?.Invoke(state);
        }

        private string DescribeState(RoundState state)
        {
            switch (state)
            {
                case RoundState.Dusk:
                    return "Round -> Dusk. The herd is reset and the light fades for " + _config.DuskSeconds.ToString("0.0#") + " s.";
                case RoundState.Night:
                    return "Round -> Night for " + _config.NightSeconds.ToString("0.0#") + " s. Makey is sent in.";
                case RoundState.Dawn:
                    return "Round -> Dawn for " + _config.DawnSeconds.ToString("0.0#") + " s. Score " + _score + " of "
                        + CowsTotal + ". Makey is sent off.";
                default:
                    return "Round -> Idle. Waiting for a start request.";
            }
        }

        // Flags a setup that would leave the round doing nothing or going wrong. The warnings
        // always show; the summary is part of the debug log.
        private void CheckWiring()
        {
            int sources = 0;
            if (_startSources != null)
            {
                for (int i = 0; i < _startSources.Length; i++)
                {
                    if (_startSources[i] != null)
                    {
                        sources++;
                    }
                }
            }

            if (sources == 0)
            {
                Debug.LogWarning("RoundController has no start source, so a round can only be started from code. Add a KeyRoundStartSource to its list.", this);
            }

            if (_makey.EntersOnEnable)
            {
                Debug.LogWarning("Makey has Enter On Enable ticked, so he flies in when the game starts and not when the night does. Untick it.", this);
            }

            if (_cowHerd == null)
            {
                Debug.LogWarning("Makey has no CowHerd, so there is no herd to reset and the score will always be 0.", this);
            }

            if (_config.DawnSeconds < _makey.LeaveSeconds)
            {
                Debug.LogWarning("Dawn Seconds (" + _config.DawnSeconds.ToString("0.0#") + ") is shorter than Makey's Leave Seconds ("
                    + _makey.LeaveSeconds.ToString("0.0#") + "). A start request right after the dawn would be ignored until he is gone, so the booth would seem to ignore the button for a moment.", this);
            }

            Log("Enabled. Config " + _config.name + ": dusk " + _config.DuskSeconds.ToString("0.0#") + " s, night "
                + _config.NightSeconds.ToString("0.0#") + " s, dawn " + _config.DawnSeconds.ToString("0.0#") + " s. Start sources: "
                + sources + ". Cows: " + CowsTotal + ".");
        }

        // One status line a second, so a quiet round can be told apart from a stuck one.
        private void LogStatus()
        {
            if (!_debugLog || Time.unscaledTime < _nextStatusLogTime)
            {
                return;
            }

            _nextStatusLogTime = Time.unscaledTime + StatusLogInterval;
            Log("Status: " + _brain.State + ", " + Mathf.RoundToInt(_brain.StateProgress * 100f) + "% through the state, night left "
                + _brain.NightSecondsLeft.ToString("0.0") + " s, cows safe " + CowsSafe + " of " + CowsTotal + ", Makey " + _makey.State + ".");
        }

        private void Log(string message)
        {
            if (_debugLog)
            {
                Debug.Log("[Round] " + message, this);
            }
        }
    }
}
