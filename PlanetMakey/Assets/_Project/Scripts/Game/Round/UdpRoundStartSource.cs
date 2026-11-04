using PlanetMakey.Hits;
using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// The booth's physical start button: a round is asked for when the hardware sends a
    /// <c>START</c> message (see PROTOCOL.md) to the <see cref="UdpHitSource"/>.
    /// </summary>
    public sealed class UdpRoundStartSource : RoundStartSource
    {
        [Tooltip("The UDP source that receives the hardware's messages. Its port is the one the start button sends to.")]
        [SerializeField] private UdpHitSource _udp;

        private void OnEnable()
        {
            if (_udp == null)
            {
                Debug.LogWarning("UdpRoundStartSource has no UdpHitSource, so a START message can never start a round.", this);
                return;
            }

            _udp.StartReceived += OnStartReceived;
        }

        private void OnDisable()
        {
            if (_udp != null)
            {
                _udp.StartReceived -= OnStartReceived;
            }
        }

        private void OnStartReceived()
        {
            RaiseStartRequested();
        }
    }
}
