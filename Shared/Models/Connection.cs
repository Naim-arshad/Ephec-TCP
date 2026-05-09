using System.Net;
using System;
using Shared.Logging;
using Shared.Protocol.TCP;


namespace Shared.Models
{
    public class Connection
    {
        public readonly IPEndPoint Endpoint;

        private readonly TimeSpan Timeout;
        private readonly Lock _sync = new();
        private readonly Dictionary<ushort, Segment> _mapSegmentToRetransmit;
        private readonly SortedDictionary<ushort, Segment> _mapOrderedSegmentReceived;

        private ushort _sequenceId;
        private int _countDuplicateAckReceived;

        public bool ActiveRetransmission { get; private set; }
        public long LastActivity { get; private set; }
        

        public Segment? LastSegmentReceived { get; private set; }
        public Segment? LastSegmentSent { get; private set; }
        
        

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

        public Connection(IPEndPoint endpoint, TimeSpan timeout)
        {
            Endpoint = endpoint;
            CurrentState = State.Closed;
            _mapSegmentToRetransmit = [];
            _mapOrderedSegmentReceived = [];
            Timeout = timeout;
            _sequenceId = TcpConnection.RandomSequenceNumber(0);
            _countDuplicateAckReceived = 0;
            ActiveRetransmission = false;
        }

        public void UpdateConnectionInfo(State newState, Segment? lastSegmentReceived, Segment? lastSegmentSent, bool toBeAck)
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
                if (lastSegmentSent != null && toBeAck)
                {
                    Logger.Debug($"Adding segment with sequence number {lastSegmentSent.SequenceNumber} and flag {lastSegmentSent.Flags.Value} to retransmission map.");
                    _mapSegmentToRetransmit.Add(lastSegmentSent.SequenceNumber, lastSegmentSent);
                } 
                if (lastSegmentReceived != null && !_mapOrderedSegmentReceived.ContainsKey(lastSegmentReceived.SequenceNumber))
                {
                    Logger.Debug($"Received segment with sequence number {lastSegmentReceived.SequenceNumber} and flag {lastSegmentReceived.Flags.Value}.");
                    _mapOrderedSegmentReceived.Add(lastSegmentReceived.SequenceNumber, lastSegmentReceived);
                }
            }
        }
        public void RemoveSegmentToRetransmit(Segment segment)
        {
            lock (_sync)
            {
                if(segment.SequenceNumber == (LastSegmentReceived?.SequenceNumber+1))
                {
                    if (_mapSegmentToRetransmit.TryGetValue(segment.GetDataToSequence(), out var segmentSaved))
                    {
                        Logger.Debug($"Acknowledging segment with sequence number {segment.GetDataToSequence()}.");
                        _mapSegmentToRetransmit.Remove(segment.GetDataToSequence());
                    } else if(LastSegmentReceived != null && segment.GetDataToSequence() == LastSegmentReceived.GetDataToSequence())
                    {
                        Logger.Warning($"Received ACK for segment with sequence number {segment.GetDataToSequence()} which is not in the retransmission map but matches the last segment sent.");
                        _countDuplicateAckReceived++;

                        if(_countDuplicateAckReceived >= 3)
                        {
                            Logger.Warning($"Duplicate ACK received {_countDuplicateAckReceived} times for segment with sequence number {segment.GetDataToSequence()}. Activating retransmission.");
                            ActiveRetransmission = true;
                        }
                    }
                } else
                {
                    Logger.Warning($"Received ACK for segment with sequence number {segment.GetDataToSequence()} which does not match the expected sequence number {LastSegmentReceived?.SequenceNumber + 1}.");
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
                ushort currentSequence = _sequenceId;
                _sequenceId++;
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
