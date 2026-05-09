using Shared.Models.Segments;
using Shared.Protocol.Serialization;
using Shared.Logging;
using Shared.Models;

namespace Shared.Protocol.TCP.Buffers
{
    public class TcpSendBuffer
    {
        private readonly Lock _sync = new();
        private readonly Dictionary<ushort, Segment> _segmentsToSend;
        private readonly HashSet<ushort> _duplicateAckPacketSequences;

        public int DuplicateAckCount { get; private set; }
        public IReadOnlyDictionary<ushort, Segment> SegmentsToSend
        {
            get
            {
                lock (_sync)
                {
                    return new Dictionary<ushort, Segment>(_segmentsToSend);
                }
            }
        }

        public TcpSendBuffer()
        {
            _segmentsToSend = [];
            _duplicateAckPacketSequences = [];
            DuplicateAckCount = 0;
        }

        public void AddSegmentToSend(Segment segment)
        {
            lock (_sync)
            {
                if (_segmentsToSend.ContainsKey(segment.SequenceNumber))
                {
                    Logger.Warning($"Segment with sequence number {segment.SequenceNumber} already exists in the send buffer.");
                }
                else
                {
                    _segmentsToSend[segment.SequenceNumber] = segment;
                }
            }
        }

        public bool Acknowledge(Segment segment, Connection connection)
        {
            ushort acknowledgedSequenceNumber = SegmentPayloadReader.ReadAckSequence(segment);
            bool acknowledged = false;
            bool duplicateAckThresholdReached = false;

            lock (_sync)
            {
                if (_segmentsToSend.ContainsKey(acknowledgedSequenceNumber))
                {
                    Logger.Info($"Acknowledging segment with sequence number {acknowledgedSequenceNumber}");
                    _segmentsToSend.Remove(acknowledgedSequenceNumber);
                    ClearDuplicateAcksUnsafe();
                    acknowledged = true;
                }
                else if(connection.LastAcknowledgedSequenceId == acknowledgedSequenceNumber)
                {
                    if (!_duplicateAckPacketSequences.Add(segment.SequenceNumber))
                    {
                        Logger.Warning($"Repeated duplicate ACK packet ignored for segment {segment.SequenceNumber}");
                        return false;
                    }

                    Logger.Warning($"Acknowledge duplicate detected for segment {segment.SequenceNumber}");
                    AddDuplicateAckUnsafe();
                    duplicateAckThresholdReached = DuplicateAckCount >= 3;
                    if(duplicateAckThresholdReached)
                    {
                        Logger.Info($"Triple duplicate ACK detected for segment {segment.SequenceNumber}. Marking for retransmission.");
                        ClearDuplicateAcksUnsafe();
                    }
                }
                else
                {
                    ClearDuplicateAcksUnsafe();
                }
            }

            if (acknowledged)
            {
                connection.SetLastAcknowledgedSequenceId(acknowledgedSequenceNumber);
                connection.DeactivateRetransmission();
                connection.ResetRetransmissionCount();
                return true;
            }

            if (duplicateAckThresholdReached)
                connection.ActivateRetransmission();

            return false;
        }

        public List<Segment> GetSegmentsToRetransmit()
        {
            lock (_sync)
            {
                return [.. _segmentsToSend.Values];
            }
        }

        public void ClearDuplicateAcks()
        {
            lock (_sync)
            {
                ClearDuplicateAcksUnsafe();
            }
        }

        private void ClearDuplicateAcksUnsafe()
        {
            DuplicateAckCount = 0;
            _duplicateAckPacketSequences.Clear();
        }

        public void AddDuplicateAck()
        {
            lock (_sync)
            {
                AddDuplicateAckUnsafe();
            }
        }

        private void AddDuplicateAckUnsafe()
        {
            DuplicateAckCount++;
        }
    }
}
