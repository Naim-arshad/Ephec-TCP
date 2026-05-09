using Shared.Models.Segments;
using Shared.Logging;

namespace Shared.Protocol.TCP.Buffers
{
    public class TcpReceiveBuffer
    {
        public readonly Dictionary<ushort, Segment> MapSegmentsReceived;
        public Segment LastSegmentReceived { get; private set; }


        public TcpReceiveBuffer() 
        {
            MapSegmentsReceived = [];
            LastSegmentReceived = new Segment(0, new SegmentFlags(SegmentFlags.FlagOptions.None), []);
        }
        public static ushort CircularDistance(ushort start, ushort value)
        {
            return (ushort)((value - start + ushort.MaxValue + 1) % (ushort.MaxValue + 1));
        }

        public void AddSegment(Segment segment)
        {
            if (MapSegmentsReceived.ContainsKey(segment.SequenceNumber))
            {
                Logger.Warning($"Segment with sequence number {segment.SequenceNumber} already exists in the receive buffer.");
            }
            else
            {
                LastSegmentReceived = segment;
                MapSegmentsReceived[segment.SequenceNumber] = segment;
            }
        }

        public ushort FindLastOrderedSequenceNumber(ushort lastAcked)
        {
            ushort lastOrderedSequenceNumber = lastAcked;

            var receivedSequenceNumbers = MapSegmentsReceived.Keys.ToHashSet();
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
