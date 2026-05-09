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

        private ushort _sequenceId;
        private int _countDuplicateAckReceived;

        public long LastActivity { get; private set; }
        

        public Segment? LastSegmentReceived { get; private set; }
        public TcpSendBuffer SendBuffer { get; }
        
        

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
            SendBuffer = new();
            Timeout = timeout;
            _sequenceId = TcpConnection.RandomSequenceNumber(0);
            _countDuplicateAckReceived = 0;
        }

        public void UpdateConnectionInfo(State newState, Segment? lastSegmentReceived, Segment? lastSegmentSent)
        {
            if (lastSegmentReceived == null && lastSegmentSent == null)
                throw new ArgumentException("At least one of lastSegmentReceived or lastSegmentSent must be non-null.");

            lock (_sync)
            {
                LastSegmentReceived = lastSegmentReceived != null ? lastSegmentReceived : LastSegmentReceived;
                CurrentState = newState;
                LastActivity = DateTime.UtcNow.Ticks;

                Logger.Info($"Update Connection state : {newState}");
                if (lastSegmentSent != null)
                {
                    Logger.Debug($"Adding segment with sequence number {lastSegmentSent.SequenceNumber} and flag {lastSegmentSent.Flags.Value} to retransmission map.");
                    using var _ = SendBuffer.AddSegmentToSendAsync(lastSegmentSent);
                } 
                if (lastSegmentReceived != null && !_mapOrderedSegmentReceived.ContainsKey(lastSegmentReceived.SequenceNumber))
                {
                    Logger.Debug($"Received segment with sequence number {lastSegmentReceived.SequenceNumber} and flag {lastSegmentReceived.Flags.Value}.");
                    _mapOrderedSegmentReceived.Add(lastSegmentReceived.SequenceNumber, lastSegmentReceived);
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
                return sequenceNumber == SendBuffer.LastSegmentSent.SequenceNumber;
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
    }
}
