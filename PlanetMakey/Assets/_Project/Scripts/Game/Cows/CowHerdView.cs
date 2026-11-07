using System;
using PlanetMakey.Core;
using PlanetMakey.Game.Ufo;
using UnityEngine;

namespace PlanetMakey.Game.Cows
{
    /// <summary>
    /// Moves the cows to show what is happening to them: standing and hopping now and then, rising in the
    /// beam and shrinking a little as they reach the ship, falling back when Makey is hit, shrinking away
    /// when absorbed, and shaking when he is hit with the wrong color. A cow object is plain; this moves all
    /// of them from here. These are effects made in code.
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

        [Header("Idle hop")]
        [Tooltip("How high a cow hops, in meters. 0 for no hopping.")]
        [Min(0f)]
        [SerializeField] private float _hopHeight = 0.25f;

        [Tooltip("Seconds one hop takes.")]
        [Min(0.05f)]
        [SerializeField] private float _hopSeconds = 0.45f;

        [Tooltip("The shortest wait between one cow's hops, in seconds. Each cow picks its own wait, so they do not hop together.")]
        [Min(0f)]
        [SerializeField] private float _hopWaitMin = 3f;

        [Tooltip("The longest wait between one cow's hops, in seconds.")]
        [Min(0f)]
        [SerializeField] private float _hopWaitMax = 8f;

        [Tooltip("How much a cow stretches as she leaves the ground and squashes as she lands, as a share of her height. 0 for none.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _hopStretch = 0.08f;

        [Tooltip("The least a cow turns during one hop, in degrees. Each hop she turns a random amount between the least and the most, to a random side, and keeps that turn when she lands.")]
        [Range(0f, 360f)]
        [SerializeField] private float _hopTurnMin = 30f;

        [Tooltip("The most a cow turns during one hop, in degrees. 0 for cows that do not turn.")]
        [Range(0f, 360f)]
        [SerializeField] private float _hopTurnMax = 120f;

        [Header("Shake while lifted")]
        [Tooltip("How far the cow being lifted trembles while Makey warps her up, in meters. 0 for no trembling.")]
        [Min(0f)]
        [SerializeField] private float _liftShakeDistance = 0.03f;

        [Tooltip("How far she twists back and forth as she trembles, in degrees. 0 for no twist.")]
        [Range(0f, 30f)]
        [SerializeField] private float _liftShakeDegrees = 4f;

        [Tooltip("How fast she trembles. Higher is a faster, buzzier shake.")]
        [Min(0f)]
        [SerializeField] private float _liftShakeSpeed = 28f;

        [Tooltip("The share of the lift (0 to 1) over which the trembling builds up at the start, so it does not begin with a jerk.")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float _liftShakeRamp = 0.1f;

        [Header("End of the lift")]
        [Tooltip("The share of the lift (0 to 1) after which the cow starts to shrink, so she does not sink into the ship. At 1 she never does.")]
        [Range(0f, 1f)]
        [SerializeField] private float _shrinkStart = 0.8f;

        [Tooltip("How big she is at the top of the lift, as a share of her size. The last shrink away, when she is absorbed, goes on from here.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _endScale = 0.55f;

        [Header("Debug")]
        [Tooltip("Write what happens to the cows to the Console. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Taken from Makey when this component is enabled.
        [NonSerialized] private CowHerd _cowHerd;

        // Where each cow was when this component was enabled, put back when it is disabled.
        [NonSerialized] private Vector3[] _restPositions;
        [NonSerialized] private Vector3[] _restScales;
        [NonSerialized] private bool[] _wereActive;

        // Where and how big each cow was drawn last frame (the size is a multiple of her resting size), and
        // the running one-shot effects.
        [NonSerialized] private Vector3[] _lastPositions;
        [NonSerialized] private Vector3[] _lastSizes;
        [NonSerialized] private Vector3[] _dropFrom;
        [NonSerialized] private Vector3[] _dropFromSizes;
        [NonSerialized] private Vector3[] _absorbFromSizes;
        [NonSerialized] private float[] _dropLeft;
        [NonSerialized] private float[] _absorbLeft;
        [NonSerialized] private float _shakeLeft;
        [NonSerialized] private int _shakeIndex;

        // The idle hops: seconds until a cow's next hop, and seconds left in the hop she is in (0 if none).
        [NonSerialized] private float[] _hopWaitLeft;
        [NonSerialized] private float[] _hopLeft;

        // Which of each cow's own axes (0 x, 1 y, 2 z) points up in the world. A model imported with its
        // axes turned (a rotation of -90 degrees about x, as is usual) has z up, and a stretch in height
        // has to be along that axis.
        [NonSerialized] private int[] _upAxes;

        // Each cow's resting turn, how far she has turned from it for good (degrees about the world's up; each
        // hop adds to it), how much of that the hop she is in turns her by, and the turn drawn this frame.
        [NonSerialized] private Quaternion[] _restRotations;
        [NonSerialized] private float[] _yaw;
        [NonSerialized] private float[] _hopTurnBy;
        [NonSerialized] private float[] _spins;
        [NonSerialized] private bool _captured;

        /// <summary>Raised when a cow lands from an idle hop, with her number. The sounds listen to this.</summary>
        public event Action<int> HopLanded;

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
            _lastSizes = new Vector3[count];
            _dropFrom = new Vector3[count];
            _dropFromSizes = new Vector3[count];
            _absorbFromSizes = new Vector3[count];
            _dropLeft = new float[count];
            _absorbLeft = new float[count];
            _hopWaitLeft = new float[count];
            _hopLeft = new float[count];
            _upAxes = new int[count];
            _restRotations = new Quaternion[count];
            _yaw = new float[count];
            _hopTurnBy = new float[count];
            _spins = new float[count];
            _shakeLeft = 0f;
            _shakeIndex = -1;

            for (int i = 0; i < count; i++)
            {
                _restPositions[i] = _cowHerd.RestPosition(i);
                _lastPositions[i] = _restPositions[i];
                _lastSizes[i] = Vector3.one;
                _hopWaitLeft[i] = NextHopWait(true);

                Transform cow = _cowHerd.GetCow(i);
                if (cow != null)
                {
                    _restScales[i] = cow.localScale;
                    _restRotations[i] = cow.rotation;
                    _wereActive[i] = cow.gameObject.activeSelf;
                    _upAxes[i] = UpAxisOf(cow);
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

                // She keeps the turns of her earlier hops; a hop in progress sets this again below.
                _spins[i] = _yaw[i];

                switch (herd.GetState(i))
                {
                    case CowState.Free:
                        ShowFreeCow(cow, i, deltaTime);
                        break;

                    case CowState.Targeted:
                        ShowTargetedCow(cow, i, deltaTime);
                        break;

                    default:
                        ShowGoneCow(cow, i);
                        break;
                }
            }
        }

        private void ShowFreeCow(Transform cow, int index, float deltaTime)
        {
            Vector3 position = _restPositions[index];
            Vector3 size = Vector3.one;

            if (_dropLeft[index] > 0f)
            {
                // Falling back, faster and faster, and growing back to full size if she had shrunk.
                float fallen = 1f - _dropLeft[index] / _dropSeconds;
                position = Vector3.Lerp(_dropFrom[index], _restPositions[index], fallen * fallen);
                size = Vector3.Lerp(_dropFromSizes[index], Vector3.one, fallen);
                _hopLeft[index] = 0f;
            }
            else
            {
                ApplyHop(index, deltaTime, true, ref position, ref size);
            }

            Place(cow, index, position, size);
        }

        // Counts down to the cow's next hop (when a new one may start) and, while she is in one, lifts and
        // squashes her. A hop that has started always finishes, even if the cow is targeted meanwhile.
        private void ApplyHop(int index, float deltaTime, bool mayStart, ref Vector3 position, ref Vector3 size)
        {
            if (_hopHeight <= 0f)
            {
                return;
            }

            if (_hopLeft[index] > 0f)
            {
                _hopLeft[index] = Mathf.Max(0f, _hopLeft[index] - deltaTime);
                if (_hopLeft[index] <= 0f)
                {
                    _hopWaitLeft[index] = NextHopWait(false);
                    HopLanded?.Invoke(index);
                    return;
                }
            }
            else
            {
                if (!mayStart)
                {
                    return;
                }

                _hopWaitLeft[index] -= deltaTime;
                if (_hopWaitLeft[index] > 0f)
                {
                    return;
                }

                _hopLeft[index] = _hopSeconds;

                // This hop's turn: a random amount to a random side. It is added to her lasting turn now, and the
                // hop shows it a little at a time, so if the hop is cut short she ends up at the same place.
                float turn = UnityEngine.Random.Range(Mathf.Min(_hopTurnMin, _hopTurnMax), _hopTurnMax);
                _hopTurnBy[index] = UnityEngine.Random.value < 0.5f ? -turn : turn;
                _yaw[index] += _hopTurnBy[index];
            }

            float through = 1f - _hopLeft[index] / _hopSeconds;
            position.y += _hopHeight * CowMotion.HopHeight(through);
            _spins[index] = _yaw[index] - _hopTurnBy[index] * (1f - CowMotion.HopTurn(through));

            // Taller means thinner, so she keeps about the same bulk. "Taller" is along the cow's own up axis.
            float tall = CowMotion.HopStretch(through, _hopStretch);
            float thin = 1f / Mathf.Sqrt(tall);
            size = new Vector3(thin, thin, thin);
            size[_upAxes[index]] = tall;
        }

        // Which of the cow's own axes points most nearly up in the world: 0 for x, 1 for y, 2 for z.
        private static int UpAxisOf(Transform cow)
        {
            Vector3 up = cow.InverseTransformDirection(Vector3.up);
            float x = Mathf.Abs(up.x);
            float y = Mathf.Abs(up.y);
            float z = Mathf.Abs(up.z);

            if (x > y && x > z)
            {
                return 0;
            }

            return y > z ? 1 : 2;
        }

        private void ShowTargetedCow(Transform cow, int index, float deltaTime)
        {
            Vector3 position = _restPositions[index];
            Vector3 size = Vector3.one;

            if (index == _makey.TargetCowIndex)
            {
                position = _makey.TargetCowPosition;
                ApplyHop(index, deltaTime, false, ref position, ref size);

                if (index == _shakeIndex && _shakeLeft > 0f)
                {
                    float elapsed = 1f - _shakeLeft / _shakeSeconds;
                    float swing = Mathf.Sin(elapsed * _shakeCycles * 2f * Mathf.PI) * _shakeDistance * (1f - elapsed);
                    position += Vector3.right * swing;
                }

                // While she is warped up she trembles, and shrinks a little at the very end of the lift, so she
                // clips less into the ship.
                if (_makey.AbductionPhase == AbductionPhase.Lifting)
                {
                    float build = Mathf.SmoothStep(0f, 1f, _makey.AbductionProgress / _liftShakeRamp);
                    Vector3 shake = CowMotion.Shake(Time.time * _liftShakeSpeed, index * 13.1f);
                    position += new Vector3(shake.x, shake.y * 0.5f, shake.z) * (_liftShakeDistance * build);
                    _spins[index] += shake.y * _liftShakeDegrees * build;

                    float shrink = CowMotion.EndShrink(_makey.AbductionProgress, _shrinkStart, _endScale);
                    size *= shrink;
                }
            }
            else
            {
                _hopLeft[index] = 0f;
            }

            Place(cow, index, position, size);
        }

        private void ShowGoneCow(Transform cow, int index)
        {
            _hopLeft[index] = 0f;

            if (_absorbLeft[index] > 0f)
            {
                // Shrinking away where the beam took it, from the size she had at the top.
                Place(cow, index, _lastPositions[index], _absorbFromSizes[index] * (_absorbLeft[index] / _absorbSeconds));
                return;
            }

            if (cow.gameObject.activeSelf)
            {
                cow.gameObject.SetActive(false);
            }
        }

        // "size" multiplies the cow's resting size, on each axis.
        private void Place(Transform cow, int index, Vector3 position, Vector3 size)
        {
            if (!cow.gameObject.activeSelf)
            {
                cow.gameObject.SetActive(true);
            }

            cow.position = position;
            cow.rotation = Quaternion.AngleAxis(_spins[index], Vector3.up) * _restRotations[index];
            cow.localScale = Vector3.Scale(_restScales[index], size);
            _lastPositions[index] = position;
            _lastSizes[index] = size;
        }

        // The wait before a cow's next hop. The first wait is anywhere from 0, so the herd starts out of step.
        private float NextHopWait(bool first)
        {
            float longest = Mathf.Max(_hopWaitMin, _hopWaitMax);
            return first ? UnityEngine.Random.Range(0f, longest) : UnityEngine.Random.Range(Mathf.Min(_hopWaitMin, longest), longest);
        }

        private void OnCowDropped(int index, bool wasLifted)
        {
            _dropFrom[index] = _lastPositions[index];
            _dropFromSizes[index] = _lastSizes[index];
            _dropLeft[index] = wasLifted ? _dropSeconds : 0f;
            _hopWaitLeft[index] = NextHopWait(false);

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
            _absorbFromSizes[index] = _lastSizes[index];

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

        // A new round brings every cow back. The one-shot effects of the last round stop, but the hops and the turns
        // carry on, so the herd keeps moving as the round starts: the cows are not put back anywhere here, the next
        // frame places each one (cows that were taken come back where they stood, at full size).
        private void OnHerdReset()
        {
            for (int i = 0; i < _dropLeft.Length; i++)
            {
                _dropLeft[i] = 0f;
                _absorbLeft[i] = 0f;
            }

            _shakeLeft = 0f;
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
                cow.rotation = _restRotations[i];
                cow.localScale = _restScales[i];
                cow.gameObject.SetActive(_wereActive[i]);
                _lastPositions[i] = _restPositions[i];
                _lastSizes[i] = Vector3.one;
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
