using System.Buffers.Binary;
using FluentAssertions;
using Shared.Models;
using Shared.Models.Segments;
using Shared.Protocol.TCP.Buffers;
using System.Net;

namespace Shared.Tests.Protocol.TCP.Buffers;

using FlagOptions = SegmentFlags.FlagOptions;

public class TcpSendBufferTests
{
    [Fact]
    public void Constructor_InitializesEmptyBuffer()
    {
        var buffer = new TcpSendBuffer();

        buffer.SegmentsToSend.Should().BeEmpty();
        buffer.DuplicateAckCount.Should().Be(0);
    }

    [Fact]
    public void AddSegmentToSend_WithNewSequenceNumber_StoresSegment()
    {
        var buffer = new TcpSendBuffer();
        var segment = CreateSegment(42, [0xAA]);

        buffer.AddSegmentToSend(segment);

        buffer.SegmentsToSend.Should().ContainKey(42)
            .WhoseValue.Should().BeSameAs(segment);
    }

    [Fact]
    public void AddSegmentToSend_WithDuplicateSequenceNumber_KeepsFirstSegment()
    {
        var buffer = new TcpSendBuffer();
        var firstSegment = CreateSegment(42, [0xAA]);
        var duplicateSegment = CreateSegment(42, [0xBB]);

        buffer.AddSegmentToSend(firstSegment);
        buffer.AddSegmentToSend(duplicateSegment);

        buffer.SegmentsToSend.Should().ContainSingle();
        buffer.SegmentsToSend[42].Should().BeSameAs(firstSegment);
    }

    [Fact]
    public void Acknowledge_WithKnownSegment_RemovesSegmentAndReturnsTrue()
    {
        var buffer = new TcpSendBuffer();
        var connection = CreateConnection();
        var segment = CreateSegment(42);
        buffer.AddSegmentToSend(segment);

        bool result = buffer.Acknowledge(CreateAckSegment(42, 100), connection);

        result.Should().BeTrue();
        buffer.SegmentsToSend.Should().NotContainKey(42);
        buffer.DuplicateAckCount.Should().Be(0);
    }

    [Fact]
    public void Acknowledge_WithKnownSegment_DeactivatesRetransmissionAndResetsCount()
    {
        var buffer = new TcpSendBuffer();
        var connection = CreateConnection();
        buffer.AddSegmentToSend(CreateSegment(42));
        connection.ActivateRetransmission();
        connection.IncrementRetransmissionCount();

        bool result = buffer.Acknowledge(CreateAckSegment(42, 100), connection);

        result.Should().BeTrue();
        connection.ActiveRetransmission.Should().BeFalse();
        connection.RetransmissionCount.Should().Be(0);
    }

    [Fact]
    public void Acknowledge_WithUnknownNonDuplicateSegment_ReturnsFalseAndKeepsBufferUnchanged()
    {
        var buffer = new TcpSendBuffer();
        var connection = CreateConnection();
        var segment = CreateSegment(42);
        buffer.AddSegmentToSend(segment);

        bool result = buffer.Acknowledge(CreateAckSegment(99), connection);

        result.Should().BeFalse();
        buffer.SegmentsToSend.Should().ContainKey(42)
            .WhoseValue.Should().BeSameAs(segment);
        buffer.DuplicateAckCount.Should().Be(0);
    }

    [Fact]
    public void Acknowledge_WithDuplicateAckBeforeThreshold_IncrementsDuplicateAckCountAndReturnsFalse()
    {
        var buffer = new TcpSendBuffer();
        var connection = CreateConnection();
        connection.SetLastAcknowledgedSequenceId(42);
        buffer.AddSegmentToSend(CreateSegment(43));

        bool result = buffer.Acknowledge(CreateAckSegment(42), connection);

        result.Should().BeFalse();
        buffer.DuplicateAckCount.Should().Be(1);
        connection.ActiveRetransmission.Should().BeFalse();
    }

    [Fact]
    public void Acknowledge_WithThirdDuplicateAck_ActivatesRetransmissionAndClearsDuplicateCount()
    {
        var buffer = new TcpSendBuffer();
        var connection = CreateConnection();
        connection.SetLastAcknowledgedSequenceId(42);
        buffer.AddSegmentToSend(CreateSegment(43));

        buffer.Acknowledge(CreateAckSegment(42, 100), connection);
        buffer.Acknowledge(CreateAckSegment(42, 101), connection);
        bool result = buffer.Acknowledge(CreateAckSegment(42, 102), connection);

        result.Should().BeFalse();
        buffer.DuplicateAckCount.Should().Be(0);
        connection.ActiveRetransmission.Should().BeTrue();
    }

    [Fact]
    public void Acknowledge_WithNewAckAfterDuplicateAck_ResetsDuplicateAckCount()
    {
        var buffer = new TcpSendBuffer();
        var connection = CreateConnection();
        connection.SetLastAcknowledgedSequenceId(42);
        buffer.AddSegmentToSend(CreateSegment(43));
        buffer.Acknowledge(CreateAckSegment(42, 100), connection);
        buffer.Acknowledge(CreateAckSegment(42, 101), connection);

        bool result = buffer.Acknowledge(CreateAckSegment(43), connection);

        result.Should().BeTrue();
        buffer.DuplicateAckCount.Should().Be(0);
        buffer.SegmentsToSend.Should().NotContainKey(43);
    }

    [Fact]
    public void GetSegmentsToRetransmit_ReturnsSegmentsStillAwaitingAcknowledgement()
    {
        var buffer = new TcpSendBuffer();
        var firstSegment = CreateSegment(42);
        var secondSegment = CreateSegment(43);
        var connection = CreateConnection();
        buffer.AddSegmentToSend(firstSegment);
        buffer.AddSegmentToSend(secondSegment);
        buffer.Acknowledge(CreateAckSegment(42), connection);

        List<Segment> result = buffer.GetSegmentsToRetransmit();

        result.Should().ContainSingle()
            .Which.Should().BeSameAs(secondSegment);
    }

    [Fact]
    public void Acknowledge_WithRepeatedSameAckPacket_CountsItOnlyOnce()
    {
        var buffer = new TcpSendBuffer();
        var connection = CreateConnection();
        connection.SetLastAcknowledgedSequenceId(42);
        buffer.AddSegmentToSend(CreateSegment(43));

        buffer.Acknowledge(CreateAckSegment(42, 100), connection);
        buffer.Acknowledge(CreateAckSegment(42, 100), connection);
        bool result = buffer.Acknowledge(CreateAckSegment(42, 100), connection);

        result.Should().BeFalse();
        buffer.DuplicateAckCount.Should().Be(1);
        connection.ActiveRetransmission.Should().BeFalse();
    }

    [Fact]
    public void Acknowledge_WithInvalidAckPayload_ThrowsInvalidOperationException()
    {
        var buffer = new TcpSendBuffer();
        var connection = CreateConnection();
        var invalidAckSegment = CreateSegment(0, [0x01]);

        var act = () => buffer.Acknowledge(invalidAckSegment, connection);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Data doit contenir exactement 2 octets pour représenter une séquence.");
    }

    private static Segment CreateSegment(ushort sequenceNumber, byte[]? data = null)
    {
        return new Segment(sequenceNumber, new SegmentFlags(FlagOptions.Ack), data ?? []);
    }

    private static Segment CreateAckSegment(ushort ackedSequenceNumber, ushort packetSequenceNumber = 0)
    {
        byte[] data = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(data, ackedSequenceNumber);
        return new Segment(packetSequenceNumber, new SegmentFlags(FlagOptions.Ack), data);
    }

    private static Connection CreateConnection()
    {
        return new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
    }
}
