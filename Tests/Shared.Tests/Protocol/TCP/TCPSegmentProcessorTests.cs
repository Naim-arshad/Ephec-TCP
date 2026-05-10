using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Shared.Models;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;
using Shared.Protocol.TCP;

namespace Shared.Tests.Protocol.TCP;

using FlagOptions = SegmentFlags.FlagOptions;

public class TCPSegmentProcessorTests
{
    [Fact]
    public void Process_WithSynOnClosedConnection_ReturnsSynAckAndUpdatesConnection()
    {
        var connection = CreateConnection();
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        var syn = CreateSegment(10, FlagOptions.Syn);

        byte[] responseBytes = TCPSegmentProcessor.Process(syn, connection, mapConnection);

        var response = SegmentParser.Deserialize(responseBytes);
        response.Flags.Value.Should().Be((byte)(FlagOptions.Syn | FlagOptions.Ack));
        response.Data.Should().Equal(CreateSequencePayload(10));
        connection.CurrentState.Should().Be(Connection.State.SynReceived);
        connection.LastSegmentReceived.Should().BeSameAs(syn);
        connection.LastSegmentSent.Should().BeEquivalentTo(response, options => options.ComparingByMembers<Segment>());
        connection.SendBuffer.SegmentsToSend.Should().ContainKey(response.SequenceNumber);
        connection.ReceiveBuffer.MapSegmentsReceived.Should().ContainKey(10);
    }

    [Fact]
    public void Process_WithSynOnActiveConnection_ReturnsRstAndRemovesConnection()
    {
        var connection = CreateConnection();
        connection.UpdateConnectionInfo(Connection.State.Established);
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        mapConnection[connection.Endpoint.ToString()] = connection;

        byte[] responseBytes = TCPSegmentProcessor.Process(
            CreateSegment(10, FlagOptions.Syn),
            connection,
            mapConnection);

        var response = SegmentParser.Deserialize(responseBytes);
        response.Flags.Value.Should().Be((byte)FlagOptions.Rst);
        mapConnection.Should().NotContainKey(connection.Endpoint.ToString());
    }

    [Fact]
    public void Process_WithSynAckInSynSentState_ReturnsAckAndEstablishesConnection()
    {
        var connection = CreateConnection();
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        var sentSyn = CreateSegment(57662, FlagOptions.Syn);
        connection.UpdateConnectionInfo(Connection.State.SynSent, lastSegmentSent: sentSyn);
        var synAck = CreateSegment(100, FlagOptions.Syn | FlagOptions.Ack, CreateSequencePayload(57662));

        byte[] responseBytes = TCPSegmentProcessor.Process(synAck, connection, mapConnection);

        var response = SegmentParser.Deserialize(responseBytes);
        response.Flags.Value.Should().Be((byte)FlagOptions.Ack);
        response.Data.Should().Equal(CreateSequencePayload(100));
        connection.CurrentState.Should().Be(Connection.State.Established);
        connection.LastSegmentReceived.Should().BeSameAs(synAck);
        connection.LastSegmentSent.Should().BeEquivalentTo(response, options => options.ComparingByMembers<Segment>());
        connection.SendBuffer.SegmentsToSend.Should().NotContainKey(57662);
    }

    [Fact]
    public void Process_WithSynAckOutsideSynSentState_ReturnsRstAndRemovesConnection()
    {
        var connection = CreateConnection();
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        mapConnection[connection.Endpoint.ToString()] = connection;
        var synAck = CreateSegment(100, FlagOptions.Syn | FlagOptions.Ack, CreateSequencePayload(42));

        byte[] responseBytes = TCPSegmentProcessor.Process(synAck, connection, mapConnection);

        var response = SegmentParser.Deserialize(responseBytes);
        response.Flags.Value.Should().Be((byte)FlagOptions.Rst);
        mapConnection.Should().NotContainKey(connection.Endpoint.ToString());
    }

    [Fact]
    public void Process_WithAckForSynAck_ReturnsNoResponseAndRemovesAcknowledgedSegment()
    {
        var connection = CreateConnection();
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        var synAck = CreateSegment(55, FlagOptions.Syn | FlagOptions.Ack, CreateSequencePayload(10));
        connection.UpdateConnectionInfo(Connection.State.SynReceived, lastSegmentSent: synAck);

        byte[] responseBytes = TCPSegmentProcessor.Process(
            CreateSegment(200, FlagOptions.Ack, CreateSequencePayload(55)),
            connection,
            mapConnection);

        responseBytes.Should().BeEmpty();
        connection.SendBuffer.SegmentsToSend.Should().NotContainKey(55);
    }

    [Fact]
    public void Process_WithInvalidAck_ReturnsLastSentSegmentForRetransmission()
    {
        var connection = CreateConnection();
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        mapConnection[connection.Endpoint.ToString()] = connection;
        var synAck = CreateSegment(55, FlagOptions.Syn | FlagOptions.Ack, CreateSequencePayload(10));
        connection.UpdateConnectionInfo(Connection.State.SynReceived, lastSegmentSent: synAck);

        byte[] responseBytes = TCPSegmentProcessor.Process(
            CreateSegment(200, FlagOptions.Ack, CreateSequencePayload(99)),
            connection,
            mapConnection);

        var response = SegmentParser.Deserialize(responseBytes);
        response.Should().BeEquivalentTo(synAck, options => options.ComparingByMembers<Segment>());
        mapConnection.Should().ContainKey(connection.Endpoint.ToString());
    }

    [Fact]
    public void Process_WithThirdDuplicateAck_ReturnsFirstMissingSegmentForFastRetransmission()
    {
        var connection = CreateConnection();
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        var missingSegment = CreateSegment(43, FlagOptions.None, [0xAA]);
        connection.UpdateConnectionInfo(Connection.State.Established, lastSegmentSent: missingSegment);
        connection.SetLastAcknowledgedSequenceId(42);

        byte[] firstResponseBytes = TCPSegmentProcessor.Process(
            CreateSegment(100, FlagOptions.Ack, CreateSequencePayload(42)),
            connection,
            mapConnection);
        byte[] secondResponseBytes = TCPSegmentProcessor.Process(
            CreateSegment(101, FlagOptions.Ack, CreateSequencePayload(42)),
            connection,
            mapConnection);
        byte[] thirdResponseBytes = TCPSegmentProcessor.Process(
            CreateSegment(102, FlagOptions.Ack, CreateSequencePayload(42)),
            connection,
            mapConnection);

        firstResponseBytes.Should().BeEmpty();
        secondResponseBytes.Should().BeEmpty();
        var response = SegmentParser.Deserialize(thirdResponseBytes);
        response.Should().BeEquivalentTo(missingSegment, options => options.ComparingByMembers<Segment>());
        connection.ActiveRetransmission.Should().BeTrue();
        connection.RetransmissionCount.Should().Be(1);
    }

    [Fact]
    public void Process_WithRepeatedTripleDuplicateAck_DoesNotFastRetransmitSameSegmentTwice()
    {
        var connection = CreateConnection();
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        var missingSegment = CreateSegment(43, FlagOptions.None, [0xAA]);
        connection.UpdateConnectionInfo(Connection.State.Established, lastSegmentSent: missingSegment);
        connection.SetLastAcknowledgedSequenceId(42);

        TCPSegmentProcessor.Process(CreateSegment(100, FlagOptions.Ack, CreateSequencePayload(42)), connection, mapConnection);
        TCPSegmentProcessor.Process(CreateSegment(101, FlagOptions.Ack, CreateSequencePayload(42)), connection, mapConnection);
        byte[] firstFastRetransmit = TCPSegmentProcessor.Process(
            CreateSegment(102, FlagOptions.Ack, CreateSequencePayload(42)),
            connection,
            mapConnection);
        TCPSegmentProcessor.Process(CreateSegment(103, FlagOptions.Ack, CreateSequencePayload(42)), connection, mapConnection);
        TCPSegmentProcessor.Process(CreateSegment(104, FlagOptions.Ack, CreateSequencePayload(42)), connection, mapConnection);
        byte[] secondFastRetransmit = TCPSegmentProcessor.Process(
            CreateSegment(105, FlagOptions.Ack, CreateSequencePayload(42)),
            connection,
            mapConnection);

        firstFastRetransmit.Should().NotBeEmpty();
        secondFastRetransmit.Should().BeEmpty();
        connection.RetransmissionCount.Should().Be(1);
    }

    [Fact]
    public void Process_WithSegmentWithoutFlagsOutsideEstablishedConnection_ReturnsRst()
    {
        var connection = CreateConnection();
        var mapConnection = new ConcurrentDictionary<string, Connection>();
        mapConnection[connection.Endpoint.ToString()] = connection;

        byte[] responseBytes = TCPSegmentProcessor.Process(
            CreateSegment(10, FlagOptions.None),
            connection,
            mapConnection);

        var response = SegmentParser.Deserialize(responseBytes);
        response.Flags.Value.Should().Be((byte)FlagOptions.Rst);
        mapConnection.Should().NotContainKey(connection.Endpoint.ToString());
    }

    [Fact]
    public void Process_WithDataSegments_FlushesAcknowledgedPayloadsToFileInSequenceOrder()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"ephec-tcp-{Guid.NewGuid():N}.bin");

        try
        {
            var connection = new Connection(
                new IPEndPoint(IPAddress.Loopback, 8080),
                TimeSpan.FromSeconds(5),
                outputPath);
            var mapConnection = new ConcurrentDictionary<string, Connection>();
            connection.UpdateConnectionInfo(Connection.State.Established);
            connection.ReceiveBuffer.MarkConsumedThrough(10);

            byte[] firstResponseBytes = TCPSegmentProcessor.Process(
                CreateSegment(12, FlagOptions.None, [0x03, 0x04]),
                connection,
                mapConnection);
            var firstResponse = SegmentParser.Deserialize(firstResponseBytes);

            firstResponse.Flags.Value.Should().Be((byte)FlagOptions.Ack);
            firstResponse.Data.Should().Equal(CreateSequencePayload(10));
            File.Exists(outputPath).Should().BeFalse();

            byte[] secondResponseBytes = TCPSegmentProcessor.Process(
                CreateSegment(11, FlagOptions.None, [0x01, 0x02]),
                connection,
                mapConnection);
            var secondResponse = SegmentParser.Deserialize(secondResponseBytes);

            secondResponse.Flags.Value.Should().Be((byte)FlagOptions.Ack);
            secondResponse.Data.Should().Equal(CreateSequencePayload(12));
            File.ReadAllBytes(outputPath).Should().Equal(0x01, 0x02, 0x03, 0x04);
            connection.ReceiveBuffer.MapSegmentsReceived.Should().NotContainKey(11);
            connection.ReceiveBuffer.MapSegmentsReceived.Should().NotContainKey(12);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static Connection CreateConnection()
    {
        return new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
    }

    private static Segment CreateSegment(ushort sequenceNumber, FlagOptions flags, byte[]? data = null)
    {
        return new Segment(sequenceNumber, new SegmentFlags(flags), data ?? []);
    }

    private static byte[] CreateSequencePayload(ushort sequenceNumber)
    {
        byte[] data = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(data, sequenceNumber);
        return data;
    }
}
