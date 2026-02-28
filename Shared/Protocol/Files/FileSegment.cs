using System;
using System.IO;
using Shared.Models; 

namespace Shared.Protocol.Files
{
    public static class FileSegment
    {
        private const int ChunkSize = 1024;

        public static void WriteToFile(string filePath, Segment segment, ushort initialSeq)
        {
            if (segment?.Data == null) return;

            try
            {
                using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);

                // Calcul de l'offset selon le schéma EPHEC (le premier segment de DATA est à x + 1)
                long relativeSeq = (ushort)(segment.SequenceNumber - (initialSeq + 1));
                long offset = relativeSeq * ChunkSize;

                if (fs.Length < offset) fs.SetLength(offset);
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Write(segment.Data, 0, segment.Data.Length);
                fs.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERREUR DISQUE] {ex.Message}");
            }
        }
    }
}