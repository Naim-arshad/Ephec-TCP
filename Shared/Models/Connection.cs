using Shared.Logging;
using Shared.Models.Segments;
using Shared.Protocol.FileTransfer;
using Shared.Protocol.Serialization;
using Shared.Protocol.TCP.Buffers;
using System.Net;

namespace Shared.Models
{
    public class Connection
    {
        private const string LogSource = "tcp.connection-state";

        private readonly Lock _sync = new();
        private readonly TimeSpan _timeout;
        private readonly FileAssembler? _fileAssembler;

        private ushort _sequenceId;

        public IPEndPoint Endpoint { get; }
        public long LastActivity { get; private set; }
        public ushort LastAcknowledgedSequenceId {  get; private set; }
        public bool ActiveRetransmission { get; private set; }
        public int RetransmissionCount { get; private set; }
        public ushort? LastFastRetransmittedSequenceId { get; private set; }
        public Segment LastSegmentSent { get; private set; }
        public Segment LastSegmentReceived { get; private set; }
        public TcpSendBuffer SendBuffer { get; }
        public TcpReceiveBuffer ReceiveBuffer { get; }
        public string? ReceivedFilePath => _fileAssembler?.OutputPath;
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

        public Connection(IPEndPoint endpoint, TimeSpan timeout, string? receivedFilePath = null)
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
            _fileAssembler = receivedFilePath is null ? null : new FileAssembler(receivedFilePath);
            _timeout = timeout;
            _sequenceId = (ushort)Random.Shared.Next(ushort.MaxValue + 1);
            LastActivity = DateTime.UtcNow.Ticks;
            ActiveRetransmission = false;
            LastFastRetransmittedSequenceId = null;
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
                State previousState = CurrentState;
                CurrentState = newState;
                LastActivity = DateTime.UtcNow.Ticks;

                if (lastSegmentReceived is not null)
                {
                    LastSegmentReceived = lastSegmentReceived;
                    ReceiveBuffer.AddSegment(lastSegmentReceived);
                    Logger.Debug(
                        LogSource,
                        $"Segment stored in receive buffer. remote={Endpoint}, sequence={lastSegmentReceived.SequenceNumber}, flags={lastSegmentReceived.Flags}, payloadBytes={lastSegmentReceived.Data.Length}");
                }

                if (lastSegmentSent is not null)
                {
                    Logger.Debug(
                        LogSource,
                        $"Segment stored in send buffer. remote={Endpoint}, sequence={lastSegmentSent.SequenceNumber}, flags={lastSegmentSent.Flags}, payloadBytes={lastSegmentSent.Data.Length}");
                    SendBuffer.AddSegmentToSend(lastSegmentSent);
                    LastSegmentSent = lastSegmentSent;
                }

                if (previousState == newState)
                {
                    Logger.Debug(LogSource, $"Connection state refreshed. remote={Endpoint}, state={newState}");
                }
                else
                {
                    Logger.Info(LogSource, $"Connection state changed. remote={Endpoint}, from={previousState}, to={newState}");
                }
            }
        }

        public List<Segment> GetSegmentsToRetransmit()
        {
            return GetSegmentsToRetransmit(1);
        }

        public List<Segment> GetSegmentsToRetransmit(int maxSegments)
        {
            lock (_sync)
            {
                return SendBuffer.GetSegmentsToRetransmit(LastAcknowledgedSequenceId, maxSegments);
            }
        }

        public IReadOnlyList<Segment> FlushAcknowledgedSegmentsToFile()
        {
            lock (_sync)
            {
                IReadOnlyList<Segment> acknowledgedSegments = ReceiveBuffer.RemoveAcknowledgedSegments();
                _fileAssembler?.AppendDataSegments(acknowledgedSegments);
                if (acknowledgedSegments.Count > 0)
                {
                    Logger.Debug(
                        LogSource,
                        $"Acknowledged receive buffer flushed. remote={Endpoint}, segments={acknowledgedSegments.Count}, outputFile={ReceivedFilePath ?? "disabled"}");
                }
                return acknowledgedSegments;
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
                if(_sequenceId++ > ushort.MaxValue)
                {
                    _sequenceId = 0;
                }
                return currentSequence;
            }
        }

        public void ActivateRetransmission()
        {
            lock (_sync)
            {
                ActiveRetransmission = true;
                Logger.Warning(LogSource, $"Retransmission activated. remote={Endpoint}, lastAcknowledgedSequence={LastAcknowledgedSequenceId}");
            }
        }

        public void DeactivateRetransmission()
        {
            lock (_sync)
            {
                if (ActiveRetransmission)
                {
                    Logger.Info(LogSource, $"Retransmission deactivated. remote={Endpoint}, lastAcknowledgedSequence={LastAcknowledgedSequenceId}");
                }
                ActiveRetransmission = false;
                LastFastRetransmittedSequenceId = null;
                SendBuffer.ClearDuplicateAcks();
            }
        }

        public void SetLastAcknowledgedSequenceId(ushort sequenceId)
        {
            lock (_sync)
            {
                Logger.Debug(LogSource, $"Last acknowledged sequence updated. remote={Endpoint}, from={LastAcknowledgedSequenceId}, to={sequenceId}");
                LastAcknowledgedSequenceId = sequenceId;
                LastFastRetransmittedSequenceId = null;
            }
        }

        public bool TryMarkFastRetransmission(ushort sequenceId)
        {
            lock (_sync)
            {
                if (LastFastRetransmittedSequenceId == sequenceId)
                {
                    return false;
                }

                LastFastRetransmittedSequenceId = sequenceId;
                RetransmissionCount++;
                return true;
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
