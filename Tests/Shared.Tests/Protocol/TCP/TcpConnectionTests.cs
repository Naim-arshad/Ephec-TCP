using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Shared.Models;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;
using Shared.Protocol.TCP;
using Shared.Protocol.UDP;

namespace Shared.Tests.Protocol.TCP;

using FlagOptions = SegmentFlags.FlagOptions;

public class TcpConnectionTests
{
    [Fact]
    public void Constructor_WithInjectedDependencies_StartsTransportAndRetransmissionLoop()
    {
        var transport = new FakeUdpTransport();
        var retransmissionHandler = new FakeRetransmissionHandler();

        var connection = new TcpConnection(
            transport,
            retransmissionHandler,
            TimeSpan.FromSeconds(5),
            "received.bin");

        transport.StartCount.Should().Be(1);
        retransmissionHandler.LoopCalls.Should().Be(1);
        ReferenceEquals(retransmissionHandler.LastMap, connection.Connections).Should().BeTrue();
        retransmissionHandler.LastCancellationToken.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullTransport_ThrowsArgumentNullException()
    {
        var act = () => new TcpConnection(
            null!,
            new FakeRetransmissionHandler(),
            TimeSpan.FromSeconds(5));

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("udpTransport");
    }

    [Fact]
    public void Constructor_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new TcpConnection(
            new FakeUdpTransport(),
            new FakeRetransmissionHandler(),
            TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("timeout");
    }

    [Fact]
    public void OnSegmentReceived_WithInvalidPacket_IgnoresPacket()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport);
        var remote = new IPEndPoint(IPAddress.Loopback, 12000);

        connection.OnSegmentReceived([0x01], remote);

        connection.Connections.Should().BeEmpty();
        transport.SentPackets.Should().BeEmpty();
    }

    [Fact]
    public void OnSegmentReceived_WithSyn_CreatesConnectionAndSendsSynAck()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport, receivedFilePath: "received.bin");
        var remote = new IPEndPoint(IPAddress.Loopback, 12000);
        var syn = CreateSegment(10, FlagOptions.Syn);

        connection.OnSegmentReceived(SegmentSerializer.Serialize(syn), remote);

        connection.Connections.Should().ContainKey(remote.ToString());
        connection.Connections[remote.ToString()].ReceivedFilePath.Should().Be("received.bin");
        transport.SentPackets.Should().ContainSingle();
        var response = SegmentParser.Deserialize(transport.SentPackets[0].Data);
        response.Flags.Value.Should().Be((byte)(FlagOptions.Syn | FlagOptions.Ack));
        response.Data.Should().Equal(CreateSequencePayload(10));
    }

    [Fact]
    public void OnSegmentReceived_WhenProcessorReturnsNoPacket_DoesNotSendResponse()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport);
        var remote = new IPEndPoint(IPAddress.Loopback, 12000);
        var rst = CreateSegment(10, FlagOptions.Rst);

        connection.OnSegmentReceived(SegmentSerializer.Serialize(rst), remote);

        connection.Connections.Should().ContainKey(remote.ToString());
        transport.SentPackets.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiatConnection_SendsSynAndCompletesWhenConnectionBecomesEstablished()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport);
        string remote = "127.0.0.1:12000";

        Task initiateTask = connection.InitiatConnection(remote);
        await WaitUntil(() => transport.SentPackets.Count == 1);
        connection.Connections[remote].UpdateConnectionInfo(Connection.State.Established);

        await initiateTask.WaitAsync(TimeSpan.FromSeconds(1));

        var syn = SegmentParser.Deserialize(transport.SentPackets[0].Data);
        syn.Flags.Value.Should().Be((byte)FlagOptions.Syn);
        transport.SentPackets[0].Endpoint.Should().Be(IPEndPoint.Parse(remote));
    }

    [Fact]
    public async Task SendFile_WithoutEstablishedConnection_DoesNotSendData()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport);
        using var tempFile = TempFile.WithBytes([0x01, 0x02]);

        await connection.SendFile("127.0.0.1:12000", tempFile.Path);

        transport.SentPackets.Should().BeEmpty();
    }

    [Fact]
    public async Task SendFile_WithEstablishedConnection_SendsFileChunksAsDataSegments()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport);
        var remote = new IPEndPoint(IPAddress.Loopback, 12000);
        connection.OnSegmentReceived(SegmentSerializer.Serialize(CreateSegment(10, FlagOptions.Syn)), remote);
        transport.SentPackets.Clear();
        Connection remoteConnection = connection.Connections[remote.ToString()];
        remoteConnection.SendBuffer.Acknowledge(CreateAckSegment(remoteConnection.LastSegmentSent.SequenceNumber), remoteConnection);
        remoteConnection.UpdateConnectionInfo(Connection.State.Established);
        List<Segment> sentSegments = [];
        transport.OnSendAsync = (bytes, endpoint) =>
        {
            var segment = SegmentParser.Deserialize(bytes);
            sentSegments.Add(segment);
            remoteConnection.SendBuffer.Acknowledge(CreateAckSegment(segment.SequenceNumber), remoteConnection);
            return Task.CompletedTask;
        };
        using var tempFile = TempFile.WithBytes(Enumerable.Range(0, 1500).Select(value => (byte)(value % 256)).ToArray());

        await connection.SendFile(remote.ToString(), tempFile.Path);

        sentSegments.Should().HaveCount(2);
        sentSegments.Select(segment => segment.Flags.Value).Should().AllBeEquivalentTo((byte)FlagOptions.None);
        sentSegments.Select(segment => segment.Data.Length).Should().Equal(1024, 476);
        remoteConnection.SendBuffer.PendingSegmentCount.Should().Be(0);
    }

    [Fact]
    public async Task SendFile_WhenFinalAckTimesOut_ReturnsWithPendingSegment()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport, timeout: TimeSpan.FromMilliseconds(20));
        var remote = new IPEndPoint(IPAddress.Loopback, 12000);
        connection.OnSegmentReceived(SegmentSerializer.Serialize(CreateSegment(10, FlagOptions.Syn)), remote);
        transport.SentPackets.Clear();
        Connection remoteConnection = connection.Connections[remote.ToString()];
        remoteConnection.SendBuffer.Acknowledge(CreateAckSegment(remoteConnection.LastSegmentSent.SequenceNumber), remoteConnection);
        remoteConnection.UpdateConnectionInfo(Connection.State.Established);
        using var tempFile = TempFile.WithBytes([0x01]);

        await connection.SendFile(remote.ToString(), tempFile.Path);

        transport.SentPackets.Should().ContainSingle();
        remoteConnection.SendBuffer.PendingSegmentCount.Should().Be(1);
    }

    [Fact]
    public async Task StopConnection_WithEstablishedConnection_SendsFinAndStopsTransport()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport);
        var remote = new IPEndPoint(IPAddress.Loopback, 12000);
        connection.OnSegmentReceived(SegmentSerializer.Serialize(CreateSegment(10, FlagOptions.Syn)), remote);
        transport.SentPackets.Clear();
        Connection remoteConnection = connection.Connections[remote.ToString()];
        remoteConnection.SendBuffer.Acknowledge(CreateAckSegment(remoteConnection.LastSegmentSent.SequenceNumber), remoteConnection);
        remoteConnection.UpdateConnectionInfo(Connection.State.Established);

        await connection.StopConnection(remote.ToString());

        transport.StopCount.Should().Be(1);
        transport.SentPackets.Should().ContainSingle();
        var fin = SegmentParser.Deserialize(transport.SentPackets[0].Data);
        fin.Flags.Value.Should().Be((byte)FlagOptions.Fin);
    }

    [Fact]
    public async Task StopConnection_WithoutEstablishedConnection_StopsTransportWithoutSendingFin()
    {
        var transport = new FakeUdpTransport();
        var connection = CreateConnection(transport);

        await connection.StopConnection("127.0.0.1:12000");

        transport.StopCount.Should().Be(1);
        transport.SentPackets.Should().BeEmpty();
    }

    private static TcpConnection CreateConnection(
        FakeUdpTransport transport,
        TimeSpan? timeout = null,
        string? receivedFilePath = "received_file.bin")
    {
        return new TcpConnection(
            transport,
            new FakeRetransmissionHandler(),
            timeout ?? TimeSpan.FromSeconds(5),
            receivedFilePath);
    }

    private static Segment CreateSegment(ushort sequenceNumber, FlagOptions flags, byte[]? data = null)
    {
        return new Segment(sequenceNumber, new SegmentFlags(flags), data ?? []);
    }

    private static Segment CreateAckSegment(ushort ackedSequenceNumber)
    {
        return CreateSegment(0, FlagOptions.Ack, CreateSequencePayload(ackedSequenceNumber));
    }

    private static byte[] CreateSequencePayload(ushort sequenceNumber)
    {
        byte[] data = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(data, sequenceNumber);
        return data;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        while (!condition())
        {
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationTokenSource.Token);
        }
    }

    private sealed class FakeUdpTransport : IUdpTransport
    {
        public event Action<byte[], IPEndPoint>? OnSegmentReceived;

        public List<(byte[] Data, IPEndPoint Endpoint)> SentPackets { get; } = [];
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public Func<byte[], IPEndPoint, Task>? OnSendAsync { get; set; }

        public void StartServer()
        {
            StartCount++;
        }

        public Task StopServer()
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public async Task SendSegmentAsync(byte[] data, IPEndPoint remoteIPEndPoint)
        {
            SentPackets.Add((data, remoteIPEndPoint));

            if (OnSendAsync is not null)
            {
                await OnSendAsync(data, remoteIPEndPoint);
            }
        }

        public void RaiseSegmentReceived(byte[] data, IPEndPoint endpoint)
        {
            OnSegmentReceived?.Invoke(data, endpoint);
        }
    }

    private sealed class FakeRetransmissionHandler : ITcpRetransmissionHandler
    {
        public int LoopCalls { get; private set; }
        public ConcurrentDictionary<string, Connection>? LastMap { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task LoopRetransmissions(
            ConcurrentDictionary<string, Connection> mapConnection,
            CancellationToken cancellationToken)
        {
            LoopCalls++;
            LastMap = mapConnection;
            LastCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TempFile : IDisposable
    {
        public string Path { get; }

        private TempFile(string path)
        {
            Path = path;
        }

        public static TempFile WithBytes(byte[] bytes)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ephec-tcp-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(path, bytes);
            return new TempFile(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
