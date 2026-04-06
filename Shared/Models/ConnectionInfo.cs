using System.Net;
using System;
using Shared.Logging;
using Shared.Protocol.TCP;


namespace Shared.Models
{
    public class ConnectionInfo
    {
        public readonly IPEndPoint Endpoint;

        private readonly Lock _sync = new();

        public Segment? LastSegmentReceived { get; private set; }
        public Segment? LastSegmentSent { get; private set; }
        public long LastActivity {  get; private set; }
        private ushort _sequencerId;
        public TimeSpan Timeout { get; private set; }
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

        public ConnectionInfo(IPEndPoint endpoint, TimeSpan timout)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            Endpoint = endpoint;
            CurrentState = State.Closed;
            _mapSegmentToRetransmit = [];
            Timeout = timout;
            _sequencerId = TcpConnection.RandomSequenceNumber(0);
        }
        public void BeginConnection(Segment segmentSent)
        {
            ArgumentNullException.ThrowIfNull(segmentSent);

            lock (_sync)
            {
                UpdateConnectionInfo(State.SynSent, null, segmentSent);
            }
        }
        public void UpdateConnectionInfo(State newState, Segment? lastSegmentReceived, Segment? lastSegmentSent)
        {
            if (lastSegmentReceived == null && lastSegmentSent == null)
                throw new ArgumentException("At least one of lastSegmentReceived or lastSegmentSent must be non-null.");

            lock (_sync)
            {
                LastSegmentReceived = lastSegmentReceived != null ? lastSegmentReceived : LastSegmentReceived;
                LastSegmentSent = lastSegmentSent != null ? lastSegmentSent : LastSegmentSent;
                CurrentState = newState;
                LastActivity = DateTime.UtcNow.Ticks;

                Logger.Info($"Update Connection state : {newState}");
                if (lastSegmentSent != null)
                {
                    Logger.Debug($"Adding segment with sequence number {lastSegmentSent.SequenceNumber} and flag {lastSegmentSent.Flags.Value} to retransmission map.");
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
        public bool IsExpired()
        {
            lock (_sync)
            {
                long currentTicks = DateTime.UtcNow.Ticks;
                return (currentTicks - LastActivity) > Timeout.Ticks;
                
            }
        }
        public bool CheckSequenceNumber(ushort sequenceNumber)
        {
            lock (_sync)
            {
                if(LastSegmentSent == null)
                    throw new ArgumentNullException(nameof(LastSegmentSent));
                return sequenceNumber == LastSegmentSent.SequenceNumber;
            }
        }
        public ushort GetNextSequenceNumber()
        {
            lock (_sync)
            {
                ushort currentSequence = _sequencerId;
                _sequencerId++;
                return currentSequence;
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
