using FluentAssertions;
using Shared.Models.FileTransfer;

namespace Shared.Tests.Models.FileTransfer;

public class FileChunkTests
{
    [Fact]
    public void Constructor_WithValidValues_InitializesChunk()
    {
        byte[] data = [0x01, 0x02];

        var chunk = new FileChunk(3, data, isLastChunk: true);

        chunk.Index.Should().Be(3);
        chunk.Data.Should().Equal(data);
        chunk.IsLastChunk.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new FileChunk(-1, [], isLastChunk: false);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
    }

    [Fact]
    public void Constructor_WithNullData_ThrowsArgumentNullException()
    {
        var act = () => new FileChunk(0, null!, isLastChunk: false);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public void Data_WhenOriginalArrayIsModified_ReturnsConstructorCopy()
    {
        byte[] data = [0x01, 0x02];
        var chunk = new FileChunk(0, data, isLastChunk: false);

        data[0] = 0xFF;

        chunk.Data.Should().Equal(0x01, 0x02);
    }

    [Fact]
    public void Data_WhenReturnedArrayIsModified_KeepsInternalDataUnchanged()
    {
        var chunk = new FileChunk(0, [0x01, 0x02], isLastChunk: false);

        byte[] returnedData = chunk.Data;
        returnedData[0] = 0xFF;

        chunk.Data.Should().Equal(0x01, 0x02);
    }
}
