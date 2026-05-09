using System.Net;
using FluentAssertions;
using Shared.Models;
using Shared.Models.Segments;

namespace Shared.Tests.Models;

using FlagOptions = SegmentFlags.FlagOptions;

public class ConnectionTests
{
    [Fact]
    public void Constructor_WithValidValues_InitializesConnection()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 8080);

        var connection = new Connection(endpoint, TimeSpan.FromSeconds(5));

        connection.Endpoint.Should().Be(endpoint);
        connection.CurrentState.Should().Be(Connection.State.Closed);
        connection.ActiveRetransmission.Should().BeFalse();
        connection.LastSegmentReceived.SequenceNumber.Should().Be(0);
        connection.LastSegmentReceived.Flags.Value.Should().Be((byte)FlagOptions.None);
        connection.LastSegmentSent.SequenceNumber.Should().Be(0);
        connection.LastSegmentSent.Flags.Value.Should().Be((byte)FlagOptions.None);
        connection.LastAcknowledgedSequenceId.Should().Be(0);
        connection.OrderedSegmentsReceived.Should().BeEmpty();
        connection.LastActivity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_WithNullEndpoint_ThrowsArgumentNullException()
    {
        var act = () => new Connection(null!, TimeSpan.FromSeconds(5));

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("endpoint");
    }

    [Fact]
    public void Constructor_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("timeout");
    }

    [Fact]
    public void UpdateConnectionInfo_WithReceivedSegments_StoresThemOrderedBySequenceNumber()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
        var segment2 = CreateSegment(2);
        var segment1 = CreateSegment(1);

        connection.UpdateConnectionInfo(Connection.State.Established, segment2);
        connection.UpdateConnectionInfo(Connection.State.Established, segment1);

        connection.LastSegmentReceived.Should().BeSameAs(segment1);
        connection.OrderedSegmentsReceived.Select(segment => segment.SequenceNumber)
            .Should().Equal([1, 2]);
    }

    [Fact]
    public void UpdateConnectionInfo_WithDuplicateReceivedSegment_KeepsFirstSegment()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
        var firstSegment = new Segment(1, new SegmentFlags(FlagOptions.Ack), [0x01]);
        var duplicateSegment = new Segment(1, new SegmentFlags(FlagOptions.Ack), [0x02]);

        connection.UpdateConnectionInfo(Connection.State.Established, firstSegment);
        connection.UpdateConnectionInfo(Connection.State.Established, duplicateSegment);

        connection.LastSegmentReceived.Should().BeSameAs(duplicateSegment);
        connection.OrderedSegmentsReceived.Should().ContainSingle()
            .Which.Data.Should().Equal([0x01]);
    }

    [Fact]
    public void UpdateConnectionInfo_WithSentSegment_AddsSegmentToSendBuffer()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
        var sentSegment = CreateSegment(10);

        connection.UpdateConnectionInfo(Connection.State.SynSent, lastSegmentSent: sentSegment);

        connection.LastSegmentSent.Should().BeSameAs(sentSegment);
        connection.SendBuffer.SegmentsToSend.Should().ContainKey(10);
        connection.CheckSequenceNumber(10).Should().BeTrue();
    }

    [Fact]
    public void GetSegmentsToRetransmit_ReturnsSegmentsAwaitingAcknowledgement()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
        var firstSegment = CreateSegment(10);
        var secondSegment = CreateSegment(11);

        connection.UpdateConnectionInfo(Connection.State.Established, lastSegmentSent: firstSegment);
        connection.UpdateConnectionInfo(Connection.State.Established, lastSegmentSent: secondSegment);

        connection.GetSegmentsToRetransmit()
            .Select(segment => segment.SequenceNumber)
            .Should().Equal([10, 11]);
    }

    [Fact]
    public void CheckSequenceNumber_WithDifferentSequenceNumber_ReturnsFalse()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
        connection.UpdateConnectionInfo(Connection.State.SynSent, lastSegmentSent: CreateSegment(10));

        connection.CheckSequenceNumber(11).Should().BeFalse();
    }

    [Fact]
    public void SetLastAcknowledgedSequenceId_UpdatesAcknowledgedSequence()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));

        connection.SetLastAcknowledgedSequenceId(42);

        connection.LastAcknowledgedSequenceId.Should().Be(42);
    }

    [Fact]
    public void GetNextSequenceNumber_ReturnsIncrementingSequenceNumbers()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));

        ushort first = connection.GetNextSequenceNumber();
        ushort second = connection.GetNextSequenceNumber();

        second.Should().Be((ushort)(first + 1));
    }

    [Fact]
    public void RetransmissionFlags_CanBeActivatedAndDeactivated()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
        connection.SendBuffer.AddDuplicateAck();

        connection.ActivateRetransmission();
        connection.ActiveRetransmission.Should().BeTrue();

        connection.DeactivateRetransmission();
        connection.ActiveRetransmission.Should().BeFalse();
        connection.SendBuffer.DuplicateAckCount.Should().Be(0);
    }

    [Fact]
    public void RetransmissionCount_CanBeIncrementedAndReset()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));

        connection.IncrementRetransmissionCount();
        connection.IncrementRetransmissionCount();

        connection.RetransmissionCount.Should().Be(2);

        connection.ResetRetransmissionCount();

        connection.RetransmissionCount.Should().Be(0);
    }

    [Fact]
    public void IsExpired_WhenTimeoutElapsed_ReturnsTrue()
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromTicks(1));

        Thread.Sleep(10);

        connection.IsExpired().Should().BeTrue();
    }

    private static Segment CreateSegment(ushort sequenceNumber)
    {
        return new Segment(sequenceNumber, new SegmentFlags(FlagOptions.Ack), []);
    }
}
