namespace Shared.Models.FileTransfer
{
    public class FileChunk
    {
        public int Index { get; }

        private readonly byte[] _data;
        public byte[] Data => [.. _data];
        public bool IsLastChunk { get; }

        public FileChunk(int index, byte[] data, bool isLastChunk)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "L'index du chunk doit etre positif ou nul.");

            Index = index;
            _data = [.. data];
            IsLastChunk = isLastChunk;
        }
    }
}
