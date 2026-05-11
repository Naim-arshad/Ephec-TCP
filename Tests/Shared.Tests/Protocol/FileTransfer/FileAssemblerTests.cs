using FluentAssertions;
using Shared.Models.Segments;
using Shared.Protocol.FileTransfer;

namespace Shared.Tests.Protocol.FileTransfer;

using FlagOptions = SegmentFlags.FlagOptions;

public class FileAssemblerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankOutputPath_ThrowsArgumentException(string outputPath)
    {
        var act = () => new FileAssembler(outputPath);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("outputPath");
    }

    [Fact]
    public void AppendDataSegments_WithNullSegments_ThrowsArgumentNullException()
    {
        var assembler = new FileAssembler(Path.Combine(Path.GetTempPath(), $"ephec-tcp-{Guid.NewGuid():N}.bin"));

        var act = () => assembler.AppendDataSegments(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("segments");
    }

    [Fact]
    public void AppendDataSegments_WithOnlyControlSegments_DoesNotCreateOutputFile()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ephec-tcp-{Guid.NewGuid():N}.bin");
        var assembler = new FileAssembler(outputPath);

        assembler.AppendDataSegments([CreateSegment(FlagOptions.Ack, [0x01])]);

        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public void AppendDataSegments_CreatesDirectoryAndWritesDataSegmentsInReceivedOrder()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ephec-tcp-{Guid.NewGuid():N}");
        string outputPath = Path.Combine(directory, "received.bin");

        try
        {
            var assembler = new FileAssembler(outputPath);

            assembler.AppendDataSegments([
                CreateSegment(FlagOptions.None, [0x01, 0x02]),
                CreateSegment(FlagOptions.Ack, [0xFF]),
                CreateSegment(FlagOptions.None, [0x03]),
            ]);

            File.ReadAllBytes(outputPath).Should().Equal(0x01, 0x02, 0x03);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void AppendDataSegments_WhenCalledMultipleTimes_AppendsToExistingOutput()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ephec-tcp-{Guid.NewGuid():N}.bin");

        try
        {
            var assembler = new FileAssembler(outputPath);

            assembler.AppendDataSegments([CreateSegment(FlagOptions.None, [0x01])]);
            assembler.AppendDataSegments([CreateSegment(FlagOptions.None, [0x02, 0x03])]);

            File.ReadAllBytes(outputPath).Should().Equal(0x01, 0x02, 0x03);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static Segment CreateSegment(FlagOptions flags, byte[] data)
    {
        return new Segment(1, new SegmentFlags(flags), data);
    }
}
