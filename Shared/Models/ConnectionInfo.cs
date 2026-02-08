using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Models
{
    public class ConnectionInfo
    {
        public readonly IPEndPoint Endpoint;
        public const int MaxRetransmissions = 5;

        public Segment? LastSegmentReceived { get; private set; }
        public Segment? LastSegmentSent { get; private set; }
        public long LastSentTimes { get; private set; }
        public int RetransmissionCount { get; private set; }

        public ConnectionState State { get; private set; }
        public enum ConnectionState
        {
            Disconnected,
            Connected,
            InitConnection,
            ClosingConnection,
            FailedConnection,
            ResetConnection,
        }

        public ConnectionInfo(IPEndPoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            Endpoint = endpoint;
            State = ConnectionState.Disconnected;
            RetransmissionCount = 0;
        }
        public void BeginConnection(Segment segmentSent)
        {
            ArgumentNullException.ThrowIfNull(segmentSent);
            State = ConnectionState.InitConnection;
            LastSegmentSent = segmentSent;
            LastSentTimes = DateTime.UtcNow.Ticks;
        }
        public void UpdateConnectionInfo(ConnectionState newState, Segment? lastSegmentReceived, Segment? lastSegmentSent)
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
            ArgumentNullException.ThrowIfNull(segmentSent);

            State = ConnectionState.ResetConnection;
            LastSegmentSent = segmentSent;
            LastSentTimes = DateTime.UtcNow.Ticks;
        }
        public void CloseConnection(Segment segmentSent)
        {
            ArgumentNullException.ThrowIfNull(segmentSent);

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
