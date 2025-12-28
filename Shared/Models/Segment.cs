using System.Buffers.Binary;

namespace Shared.Models
{
    public class Segment
    {
        public ushort SequenceNumber { get; private set; }
        public byte Flags { get; private set; }
        public byte[] Data { get; private set; }

        [Flags]
        public enum PacketFlags : byte
        {
            SYN = 1 << 0,
            ACK = 1 << 1,
            FIN = 1 << 2,
            RST = 1 << 3,
        }
        

        // Constructor
        // ushort a pour seul valeur possible 16 bits non signé (0 à 65535)
        public Segment(ushort sequence, PacketFlags flags, byte[] data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length > 65535)
                throw new ArgumentException("La longueur de data dépasse la taille maximal de 16 bits (65535)\n" +
                    "data Length : " + data.Length);
            if (!AreFlagsValid(flags))
                throw new InvalidOperationException($"Combinaison de flags invalide : {flags}");
            SequenceNumber = sequence;
            Flags = (byte)flags;
            Data = data.ToArray(); //Une copie pour empecher les modifications externes si la référence est conservée de data

        }

        // Méthode pour vérifier la validité des flags
        private bool AreFlagsValid(PacketFlags flags)
        {
            const PacketFlags SynFinRstMask = PacketFlags.SYN | PacketFlags.FIN | PacketFlags.RST; // Masque pour SYN, FIN et RST : 0000 1101
            const PacketFlags AckRstMask = PacketFlags.ACK | PacketFlags.RST; // Masque pour ACK et RST : 0000 1010

            // 1) Vérifie qu'il n'y a pas plus d'un flag parmi SYN / FIN / RST
            var activeMaskFlags = (byte)(flags & SynFinRstMask);
            if (CountSetBits(activeMaskFlags) > 1)
                return false;

            // 2) Interdire la combinaison ACK + RST
            if ((flags & AckRstMask) == AckRstMask)
                return false;

            return true;
        }

        private int CountSetBits(byte flags)
        {
            int count = 0;
            while (flags != 0)
            {
                count += flags & 1;
                flags >>= 1;
            }
            return count;
        }

        public byte[] Serialize()
        {
            // Le paquet est composé de : 0-1 : longueur des données (16 bits) 2-3 : numéro de séquence (16 bits) 4 : flags (8 bits) 5-n : données
            int packetLength = 5 + Data.Length;
            using var stream = new MemoryStream(packetLength);

            Span<byte> dataSizeBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(dataSizeBuffer, (ushort)Data.Length);
            stream.Write(dataSizeBuffer);

            Span<byte> sequenceBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(sequenceBuffer, SequenceNumber);
            stream.Write(sequenceBuffer);

            Span<byte> flagsBuffer = stackalloc byte[1];
            flagsBuffer[0] = Flags;
            stream.Write(flagsBuffer);

            stream.Write(Data, 0, Data.Length);

            return stream.ToArray();
        }

        public static Segment Deserialize(byte[] packetBytes)
        {
            if (packetBytes is null)
                throw new ArgumentNullException(nameof(packetBytes));
            if (packetBytes.Length < 5)
                throw new ArgumentException("Le tableau d'octets est trop court pour contenir un paquet valide.");
            ushort dataLength = BinaryPrimitives.ReadUInt16BigEndian(packetBytes.AsSpan(0, 2));
            ushort sequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packetBytes.AsSpan(2, 2));
            byte flags = packetBytes[4];
            if (packetBytes.Length != 5 + dataLength)
                throw new ArgumentException("La longueur des données ne correspond pas à la taille du tableau d'octets.\n" + BytesToHexString(packetBytes));
            byte[] data = new byte[dataLength];
            Array.Copy(packetBytes, 5, data, 0, dataLength);
            return new Segment(sequenceNumber, (PacketFlags)flags, data);
        }

        public static string BytesToHexString(byte[] bytes)
        {
            // Utilise BitConverter pour convertir chaque octet en sa représentation hexadécimale
            // et joint le tout avec des espaces pour une meilleur lisibilité.
            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }
    }
}
