using FluentAssertions;
using Shared.Models.Segments;

namespace Shared.Tests.Models.Segments;

using FlagOptions = SegmentFlags.FlagOptions;

public class SegmentTests
{
    [Fact]
    public void Constructor_WithValidValues_SetsProperties()
    {
        var flags = new SegmentFlags(FlagOptions.Syn | FlagOptions.Ack);
        byte[] data = [0xAA, 0xBB, 0xCC];

        var segment = new Segment(0x1234, flags, data);

        segment.SequenceNumber.Should().Be(0x1234);
        segment.Flags.Should().BeSameAs(flags);
        segment.Data.Should().Equal([0xAA, 0xBB, 0xCC]);
    }

    [Fact]
    public void Constructor_CopiesDataToProtectAgainstExternalMutation()
    {
        byte[] data = [0xAA, 0xBB];

        var segment = new Segment(1, new SegmentFlags(FlagOptions.Ack), data);
        data[0] = 0xFF;

        segment.Data.Should().Equal([0xAA, 0xBB]);
    }

    [Fact]
    public void Data_ReturnsCopyToProtectAgainstMutation()
    {
        var segment = new Segment(1, new SegmentFlags(FlagOptions.Ack), [0xAA, 0xBB]);

        byte[] data = segment.Data;
        data[0] = 0xFF;

        segment.Data.Should().Equal([0xAA, 0xBB]);
    }

    [Fact]
    public void Constructor_WithNullFlags_ThrowsArgumentNullException()
    {
        var act = () => new Segment(1, null!, []);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("flags");
    }

    [Fact]
    public void Constructor_WithNullData_ThrowsArgumentNullException()
    {
        var act = () => new Segment(1, new SegmentFlags(FlagOptions.None), null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public void Constructor_WithDataLongerThanUInt16_ThrowsArgumentException()
    {
        byte[] data = new byte[65536];

        var act = () => new Segment(1, new SegmentFlags(FlagOptions.None), data);

        act.Should().Throw<ArgumentException>()
            .WithMessage("La longueur de data dépasse la taille maximal de 16 bits (65535)*");
    }

    [Fact]
    public void ToString_ReturnsSegmentSummary()
    {
        var segment = new Segment(42, new SegmentFlags(FlagOptions.Ack), [0x10, 0x20, 0x30]);

        var result = segment.ToString();

        result.Should().Be("Segment(SequenceNumber: 42, Flags: Ack (0x02), DataLength: 3)");
    }
}
