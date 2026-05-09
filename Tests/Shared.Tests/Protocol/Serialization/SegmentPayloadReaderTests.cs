using FluentAssertions;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;

namespace Shared.Tests.Protocol.Serialization;

using FlagOptions = SegmentFlags.FlagOptions;

public class SegmentPayloadReaderTests
{
    [Theory]
    [InlineData(new byte[] { 0x00, 0x00 }, (ushort)0x0000)]
    [InlineData(new byte[] { 0x12, 0x34 }, (ushort)0x1234)]
    [InlineData(new byte[] { 0xFF, 0xFF }, (ushort)0xFFFF)]
    public void ReadAckSequence_WithTwoDataBytes_ReturnsBigEndianSequence(byte[] data, ushort expectedSequence)
    {
        var segment = new Segment(1, new SegmentFlags(FlagOptions.Ack), data);

        var sequence = SegmentPayloadReader.ReadAckSequence(segment);

        sequence.Should().Be(expectedSequence);
    }

    [Fact]
    public void ReadAckSequence_WithNullSegment_ThrowsArgumentNullException()
    {
        var act = () => SegmentPayloadReader.ReadAckSequence(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("segment");
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x12 })]
    [InlineData(new byte[] { 0x12, 0x34, 0x56 })]
    public void ReadAckSequence_WithInvalidDataLength_ThrowsInvalidOperationException(byte[] data)
    {
        var segment = new Segment(1, new SegmentFlags(FlagOptions.Ack), data);

        var act = () => SegmentPayloadReader.ReadAckSequence(segment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Data doit contenir exactement 2 octets pour représenter une séquence.");
    }
}
