using System;
using PlanetMakey.Core;
using PlanetMakey.Game.Ufo;
using UnityEngine;

namespace PlanetMakey.Game.Cows
{
    /// <summary>
    /// Moves the cows to show what is happening to them: standing, rising in the beam, falling
    /// back when Makey is hit, shrinking away when absorbed, and shaking when he is hit with the
    /// wrong color. A cow object is plain; this moves all of them from here. These are placeholder
    /// effects made in code.
    /// </summary>
    public sealed class CowHerdView : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The herd comes from Makey, so it is wired only once.")]
        [SerializeField] private Makey _makey;

        [Header("Motion")]
        [Tooltip("Seconds a dropped cow takes to fall back to where it stood.")]
        [Min(0.01f)]
        [SerializeField] private float _dropSeconds = 0.5f;

        [Tooltip("Seconds an absorbed cow takes to shrink away.")]
        [Min(0.01f)]
        [SerializeField] private float _absorbSeconds = 0.25f;

        [Tooltip("Seconds the lifted cow shakes after a wrong color hits Makey.")]
        [Min(0.01f)]
        [SerializeField] private float _shakeSeconds = 0.35f;

        [Tooltip("How far the lifted cow shakes to each side, in meters.")]
        [Min(0f)]
        [SerializeField] private float _shakeDistance = 0.12f;

        [Tooltip("Shakes from side to side during one shake.")]
        [Min(0f)]
        [SerializeField] private float _shakeCycles = 4f;

        [Header("Debug")]
        [Tooltip("Write what happens to the cows to the Console. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Taken from Makey when this component is enabled.
        [NonSerialized] private CowHerd _cowHerd;

        // Where each cow was when this component was enabled, put back when it is disabled.
        [NonSerialized] private Vector3[] _restPositions;
        [NonSerialized] private Vector3[] _restScales;
        [NonSerialized] private bool[] _wereActive;

        // Where each cow was drawn last frame, and the running one-shot effects.
        [NonSerialized] private Vector3[] _lastPositions;
        [NonSerialized] private Vector3[] _dropFrom;
        [NonSerialized] private float[] _dropLeft;
        [NonSerialized] private float[] _absorbLeft;
        [NonSerialized] private float _shakeLeft;
        [NonSerialized] private int _shakeIndex;
        [NonSerialized] private bool _captured;

        private void OnEnable()
        {
            _cowHerd = _makey != null ? _makey.CowHerd : null;
            if (_makey == null || _cowHerd == null)
            {
                Debug.LogError("CowHerdView needs a Makey that has a CowHerd wired in, so it will do nothing.", this);
                return;
            }

            int count = _cowHerd.Count;
            _restPositions = new Vector3[count];
            _restScales = new Vector3[count];
            _wereActive = new bool[count];
            _lastPositions = new Vector3[count];
            _dropFrom = new Vector3[count];
            _dropLeft = new float[count];
            _absorbLeft = new float[count];
            _shakeLeft = 0f;
            _shakeIndex = -1;

            for (int i = 0; i < count; i++)
            {
                _restPositions[i] = _cowHerd.RestPosition(i);
                _lastPositions[i] = _restPositions[i];

                Transform cow = _cowHerd.GetCow(i);
                if (cow != null)
                {
                    _restScales[i] = cow.localScale;
                    _wereActive[i] = cow.gameObject.activeSelf;
                }
            }

            _captured = true;

            _makey.CowDropped += OnCowDropped;
            _makey.CowAbsorbed += OnCowAbsorbed;
            _makey.BallDeflected += OnBallDeflected;
            _cowHerd.HerdReset += OnHerdReset;
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
                _makey.BallDeflected -= OnBallDeflected;
            }

            if (_cowHerd != null)
            {
                _cowHerd.HerdReset -= OnHerdReset;
            }

            // Leave every cow as it was found, so a Play session leaves nothing behind.
            if (_cowHerd != null)
            {
                RestoreCows();
            }
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _shakeLeft = Mathf.Max(0f, _shakeLeft - deltaTime);

            Herd herd = _cowHerd.Rules;
            for (int i = 0; i < _restPositions.Length; i++)
            {
                Transform cow = _cowHerd.GetCow(i);
                if (cow == null)
                {
                    continue;
                }

                _dropLeft[i] = Mathf.Max(0f, _dropLeft[i] - deltaTime);
                _absorbLeft[i] = Mathf.Max(0f, _absorbLeft[i] - deltaTime);

                switch (herd.GetState(i))
                {
                    case CowState.Free:
                        ShowFreeCow(cow, i);
                        break;

                    case CowState.Targeted:
                        ShowTargetedCow(cow, i);
                        break;

                    default:
                        ShowGoneCow(cow, i);
                        break;
                }
            }
        }

        private void ShowFreeCow(Transform cow, int index)
        {
            Vector3 position = _restPositions[index];

            if (_dropLeft[index] > 0f)
            {
                // Falling back, faster and faster.
                float fallen = 1f - _dropLeft[index] / _dropSeconds;
                position = Vector3.Lerp(_dropFrom[index], _restPositions[index], fallen * fallen);
            }

            Place(cow, index, position, 1f);
        }

        private void ShowTargetedCow(Transform cow, int index)
        {
            Vector3 position = _restPositions[index];

            if (index == _makey.TargetCowIndex)
            {
                position = _makey.TargetCowPosition;

                if (index == _shakeIndex && _shakeLeft > 0f)
                {
                    float elapsed = 1f - _shakeLeft / _shakeSeconds;
                    float swing = Mathf.Sin(elapsed * _shakeCycles * 2f * Mathf.PI) * _shakeDistance * (1f - elapsed);
                    position += Vector3.right * swing;
                }
            }

            Place(cow, index, position, 1f);
        }

        private void ShowGoneCow(Transform cow, int index)
        {
            if (_absorbLeft[index] > 0f)
            {
                // Shrinking away where the beam took it.
                Place(cow, index, _lastPositions[index], _absorbLeft[index] / _absorbSeconds);
                return;
            }

            if (cow.gameObject.activeSelf)
            {
                cow.gameObject.SetActive(false);
            }
        }

        private void Place(Transform cow, int index, Vector3 position, float sizeFactor)
        {
            if (!cow.gameObject.activeSelf)
            {
                cow.gameObject.SetActive(true);
            }

            cow.position = position;
            cow.localScale = _restScales[index] * sizeFactor;
            _lastPositions[index] = position;
        }

        private void OnCowDropped(int index, bool wasLifted)
        {
            _dropFrom[index] = _lastPositions[index];
            _dropLeft[index] = wasLifted ? _dropSeconds : 0f;

            if (_debugLog)
            {
                Log(wasLifted
                    ? "Cow " + index + " dropped from the beam and falls back over " + _dropSeconds.ToString("0.0#") + " s."
                    : "Cow " + index + " was released before it left the ground.");
            }
        }

        private void OnCowAbsorbed(int index)
        {
            _absorbLeft[index] = _absorbSeconds;

            if (_debugLog)
            {
                Log("Cow " + index + " was absorbed: it shrinks away over " + _absorbSeconds.ToString("0.0#") + " s and is gone.");
            }
        }

        private void OnBallDeflected(BallColor color, Vector3 worldPoint)
        {
            int target = _makey.TargetCowIndex;
            if (_makey.AbductionPhase != AbductionPhase.Lifting || target < 0)
            {
                return;
            }

            _shakeIndex = target;
            _shakeLeft = _shakeSeconds;

            if (_debugLog)
            {
                Log("A " + color + " ball bounced off the shield: cow " + target + " shakes for " + _shakeSeconds.ToString("0.0#") + " s. Moo!");
            }
        }

        private void OnHerdReset()
        {
            for (int i = 0; i < _dropLeft.Length; i++)
            {
                _dropLeft[i] = 0f;
                _absorbLeft[i] = 0f;
            }

            _shakeLeft = 0f;
            RestoreCows();
        }

        // Puts every cow back at its resting place, size and on/off state.
        private void RestoreCows()
        {
            for (int i = 0; i < _restPositions.Length; i++)
            {
                Transform cow = _cowHerd.GetCow(i);
                if (cow == null)
                {
                    continue;
                }

                cow.position = _restPositions[i];
                cow.localScale = _restScales[i];
                cow.gameObject.SetActive(_wereActive[i]);
                _lastPositions[i] = _restPositions[i];
            }
        }

        private void Log(string message)
        {
            if (_debugLog)
            {
                Debug.Log("[CowHerdView] " + message, this);
            }
        }
    }
}
