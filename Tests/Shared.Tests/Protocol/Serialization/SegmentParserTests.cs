using FluentAssertions;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;

namespace Shared.Tests.Protocol.Serialization;

using FlagOptions = SegmentFlags.FlagOptions;

public class SegmentParserTests
{
    [Fact]
    public void Deserialize_WithValidPacket_ReturnsSegment()
    {
        byte[] packet = [
            0x00, 0x03,
            0x12, 0x34,
            0x06,
            0xAA, 0xBB, 0xCC,
        ];

        var segment = SegmentParser.Deserialize(packet);

        segment.SequenceNumber.Should().Be(0x1234);
        segment.Flags.Value.Should().Be((byte)(FlagOptions.Fin | FlagOptions.Ack));
        segment.Data.Should().Equal([0xAA, 0xBB, 0xCC]);
    }

    [Fact]
    public void Deserialize_WithEmptyPayload_ReturnsSegmentWithEmptyData()
    {
        byte[] packet = [
            0x00, 0x00,
            0x00, 0x2A,
            0x01,
        ];

        var segment = SegmentParser.Deserialize(packet);

        segment.SequenceNumber.Should().Be(42);
        segment.Flags.Value.Should().Be((byte)FlagOptions.Syn);
        segment.Data.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_WithNullPacket_ThrowsArgumentNullException()
    {
        var act = () => SegmentParser.Deserialize(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("packetBytes");
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x00, 0x01, 0x00, 0x2A })]
    public void Deserialize_WithPacketShorterThanHeader_ThrowsArgumentException(byte[] packet)
    {
        var act = () => SegmentParser.Deserialize(packet);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Le tableau d'octets est trop court pour contenir un paquet valide.");
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x02, 0x00, 0x2A, 0x02, 0xAA })]
    [InlineData(new byte[] { 0x00, 0x01, 0x00, 0x2A, 0x02, 0xAA, 0xBB })]
    public void Deserialize_WithPayloadLengthMismatch_ThrowsArgumentException(byte[] packet)
    {
        var act = () => SegmentParser.Deserialize(packet);

        act.Should().Throw<ArgumentException>()
            .WithMessage("La longueur des données ne correspond pas à la taille du tableau d'octets.*");
    }

    [Fact]
    public void Deserialize_WithInvalidFlags_ThrowsInvalidOperationException()
    {
        byte[] packet = [
            0x00, 0x00,
            0x00, 0x2A,
            (byte)(FlagOptions.Syn | FlagOptions.Fin),
        ];

        var act = () => SegmentParser.Deserialize(packet);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Combinaison de flags invalide : *");
    }
}
