namespace Shared.Models.Segments
{
    public class Segment
    {
        private readonly byte[] _data;

        public ushort SequenceNumber { get; }
        public SegmentFlags Flags { get; }
        public byte[] Data => [.. _data];

      
        // ushort a pour seul valeur possible 16 bits non signé (0 à 65535)
        public Segment(ushort sequence, SegmentFlags flags, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(flags);

            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length > 65535)
            {
                throw new ArgumentException("La longueur de data dépasse la taille maximal de 16 bits (65535)\n" +
                    "data Length : " + data.Length);
            }

            SequenceNumber = sequence;
            Flags = flags;
            _data = [.. data]; //Une copie pour empecher les modifications externes si la référence est conservée de data
        }

        public override string ToString()
        {
            return $"Segment(SequenceNumber: {SequenceNumber}, Flags: {Flags}, DataLength: {_data.Length})";
        }
    }
}
