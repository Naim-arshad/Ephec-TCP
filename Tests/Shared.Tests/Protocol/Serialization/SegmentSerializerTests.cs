using FluentAssertions;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;

namespace Shared.Tests.Protocol.Serialization;

using FlagOptions = SegmentFlags.FlagOptions;

public class SegmentSerializerTests
{
    [Fact]
    public void Serialize_WithData_ReturnsPacketWithBigEndianHeaderAndPayload()
    {
        var segment = new Segment(
            sequence: 0x1234,
            flags: new SegmentFlags(FlagOptions.Syn | FlagOptions.Ack),
            data: [0xCA, 0xFE]);

        var packet = SegmentSerializer.Serialize(segment);

        packet.Should().Equal([
            0x00, 0x02,
            0x12, 0x34,
            0x03,
            0xCA, 0xFE,
        ]);
    }

    [Fact]
    public void Serialize_WithEmptyData_ReturnsHeaderOnlyPacket()
    {
        var segment = new Segment(
            sequence: 0xFFFF,
            flags: new SegmentFlags(FlagOptions.Ack),
            data: []);

        var packet = SegmentSerializer.Serialize(segment);

        packet.Should().Equal([
            0x00, 0x00,
            0xFF, 0xFF,
            0x02,
        ]);
    }

    [Fact]
    public void Serialize_ThenDeserialize_ReturnsEquivalentSegment()
    {
        var segment = new Segment(
            sequence: 42,
            flags: new SegmentFlags(FlagOptions.Fin | FlagOptions.Ack),
            data: [0x10, 0x20, 0x30]);

        var parsedSegment = SegmentParser.Deserialize(SegmentSerializer.Serialize(segment));

        parsedSegment.SequenceNumber.Should().Be(segment.SequenceNumber);
        parsedSegment.Flags.Value.Should().Be(segment.Flags.Value);
        parsedSegment.Data.Should().Equal(segment.Data);
    }
}
