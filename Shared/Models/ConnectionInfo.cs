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
        public long LastSentTimes { get; private set; }
        public int RetransmissionCount { get; private set; } = 0;

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

        public void BeginConnection(Segment segmentSent)
        {
            State = ConnectionState.InitiatedConnection;
            LastSegmentSent = segmentSent;
            LastSentTimes = DateTime.UtcNow.Ticks;
        }
        public void UpdateConnectionInfo(ConnectionState newState, Segment lastSegmentReceived, Segment lastSegmentSent)
        {
            if(lastSegmentReceived == null && lastSegmentSent == null)
                throw new ArgumentException("At least one of lastSegmentReceived or lastSegmentSent must be non-null.");
            
            LastSegmentReceived = lastSegmentReceived;
            LastSegmentSent = lastSegmentSent;
            State = newState;
            LastSentTimes = DateTime.UtcNow.Ticks;
        }

        public void ResetConnection(Segment segmentSent)
        {
            State = ConnectionState.ResetConnection;
            LastSegmentSent = segmentSent;
            LastSentTimes = DateTime.UtcNow.Ticks;
        }
        public void CloseConnection(Segment segmentSent)
        {
            State = ConnectionState.ClosingConnection;
            LastSegmentSent = segmentSent;
            LastSentTimes = DateTime.UtcNow.Ticks;
        }

        public void IncrementRetransmissionCount()
        {
            RetransmissionCount++;
            LastSentTimes = DateTime.UtcNow.Ticks;
        }
    }
}
