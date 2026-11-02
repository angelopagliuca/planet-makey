using System;
using UnityEngine;

namespace PlanetMakey.Game.Cows
{
    /// <summary>
    /// The herd in the scene: it holds the cow objects, owns the <see cref="Herd"/> rules, and
    /// remembers where each cow stands. <see cref="CowHerdView"/> moves the cows around; Makey
    /// reads from here which cows are free and where they are.
    /// </summary>
    /// <remarks>
    /// The cows can stand anywhere in the 3D scene. They should have no colliders, or they could
    /// block balls aimed at Makey. Keep this component enabled for as long as the game runs:
    /// disabling it throws away the herd's state, so Makey would keep the old herd while the cow
    /// view picked up a new one, and the two would disagree.
    /// </remarks>
    public sealed class CowHerd : MonoBehaviour
    {
        [Tooltip("The cow objects, in any order. Each one is a cow. Give them no colliders.")]
        [SerializeField] private Transform[] _cows;

        [Header("Debug")]
        [Tooltip("Write the herd's setup and every change of it to the Console. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        [NonSerialized] private Herd _herd;
        [NonSerialized] private Vector3[] _restPositions;

        /// <summary>Raised after <see cref="ResetHerd"/> has brought every cow back.</summary>
        public event Action HerdReset;

        /// <summary>How many cow objects there are.</summary>
        public int Count => _cows != null ? _cows.Length : 0;

        /// <summary>The herd's rules: which cows are free, targeted or gone.</summary>
        public Herd Rules
        {
            get
            {
                EnsureReady();
                return _herd;
            }
        }

        private void OnEnable()
        {
            EnsureReady();
            CheckWiring();
        }

        private void OnDisable()
        {
            if (_herd != null)
            {
                _herd.Changed -= OnHerdChanged;
            }

            _herd = null;
            _restPositions = null;
        }

        /// <summary>Where a cow stands when it is not being abducted, in world space.</summary>
        public Vector3 RestPosition(int index)
        {
            EnsureReady();
            return _restPositions[index];
        }

        /// <summary>The object of one cow. Can be null if the list has an empty slot.</summary>
        public Transform GetCow(int index)
        {
            return _cows[index];
        }

        /// <summary>
        /// Brings every cow back, for a new round. Also in the component's context menu, to try
        /// the game again without stopping Play.
        /// </summary>
        [ContextMenu("Reset Herd")]
        public void ResetHerd()
        {
            // In Edit mode there is no game to reset, and building the herd then would keep stale
            // positions into the next Play session.
            if (!Application.isPlaying)
            {
                Debug.Log("[CowHerd] Reset Herd only works while the game is playing.", this);
                return;
            }

            EnsureReady();
            _herd.Reset();

            if (_debugLog)
            {
                Debug.Log("[CowHerd] Herd reset: all " + _herd.Count + " cows are back.", this);
            }

            HerdReset?.Invoke();
        }

        // Done on first use and not only in OnEnable, because Makey may ask before this
        // component has been enabled: the order is not defined.
        private void EnsureReady()
        {
            if (_herd != null)
            {
                return;
            }

            int count = Count;
            _herd = new Herd(count);
            _restPositions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                _restPositions[i] = _cows[i] != null ? _cows[i].position : Vector3.zero;
            }

            _herd.Changed += OnHerdChanged;
        }

        private void OnHerdChanged()
        {
            if (!_debugLog)
            {
                return;
            }

            Debug.Log("[CowHerd] Herd now: " + _herd.FreeCount + " free, " + (_herd.RemainingCount - _herd.FreeCount) + " targeted, "
                + (_herd.Count - _herd.RemainingCount) + " gone (" + _herd.RemainingCount + " of " + _herd.Count + " still safe).", this);
        }

        // Flags a setup that leaves the herd empty or makes balls miss Makey.
        private void CheckWiring()
        {
            if (Count == 0)
            {
                Debug.LogWarning("CowHerd has no cows, so Makey has nothing to abduct. Add the cow objects to its list.", this);
                return;
            }

            for (int i = 0; i < _cows.Length; i++)
            {
                if (_cows[i] == null)
                {
                    Debug.LogWarning("Slot " + i + " of the CowHerd's cow list is empty. It counts as a cow at the origin.", this);
                    continue;
                }

                if (_cows[i].GetComponentInChildren<Collider>(true) != null)
                {
                    Debug.LogWarning("The cow '" + _cows[i].name + "' has a collider. Remove it, or balls aimed at Makey can hit the cow instead.", this);
                }
            }

            if (_debugLog)
            {
                Debug.Log("[CowHerd] Enabled with " + Count + " cows.", this);
            }
        }
    }
}
