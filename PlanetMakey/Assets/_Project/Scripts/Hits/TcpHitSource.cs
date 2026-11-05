using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Hits
{
    /// <summary>
    /// Receives messages from the booth hardware over TCP (see PROTOCOL.md). The game listens and
    /// the hardware connects, one connection per device or all through one. Each message is a line
    /// of text. Everything is read on the main thread in <c>Update</c> without blocking, so there
    /// are no threads to shut down and a hit is raised within one frame of arriving.
    /// </summary>
    public sealed class TcpHitSource : HitSource
    {
        // Bounds the work done in one frame if something floods the port.
        private const int MaxBytesPerConnectionPerFrame = 4096;
        private const int MaxAcceptsPerFrame = 16;
        private const int MaxConnections = 8;

        // The longest valid message is a few dozen bytes. A longer line is junk and is thrown away.
        private const int MaxLineBytes = 256;

        // The last message is only kept for display, so a long line cannot bloat it.
        private const int MaxStoredMessageLength = 80;

        private const int MaxPort = 65535;

        /// <summary>One connected device, with the line it has sent so far.</summary>
        private sealed class Connection
        {
            public Socket Socket;
            public readonly byte[] Line = new byte[MaxLineBytes];
            public int Length;

            // True from a line that grew too long until its line end, so the rest is skipped.
            public bool Discarding;
        }

        [Tooltip("TCP port to listen on. It is read when the component is enabled, and the hardware must connect to it.")]
        [SerializeField] private int _port = 9000;

        [Tooltip("The cannon. A LOAD message loads it, and a HIT with no color is reported with its color. Without it only HIT messages that carry a color are used.")]
        [SerializeField] private Cannon _cannon;

        [NonSerialized] private TcpListener _listener;
        [NonSerialized] private List<Connection> _connections;
        [NonSerialized] private byte[] _chunk;

        [NonSerialized] private float _lastMessageTime;

        /// <summary>Raised on the main thread each time a valid <c>START</c> message arrives.</summary>
        public event Action StartReceived;

        /// <summary>The port this source listens on.</summary>
        public int Port => _port;

        /// <summary>True once any message, valid or not, has arrived since this component was enabled.</summary>
        public bool HasReceived { get; private set; }

        /// <summary>
        /// Seconds since the latest message, valid or not, in unscaled time. -1 before the first one.
        /// It tells the operator whether the hardware is still talking.
        /// </summary>
        public float SecondsSinceLastMessage => HasReceived ? Time.unscaledTime - _lastMessageTime : -1f;

        /// <summary>False if the port could not be opened or the socket failed.</summary>
        public bool IsListening => _listener != null;

        /// <summary>How many devices are connected right now.</summary>
        public int ClientCount => _connections != null ? _connections.Count : 0;

        /// <summary>The text of the latest message, accepted or not. Null before the first one.</summary>
        public string LastMessage { get; private set; }

        /// <summary>True if the latest message was neither a valid hit, a valid LOAD nor a valid START.</summary>
        public bool LastMessageRejected { get; private set; }

        /// <summary>How many messages were not understood since this component was enabled.</summary>
        public int RejectedCount { get; private set; }

        /// <summary>
        /// How many hits with no color were dropped because the cannon had no ball loaded (or none is
        /// wired) since this component was enabled.
        /// </summary>
        public int NoBallCount { get; private set; }

        private void OnEnable()
        {
            LastMessage = null;
            LastMessageRejected = false;
            RejectedCount = 0;
            NoBallCount = 0;
            HasReceived = false;

            if (_connections == null)
            {
                _connections = new List<Connection>(MaxConnections);
            }

            if (_chunk == null)
            {
                _chunk = new byte[MaxLineBytes * 4];
            }

            if (_cannon == null)
            {
                Debug.LogWarning(
                    "TcpHitSource has no Cannon, so LOAD messages do nothing and a HIT with no color is dropped.",
                    this);
            }

            StartListening();
        }

        private void OnDisable()
        {
            StopListening();
        }

        private void Update()
        {
            if (_listener == null)
            {
                return;
            }

            AcceptPending();

            // Backwards, so a connection that closes can be removed while looping.
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                // A subscriber may disable this component while handling a message.
                if (_listener == null)
                {
                    return;
                }

                if (!ReadFrom(_connections[i]))
                {
                    if (_listener == null)
                    {
                        return;
                    }

                    CloseAt(i);
                }
            }
        }

        private void StartListening()
        {
            if (_port < 1 || _port > MaxPort)
            {
                Debug.LogError("TcpHitSource port " + _port + " is not valid. Use 1 to " + MaxPort + ".", this);
                return;
            }

            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, _port);
                listener.Start();
                _listener = listener;
            }
            catch (SocketException exception)
            {
                // Usually the port is already taken, for example by a second copy of the game.
                if (listener != null)
                {
                    listener.Stop();
                }

                Debug.LogError("TcpHitSource could not listen on port " + _port + ": " + exception.Message, this);
            }
        }

        private void StopListening()
        {
            if (_connections != null)
            {
                for (int i = 0; i < _connections.Count; i++)
                {
                    _connections[i].Socket.Close();
                }

                _connections.Clear();
            }

            if (_listener != null)
            {
                _listener.Stop();
            }

            _listener = null;
        }

        private void AcceptPending()
        {
            for (int i = 0; i < MaxAcceptsPerFrame; i++)
            {
                // Only an error from the listener itself ends listening for everyone.
                bool pending;
                try
                {
                    pending = _listener.Pending();
                }
                catch (SocketException exception)
                {
                    Debug.LogError("TcpHitSource stopped listening after a socket error: " + exception.Message, this);
                    StopListening();
                    return;
                }

                if (!pending)
                {
                    return;
                }

                Socket socket = null;
                try
                {
                    socket = _listener.AcceptSocket();

                    // Messages are tiny. Sending them at once beats waiting to fill a packet.
                    socket.NoDelay = true;

                    // Lets the system notice, in the end, a device that vanished without closing.
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                }
                catch (SocketException)
                {
                    // This one device went away before it was accepted. Nothing else is wrong.
                    if (socket != null)
                    {
                        socket.Close();
                    }

                    continue;
                }

                // A device that lost power or had its cable pulled sends no close, so its dead
                // connection would hold a place until the game stops. When the list is full the
                // oldest goes, so a device that reconnects can always get in.
                if (_connections.Count >= MaxConnections)
                {
                    CloseAt(0);
                }

                Connection connection = new Connection();
                connection.Socket = socket;
                _connections.Add(connection);
            }
        }

        // Returns false when the connection is finished (closed or failed) or this component was
        // stopped by a subscriber, so the caller can drop it.
        private bool ReadFrom(Connection connection)
        {
            try
            {
                Socket socket = connection.Socket;
                int budget = MaxBytesPerConnectionPerFrame;

                while (budget > 0 && socket.Poll(0, SelectMode.SelectRead))
                {
                    // Readable with nothing to read means the other side closed the connection.
                    int available = socket.Available;
                    if (available == 0)
                    {
                        return false;
                    }

                    int size = Math.Min(Math.Min(_chunk.Length, available), budget);
                    int count = socket.Receive(_chunk, 0, size, SocketFlags.None);
                    if (count == 0)
                    {
                        return false;
                    }

                    budget -= count;
                    if (!ProcessBytes(connection, count))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (SocketException)
            {
                // The device went away without closing properly (cable pulled, program killed).
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        // Splits the bytes just received into lines. Returns false if this component was stopped
        // while a message was being handled.
        private bool ProcessBytes(Connection connection, int count)
        {
            for (int i = 0; i < count; i++)
            {
                byte value = _chunk[i];

                if (value == (byte)'\n')
                {
                    int length = connection.Length;
                    bool wasDiscarding = connection.Discarding;
                    connection.Length = 0;
                    connection.Discarding = false;

                    if (!wasDiscarding && length > 0)
                    {
                        // A subscriber that throws must not lose the rest of what was received.
                        try
                        {
                            Handle(Encoding.ASCII.GetString(connection.Line, 0, length));
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception, this);
                        }

                        if (_listener == null)
                        {
                            return false;
                        }
                    }

                    continue;
                }

                if (connection.Discarding)
                {
                    continue;
                }

                if (connection.Length >= MaxLineBytes)
                {
                    // Counted once, when it starts, and the rest of the line is skipped.
                    connection.Discarding = true;
                    connection.Length = 0;
                    Reject("(line too long)");
                    continue;
                }

                connection.Line[connection.Length] = value;
                connection.Length++;
            }

            return true;
        }

        private void CloseAt(int index)
        {
            // The list can have been emptied if a subscriber disabled and re-enabled this component.
            if (index < 0 || index >= _connections.Count)
            {
                return;
            }

            _connections[index].Socket.Close();
            _connections.RemoveAt(index);
        }

        private void Handle(string message)
        {
            // A blank line carries no message, so it is neither accepted nor rejected.
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            LastMessage = ToDisplayText(message);
            HasReceived = true;
            _lastMessageTime = Time.unscaledTime;

            // A hit that carries its own color is used as it is.
            if (HitMessageParser.TryParseHit(message, out BallHit hit))
            {
                LastMessageRejected = false;
                RaiseHit(hit);
                return;
            }

            // A hit with no color takes the color of the ball in the cannon.
            if (HitMessageParser.TryParsePosition(message, out Vector2 position))
            {
                LastMessageRejected = false;
                if (_cannon != null && _cannon.HasBall)
                {
                    RaiseHit(new BallHit(position, _cannon.LoadedColor));
                }
                else
                {
                    NoBallCount++;
                }

                return;
            }

            if (HitMessageParser.TryParseLoad(message, out BallColor loaded))
            {
                LastMessageRejected = false;
                if (_cannon != null)
                {
                    _cannon.Load(loaded);
                }

                return;
            }

            if (HitMessageParser.IsStart(message))
            {
                LastMessageRejected = false;
                StartReceived?.Invoke();
                return;
            }

            LastMessageRejected = true;
            RejectedCount++;
        }

        private void Reject(string text)
        {
            LastMessage = text;
            HasReceived = true;
            _lastMessageTime = Time.unscaledTime;
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
