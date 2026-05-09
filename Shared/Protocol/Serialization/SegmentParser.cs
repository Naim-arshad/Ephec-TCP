using System.Buffers.Binary;
using Shared.Logging;
using Shared.Models.Segments;

namespace Shared.Protocol.Serialization
{
    public class SegmentParser
    {
        public static Segment Deserialize(byte[] packetBytes)
        {
            ArgumentNullException.ThrowIfNull(packetBytes);

            if (packetBytes.Length < 5)
            {
                Logger.Warning($"Packet rejected: header is incomplete. Received {packetBytes.Length} byte(s), expected at least 5.");
                throw new ArgumentException("Le tableau d'octets est trop court pour contenir un paquet valide.");
            }

            ushort dataLength = BinaryPrimitives.ReadUInt16BigEndian(packetBytes.AsSpan(0, 2));
            ushort sequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packetBytes.AsSpan(2, 2));
            byte rawFlags = packetBytes[4];
            SegmentFlags flags;

            try
            {
                flags = new SegmentFlags(rawFlags);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error(ex, $"Packet rejected: invalid flags value 0x{rawFlags:X2} for sequence {sequenceNumber}.");
                throw;
            }

            if (packetBytes.Length != 5 + dataLength)
            {
                Logger.Warning(
                    $"Packet rejected: payload length mismatch for sequence {sequenceNumber}. " +
                    $"Header announces {dataLength} byte(s), packet contains {packetBytes.Length - 5} byte(s).");
                throw new ArgumentException("La longueur des données ne correspond pas à la taille du tableau d'octets.\n" + BytesToHexString(packetBytes));
            }

            byte[] data = new byte[dataLength];
            Array.Copy(packetBytes, 5, data, 0, dataLength);
            var segment = new Segment(sequenceNumber, flags, data);
            Logger.Debug($"Packet deserialized: {segment}.");

            return segment;
        }

        private static string BytesToHexString(byte[] bytes)
        {
            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }
    }
}
