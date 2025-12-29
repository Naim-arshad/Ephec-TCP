using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Shared.Models
{
    internal class ConnectionInfo(IPEndPoint endpoint)
    {
        public readonly IPEndPoint Endpoint = endpoint;
        public const int MaxRetransmissions = 5;

        public Segment? LastSegmentReceived { get; private set; }
        public Segment? LastSegmentSent { get; private set; }
        public long LastSentTimes { get; set; }
        public int RetransmissionCount { get; set; } = 0;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public enum ConnectionState
        {
            Disconnected,
            Connected,
            InitiatedConnection,
            ClosingConnection,
            FailedConnection,
            ResetConnection,
        }

        public void UpdateConnectionInfo(ConnectionState newState, Segment lastSegmentReceived, Segment lastSegmentSent)
        {
            if (lastSegmentReceived != null || lastSegmentSent != null)
            {
                State = newState;
                LastSegmentReceived = lastSegmentReceived;
                LastSegmentSent = lastSegmentSent;
                LastSentTimes = DateTime.UtcNow.Ticks;
            }
            else
            {
                throw new ArgumentException("At least one of lastSegmentReceived or lastSegmentSent must be non-null.");
            }
        }

        public void IncrementedRetransmissionCount()
        {
            RetransmissionCount++;
            LastSentTimes = DateTime.UtcNow.Ticks;
        }
    }
}
