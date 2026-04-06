using System.Buffers.Binary;

namespace Shared.Models
{
    public class Segment
    {
        private readonly Lock _sync = new();
        public ushort SequenceNumber { get; private set; }
        public SegmentFlags Flags { get; private set; }
        public byte[] Data { get; private set; }
        public int RetransmissionCount { get; private set; }
        public bool AckReceived { get; set; }


        // Constructor
        // ushort a pour seul valeur possible 16 bits non signé (0 à 65535)
        public Segment(ushort sequence, SegmentFlags flags, byte[] data, bool ackReceived)
        {
            ArgumentNullException.ThrowIfNull(data);

            if (data.Length > 65535)
                throw new ArgumentException("La longueur de data dépasse la taille maximal de 16 bits (65535)\n" +
                    "data Length : " + data.Length);
            SequenceNumber = sequence;
            Flags = flags;
            Data = [.. data]; //Une copie pour empecher les modifications externes si la référence est conservée de data
            RetransmissionCount = 0;
            AckReceived = ackReceived;
        }

        public Segment(ushort sequence, SegmentFlags flags, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);

            if (data.Length > 65535)
                throw new ArgumentException("La longueur de data dépasse la taille maximal de 16 bits (65535)\n" +
                    "data Length : " + data.Length);
            SequenceNumber = sequence;
            Flags = flags;
            Data = [.. data]; //Une copie pour empecher les modifications externes si la référence est conservée de data
            RetransmissionCount = 0;
            AckReceived = true;
        }

        public byte[] Serialize()
        {
            // Le paquet est composé de : 0-1 : longueur des données (16 bits) 2-3 : numéro de séquence (16 bits) 4 : flags (8 bits) 5-n : données
            int packetLength = 5 + Data.Length;
            using var stream = new MemoryStream(packetLength);

            Span<byte> dataSizeBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(dataSizeBuffer, (ushort)Data.Length);
            stream.Write(dataSizeBuffer);

            stream.Write(GetSequenceToBytes());

            Span<byte> flagsBuffer = [Flags.Value];
            stream.Write(flagsBuffer);

            stream.Write(Data, 0, Data.Length);

            return stream.ToArray();
        }

        public static Segment Deserialize(byte[] packetBytes)
        {
            ArgumentNullException.ThrowIfNull(packetBytes);

            if (packetBytes.Length < 5)
                throw new ArgumentException("Le tableau d'octets est trop court pour contenir un paquet valide.");

            ushort dataLength = BinaryPrimitives.ReadUInt16BigEndian(packetBytes.AsSpan(0, 2));
            ushort sequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packetBytes.AsSpan(2, 2));
            SegmentFlags flags = new(packetBytes[4]);

            if (packetBytes.Length != 5 + dataLength)
                throw new ArgumentException("La longueur des données ne correspond pas à la taille du tableau d'octets.\n" + BytesToHexString(packetBytes));

            byte[] data = new byte[dataLength];
            Array.Copy(packetBytes, 5, data, 0, dataLength);
            return new Segment(sequenceNumber, flags, data);
        }

        public byte[] GetSequenceToBytes()
        {
            Span<byte> sequenceBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(sequenceBuffer, SequenceNumber);
            return sequenceBuffer.ToArray();
        }

        public ushort GetDataToSequence()
        {
            return BinaryPrimitives.ReadUInt16BigEndian(Data);
        }

        public void IncrementRetransmissionCount()
        {
            RetransmissionCount++;
        }

        public static string BytesToHexString(byte[] bytes)
        {
            // Utilise BitConverter pour convertir chaque octet en sa représentation hexadécimale
            // et joint le tout avec des espaces pour une meilleur lisibilité.
            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }

        public override string ToString()
        {
            return $"Segment(SequenceNumber: {SequenceNumber}, Flags: {Flags}, DataLength: {Data.Length})";
        }
    }
}
