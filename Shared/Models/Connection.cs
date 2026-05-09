using Shared.Logging;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;
using Shared.Protocol.TCP.Buffers;
using System.Net;

namespace Shared.Models
{
    public class Connection
    {
        private readonly Lock _sync = new();
        private readonly TimeSpan _timeout;

        private ushort _sequenceId;

        public IPEndPoint Endpoint { get; }
        public long LastActivity { get; private set; }
        public ushort LastAcknowledgedSequenceId {  get; private set; }
        public bool ActiveRetransmission { get; private set; }
        public int RetransmissionCount { get; private set; }
        public Segment LastSegmentSent { get; private set; }
        public Segment LastSegmentReceived { get; private set; }
        public TcpSendBuffer SendBuffer { get; }
        public TcpReceiveBuffer ReceiveBuffer { get; }
        public State CurrentState { get; private set; }

        private const int MaxDuplicateAckCount = 3;
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
            ArgumentNullException.ThrowIfNull(endpoint);

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Le timeout doit etre strictement positif.");
            }

            Endpoint = endpoint;
            CurrentState = State.Closed;
            SendBuffer = new TcpSendBuffer();
            ReceiveBuffer = new TcpReceiveBuffer();
            _timeout = timeout;
            _sequenceId = (ushort)Random.Shared.Next(ushort.MaxValue + 1);
            LastActivity = DateTime.UtcNow.Ticks;
            ActiveRetransmission = false;
            LastSegmentSent = new Segment(0, new (SegmentFlags.FlagOptions.None), Array.Empty<byte>());
            LastSegmentReceived = new Segment(0, new (SegmentFlags.FlagOptions.None), Array.Empty<byte>());
            LastAcknowledgedSequenceId = 0;
            RetransmissionCount = 0;
        }

        public IReadOnlyList<Segment> OrderedSegmentsReceived
        {
            get
            {
                lock (_sync)
                {
                    return [.. ReceiveBuffer.MapSegmentsReceived.Values
                        .OrderBy(segment => segment.SequenceNumber)];
                }
            }
        }

        public void UpdateConnectionInfo(State newState, Segment? lastSegmentReceived = null, Segment? lastSegmentSent = null)
        {
            lock (_sync)
            {
                CurrentState = newState;
                LastActivity = DateTime.UtcNow.Ticks;

                if (lastSegmentReceived is not null)
                {
                    LastSegmentReceived = lastSegmentReceived;
                    ReceiveBuffer.AddSegment(lastSegmentReceived);
                }

                if (lastSegmentSent is not null)
                {
                    Logger.Debug(
                        $"Adding segment with sequence number {lastSegmentSent.SequenceNumber} and flag {lastSegmentSent.Flags.Value} to send buffer.");
                    SendBuffer.AddSegmentToSend(lastSegmentSent);
                    LastSegmentSent = lastSegmentSent;
                }

                Logger.Info($"Update Connection state : {newState}");
            }
        }

        public List<Segment> GetSegmentsToRetransmit()
        {
            lock (_sync)
            {
                return SendBuffer.GetSegmentsToRetransmit();
            }
        }

        public bool IsExpired()
        {
            lock (_sync)
            {
                long currentTicks = DateTime.UtcNow.Ticks;
                return currentTicks - LastActivity > _timeout.Ticks;
            }
        }

        public bool CheckSequenceNumber(ushort sequenceNumber)
        {
            lock (_sync)
            {
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

        public void ActivateRetransmission()
        {
            lock (_sync)
            {
                ActiveRetransmission = true;
            }
        }

        public void DeactivateRetransmission()
        {
            lock (_sync)
            {
                ActiveRetransmission = false;
                SendBuffer.ClearDuplicateAcks();
            }
        }

        public void SetLastAcknowledgedSequenceId(ushort sequenceId)
        {
            lock (_sync)
            {
                LastAcknowledgedSequenceId = sequenceId;
            }
        }

        public void IncrementRetransmissionCount()
        {
            lock (_sync)
            {
                RetransmissionCount++;
            }
        }

        public void ResetRetransmissionCount()
        {
            lock (_sync)
            {
                RetransmissionCount = 0;
            }
        }
    }
}
