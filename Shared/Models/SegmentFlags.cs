using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.Models
{
    public record SegmentFlags
    {
        public byte Value { get; private set; }
        
        [Flags]
        public enum FlagOptions : byte
        {
            None = 0,
            Syn = 0b0000_0001,
            Ack = 0b0000_0010,
            Fin = 0b0000_0100,
            Rst = 0b0000_1000,
        }

        public SegmentFlags(FlagOptions options)
        {
            if (!AreFlagsValid(options))
                throw new InvalidOperationException($"Combinaison de flags invalide : {options}");
            this.Value = (byte)options;
        }
        public SegmentFlags(byte value)
        {
            FlagOptions options = (FlagOptions)value;
            if (!AreFlagsValid(options))
                throw new InvalidOperationException($"Combinaison de flags invalide : {options}");
            this.Value = value;
        }

        private static bool AreFlagsValid(FlagOptions options)
        {
            const FlagOptions SynFinRstMask = FlagOptions.Syn | FlagOptions.Fin | FlagOptions.Rst; // Masque pour SYN, FIN et RST : 0000 1101
            const FlagOptions AckRstMask = FlagOptions.Ack | FlagOptions.Rst; // Masque pour ACK et RST : 0000 1010

            // 1) Vérifie qu'il n'y a pas plus d'un flag parmi SYN / FIN / RST
            var activeMaskFlags = (byte)(options & SynFinRstMask);

            if (System.Numerics.BitOperations.PopCount(activeMaskFlags) > 1)
                return false;

            // 2) Interdire la combinaison ACK + RST
            if ((options & AckRstMask) == AckRstMask)
                return false;

            return true;
        }
        public static bool IsFlagSet(byte value, FlagOptions flag)
        {
            return (value & (byte)flag) == (byte)flag;
        }
    }
}
