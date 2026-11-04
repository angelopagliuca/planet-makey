using PlanetMakey.Game.Round;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PlanetMakey.Game.Operator
{
    /// <summary>
    /// The operator's keys that are not tied to one system: Shift shows and hides the
    /// <see cref="OperatorOverlay"/>, and holding Space during a round aborts it. Starting a round
    /// (Space, <see cref="KeyRoundStartSource"/>) and calibrating (F9, CalibrationMode) have their own
    /// components.
    /// </summary>
    public sealed class OperatorControls : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("The panel the Shift key shows and hides.")]
        [SerializeField] private OperatorOverlay _overlay;

        [Header("Abort")]
        [Tooltip("Hold this key during dusk or night to abort the round. A press that starts a round does not count: let go and press again.")]
        [SerializeField] private Key _abortKey = Key.Space;

        [Tooltip("Seconds the abort key must be held before the round is aborted. Long enough that a tap on the start key never aborts a round.")]
        [Min(0f)]
        [SerializeField] private float _abortHoldSeconds = 1.5f;

        // True while a press that began during a round is still held, and for how long.
        private bool _holdArmed;
        private float _heldSeconds;

        // The round's state at the end of the previous frame. A press is judged by this and not by
        // the state now, because the start source may already have started the round this frame,
        // and components update in no fixed order.
        private RoundState _previousState;

        private void OnEnable()
        {
            _holdArmed = false;
            _heldSeconds = 0f;

            if (_round == null)
            {
                Debug.LogWarning("OperatorControls has no RoundController, so the abort key will do nothing.", this);
            }
            else
            {
                _previousState = _round.State;
            }

            if (_overlay == null)
            {
                Debug.LogWarning("OperatorControls has no OperatorOverlay, so the Shift key will do nothing.", this);
            }
        }

        private void OnDisable()
        {
            _holdArmed = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // The Shift on either side of the keyboard.
                if (_overlay != null && keyboard.shiftKey.wasPressedThisFrame)
                {
                    _overlay.Toggle();
                }

                UpdateAbortHold(keyboard);
            }

            if (_round != null)
            {
                _previousState = _round.State;
            }
        }

        private void UpdateAbortHold(Keyboard keyboard)
        {
            if (_round == null || _abortKey == Key.None)
            {
                return;
            }

            KeyControl key = keyboard[_abortKey];

            if (key.wasPressedThisFrame)
            {
                // Only a press made while a round was already running can become an abort.
                _holdArmed = _previousState == RoundState.Dusk || _previousState == RoundState.Night;
                _heldSeconds = 0f;
            }

            if (!_holdArmed)
            {
                return;
            }

            if (!key.isPressed)
            {
                _holdArmed = false;
                return;
            }

            // Unscaled, so the hold works even if the game is paused with a zero time scale.
            _heldSeconds += Time.unscaledDeltaTime;
            if (_heldSeconds >= _abortHoldSeconds)
            {
                _holdArmed = false;
                _round.Abort();
            }
        }
    }
}
