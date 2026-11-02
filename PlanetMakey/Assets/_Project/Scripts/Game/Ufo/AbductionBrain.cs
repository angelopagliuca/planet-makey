using System;
using PlanetMakey.Game.Cows;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// The rules for one attempt to abduct a cow, with no scene objects, so they can be tested on
    /// their own. <see cref="Makey"/> starts an attempt when a shield goes up and aborts it when
    /// the shield breaks. This does not know about the shield itself.
    /// </summary>
    /// <remarks>
    /// Begin picks a cow, then Approaching, LoweringBeam and Lifting follow for their times. When
    /// the lift ends the cow is absorbed (lost) and Makey celebrates, then the next cow is picked.
    /// One <see cref="Tick"/> ends at most one timed phase, so a long frame cannot skip one.
    /// </remarks>
    public sealed class AbductionBrain
    {
        private readonly Herd _herd;
        private readonly float _approachSeconds;
        private readonly float _beamSeconds;
        private readonly float _liftSeconds;
        private readonly float _celebrateSeconds;
        private readonly Random _random;

        private float _phaseTime;
        private int _lastTarget = -1;

        /// <param name="herd">The cows to pick from.</param>
        /// <param name="approachSeconds">Time spent gliding over the cow. Negative values count as zero.</param>
        /// <param name="beamSeconds">Time the beam takes to reach the cow.</param>
        /// <param name="liftSeconds">Time the cow takes to rise. This is how long the player has.</param>
        /// <param name="celebrateSeconds">Time spent celebrating an absorbed cow.</param>
        /// <param name="random">Picks the cows. Pass a seeded one to repeat a run.</param>
        public AbductionBrain(
            Herd herd,
            float approachSeconds,
            float beamSeconds,
            float liftSeconds,
            float celebrateSeconds,
            Random random)
        {
            if (herd == null)
            {
                throw new ArgumentNullException(nameof(herd));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            _herd = herd;
            _approachSeconds = Math.Max(0f, approachSeconds);
            _beamSeconds = Math.Max(0f, beamSeconds);
            _liftSeconds = Math.Max(0f, liftSeconds);
            _celebrateSeconds = Math.Max(0f, celebrateSeconds);
            _random = random;
        }

        /// <summary>Where the attempt is.</summary>
        public AbductionPhase Phase { get; private set; }

        /// <summary>The cow being abducted, or -1 when there is none (idle, or celebrating).</summary>
        public int TargetIndex { get; private set; } = -1;

        /// <summary>How far through the current phase it is, from 0 to 1. It is 0 when idle.</summary>
        public float PhaseProgress
        {
            get
            {
                if (Phase == AbductionPhase.Idle)
                {
                    return 0f;
                }

                float duration = DurationOf(Phase);
                if (duration <= 0f)
                {
                    return 1f;
                }

                return Math.Min(1f, _phaseTime / duration);
            }
        }

        /// <summary>Raised after every change of <see cref="Phase"/>, with the new phase.</summary>
        public event Action<AbductionPhase> PhaseChanged;

        /// <summary>Raised when a cow is picked, with its number.</summary>
        public event Action<int> CowTargeted;

        /// <summary>
        /// Raised when an attempt is aborted with a cow picked. The flag says whether the cow had
        /// already started to rise.
        /// </summary>
        public event Action<int, bool> CowDropped;

        /// <summary>Raised when the lift ends and the cow is lost, with its number.</summary>
        public event Action<int> CowAbsorbed;

        /// <summary>Raised each time <see cref="Begin"/> finds that every cow is lost.</summary>
        public event Action HerdEmpty;

        /// <summary>
        /// Picks a cow and starts an attempt. Does nothing if one is already running. If no cow is
        /// free, nothing starts, and <see cref="HerdEmpty"/> is raised if every cow is lost.
        /// </summary>
        public void Begin()
        {
            if (Phase != AbductionPhase.Idle)
            {
                return;
            }

            if (!_herd.TryPickFree(_random, _lastTarget, out int index))
            {
                if (_herd.RemainingCount == 0)
                {
                    HerdEmpty?.Invoke();
                }

                return;
            }

            TargetIndex = index;
            _lastTarget = index;
            SetPhase(AbductionPhase.Approaching);
            CowTargeted?.Invoke(index);
        }

        /// <summary>
        /// Moves time forward. Zero, negative and NaN steps are ignored. A step longer than the
        /// current phase's remaining time ends that phase and nothing more.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Written so that NaN is ignored too.
            if (!(deltaTime > 0f) || Phase == AbductionPhase.Idle)
            {
                return;
            }

            _phaseTime += deltaTime;
            if (_phaseTime < DurationOf(Phase))
            {
                return;
            }

            switch (Phase)
            {
                case AbductionPhase.Approaching:
                    SetPhase(AbductionPhase.LoweringBeam);
                    break;

                case AbductionPhase.LoweringBeam:
                    SetPhase(AbductionPhase.Lifting);
                    break;

                case AbductionPhase.Lifting:
                    Absorb();
                    break;

                case AbductionPhase.Celebrating:
                    SetPhase(AbductionPhase.Idle);
                    Begin();
                    break;
            }
        }

        /// <summary>
        /// Ends the attempt. A cow that was picked goes back to the herd and
        /// <see cref="CowDropped"/> is raised. Does nothing when idle.
        /// </summary>
        public void Abort()
        {
            if (Phase == AbductionPhase.Idle)
            {
                return;
            }

            int index = TargetIndex;
            bool wasLifting = Phase == AbductionPhase.Lifting;

            if (index >= 0)
            {
                _herd.Release(index);
            }

            TargetIndex = -1;
            SetPhase(AbductionPhase.Idle);

            if (index >= 0)
            {
                CowDropped?.Invoke(index, wasLifting);
            }
        }

        private void Absorb()
        {
            int index = TargetIndex;

            _herd.Remove(index);
            TargetIndex = -1;
            SetPhase(AbductionPhase.Celebrating);
            CowAbsorbed?.Invoke(index);
        }

        private float DurationOf(AbductionPhase phase)
        {
            switch (phase)
            {
                case AbductionPhase.Approaching:
                    return _approachSeconds;
                case AbductionPhase.LoweringBeam:
                    return _beamSeconds;
                case AbductionPhase.Lifting:
                    return _liftSeconds;
                case AbductionPhase.Celebrating:
                    return _celebrateSeconds;
                default:
                    return 0f;
            }
        }

        private void SetPhase(AbductionPhase phase)
        {
            Phase = phase;
            _phaseTime = 0f;
            PhaseChanged?.Invoke(phase);
        }
    }
}
