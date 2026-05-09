using System.Buffers.Binary;
using Shared.Models.Segments;

namespace Shared.Protocol.Serialization
{
    public class SegmentSerializer
    {
        public static byte[] Serialize(Segment segment)
        {
            // Le paquet est composé de :
            // 0-1 : longueur des données (16 bits)
            // 2-3 : numéro de séquence (16 bits)
            // 4 : flags (8 bits)
            // 5-n : données
            int packetLength = 5 + segment.Data.Length;
            using var stream = new MemoryStream(packetLength);

            Span<byte> dataSizeBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(dataSizeBuffer, (ushort)segment.Data.Length);
            stream.Write(dataSizeBuffer);

            stream.Write(segment.GetSequenceToBytes());

            Span<byte> flagsBuffer = [segment.Flags.Value];
            stream.Write(flagsBuffer);

            stream.Write(segment.Data, 0, segment.Data.Length);

            return stream.ToArray();
        }
    }
}
