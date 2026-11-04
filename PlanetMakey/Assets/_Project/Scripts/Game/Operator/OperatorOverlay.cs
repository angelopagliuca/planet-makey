using System;
using System.Globalization;
using PlanetMakey.Core;
using PlanetMakey.Game.Round;
using PlanetMakey.Game.Ufo;
using PlanetMakey.Hits;
using UnityEngine;

namespace PlanetMakey.Game.Operator
{
    /// <summary>
    /// A status panel for the person running the booth: the round, Makey, the latest hit and what it
    /// landed on, whether the hardware is still talking, the calibration, and the operator keys.
    /// It is hidden until <see cref="OperatorControls"/> (or <see cref="Toggle"/>) shows it. It only
    /// reads from the systems it watches and holds no game state.
    /// </summary>
    /// <remarks>
    /// Drawn with OnGUI, like the other overlays. The text is rebuilt only when a value changes,
    /// and not at all while the panel is hidden. Only <c>round</c> is required; a row whose source is
    /// not wired is left out.
    /// </remarks>
    public sealed class OperatorOverlay : MonoBehaviour
    {
        // Rows in the order they are drawn.
        private const int RoundRow = 0;
        private const int MakeyRow = 1;
        private const int HitRow = 2;
        private const int HardwareRow = 3;
        private const int LastMessageRow = 4;
        private const int CalibrationRow = 5;
        private const int RowCount = 6;

        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("Optional. Adds a row with Makey's state, shield color and abduction.")]
        [SerializeField] private Makey _makey;

        [Tooltip("Optional. Adds a row with the latest hit and what it landed on.")]
        [SerializeField] private HitResolver _resolver;

        [Tooltip("Optional. Adds the rows about the hardware link: port, time since the last message, rejected count.")]
        [SerializeField] private UdpHitSource _udp;

        [Tooltip("Optional. Adds a row with the calibration state.")]
        [SerializeField] private WallCalibration _calibration;

        [Header("Behavior")]
        [Tooltip("Show the panel as soon as the game starts. Normally it starts hidden, and Shift shows it.")]
        [SerializeField] private bool _visibleAtStart;

        [Header("Look")]
        [Tooltip("Text size in pixels.")]
        [Min(8)]
        [SerializeField] private int _fontSize = 18;

        [Tooltip("Width of the panel in pixels.")]
        [Min(100f)]
        [SerializeField] private float _panelWidth = 640f;

        [Tooltip("How opaque the dark box behind the text is, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _panelAlpha = 0.75f;

        [Header("Keys")]
        [Tooltip("The key list shown at the bottom, one line each. It is plain text, so keep it in step with the keys set on the key components.")]
        [TextArea(3, 8)]
        [SerializeField] private string _keyHelp =
            "Space  start a round\n" +
            "Hold Space during a round  abort it\n" +
            "Shift  show or hide this panel\n" +
            "F9  calibrate\n" +
            "1-4  mouse ball color";

        // The text of each row, built when its values change. Null means the row is not shown.
        private readonly string[] _rows = new string[RowCount];

        private string[] _helpLines;
        private bool _visible;

        // What the rows were last built from, so a row is rebuilt only when it would change.
        private bool _dirty;
        private RoundState _shownRoundState;
        private int _shownSeconds;
        private int _shownCows;
        private int _shownTotal;
        private int _shownScore;
        private MakeyState _shownMakeyState;
        private BallColor _shownShield;
        private AbductionPhase _shownPhase;
        private bool _shownListening;
        private int _shownHardwareSeconds;
        private int _shownRejected;
        private bool _shownLastRejected;
        private string _shownLastMessage;
        private bool _shownCalibrating;
        private bool _shownCalibrated;

        // Not kept across a script recompile in Play mode, so it is rebuilt from the skin.
        [NonSerialized] private GUIStyle _labelStyle;

        /// <summary>True while the panel is on screen.</summary>
        public bool IsVisible => _visible;

        /// <summary>Shows the panel if it is hidden, and hides it if it is shown.</summary>
        public void Toggle()
        {
            _visible = !_visible;
            _dirty = true;
        }

        private void OnEnable()
        {
            _visible = _visibleAtStart;
            _dirty = true;
            _helpLines = string.IsNullOrEmpty(_keyHelp) ? Array.Empty<string>() : _keyHelp.Split('\n');

            for (int i = 0; i < RowCount; i++)
            {
                _rows[i] = null;
            }

            if (_round == null)
            {
                Debug.LogError("OperatorOverlay needs a RoundController, so it will do nothing.", this);
                return;
            }

            if (_resolver != null)
            {
                _resolver.HitResolved += OnHitResolved;
            }
        }

        private void OnDisable()
        {
            if (_resolver != null)
            {
                _resolver.HitResolved -= OnHitResolved;
            }
        }

        private void Update()
        {
            if (_round == null || !_visible)
            {
                return;
            }

            RefreshRoundRow();
            RefreshMakeyRow();
            RefreshHardwareRows();
            RefreshCalibrationRow();
            _dirty = false;
        }

        private void RefreshRoundRow()
        {
            RoundState state = _round.State;
            int seconds = state == RoundState.Night ? Mathf.CeilToInt(_round.NightSecondsLeft) : 0;
            int cows = _round.CowsSafe;
            int total = _round.CowsTotal;
            int score = _round.Score;

            if (!_dirty && state == _shownRoundState && seconds == _shownSeconds
                && cows == _shownCows && total == _shownTotal && score == _shownScore)
            {
                return;
            }

            _shownRoundState = state;
            _shownSeconds = seconds;
            _shownCows = cows;
            _shownTotal = total;
            _shownScore = score;

            switch (state)
            {
                case RoundState.Night:
                    _rows[RoundRow] = string.Format(CultureInfo.InvariantCulture, "Round: Night   time left {0}:{1:00}   cows {2} / {3}",
                        seconds / 60, seconds % 60, cows, total);
                    break;

                case RoundState.Dusk:
                    _rows[RoundRow] = string.Format(CultureInfo.InvariantCulture, "Round: Dusk   cows {0} / {1}", cows, total);
                    break;

                case RoundState.Dawn:
                    _rows[RoundRow] = string.Format(CultureInfo.InvariantCulture, "Round: Dawn   score {0} of {1}", score, total);
                    break;

                default:
                    _rows[RoundRow] = score > 0
                        ? string.Format(CultureInfo.InvariantCulture, "Round: Idle   last score {0} of {1}", score, total)
                        : "Round: Idle";
                    break;
            }
        }

        private void RefreshMakeyRow()
        {
            if (_makey == null)
            {
                return;
            }

            MakeyState state = _makey.State;
            BallColor shield = _makey.ShieldColor;
            AbductionPhase phase = _makey.AbductionPhase;

            if (!_dirty && state == _shownMakeyState && shield == _shownShield && phase == _shownPhase)
            {
                return;
            }

            _shownMakeyState = state;
            _shownShield = shield;
            _shownPhase = phase;

            string text = "Makey: " + state;
            if (state == MakeyState.Shielded)
            {
                text += " (" + shield + " shield)";
            }

            if (phase != AbductionPhase.Idle)
            {
                text += "   abduction: " + phase;
            }

            _rows[MakeyRow] = text;
        }

        private void RefreshHardwareRows()
        {
            if (_udp == null)
            {
                return;
            }

            bool listening = _udp.IsListening;
            int rejected = _udp.RejectedCount;
            bool lastRejected = _udp.LastMessageRejected;
            string last = _udp.LastMessage;

            // -1 before the first message. Whole seconds, so the row changes once a second at most.
            int seconds = _udp.HasReceived ? Mathf.FloorToInt(_udp.SecondsSinceLastMessage) : -1;

            if (!_dirty && listening == _shownListening && seconds == _shownHardwareSeconds && rejected == _shownRejected
                && lastRejected == _shownLastRejected && last == _shownLastMessage)
            {
                return;
            }

            _shownListening = listening;
            _shownHardwareSeconds = seconds;
            _shownRejected = rejected;
            _shownLastRejected = lastRejected;
            _shownLastMessage = last;

            string state = listening ? "listening" : "NOT LISTENING (see Console)";
            string age = seconds < 0 ? "no message yet" : "last message " + seconds + " s ago";
            _rows[HardwareRow] = "Hardware: UDP :" + _udp.Port + " " + state + "   " + age + "   rejected " + rejected;

            string lastText = string.IsNullOrEmpty(last) ? "-" : (lastRejected ? last + "  (rejected)" : last);
            _rows[LastMessageRow] = "Last message: " + lastText;
        }

        private void RefreshCalibrationRow()
        {
            if (_calibration == null)
            {
                return;
            }

            bool calibrating = _calibration.IsCalibrating;
            bool calibrated = _calibration.IsCalibrated;

            if (!_dirty && calibrating == _shownCalibrating && calibrated == _shownCalibrated)
            {
                return;
            }

            _shownCalibrating = calibrating;
            _shownCalibrated = calibrated;

            _rows[CalibrationRow] = calibrating
                ? "Calibration: in progress"
                : (calibrated ? "Calibration: active" : "Calibration: none (raw positions)");
        }

        // Built once per hit and not per frame. The collider is checked first because a target may
        // destroy its own object while handling the ball.
        private void OnHitResolved(ResolvedHit resolved)
        {
            string result;
            if (resolved.Collider == null)
            {
                result = "Miss";
            }
            else if (resolved.HitTarget)
            {
                result = "Hit target " + resolved.Collider.name;
            }
            else
            {
                result = "Hit non-target " + resolved.Collider.name;
            }

            // Invariant culture keeps the decimal point the same as in the hardware protocol.
            _rows[HitRow] = string.Format(
                CultureInfo.InvariantCulture,
                "Last hit: {0} {1:0.00}, {2:0.00}  ->  {3}",
                resolved.Hit.Color,
                resolved.Hit.Position.x,
                resolved.Hit.Position.y,
                result);
        }

        private void OnGUI()
        {
            // OnGUI also runs for layout and input events. Draw once per frame only.
            if (!_visible || _round == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureLabelStyle();

            float lineHeight = _fontSize + 8f;
            int lines = _helpLines.Length;
            for (int i = 0; i < RowCount; i++)
            {
                if (_rows[i] != null)
                {
                    lines++;
                }
            }

            const float Margin = 10f;
            float height = lines * lineHeight + Margin * 2f + (_helpLines.Length > 0 ? lineHeight * 0.5f : 0f);
            Rect panel = new Rect(Screen.width - _panelWidth - Margin, Margin, _panelWidth, height);

            GUI.color = new Color(0f, 0f, 0f, _panelAlpha);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float top = panel.y + Margin;
            for (int i = 0; i < RowCount; i++)
            {
                if (_rows[i] == null)
                {
                    continue;
                }

                GUI.Label(new Rect(panel.x + Margin, top, panel.width - Margin * 2f, lineHeight), _rows[i], _labelStyle);
                top += lineHeight;
            }

            top += lineHeight * 0.5f;
            for (int i = 0; i < _helpLines.Length; i++)
            {
                GUI.Label(new Rect(panel.x + Margin, top, panel.width - Margin * 2f, lineHeight), _helpLines[i], _labelStyle);
                top += lineHeight;
            }
        }

        private void EnsureLabelStyle()
        {
            if (_labelStyle == null)
            {
                // GUI.skin is only available inside OnGUI, so the style cannot be made earlier.
                _labelStyle = new GUIStyle(GUI.skin.label);
                _labelStyle.normal.textColor = Color.white;
                _labelStyle.clipping = TextClipping.Clip;
                _labelStyle.wordWrap = false;
            }

            // Set every time so a change in the Inspector shows while playing.
            _labelStyle.fontSize = _fontSize;
        }
    }
}
