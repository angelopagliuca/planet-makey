using System;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The rules for a round, with no scene objects, so they can be tested on their own.
    /// <see cref="RoundController"/> ticks this and does what each new state needs. It knows
    /// nothing about Makey or the herd, so it does not keep the score.
    /// </summary>
    /// <remarks>
    /// Idle, then <see cref="RequestStart"/>, then Dusk, Night and Dawn for their times, then Idle.
    /// <see cref="EndNightEarly"/> cuts the night short. One <see cref="Tick"/> ends at most one
    /// state, so a long frame cannot skip a state.
    /// </remarks>
    public sealed class RoundBrain
    {
        private readonly float _duskSeconds;
        private readonly float _nightSeconds;
        private readonly float _dawnSeconds;

        private float _stateTime;

        /// <param name="duskSeconds">Time spent in Dusk. Negative values count as zero.</param>
        /// <param name="nightSeconds">Length of the night.</param>
        /// <param name="dawnSeconds">Time spent in Dawn before the round is over.</param>
        public RoundBrain(float duskSeconds, float nightSeconds, float dawnSeconds)
        {
            _duskSeconds = Math.Max(0f, duskSeconds);
            _nightSeconds = Math.Max(0f, nightSeconds);
            _dawnSeconds = Math.Max(0f, dawnSeconds);
            State = RoundState.Idle;
        }

        /// <summary>Where the round is.</summary>
        public RoundState State { get; private set; }

        /// <summary>How far through Dusk, Night or Dawn the round is, from 0 to 1. It is 0 in Idle.</summary>
        public float StateProgress
        {
            get
            {
                if (State == RoundState.Idle)
                {
                    return 0f;
                }

                float duration = DurationOf(State);
                if (duration <= 0f)
                {
                    return 1f;
                }

                return Math.Min(1f, _stateTime / duration);
            }
        }

        /// <summary>
        /// Seconds of night left. The full length before the night starts, what is left during it,
        /// and 0 once it is over (also when it ended early).
        /// </summary>
        public float NightSecondsLeft
        {
            get
            {
                switch (State)
                {
                    case RoundState.Night:
                        return Math.Max(0f, _nightSeconds - _stateTime);
                    case RoundState.Dawn:
                        return 0f;
                    default:
                        return _nightSeconds;
                }
            }
        }

        /// <summary>Raised after every change of <see cref="State"/>, with the new state.</summary>
        public event Action<RoundState> StateChanged;

        /// <summary>Starts a round. Returns false, and does nothing, unless the round is Idle.</summary>
        public bool RequestStart()
        {
            if (State != RoundState.Idle)
            {
                return false;
            }

            SetState(RoundState.Dusk);
            return true;
        }

        /// <summary>Ends the night now, for example because every cow is lost. Ignored outside Night.</summary>
        public void EndNightEarly()
        {
            if (State != RoundState.Night)
            {
                return;
            }

            SetState(RoundState.Dawn);
        }

        /// <summary>
        /// Moves time forward. Zero, negative and NaN steps are ignored. A step longer than the
        /// current state's remaining time ends that state and nothing more.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Written so that NaN is ignored too.
            if (!(deltaTime > 0f) || State == RoundState.Idle)
            {
                return;
            }

            _stateTime += deltaTime;
            if (_stateTime < DurationOf(State))
            {
                return;
            }

            switch (State)
            {
                case RoundState.Dusk:
                    SetState(RoundState.Night);
                    break;

                case RoundState.Night:
                    SetState(RoundState.Dawn);
                    break;

                case RoundState.Dawn:
                    SetState(RoundState.Idle);
                    break;
            }
        }

        private float DurationOf(RoundState state)
        {
            switch (state)
            {
                case RoundState.Dusk:
                    return _duskSeconds;
                case RoundState.Night:
                    return _nightSeconds;
                case RoundState.Dawn:
                    return _dawnSeconds;
                default:
                    return 0f;
            }
        }

        private void SetState(RoundState state)
        {
            State = state;
            _stateTime = 0f;
            StateChanged?.Invoke(state);
        }
    }
}
