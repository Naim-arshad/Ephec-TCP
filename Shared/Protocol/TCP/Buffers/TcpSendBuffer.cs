using Shared.Models.Segments;
using Shared.Protocol.Serialization;
using Shared.Logging;
using Shared.Models;

namespace Shared.Protocol.TCP.Buffers
{
    public class TcpSendBuffer
    {
        private const string LogSource = "tcp.send-buffer";

        private readonly Lock _sync = new();
        private readonly Dictionary<ushort, Segment> _segmentsToSend;
        private readonly HashSet<ushort> _duplicateAckPacketSequences;

        public int DuplicateAckCount { get; private set; }
        public int PendingSegmentCount
        {
            get
            {
                lock (_sync)
                {
                    return _segmentsToSend.Count;
                }
            }
        }

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
                    Logger.Warning(LogSource, $"Segment already exists in send buffer. sequence={segment.SequenceNumber}");
                }
                else
                {
                    _segmentsToSend[segment.SequenceNumber] = segment;
                    Logger.Debug(LogSource, $"Segment added to send buffer. sequence={segment.SequenceNumber}, pendingSegments={_segmentsToSend.Count}");
                }
            }
        }

        public bool Acknowledge(Segment segment, Connection connection)
        {
            ushort acknowledgedSequenceNumber = SegmentPayloadReader.ReadAckSequence(segment);
            bool acknowledged = false;
            bool duplicateAckThresholdReached = false;
            List<ushort> acknowledgedSegments = [];

            lock (_sync)
            {
                acknowledgedSegments = GetCumulativelyAcknowledgedSequencesUnsafe(
                    acknowledgedSequenceNumber,
                    connection.LastAcknowledgedSequenceId);

                if (acknowledgedSegments.Count > 0)
                {
                    Logger.Info(LogSource, $"Segments acknowledged. throughSequence={acknowledgedSequenceNumber}, count={acknowledgedSegments.Count}");
                    foreach (ushort sequenceNumber in acknowledgedSegments)
                    {
                        _segmentsToSend.Remove(sequenceNumber);
                    }

                    ClearDuplicateAcksUnsafe();
                    acknowledged = true;
                }
                else if (connection.LastAcknowledgedSequenceId == acknowledgedSequenceNumber)
                {
                    if (!_duplicateAckPacketSequences.Add(segment.SequenceNumber))
                    {
                        Logger.Warning(LogSource, $"Repeated duplicate ACK ignored. ackPacketSequence={segment.SequenceNumber}, acknowledgedSequence={acknowledgedSequenceNumber}");
                        return false;
                    }

                    Logger.Warning(LogSource, $"Duplicate ACK detected. ackPacketSequence={segment.SequenceNumber}, acknowledgedSequence={acknowledgedSequenceNumber}");
                    AddDuplicateAckUnsafe();
                    duplicateAckThresholdReached = DuplicateAckCount >= 3;
                    if(duplicateAckThresholdReached)
                    {
                        Logger.Info(LogSource, $"Triple duplicate ACK detected. ackPacketSequence={segment.SequenceNumber}, acknowledgedSequence={acknowledgedSequenceNumber}, action=activate-retransmission");
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

        private List<ushort> GetCumulativelyAcknowledgedSequencesUnsafe(
            ushort acknowledgedSequenceNumber,
            ushort lastAcknowledgedSequenceNumber)
        {
            ushort acknowledgedDistance = TcpReceiveBuffer.CircularDistance(
                lastAcknowledgedSequenceNumber,
                acknowledgedSequenceNumber);

            if (acknowledgedDistance == 0)
            {
                return [];
            }

            bool acknowledgementMovesForward = acknowledgedDistance <= ushort.MaxValue / 2;
            if (!acknowledgementMovesForward)
            {
                if (_segmentsToSend.ContainsKey(acknowledgedSequenceNumber))
                {
                    return [acknowledgedSequenceNumber];
                }

                return [];
            }

            ushort highestPendingDistance = _segmentsToSend.Keys
                .Select(sequenceNumber => TcpReceiveBuffer.CircularDistance(
                    lastAcknowledgedSequenceNumber,
                    sequenceNumber))
                .Where(distance => distance > 0 && distance <= ushort.MaxValue / 2)
                .DefaultIfEmpty((ushort)0)
                .Max();

            if (acknowledgedDistance > highestPendingDistance)
            {
                return [];
            }

            return [.. _segmentsToSend.Keys
                .Where(sequenceNumber =>
                {
                    ushort distanceFromLastAck = TcpReceiveBuffer.CircularDistance(
                        lastAcknowledgedSequenceNumber,
                        sequenceNumber);

                    if (distanceFromLastAck == 0 || distanceFromLastAck > acknowledgedDistance)
                    {
                        return false;
                    }

                    ushort distanceToAcknowledgement = TcpReceiveBuffer.CircularDistance(
                        sequenceNumber,
                        acknowledgedSequenceNumber);

                    return distanceToAcknowledgement <= ushort.MaxValue / 2;
                })];
        }

        public List<Segment> GetSegmentsToRetransmit()
        {
            lock (_sync)
            {
                return [.. _segmentsToSend.Values];
            }
        }

        public List<Segment> GetSegmentsToRetransmit(ushort lastAcknowledgedSequenceNumber, int maxSegments)
        {
            if (maxSegments <= 0)
            {
                return [];
            }

            lock (_sync)
            {
                return [.. _segmentsToSend.Values
                    .OrderBy(segment => TcpReceiveBuffer.CircularDistance(
                        lastAcknowledgedSequenceNumber,
                        segment.SequenceNumber))
                    .Take(maxSegments)];
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
