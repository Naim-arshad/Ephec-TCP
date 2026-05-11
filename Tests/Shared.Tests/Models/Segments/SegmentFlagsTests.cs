using FluentAssertions;
using Shared.Models.Segments;

namespace Shared.Tests.Models.Segments;

using FlagOptions = SegmentFlags.FlagOptions;

public class SegmentFlagsTests
{
    public static TheoryData<FlagOptions, byte> ValidFlags =>
        new()
        {
            { FlagOptions.None, 0b0000_0000 },
            { FlagOptions.Syn, 0b0000_0001 },
            { FlagOptions.Ack, 0b0000_0010 },
            { FlagOptions.Fin, 0b0000_0100 },
            { FlagOptions.Rst, 0b0000_1000 },
            { FlagOptions.Syn | FlagOptions.Ack, 0b0000_0011 },
            { FlagOptions.Fin | FlagOptions.Ack, 0b0000_0110 },
        };

    public static TheoryData<FlagOptions> InvalidFlags =>
        new()
        {
            FlagOptions.Syn | FlagOptions.Fin,
            FlagOptions.Syn | FlagOptions.Rst,
            FlagOptions.Fin | FlagOptions.Rst,
            FlagOptions.Syn | FlagOptions.Fin | FlagOptions.Rst,
            FlagOptions.Ack | FlagOptions.Rst,
            FlagOptions.Syn | FlagOptions.Ack | FlagOptions.Rst,
            FlagOptions.Fin | FlagOptions.Ack | FlagOptions.Rst,
            (FlagOptions)0b0001_0000,
            (FlagOptions)0b1111_1111,
        };

    [Theory]
    [MemberData(nameof(ValidFlags))]
    public void Constructor_WithValidFlagOptions_SetsValue(FlagOptions options, byte expectedValue)
    {
        var flags = new SegmentFlags(options);

        flags.Value.Should().Be(expectedValue);
    }

    [Theory]
    [MemberData(nameof(ValidFlags))]
    public void Constructor_WithValidByteValue_SetsValue(FlagOptions options, byte expectedValue)
    {
        var flags = new SegmentFlags((byte)options);

        flags.Value.Should().Be(expectedValue);
    }

    [Theory]
    [MemberData(nameof(InvalidFlags))]
    public void Constructor_WithInvalidFlagOptions_ThrowsInvalidOperationException(FlagOptions options)
    {
        var act = () => new SegmentFlags(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Combinaison de flags invalide : *");
    }

    [Theory]
    [MemberData(nameof(InvalidFlags))]
    public void Constructor_WithInvalidByteValue_ThrowsInvalidOperationException(FlagOptions options)
    {
        var act = () => new SegmentFlags((byte)options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Combinaison de flags invalide : *");
    }

    [Theory]
    [InlineData(0b0000_0011, FlagOptions.Syn, true)]
    [InlineData(0b0000_0011, FlagOptions.Ack, true)]
    [InlineData(0b0000_0011, FlagOptions.Fin, false)]
    [InlineData(0b0000_0110, FlagOptions.Fin, true)]
    [InlineData(0b0000_0110, FlagOptions.Rst, false)]
    [InlineData(0b0000_0000, FlagOptions.Syn, false)]
    [InlineData(0b0000_1000, FlagOptions.Rst, true)]
    public void IsFlagSet_ReturnsWhetherFlagIsPresent(byte value, FlagOptions flag, bool expected)
    {
        var result = SegmentFlags.IsFlagSet(value, flag);

        result.Should().Be(expected);
    }
}
