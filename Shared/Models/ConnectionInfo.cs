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

        private readonly Lock _sync = new();

        public Segment? LastSegmentReceived { get; private set; }
        public Segment? LastSegmentSent { get; private set; }
        public long LastSentTimes { get; private set; }

        private readonly Dictionary<ushort, Segment> _mapSegmentToRetransmit;

        public State CurrentState { get; private set; }
        public enum State
        {
            Closed,
            Established,
            SynSent,
            SynReceived,
            FinWait1,
            FinWait2,
            CloseWait,
            Closing,
            TimeWait,
            TimeOut
        }

        public ConnectionInfo(IPEndPoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            Endpoint = endpoint;
            CurrentState = State.Closed;
            _mapSegmentToRetransmit = [];
        }
        public void BeginConnection(Segment segmentSent)
        {
            ArgumentNullException.ThrowIfNull(segmentSent);

            lock (_sync)
            {
                CurrentState = State.SynSent;
                LastSegmentSent = segmentSent;
                LastSentTimes = DateTime.UtcNow.Ticks;
            }
        }
        public void UpdateConnectionInfo(State newState, Segment? lastSegmentReceived, Segment? lastSegmentSent)
        {
            if (lastSegmentReceived == null && lastSegmentSent == null)
                throw new ArgumentException("At least one of lastSegmentReceived or lastSegmentSent must be non-null.");

            lock (_sync)
            {
                LastSegmentReceived = lastSegmentReceived;
                LastSegmentSent = lastSegmentSent;
                CurrentState = newState;
                LastSentTimes = DateTime.UtcNow.Ticks;

                if(lastSegmentSent != null)
                {
                    Console.WriteLine($"Adding segment with sequence number {lastSegmentSent.SequenceNumber} to retransmission map.");
                    _mapSegmentToRetransmit.Add(lastSegmentSent.SequenceNumber, lastSegmentSent);
                }
                    
            }
        }
        public void MarkSegmentAsAcknowledged(ushort sequenceNumber)
        {
            lock (_sync)
            {
                if (_mapSegmentToRetransmit.TryGetValue(sequenceNumber, out var segment))
                {
                    segment.AckReceived = true;
                    _mapSegmentToRetransmit.Remove(sequenceNumber);
                }
            }
        }
        public IEnumerable<Segment> GetSegmentsToRetransmit()
        {
            lock (_sync)
            {
                return [.. _mapSegmentToRetransmit.Values];
            }
        }
    }
}
