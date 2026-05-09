using System.Buffers.Binary;
using Shared.Logging;
using Shared.Models.Segments;

namespace Shared.Protocol.Serialization
{
    public static class SegmentPayloadReader
    {
        public static ushort ReadAckSequence(Segment segment)
        {
            ArgumentNullException.ThrowIfNull(segment);
            byte[] data = segment.Data;

            if (data.Length != 2)
            {
                Logger.Warning($"Cannot read ACK sequence from segment {segment.SequenceNumber}: data length is {data.Length}, expected 2.");
                throw new InvalidOperationException("Data doit contenir exactement 2 octets pour représenter une séquence.");
            }

            return BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(0, 2));
        }
    }
}
