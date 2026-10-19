using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// Receives hits from the booth hardware as UDP text messages (see PROTOCOL.md). Packets are
    /// read on the main thread in <c>Update</c>, so there are no threads to shut down and a hit
    /// is raised within one frame of arriving.
    /// </summary>
    public sealed class UdpHitSource : HitSource
    {
        // Bounds the work done in one frame if something floods the port.
        private const int MaxPacketsPerFrame = 32;

        // The last message is only kept for display, so a huge packet cannot bloat it.
        private const int MaxStoredMessageLength = 80;

        private const int MaxPort = 65535;

        [Tooltip("UDP port to listen on. It is read when the component is enabled, and the hardware must send to it.")]
        [SerializeField] private int _port = 9000;

        [NonSerialized] private UdpClient _client;
        [NonSerialized] private IPEndPoint _remote;

        /// <summary>The port this source listens on.</summary>
        public int Port => _port;

        /// <summary>False if the port could not be opened or the socket failed.</summary>
        public bool IsListening => _client != null;

        /// <summary>The text of the latest packet, accepted or not. Null before the first one.</summary>
        public string LastMessage { get; private set; }

        /// <summary>True if the latest packet was not a valid hit.</summary>
        public bool LastMessageRejected { get; private set; }

        /// <summary>How many packets were not valid hits since this component was enabled.</summary>
        public int RejectedCount { get; private set; }

        private void OnEnable()
        {
            LastMessage = null;
            LastMessageRejected = false;
            RejectedCount = 0;

            StartListening();
        }

        private void OnDisable()
        {
            StopListening();
        }

        private void Update()
        {
            for (int i = 0; i < MaxPacketsPerFrame; i++)
            {
                // A subscriber may disable this component while handling a hit.
                if (_client == null)
                {
                    return;
                }

                if (!TryReceive(out string message))
                {
                    return;
                }

                Handle(message);
            }
        }

        private void StartListening()
        {
            if (_port < 1 || _port > MaxPort)
            {
                Debug.LogError("UdpHitSource port " + _port + " is not valid. Use 1 to " + MaxPort + ".", this);
                return;
            }

            try
            {
                _client = new UdpClient(_port);
                _remote = new IPEndPoint(IPAddress.Any, 0);
            }
            catch (SocketException exception)
            {
                // Usually the port is already taken, for example by a second copy of the game.
                Debug.LogError("UdpHitSource could not listen on port " + _port + ": " + exception.Message, this);
            }
        }

        private void StopListening()
        {
            if (_client != null)
            {
                _client.Close();
            }

            _client = null;
            _remote = null;
        }

        private bool TryReceive(out string message)
        {
            message = null;

            try
            {
                // Poll and not Available: a zero-length packet reports 0 bytes available, which
                // would leave it at the head of the queue and block every packet behind it.
                if (!_client.Client.Poll(0, SelectMode.SelectRead))
                {
                    return false;
                }

                byte[] data = _client.Receive(ref _remote);
                message = Encoding.ASCII.GetString(data);
                return true;
            }
            catch (SocketException exception)
            {
                Debug.LogError("UdpHitSource stopped listening after a socket error: " + exception.Message, this);
                StopListening();
                return false;
            }
        }

        private void Handle(string message)
        {
            LastMessage = ToDisplayText(message);

            if (HitMessageParser.TryParseHit(message, out BallHit hit))
            {
                LastMessageRejected = false;
                RaiseHit(hit);
                return;
            }

            LastMessageRejected = true;
            RejectedCount++;
        }

        private static string ToDisplayText(string message)
        {
            string text = message.Trim().Replace('\r', ' ').Replace('\n', ' ');
            if (text.Length > MaxStoredMessageLength)
            {
                text = text.Substring(0, MaxStoredMessageLength) + "...";
            }

            return text;
        }
    }
}
