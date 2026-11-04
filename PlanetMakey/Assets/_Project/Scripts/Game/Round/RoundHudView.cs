using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The round's text on screen: the time left and the cows still safe during the night, and a
    /// message for idle, dusk and dawn (with the score). It only reads from
    /// <see cref="RoundController"/> and holds no game state. Put the three texts on a Canvas; any of
    /// them may be left empty. A text is rewritten only when what it shows changes.
    /// </summary>
    public sealed class RoundHudView : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("Optional. Shows the time left as m:ss during the night, and nothing otherwise.")]
        [SerializeField] private TMP_Text _timerText;

        [Tooltip("Optional. Shows how many cows are still safe during dusk and night, and nothing otherwise.")]
        [SerializeField] private TMP_Text _cowsText;

        [Tooltip("Optional. Shows the idle, dusk and dawn messages below, and nothing during the night.")]
        [SerializeField] private TMP_Text _messageText;

        [Header("Messages")]
        [Tooltip("Shown while waiting for a round. Leave it empty to show nothing.")]
        [SerializeField] private string _idleMessage = "Press SPACE to start";

        [Tooltip("Shown during dusk. Leave it empty to show nothing.")]
        [SerializeField] private string _duskMessage = "Night is falling...";

        [Tooltip("Shown at dawn. {0} becomes the score, the number of cows kept through the night. Leave it empty to show nothing.")]
        [SerializeField] private string _dawnMessage = "You saved {0} cows!";

        [Tooltip("The cows text. {0} is the cows still safe and {1} is how many there were.")]
        [SerializeField] private string _cowsFormat = "Cows: {0} / {1}";

        // What is on screen now, so a text is rewritten only when it would change.
        [NonSerialized] private bool _shown;
        [NonSerialized] private RoundState _shownState;
        [NonSerialized] private int _shownScore;
        [NonSerialized] private int _shownSeconds;
        [NonSerialized] private int _shownCows;
        [NonSerialized] private int _shownTotal;

        // The texts as they were when this component was enabled, put back when it is disabled.
        [NonSerialized] private string _timerWas;
        [NonSerialized] private string _cowsWas;
        [NonSerialized] private string _messageWas;
        [NonSerialized] private bool _captured;

        private void OnEnable()
        {
            if (_round == null)
            {
                Debug.LogError("RoundHudView needs a RoundController, so it will do nothing.", this);
                return;
            }

            if (_timerText == null && _cowsText == null && _messageText == null)
            {
                Debug.LogWarning("RoundHudView has none of its three texts wired, so it will show nothing.", this);
            }

            _timerWas = _timerText != null ? _timerText.text : null;
            _cowsWas = _cowsText != null ? _cowsText.text : null;
            _messageWas = _messageText != null ? _messageText.text : null;
            _captured = true;
            _shown = false;
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // Any of these can already be destroyed when the scene shuts down.
            if (_timerText != null)
            {
                _timerText.text = _timerWas;
            }

            if (_cowsText != null)
            {
                _cowsText.text = _cowsWas;
            }

            if (_messageText != null)
            {
                _messageText.text = _messageWas;
            }
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            RoundState state = _round.State;
            bool night = state == RoundState.Night;
            int score = _round.Score;

            // Whole seconds, rounded up, so the clock reads 0:01 until the night is over.
            int seconds = night ? Mathf.CeilToInt(_round.NightSecondsLeft) : 0;
            bool showCows = night || state == RoundState.Dusk;
            int cows = showCows ? _round.CowsSafe : 0;
            int total = showCows ? _round.CowsTotal : 0;

            if (!_shown || state != _shownState || score != _shownScore)
            {
                ShowMessage(state, score);
            }

            if (!_shown || state != _shownState || seconds != _shownSeconds)
            {
                ShowTimer(night, seconds);
            }

            if (!_shown || state != _shownState || cows != _shownCows || total != _shownTotal)
            {
                ShowCows(showCows, cows, total);
            }

            _shown = true;
            _shownState = state;
            _shownScore = score;
            _shownSeconds = seconds;
            _shownCows = cows;
            _shownTotal = total;
        }

        private void ShowMessage(RoundState state, int score)
        {
            if (_messageText == null)
            {
                return;
            }

            switch (state)
            {
                case RoundState.Idle:
                    _messageText.text = _idleMessage;
                    break;

                case RoundState.Dusk:
                    _messageText.text = _duskMessage;
                    break;

                case RoundState.Dawn:
                    _messageText.text = Format(_dawnMessage, score, 0);
                    break;

                default:
                    _messageText.text = string.Empty;
                    break;
            }
        }

        private void ShowTimer(bool night, int seconds)
        {
            if (_timerText == null)
            {
                return;
            }

            _timerText.text = night
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", seconds / 60, seconds % 60)
                : string.Empty;
        }

        private void ShowCows(bool show, int cows, int total)
        {
            if (_cowsText == null)
            {
                return;
            }

            _cowsText.text = show ? Format(_cowsFormat, cows, total) : string.Empty;
        }

        // A message from the Inspector may have a bad placeholder; show it as it is and not an error.
        private static string Format(string format, int first, int second)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, first, second);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
