using System;

namespace PlanetMakey.Game.Cows
{
    /// <summary>
    /// The rules for the herd: which cows are free, which one Makey has picked, and which are
    /// lost. Cows are numbered from 0. It knows nothing about the scene, so it can be tested on
    /// its own; <see cref="CowHerd"/> holds the cow objects.
    /// </summary>
    public sealed class Herd
    {
        private readonly CowState[] _states;

        /// <param name="count">How many cows are in the herd. All of them start Free.</param>
        public Herd(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "A herd cannot have a negative number of cows.");
            }

            _states = new CowState[count];
        }

        /// <summary>Raised after every change of any cow's state, and after a reset.</summary>
        public event Action Changed;

        /// <summary>How many cows the herd started with.</summary>
        public int Count => _states.Length;

        /// <summary>How many cows can be picked now.</summary>
        public int FreeCount => CountOf(CowState.Free);

        /// <summary>
        /// How many cows are not lost: the Free ones and the one being abducted. This is the
        /// score at the end of a round.
        /// </summary>
        public int RemainingCount => _states.Length - CountOf(CowState.Gone);

        /// <summary>The state of one cow.</summary>
        public CowState GetState(int index)
        {
            CheckIndex(index);
            return _states[index];
        }

        /// <summary>
        /// Picks a random Free cow and marks it Targeted. The cow <paramref name="avoid"/> is
        /// skipped unless it is the only one free, so the same cow is not picked twice in a row.
        /// </summary>
        /// <param name="random">Chooses among the candidates.</param>
        /// <param name="avoid">A cow to skip if possible, or -1 for none.</param>
        /// <param name="index">The cow picked, or -1 if none was free.</param>
        /// <returns>True if a cow was picked.</returns>
        public bool TryPickFree(Random random, int avoid, out int index)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            index = -1;

            int candidates = 0;
            for (int i = 0; i < _states.Length; i++)
            {
                if (_states[i] == CowState.Free && i != avoid)
                {
                    candidates++;
                }
            }

            if (candidates == 0)
            {
                bool onlyTheAvoidedIsFree = avoid >= 0 && avoid < _states.Length && _states[avoid] == CowState.Free;
                if (!onlyTheAvoidedIsFree)
                {
                    return false;
                }

                index = avoid;
            }
            else
            {
                int choice = random.Next(candidates);
                for (int i = 0; i < _states.Length; i++)
                {
                    if (_states[i] != CowState.Free || i == avoid)
                    {
                        continue;
                    }

                    if (choice == 0)
                    {
                        index = i;
                        break;
                    }

                    choice--;
                }
            }

            _states[index] = CowState.Targeted;
            Changed?.Invoke();
            return true;
        }

        /// <summary>A Targeted cow goes back to Free. Returns false if it was not Targeted.</summary>
        public bool Release(int index)
        {
            return Change(index, CowState.Targeted, CowState.Free);
        }

        /// <summary>A Targeted cow is lost. Returns false if it was not Targeted.</summary>
        public bool Remove(int index)
        {
            return Change(index, CowState.Targeted, CowState.Gone);
        }

        /// <summary>Brings every cow back, for a new round.</summary>
        public void Reset()
        {
            for (int i = 0; i < _states.Length; i++)
            {
                _states[i] = CowState.Free;
            }

            Changed?.Invoke();
        }

        private bool Change(int index, CowState from, CowState to)
        {
            CheckIndex(index);

            if (_states[index] != from)
            {
                return false;
            }

            _states[index] = to;
            Changed?.Invoke();
            return true;
        }

        private int CountOf(CowState state)
        {
            int count = 0;
            for (int i = 0; i < _states.Length; i++)
            {
                if (_states[i] == state)
                {
                    count++;
                }
            }

            return count;
        }

        private void CheckIndex(int index)
        {
            if (index < 0 || index >= _states.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "There is no cow " + index + " in a herd of " + _states.Length + ".");
            }
        }
    }
}
