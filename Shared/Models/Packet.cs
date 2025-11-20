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
        // ushort a pour seul valeur possible 16 bits non signé (0 à 65535), donc la vérification de la taille n'est pas nécessaire
        public Packet(ushort dataSize, ushort sequence, Flags flags, byte[] data)
        {
            if(data.Length != dataSize)
                throw new ArgumentException("La longueur de data no correspond pas a dataSize\n data.lenght : " + data.Length + " dataSize : " + dataSize);
            _dataSize = dataSize;
            _sequence = sequence;

        }

        private bool areFlagsCorrect(Flags flags)
        {
            const Flags WRONG_COMBINATION_FLAGS = Flags.SYN | Flags.FIN | Flags.RST;
            const Flags WRONG_COMBINATION_FLAGS_ACK = Flags.ACK | Flags.RST;

            var count
        }
    }
}
