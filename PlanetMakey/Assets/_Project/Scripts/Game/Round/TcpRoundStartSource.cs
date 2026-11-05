using PlanetMakey.Hits;
using UnityEngine;
using UnityEngine.Serialization;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The booth's physical start button: a round is asked for when the hardware sends a
    /// <c>START</c> message (see PROTOCOL.md) to the <see cref="TcpHitSource"/>.
    /// </summary>
    public sealed class TcpRoundStartSource : RoundStartSource
    {
        [Tooltip("The TCP source that receives the hardware's messages. Its port is the one the start button connects to.")]
        [FormerlySerializedAs("_udp")]
        [SerializeField] private TcpHitSource _hardware;

        private void OnEnable()
        {
            if (_hardware == null)
            {
                Debug.LogWarning("TcpRoundStartSource has no TcpHitSource, so a START message can never start a round.", this);
                return;
            }

            _hardware.StartReceived += OnStartReceived;
        }

        private void OnDisable()
        {
            if (_hardware != null)
            {
                _hardware.StartReceived -= OnStartReceived;
            }
        }

        private void OnStartReceived()
        {
            RaiseStartRequested();
        }
    }
}
