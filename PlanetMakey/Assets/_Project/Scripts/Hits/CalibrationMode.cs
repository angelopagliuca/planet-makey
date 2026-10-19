using System;
using PlanetMakey.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// The operator's calibration routine. A key press starts it, four targets are drawn on the
    /// projected picture one at a time, and the hit received for each one is recorded. From the
    /// four recorded positions it works out the mapping and hands it to <see cref="WallCalibration"/>.
    /// </summary>
    public sealed class CalibrationMode : MonoBehaviour
    {
        private const int TargetCount = 4;

        private enum Phase
        {
            Idle,
            Collecting,
            Message
        }

        [Header("Wiring")]
        [Tooltip("Receives the finished calibration.")]
        [SerializeField] private WallCalibration _calibration;

        [Tooltip("Every hit source whose hits can be used as calibration points.")]
        [SerializeField] private HitSource[] _sources;

        [Header("Controls")]
        [Tooltip("Starts a calibration. Pressing it during a calibration cancels it.")]
        [SerializeField] private Key _toggleKey = Key.F9;

        [Header("Targets")]
        [Tooltip("How far the targets sit inside the corners of the picture, as a fraction of its size.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float _inset = 0.1f;

        [Tooltip("How many hits are averaged for each target. More hits smooth out a noisy sensor.")]
        [Min(1)]
        [SerializeField] private int _hitsPerTarget = 1;

        [Header("Look")]
        [Tooltip("Edge length of the cross-hair drawn for each target, in pixels.")]
        [SerializeField] private float _targetSize = 48f;

        [Tooltip("Text size in pixels.")]
        [SerializeField] private int _fontSize = 28;

        [Tooltip("Seconds the result message stays on screen.")]
        [SerializeField] private float _messageSeconds = 2.5f;

        // Where each target is drawn, as a point of the picture (0 to 1, origin bottom left), and
        // the sensor position recorded for it. Order: bottom left, bottom right, top right, top left.
        private readonly Vector2[] _targets = new Vector2[TargetCount];
        private readonly Vector2[] _sensorPoints = new Vector2[TargetCount];

        [NonSerialized] private Phase _phase;

        // Set when the fourth point is in. The calibration is worked out on the next frame, see Update.
        [NonSerialized] private bool _finishPending;
        [NonSerialized] private int _targetIndex;
        [NonSerialized] private int _hitsForTarget;
        [NonSerialized] private Vector2 _positionSum;
        [NonSerialized] private float _messageEndTime;
        [NonSerialized] private string _bannerText;
        [NonSerialized] private GUIStyle _labelStyle;

        private void OnEnable()
        {
            _phase = Phase.Idle;
            _finishPending = false;

            if (_calibration == null)
            {
                Debug.LogWarning("CalibrationMode has no WallCalibration, so it cannot calibrate.", this);
            }

            if (_sources == null || _sources.Length == 0)
            {
                Debug.LogWarning(
                    "CalibrationMode has no hit sources, so it cannot record any calibration points. " +
                    "Add the hit sources to its Sources list.",
                    this);
                return;
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null)
                {
                    _sources[i].HitReceived += OnHitReceived;
                }
            }
        }

        private void OnDisable()
        {
            // The resolver ignores hits while a calibration runs, so never leave one running.
            if (_phase == Phase.Collecting && _calibration != null)
            {
                _calibration.CancelCalibration();
            }

            _phase = Phase.Idle;
            _finishPending = false;

            if (_sources == null)
            {
                return;
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null)
                {
                    _sources[i].HitReceived -= OnHitReceived;
                }
            }
        }

        private void Update()
        {
            // The calibration may have been ended elsewhere, for example by disabling WallCalibration.
            if (_phase == Phase.Collecting && (_calibration == null || !_calibration.IsCalibrating))
            {
                _phase = Phase.Idle;
                _finishPending = false;
            }

            // Finishing ends the calibration, after which the resolver treats hits as shots again.
            // Doing it here and not inside the hit handler means the resolver, which hears the
            // same hit, still sees the calibration running whatever order the two subscribed in.
            if (_finishPending)
            {
                _finishPending = false;
                Finish();
            }
            else
            {
                // Not in the frame that finishes, so a key press cannot start a new calibration
                // before the result message has been seen.
                HandleToggleKey();
            }

            if (_phase == Phase.Message && Time.unscaledTime >= _messageEndTime)
            {
                _phase = Phase.Idle;
            }
        }

        private void HandleToggleKey()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _toggleKey == Key.None)
            {
                return;
            }

            if (!keyboard[_toggleKey].wasPressedThisFrame)
            {
                return;
            }

            if (_phase == Phase.Collecting)
            {
                Cancel();
            }
            else
            {
                Begin();
            }
        }

        private void Begin()
        {
            if (_calibration == null)
            {
                return;
            }

            // Targets are fixed for the whole run, so changing the inset in the Inspector
            // takes effect at the next start.
            float near = _inset;
            float far = 1f - _inset;
            _targets[0] = new Vector2(near, near);
            _targets[1] = new Vector2(far, near);
            _targets[2] = new Vector2(far, far);
            _targets[3] = new Vector2(near, far);

            _phase = Phase.Collecting;
            _finishPending = false;
            _targetIndex = 0;
            StartTarget();
            _calibration.BeginCalibration();
        }

        private void Cancel()
        {
            _phase = Phase.Idle;
            _finishPending = false;
            _calibration.CancelCalibration();
        }

        private void StartTarget()
        {
            _hitsForTarget = 0;
            _positionSum = Vector2.zero;
            UpdateBanner();
        }

        private void OnHitReceived(BallHit hit)
        {
            // Hits that arrive after the fourth point, before it is processed, are ignored.
            if (_phase != Phase.Collecting || _finishPending)
            {
                return;
            }

            if (!IsFinite(hit.Position))
            {
                return;
            }

            // The raw sensor position is what must be recorded: it is the one that has to be
            // mapped onto the target.
            _positionSum += hit.Position;
            _hitsForTarget++;

            if (_hitsForTarget < _hitsPerTarget)
            {
                UpdateBanner();
                return;
            }

            _sensorPoints[_targetIndex] = _positionSum / _hitsForTarget;
            _targetIndex++;

            if (_targetIndex < TargetCount)
            {
                StartTarget();
                return;
            }

            _finishPending = true;
            _bannerText = "Calibration: working it out...";
        }

        private void Finish()
        {
            if (Homography.TrySolve(_sensorPoints, _targets, out Homography result))
            {
                bool saved = _calibration.CompleteCalibration(result);
                ShowMessage(saved ? "Calibration saved" : "Calibration applied but NOT saved (see Console)");
                return;
            }

            _calibration.CancelCalibration();
            ShowMessage("Calibration failed: hit the four targets in order, each in a different place. Press " + _toggleKey + " to try again.");
        }

        private void ShowMessage(string text)
        {
            _phase = Phase.Message;
            _bannerText = text;
            _messageEndTime = Time.unscaledTime + _messageSeconds;
        }

        // Built when the state changes and not every frame.
        private void UpdateBanner()
        {
            string text = "Calibration " + (_targetIndex + 1) + "/" + TargetCount + ": hit the target";
            if (_hitsPerTarget > 1)
            {
                text += " (" + (_hitsForTarget + 1) + "/" + _hitsPerTarget + ")";
            }

            _bannerText = text;
        }

        private void OnGUI()
        {
            // OnGUI also runs for layout and input events. Draw once per frame only.
            if (Event.current.type != EventType.Repaint || _phase == Phase.Idle)
            {
                return;
            }

            EnsureLabelStyle();

            // No target is left to draw once the fourth point is in.
            if (_phase == Phase.Collecting && !_finishPending)
            {
                DrawTarget(_targets[_targetIndex]);
            }

            DrawBanner(_bannerText);
            GUI.color = Color.white;
        }

        private void EnsureLabelStyle()
        {
            if (_labelStyle == null)
            {
                // GUI.skin is only available inside OnGUI, so the style cannot be made earlier.
                _labelStyle = new GUIStyle(GUI.skin.label);
                _labelStyle.alignment = TextAnchor.MiddleCenter;
                _labelStyle.fontStyle = FontStyle.Bold;
            }

            // Set every time so a change in the Inspector shows while playing.
            _labelStyle.fontSize = _fontSize;
        }

        private void DrawTarget(Vector2 position)
        {
            // The picture has its origin at the bottom left; OnGUI at the top left.
            float x = position.x * Screen.width;
            float y = (1f - position.y) * Screen.height;
            float half = _targetSize * 0.5f;

            const float outline = 3f;
            const float bar = 3f;

            // A dark outline under a light cross-hair stays visible on any background.
            FillRect(new Rect(x - half - outline, y - bar * 0.5f - outline, _targetSize + 2f * outline, bar + 2f * outline), Color.black);
            FillRect(new Rect(x - bar * 0.5f - outline, y - half - outline, bar + 2f * outline, _targetSize + 2f * outline), Color.black);
            FillRect(new Rect(x - half, y - bar * 0.5f, _targetSize, bar), Color.white);
            FillRect(new Rect(x - bar * 0.5f, y - half, bar, _targetSize), Color.white);
        }

        private void DrawBanner(string text)
        {
            float height = _fontSize * 2f;
            Rect rect = new Rect(0f, (Screen.height - height) * 0.5f, Screen.width, height);

            // A dark copy underneath keeps the text readable on any background.
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, _labelStyle);
            GUI.color = Color.white;
            GUI.Label(rect, text, _labelStyle);
        }

        private static void FillRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }
    }
}
