namespace Shared.Models
{
    public class Packet
    {
        [Flags]
        public enum Flags : byte
        {
            SYN = 1 << 0,
            ACK = 1 << 1,
            FIN = 1 << 2,
            RST = 1 << 3,
        }
        private ushort _dataSize;
        private ushort _sequence;
        private Flags _flags;
        private byte[] _data;

        // Constructor
        // ushort a pour seul valeur possible 16 bits non signé (0 à 65535)
        public Packet(ushort sequence, Flags flags, byte[] data)
        {
            if (data.Length > 65535)
                throw new ArgumentException("La longueur de data dépasse la taille maximal de 16 bits (65535)\n" +
                    "data lenght : " + data.Length);
            if (!AreFlagsValid(flags))
                throw new InvalidOperationException($"Combinaison de flags invalide : {flags}");
            _dataSize = (ushort)data.Length;
            _sequence = sequence;
            _flags = flags;
            _data = data;

        }

        // Méthode pour vérifier la validité des flags
        private bool AreFlagsValid(Flags flags)
        {
            const Flags SynFinRstMask = Flags.SYN | Flags.FIN | Flags.RST; // Masque pour SYN, FIN et RST : 0000 1101
            const Flags AckRstMask = Flags.ACK | Flags.RST; // Masque pour ACK et RST : 0000 1010

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
    }
}
