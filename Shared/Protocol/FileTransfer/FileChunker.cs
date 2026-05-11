using Shared.Logging;
using Shared.Models.FileTransfer;

namespace Shared.Protocol.FileTransfer
{
    public class FileChunker : IDisposable
    {
        private const string LogSource = "file.chunker";

        private readonly FileStream _stream;
        private readonly int _chunkSize;

        public int CurrentIndex { get; private set; }
        public long TotalSize { get; }
        public bool EndOfFile => _stream.Position >= _stream.Length;

        public FileChunker(string filePath, int chunkSize)
        {
            if(!File.Exists(filePath))
                throw new FileNotFoundException("Le fichier spécifié est introuvable.", filePath);
            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Le chunk size doit etre strictement positif.");
            }
            _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _chunkSize = chunkSize;
            TotalSize = _stream.Length;
            CurrentIndex = 0;
            Logger.Info(LogSource, $"File opened for chunking. file={filePath}, bytes={TotalSize}, chunkSize={_chunkSize}");
        }

        public FileChunk? GetNextChunk()
        {
            if (EndOfFile)
                return null;

            byte[] buffer = new byte[_chunkSize];

            int bytesRead = _stream.Read(buffer, 0, _chunkSize);

            if (bytesRead == 0)
                return null;

            byte[] data = buffer[..bytesRead];

            var chunk = new FileChunk(CurrentIndex, data, EndOfFile);
            Logger.Debug(LogSource, $"Chunk read. index={chunk.Index}, bytes={bytesRead}, isLast={chunk.IsLastChunk}");
            CurrentIndex++;
            return chunk;
        }

        public void Dispose()
        {
            _stream.Dispose();
            Logger.Debug(LogSource, "File chunker disposed.");
        }
    }
}
