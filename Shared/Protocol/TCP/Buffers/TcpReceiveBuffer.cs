using Shared.Models.Segments;
using Shared.Logging;

namespace Shared.Protocol.TCP.Buffers
{
    public class TcpReceiveBuffer
    {
        private const string LogSource = "tcp.receive-buffer";

        private readonly Dictionary<ushort, Segment> _segmentsReceived;

        public IReadOnlyDictionary<ushort, Segment> MapSegmentsReceived => _segmentsReceived;
        public Segment LastSegmentReceived { get; private set; }
        public ushort LastOrderedSequenceNumber { get; private set; }
        public ushort LastConsumedSequenceNumber { get; private set; }


        public TcpReceiveBuffer(ushort lastOrderedSequenceNumber = 0) 
        {
            _segmentsReceived = [];
            LastSegmentReceived = new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.None), []);
            LastOrderedSequenceNumber = lastOrderedSequenceNumber;
            LastConsumedSequenceNumber = lastOrderedSequenceNumber;
        }

        public static ushort CircularDistance(ushort start, ushort value)
        {
            return (ushort)((value - start + ushort.MaxValue + 1) % (ushort.MaxValue + 1));
        }

        public void AddSegment(Segment segment)
        {
            ArgumentNullException.ThrowIfNull(segment);

            ushort distanceFromConsumed = CircularDistance(LastConsumedSequenceNumber, segment.SequenceNumber);
            if (distanceFromConsumed == 0 || distanceFromConsumed > ushort.MaxValue / 2)
            {
                Logger.Warning(LogSource, $"Segment already consumed. sequence={segment.SequenceNumber}, lastConsumedSequence={LastConsumedSequenceNumber}, action=ignore");
                return;
            }

            if (_segmentsReceived.ContainsKey(segment.SequenceNumber))
            {
                Logger.Warning(LogSource, $"Segment already exists in receive buffer. sequence={segment.SequenceNumber}");
            }
            else
            {
                LastSegmentReceived = segment;
                _segmentsReceived[segment.SequenceNumber] = segment;
                LastOrderedSequenceNumber = FindLastOrderedSequenceNumber(LastOrderedSequenceNumber);
                Logger.Debug(LogSource, $"Segment added to receive buffer. sequence={segment.SequenceNumber}, lastOrderedSequence={LastOrderedSequenceNumber}, bufferedSegments={_segmentsReceived.Count}");
            }
        }

        public void SetLastOrderedSequenceNumber(ushort sequenceNumber)
        {
            LastOrderedSequenceNumber = FindLastOrderedSequenceNumber(sequenceNumber);
        }

        public void MarkConsumedThrough(ushort sequenceNumber)
        {
            LastOrderedSequenceNumber = sequenceNumber;
            LastConsumedSequenceNumber = sequenceNumber;
        }

        public IReadOnlyList<Segment> RemoveAcknowledgedSegments()
        {
            ushort acknowledgedDistance = CircularDistance(LastConsumedSequenceNumber, LastOrderedSequenceNumber);
            if (acknowledgedDistance == 0)
            {
                return [];
            }

            List<Segment> acknowledgedSegments = [.. _segmentsReceived.Values
                .Where(segment =>
                {
                    ushort segmentDistance = CircularDistance(LastConsumedSequenceNumber, segment.SequenceNumber);
                    return segmentDistance > 0 && segmentDistance <= acknowledgedDistance;
                })
                .OrderBy(segment => CircularDistance(LastConsumedSequenceNumber, segment.SequenceNumber))];

            foreach (Segment segment in acknowledgedSegments)
            {
                _segmentsReceived.Remove(segment.SequenceNumber);
                LastConsumedSequenceNumber = segment.SequenceNumber;
            }

            if (acknowledgedSegments.Count > 0)
            {
                Logger.Debug(LogSource, $"Acknowledged segments removed. count={acknowledgedSegments.Count}, lastConsumedSequence={LastConsumedSequenceNumber}");
            }

            return acknowledgedSegments;
        }

        public ushort FindLastOrderedSequenceNumber(ushort lastAcked)
        {
            ushort lastOrderedSequenceNumber = lastAcked;

            var receivedSequenceNumbers = _segmentsReceived.Keys.ToHashSet();
            ushort currentExpected = (ushort)(lastAcked + 1);

            while (receivedSequenceNumbers.Contains(currentExpected))
            {
                lastOrderedSequenceNumber = currentExpected;

                if (currentExpected == lastAcked)
                {
                    break;
                }

                currentExpected = (ushort)(currentExpected + 1);
                
            }

            return lastOrderedSequenceNumber;
        }
    }
}
