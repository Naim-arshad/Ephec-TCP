using System.Net;
using FluentAssertions;
using Shared.Models;
using Shared.Models.Segments;
using Shared.Protocol.TCP;

namespace Shared.Tests.Protocol.TCP;

using FlagOptions = SegmentFlags.FlagOptions;

public class TCPSegmentBuilderTests
{
    [Fact]
    public void BuildSyn_ReturnsSynSegmentWithNextConnectionSequenceNumber()
    {
        var connection = CreateConnection();
        ushort expectedSequence = connection.GetNextSequenceNumber();

        var segment = TCPSegmentBuilder.BuildSyn(connection);

        segment.SequenceNumber.Should().Be((ushort)(expectedSequence + 1));
        segment.Flags.Value.Should().Be((byte)FlagOptions.Syn);
        segment.Data.Should().BeEmpty();
    }

    [Fact]
    public void BuildSynAck_ReturnsSynAckSegmentAcknowledgingReceivedSegment()
    {
        var connection = CreateConnection();
        var receivedSegment = CreateSegment(0x1234, FlagOptions.Syn);

        var segment = TCPSegmentBuilder.BuildSynAck(connection, receivedSegment);

        segment.Flags.Value.Should().Be((byte)(FlagOptions.Syn | FlagOptions.Ack));
        segment.Data.Should().Equal([0x12, 0x34]);
    }

    [Fact]
    public void BuildAck_ReturnsAckSegmentAcknowledgingLastOrderedReceivedSegment()
    {
        var connection = CreateConnection();
        var receivedSegment = CreateSegment(0xBEEF, FlagOptions.Syn | FlagOptions.Ack);
        connection.ReceiveBuffer.SetLastOrderedSequenceNumber(0x1234);

        var segment = TCPSegmentBuilder.BuildAck(receivedSegment, connection);

        segment.Flags.Value.Should().Be((byte)FlagOptions.Ack);
        segment.Data.Should().Equal([0x12, 0x34]);
    }

    [Fact]
    public void BuildRst_ReturnsRstSegmentWithoutPayload()
    {
        var connection = CreateConnection();
        var receivedSegment = CreateSegment(42, FlagOptions.Syn);

        var segment = TCPSegmentBuilder.BuildRst(receivedSegment, connection);

        segment.Flags.Value.Should().Be((byte)FlagOptions.Rst);
        segment.Data.Should().BeEmpty();
    }

    [Fact]
    public void BuildData_ReturnsDataSegmentWithPayload()
    {
        var connection = CreateConnection();
        byte[] payload = [0xCA, 0xFE];

        var segment = TCPSegmentBuilder.BuildData(connection, payload);

        segment.Flags.Value.Should().Be((byte)FlagOptions.None);
        segment.Data.Should().Equal(payload);
    }

    [Fact]
    public void BuildFin_ReturnsFinSegmentWithoutPayload()
    {
        var connection = CreateConnection();

        var segment = TCPSegmentBuilder.BuildFin(connection);

        segment.Flags.Value.Should().Be((byte)FlagOptions.Fin);
        segment.Data.Should().BeEmpty();
    }

    [Fact]
    public void BuildFinAck_ReturnsFinAckAcknowledgingLastOrderedReceivedSegment()
    {
        var connection = CreateConnection();
        var receivedSegment = CreateSegment(0x9999, FlagOptions.Fin);
        connection.ReceiveBuffer.SetLastOrderedSequenceNumber(0x1234);

        var segment = TCPSegmentBuilder.BuildFinAck(connection, receivedSegment);

        segment.Flags.Value.Should().Be((byte)(FlagOptions.Fin | FlagOptions.Ack));
        segment.Data.Should().Equal([0x12, 0x34]);
    }

    private static Connection CreateConnection()
    {
        return new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
    }

    private static Segment CreateSegment(ushort sequenceNumber, FlagOptions flags)
    {
        return new Segment(sequenceNumber, new SegmentFlags(flags), []);
    }
}
