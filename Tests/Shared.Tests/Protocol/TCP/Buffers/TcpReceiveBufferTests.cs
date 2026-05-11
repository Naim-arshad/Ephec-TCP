using FluentAssertions;
using Shared.Models.Segments;
using Shared.Protocol.TCP.Buffers;

namespace Shared.Tests.Protocol.TCP.Buffers;

using FlagOptions = SegmentFlags.FlagOptions;

public class TcpReceiveBufferTests
{
    [Fact]
    public void Constructor_InitializesEmptyBuffer()
    {
        var buffer = new TcpReceiveBuffer();

        buffer.MapSegmentsReceived.Should().BeEmpty();
        buffer.LastSegmentReceived.SequenceNumber.Should().Be(0);
        buffer.LastSegmentReceived.Flags.Value.Should().Be((byte)FlagOptions.None);
        buffer.LastSegmentReceived.Data.Should().BeEmpty();
        buffer.LastOrderedSequenceNumber.Should().Be(0);
        buffer.LastConsumedSequenceNumber.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithLastOrderedSequenceNumber_InitializesLastOrderedSequenceNumber()
    {
        var buffer = new TcpReceiveBuffer(10);

        buffer.LastOrderedSequenceNumber.Should().Be(10);
        buffer.LastConsumedSequenceNumber.Should().Be(10);
    }

    [Fact]
    public void CircularDistance_WhenValueIsAfterStart_ReturnsForwardDistance()
    {
        ushort result = TcpReceiveBuffer.CircularDistance(10, 15);

        result.Should().Be(5);
    }

    [Fact]
    public void CircularDistance_WhenValueWrapsAround_ReturnsWrappedDistance()
    {
        ushort result = TcpReceiveBuffer.CircularDistance(ushort.MaxValue - 1, 1);

        result.Should().Be(3);
    }

    [Fact]
    public void AddSegment_WithNewSequenceNumber_StoresSegmentAndUpdatesLastSegmentReceived()
    {
        var buffer = new TcpReceiveBuffer();
        var segment = CreateSegment(42, [0xAA]);

        buffer.AddSegment(segment);

        buffer.MapSegmentsReceived.Should().ContainKey(42)
            .WhoseValue.Should().BeSameAs(segment);
        buffer.LastSegmentReceived.Should().BeSameAs(segment);
    }

    [Fact]
    public void AddSegment_WithDuplicateSequenceNumber_KeepsFirstSegment()
    {
        var buffer = new TcpReceiveBuffer();
        var firstSegment = CreateSegment(42, [0xAA]);
        var duplicateSegment = CreateSegment(42, [0xBB]);

        buffer.AddSegment(firstSegment);
        buffer.AddSegment(duplicateSegment);

        buffer.MapSegmentsReceived.Should().ContainSingle();
        buffer.MapSegmentsReceived[42].Should().BeSameAs(firstSegment);
        buffer.LastSegmentReceived.Should().BeSameAs(firstSegment);
    }

    [Fact]
    public void AddSegment_WithContiguousSegments_UpdatesLastOrderedSequenceNumber()
    {
        var buffer = new TcpReceiveBuffer(10);

        buffer.AddSegment(CreateSegment(11));
        buffer.AddSegment(CreateSegment(12));
        buffer.AddSegment(CreateSegment(13));

        buffer.LastOrderedSequenceNumber.Should().Be(13);
    }

    [Fact]
    public void AddSegment_WithGap_KeepsLastOrderedSequenceNumberBeforeGap()
    {
        var buffer = new TcpReceiveBuffer(10);

        buffer.AddSegment(CreateSegment(11));
        buffer.AddSegment(CreateSegment(13));

        buffer.LastOrderedSequenceNumber.Should().Be(11);
    }

    [Fact]
    public void AddSegment_WhenMissingSegmentArrives_AdvancesLastOrderedSequenceNumberAcrossBufferedSegments()
    {
        var buffer = new TcpReceiveBuffer(10);

        buffer.AddSegment(CreateSegment(11));
        buffer.AddSegment(CreateSegment(13));
        buffer.AddSegment(CreateSegment(12));

        buffer.LastOrderedSequenceNumber.Should().Be(13);
    }

    [Fact]
    public void AddSegment_WithAlreadyConsumedSegment_IgnoresSegment()
    {
        var buffer = new TcpReceiveBuffer(10);
        buffer.AddSegment(CreateSegment(11));
        buffer.RemoveAcknowledgedSegments();

        buffer.AddSegment(CreateSegment(11, [0xAA]));

        buffer.MapSegmentsReceived.Should().BeEmpty();
        buffer.LastConsumedSequenceNumber.Should().Be(11);
        buffer.LastOrderedSequenceNumber.Should().Be(11);
    }

    [Fact]
    public void FindLastOrderedSequenceNumber_WithEmptyBuffer_ReturnsLastAcked()
    {
        var buffer = new TcpReceiveBuffer();

        ushort result = buffer.FindLastOrderedSequenceNumber(10);

        result.Should().Be(10);
    }

    [Fact]
    public void FindLastOrderedSequenceNumber_WithContiguousSegments_ReturnsHighestOrderedSequenceNumber()
    {
        var buffer = new TcpReceiveBuffer();
        buffer.AddSegment(CreateSegment(11));
        buffer.AddSegment(CreateSegment(12));
        buffer.AddSegment(CreateSegment(13));

        ushort result = buffer.FindLastOrderedSequenceNumber(10);

        result.Should().Be(13);
    }

    [Fact]
    public void FindLastOrderedSequenceNumber_WithGap_ReturnsSequenceNumberBeforeGap()
    {
        var buffer = new TcpReceiveBuffer();
        buffer.AddSegment(CreateSegment(11));
        buffer.AddSegment(CreateSegment(13));

        ushort result = buffer.FindLastOrderedSequenceNumber(10);

        result.Should().Be(11);
    }

    [Fact]
    public void FindLastOrderedSequenceNumber_WithWrappedContiguousSegments_ReturnsHighestOrderedSequenceNumber()
    {
        var buffer = new TcpReceiveBuffer(ushort.MaxValue - 1);
        buffer.AddSegment(CreateSegment(ushort.MaxValue));
        buffer.AddSegment(CreateSegment(0));
        buffer.AddSegment(CreateSegment(1));

        ushort result = buffer.FindLastOrderedSequenceNumber(ushort.MaxValue - 1);

        result.Should().Be(1);
    }

    [Fact]
    public void RemoveAcknowledgedSegments_WithContiguousSegments_RemovesAndReturnsThemInOrder()
    {
        var buffer = new TcpReceiveBuffer(10);
        var segment11 = CreateSegment(11, [0x11]);
        var segment12 = CreateSegment(12, [0x12]);

        buffer.AddSegment(segment12);
        buffer.AddSegment(segment11);

        IReadOnlyList<Segment> result = buffer.RemoveAcknowledgedSegments();

        result.Select(segment => segment.SequenceNumber).Should().Equal(11, 12);
        buffer.MapSegmentsReceived.Should().BeEmpty();
        buffer.LastConsumedSequenceNumber.Should().Be(12);
    }

    [Fact]
    public void RemoveAcknowledgedSegments_WithGap_KeepsSegmentsAfterGap()
    {
        var buffer = new TcpReceiveBuffer(10);
        buffer.AddSegment(CreateSegment(11));
        buffer.AddSegment(CreateSegment(13));

        IReadOnlyList<Segment> result = buffer.RemoveAcknowledgedSegments();

        result.Select(segment => segment.SequenceNumber).Should().Equal(11);
        buffer.MapSegmentsReceived.Should().ContainKey(13);
        buffer.LastConsumedSequenceNumber.Should().Be(11);
    }

    [Fact]
    public void RemoveAcknowledgedSegments_WithWrappedSegments_RemovesInCircularOrder()
    {
        var buffer = new TcpReceiveBuffer(ushort.MaxValue - 1);
        buffer.AddSegment(CreateSegment(0));
        buffer.AddSegment(CreateSegment(ushort.MaxValue));
        buffer.AddSegment(CreateSegment(1));

        IReadOnlyList<Segment> result = buffer.RemoveAcknowledgedSegments();

        result.Select(segment => segment.SequenceNumber).Should().Equal(ushort.MaxValue, 0, 1);
        buffer.MapSegmentsReceived.Should().BeEmpty();
        buffer.LastConsumedSequenceNumber.Should().Be(1);
    }

    private static Segment CreateSegment(ushort sequenceNumber, byte[]? data = null)
    {
        return new Segment(sequenceNumber, new SegmentFlags(FlagOptions.Ack), data ?? []);
    }
}
