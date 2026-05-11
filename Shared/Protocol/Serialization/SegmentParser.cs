using System.Buffers.Binary;
using Shared.Logging;
using Shared.Models.Segments;

namespace Shared.Protocol.Serialization
{
    public class SegmentParser
    {
        private const string LogSource = "protocol.serialization";

        public static Segment Deserialize(byte[] packetBytes)
        {
            ArgumentNullException.ThrowIfNull(packetBytes);

            if (packetBytes.Length < 5)
            {
                Logger.Warning(LogSource, $"Packet rejected. reason=incomplete-header, bytes={packetBytes.Length}, expectedMinimum=5");
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
                Logger.Error(ex, LogSource, $"Packet rejected. reason=invalid-flags, sequence={sequenceNumber}, flags=0x{rawFlags:X2}");
                throw;
            }

            if (packetBytes.Length != 5 + dataLength)
            {
                Logger.Warning(
                    LogSource,
                    $"Packet rejected. reason=payload-length-mismatch, sequence={sequenceNumber}, announcedPayloadBytes={dataLength}, actualPayloadBytes={packetBytes.Length - 5}");
                throw new ArgumentException("La longueur des données ne correspond pas à la taille du tableau d'octets.\n" + BytesToHexString(packetBytes));
            }

            byte[] data = new byte[dataLength];
            Array.Copy(packetBytes, 5, data, 0, dataLength);
            var segment = new Segment(sequenceNumber, flags, data);
            Logger.Debug(LogSource, $"Packet deserialized. sequence={segment.SequenceNumber}, flags={segment.Flags}, payloadBytes={segment.Data.Length}");

            return segment;
        }

        private static string BytesToHexString(byte[] bytes)
        {
            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }
    }
}
