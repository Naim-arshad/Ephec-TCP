using FluentAssertions;
using Shared.Protocol.FileTransfer;

namespace Shared.Tests.Protocol.FileTransfer;

public class FileChunkerTests
{
    [Fact]
    public void Constructor_WithMissingFile_ThrowsFileNotFoundException()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin");

        var act = () => new FileChunker(path, 4);

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("Le fichier spécifié est introuvable.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveChunkSize_ThrowsArgumentOutOfRangeException(int chunkSize)
    {
        using var tempFile = TempFile.WithBytes([0x01]);

        var act = () => new FileChunker(tempFile.Path, chunkSize);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("chunkSize");
    }

    [Fact]
    public void Constructor_WithEmptyFile_InitializesEndOfFile()
    {
        using var tempFile = TempFile.WithBytes([]);
        using var chunker = new FileChunker(tempFile.Path, 4);

        chunker.TotalSize.Should().Be(0);
        chunker.CurrentIndex.Should().Be(0);
        chunker.EndOfFile.Should().BeTrue();
        chunker.GetNextChunk().Should().BeNull();
    }

    [Fact]
    public void GetNextChunk_WhenFileIsSmallerThanChunkSize_ReturnsSingleLastChunk()
    {
        using var tempFile = TempFile.WithBytes([0x01, 0x02]);
        using var chunker = new FileChunker(tempFile.Path, 4);

        var chunk = chunker.GetNextChunk();

        chunk.Should().NotBeNull();
        chunk!.Index.Should().Be(0);
        chunk.Data.Should().Equal(0x01, 0x02);
        chunk.IsLastChunk.Should().BeTrue();
        chunker.CurrentIndex.Should().Be(1);
        chunker.EndOfFile.Should().BeTrue();
        chunker.GetNextChunk().Should().BeNull();
    }

    [Fact]
    public void GetNextChunk_WhenFileSizeIsExactMultiple_ReturnsOrderedLastChunk()
    {
        using var tempFile = TempFile.WithBytes([0x01, 0x02, 0x03, 0x04]);
        using var chunker = new FileChunker(tempFile.Path, 2);

        var first = chunker.GetNextChunk();
        var second = chunker.GetNextChunk();

        first!.Index.Should().Be(0);
        first.Data.Should().Equal(0x01, 0x02);
        first.IsLastChunk.Should().BeFalse();
        second!.Index.Should().Be(1);
        second.Data.Should().Equal(0x03, 0x04);
        second.IsLastChunk.Should().BeTrue();
        chunker.CurrentIndex.Should().Be(2);
        chunker.GetNextChunk().Should().BeNull();
    }

    [Fact]
    public void GetNextChunk_WhenLastChunkIsPartial_ReturnsPartialLastChunk()
    {
        using var tempFile = TempFile.WithBytes([0x01, 0x02, 0x03, 0x04, 0x05]);
        using var chunker = new FileChunker(tempFile.Path, 2);

        var chunks = new[]
        {
            chunker.GetNextChunk(),
            chunker.GetNextChunk(),
            chunker.GetNextChunk(),
        };

        chunks.Select(chunk => chunk!.Index).Should().Equal(0, 1, 2);
        chunks.SelectMany(chunk => chunk!.Data).Should().Equal(0x01, 0x02, 0x03, 0x04, 0x05);
        chunks[2]!.Data.Should().Equal(0x05);
        chunks[2]!.IsLastChunk.Should().BeTrue();
        chunker.GetNextChunk().Should().BeNull();
    }

    private sealed class TempFile : IDisposable
    {
        public string Path { get; }

        private TempFile(string path)
        {
            Path = path;
        }

        public static TempFile WithBytes(byte[] bytes)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ephec-tcp-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(path, bytes);
            return new TempFile(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
