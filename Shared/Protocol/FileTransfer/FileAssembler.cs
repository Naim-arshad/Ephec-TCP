using Shared.Models.Segments;
using Shared.Logging;

namespace Shared.Protocol.FileTransfer
{
    public sealed class FileAssembler
    {
        private const string LogSource = "file.assembler";

        private readonly Lock _sync = new();
        private bool _hasStarted;

        public string OutputPath { get; }

        public FileAssembler(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Le chemin du fichier de sortie ne peut pas etre vide.", nameof(outputPath));
            }

            OutputPath = outputPath;
            Logger.Info(LogSource, $"File assembler initialized. outputFile={OutputPath}");
        }

        public void AppendDataSegments(IEnumerable<Segment> segments)
        {
            ArgumentNullException.ThrowIfNull(segments);

            List<Segment> dataSegments = [.. segments.Where(segment =>
                segment.Flags.Value == (byte)SegmentFlags.FlagOptions.None)];

            if (dataSegments.Count == 0)
            {
                return;
            }

            string? outputDirectory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            lock (_sync)
            {
                FileMode fileMode = _hasStarted ? FileMode.Append : FileMode.Create;
                using FileStream stream = new(OutputPath, fileMode, FileAccess.Write, FileShare.Read);

                foreach (Segment segment in dataSegments)
                {
                    byte[] data = segment.Data;
                    stream.Write(data, 0, data.Length);
                }

                _hasStarted = true;
                Logger.Debug(LogSource, $"Data appended to output file. outputFile={OutputPath}, segments={dataSegments.Count}, bytes={dataSegments.Sum(segment => segment.Data.Length)}");
            }
        }
    }
}
