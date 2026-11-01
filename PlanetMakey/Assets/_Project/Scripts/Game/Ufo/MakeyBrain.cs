using System;
using PlanetMakey.Core;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// The rules for Makey and his shield, with no scene objects, so they can be tested on
    /// their own. <see cref="Makey"/> ticks this and turns what happens into movement and events.
    /// </summary>
    /// <remarks>
    /// Away, then Enter, then Entering for the enter time, then Shielded. A ball of the shield's
    /// color breaks it: Dazed, then Recovering, then Shielded again with a new color. Balls only
    /// count while Shielded. One <see cref="Tick"/> moves at most one state, so a long frame
    /// cannot skip the dazed or recovering time.
    /// </remarks>
    public sealed class MakeyBrain
    {
        private readonly float _enterSeconds;
        private readonly float _dazeSeconds;
        private readonly float _recoverSeconds;
        private readonly Random _random;
        private readonly int _colorCount;

        private float _stateTime;
        private bool _hasLastColor;
        private BallColor _lastColor;

        /// <param name="enterSeconds">Time spent flying in. Negative values count as zero.</param>
        /// <param name="dazeSeconds">Time spent dazed after a shield breaks.</param>
        /// <param name="recoverSeconds">Time spent recovering before the next shield.</param>
        /// <param name="random">Picks the shield colors. Pass a seeded one to repeat a run.</param>
        public MakeyBrain(float enterSeconds, float dazeSeconds, float recoverSeconds, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            _enterSeconds = Math.Max(0f, enterSeconds);
            _dazeSeconds = Math.Max(0f, dazeSeconds);
            _recoverSeconds = Math.Max(0f, recoverSeconds);
            _random = random;
            _colorCount = Enum.GetValues(typeof(BallColor)).Length;
            State = MakeyState.Away;
        }

        /// <summary>What Makey is doing now.</summary>
        public MakeyState State { get; private set; }

        /// <summary>
        /// How far through the current Entering, Dazed or Recovering state Makey is, from 0 to 1.
        /// It is 0 in the other states.
        /// </summary>
        public float StateProgress
        {
            get
            {
                float duration = DurationOf(State);
                if (State == MakeyState.Away || State == MakeyState.Shielded)
                {
                    return 0f;
                }

                if (duration <= 0f)
                {
                    return 1f;
                }

                return Math.Min(1f, _stateTime / duration);
            }
        }

        /// <summary>
        /// The color of the latest shield. It stays the same after the shield breaks, until the
        /// next one goes up.
        /// </summary>
        public BallColor ShieldColor { get; private set; }

        /// <summary>Raised after every change of <see cref="State"/>, with the new state.</summary>
        public event Action<MakeyState> StateChanged;

        /// <summary>Starts flying in. Does nothing unless Makey is Away.</summary>
        public void Enter()
        {
            if (State != MakeyState.Away)
            {
                return;
            }

            SetState(MakeyState.Entering);
        }

        /// <summary>
        /// Moves time forward. Zero, negative and NaN steps are ignored. A step that is longer
        /// than the current state's remaining time ends that state and nothing more.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Written so that NaN is ignored too.
            if (!(deltaTime > 0f))
            {
                return;
            }

            switch (State)
            {
                case MakeyState.Entering:
                    _stateTime += deltaTime;
                    if (_stateTime >= _enterSeconds)
                    {
                        RaiseShield();
                    }

                    break;

                case MakeyState.Dazed:
                    _stateTime += deltaTime;
                    if (_stateTime >= _dazeSeconds)
                    {
                        SetState(MakeyState.Recovering);
                    }

                    break;

                case MakeyState.Recovering:
                    _stateTime += deltaTime;
                    if (_stateTime >= _recoverSeconds)
                    {
                        RaiseShield();
                    }

                    break;
            }
        }

        /// <summary>
        /// A ball of this color reached Makey. Only counts while Shielded: the shield's color
        /// breaks it, any other color bounces off.
        /// </summary>
        public MakeyHitResult ReceiveBall(BallColor color)
        {
            if (State != MakeyState.Shielded)
            {
                return MakeyHitResult.Ignored;
            }

            if (color != ShieldColor)
            {
                return MakeyHitResult.Deflected;
            }

            SetState(MakeyState.Dazed);
            return MakeyHitResult.Shattered;
        }

        private void RaiseShield()
        {
            // The color is set first so anyone told about the new state already sees it.
            ShieldColor = PickColor();
            _hasLastColor = true;
            _lastColor = ShieldColor;
            SetState(MakeyState.Shielded);
        }

        // Any color for the first shield. After that, any color except the previous one, so
        // the same color never comes up twice in a row. This relies on BallColor's values being
        // 0, 1, 2 and so on without gaps, which is how the enum is written.
        private BallColor PickColor()
        {
            if (!_hasLastColor)
            {
                return (BallColor)_random.Next(_colorCount);
            }

            int step = _random.Next(_colorCount - 1) + 1;
            return (BallColor)(((int)_lastColor + step) % _colorCount);
        }

        private float DurationOf(MakeyState state)
        {
            switch (state)
            {
                case MakeyState.Entering:
                    return _enterSeconds;
                case MakeyState.Dazed:
                    return _dazeSeconds;
                case MakeyState.Recovering:
                    return _recoverSeconds;
                default:
                    return 0f;
            }
        }

        private void SetState(MakeyState state)
        {
            State = state;
            _stateTime = 0f;
            StateChanged?.Invoke(state);
        }
    }
}
