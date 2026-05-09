using System.Buffers.Binary;
using Shared.Logging;
using Shared.Models.Segments;

namespace Shared.Protocol.Serialization
{
    public class SegmentSerializer
    {
        public static byte[] Serialize(Segment segment)
        {
            ArgumentNullException.ThrowIfNull(segment);
            byte[] data = segment.Data;

            // Le paquet est compose de :
            // 0-1 : longueur des donnees (16 bits)
            // 2-3 : numero de sequence (16 bits)
            // 4 : flags (8 bits)
            // 5-n : donnees
            int packetLength = 5 + data.Length;
            using var stream = new MemoryStream(packetLength);

            Span<byte> dataSizeBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(dataSizeBuffer, (ushort)data.Length);
            stream.Write(dataSizeBuffer);

            Span<byte> sequenceBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(sequenceBuffer, segment.SequenceNumber);
            stream.Write(sequenceBuffer);

            Span<byte> flagsBuffer = [segment.Flags.Value];
            stream.Write(flagsBuffer);

            stream.Write(data, 0, data.Length);

            byte[] packet = stream.ToArray();
            Logger.Debug(
                $"Packet serialized: sequence={segment.SequenceNumber}, flags=0x{segment.Flags.Value:X2}, " +
                $"payloadLength={data.Length}, packetLength={packet.Length}.");

            return packet;
        }

        public static byte[] SerializeSequenceNumber(Segment segment)
        {
            Span<byte> sequenceBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(sequenceBuffer, segment.SequenceNumber);
            return sequenceBuffer.ToArray();
        }
    }
}
