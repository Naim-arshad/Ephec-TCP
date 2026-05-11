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

public class TcpRetransmissionHandlerTests
{
    [Fact]
    public async Task LoopRetransmissions_WithNullConnectionMap_ThrowsArgumentNullException()
    {
        await using var transport = new UdpTransport(0);
        var handler = new TcpRetransmissionHandler(transport);

        var act = () => handler.LoopRetransmissions(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("mapConnection");
    }

    [Fact]
    public async Task LoopRetransmissions_WithAlreadyCanceledToken_CompletesWithoutThrowing()
    {
        await using var transport = new UdpTransport(0);
        var handler = new TcpRetransmissionHandler(transport);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var act = () => handler.LoopRetransmissions(
            new ConcurrentDictionary<string, Connection>(),
            cancellationTokenSource.Token);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LoopRetransmissions_WithActiveRetransmission_SendsPendingSegment()
    {
        var transport = new FakeUdpTransport();
        using var cancellationTokenSource = new CancellationTokenSource();
        transport.OnSendAsync = (_, _) =>
        {
            cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        };
        var handler = new TcpRetransmissionHandler(
            transport,
            retransmissionInterval: TimeSpan.FromMilliseconds(1));
        var connection = CreateConnectionWithPendingSegments([CreateSegment(42)]);
        connection.ActivateRetransmission();
        var map = CreateMap(connection);

        await handler.LoopRetransmissions(map, cancellationTokenSource.Token);

        transport.SentPackets.Should().ContainSingle();
        SegmentParser.Deserialize(transport.SentPackets[0].Data).SequenceNumber.Should().Be(42);
        connection.RetransmissionCount.Should().Be(1);
    }

    [Fact]
    public async Task LoopRetransmissions_WhenWindowIsFull_RetransmitsWindowBatch()
    {
        var transport = new FakeUdpTransport();
        using var cancellationTokenSource = new CancellationTokenSource();
        transport.OnSendAsync = (_, _) =>
        {
            if (transport.SentPackets.Count == 2)
            {
                cancellationTokenSource.Cancel();
            }

            return Task.CompletedTask;
        };
        var handler = new TcpRetransmissionHandler(
            transport,
            fullWindowThreshold: 2,
            retransmissionInterval: TimeSpan.FromMilliseconds(1));
        var connection = CreateConnectionWithPendingSegments([CreateSegment(42), CreateSegment(43)]);
        var map = CreateMap(connection);

        await handler.LoopRetransmissions(map, cancellationTokenSource.Token);

        transport.SentPackets.Select(packet => SegmentParser.Deserialize(packet.Data).SequenceNumber)
            .Should().Equal(42, 43);
        connection.RetransmissionCount.Should().Be(1);
    }

    [Fact]
    public async Task LoopRetransmissions_WhenMaxRetransmissionsReached_RemovesConnection()
    {
        var transport = new FakeUdpTransport();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var handler = new TcpRetransmissionHandler(
            transport,
            retransmissionInterval: TimeSpan.FromMilliseconds(1),
            maxRetransmissions: 0);
        var connection = CreateConnectionWithPendingSegments([CreateSegment(42)]);
        connection.ActivateRetransmission();
        var map = CreateMap(connection);

        await handler.LoopRetransmissions(map, cancellationTokenSource.Token);

        map.Should().BeEmpty();
        transport.SentPackets.Should().BeEmpty();
    }

    [Fact]
    public async Task LoopRetransmissions_WhenSendFails_LogsAndContinuesUntilCancelled()
    {
        var transport = new FakeUdpTransport();
        using var cancellationTokenSource = new CancellationTokenSource();
        transport.OnSendAsync = (_, _) =>
        {
            cancellationTokenSource.Cancel();
            throw new InvalidOperationException("send failed");
        };
        var handler = new TcpRetransmissionHandler(
            transport,
            retransmissionInterval: TimeSpan.FromMilliseconds(1));
        var connection = CreateConnectionWithPendingSegments([CreateSegment(42)]);
        connection.ActivateRetransmission();
        var map = CreateMap(connection);

        var act = () => handler.LoopRetransmissions(map, cancellationTokenSource.Token);

        await act.Should().NotThrowAsync();
        transport.SentPackets.Should().ContainSingle();
        map.Should().ContainKey(connection.Endpoint.ToString());
    }

    private static ConcurrentDictionary<string, Connection> CreateMap(Connection connection)
    {
        return new ConcurrentDictionary<string, Connection>(
            [new KeyValuePair<string, Connection>(connection.Endpoint.ToString(), connection)]);
    }

    private static Connection CreateConnectionWithPendingSegments(IReadOnlyList<Segment> segments)
    {
        var connection = new Connection(new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(5));
        connection.SetLastAcknowledgedSequenceId((ushort)(segments[0].SequenceNumber - 1));

        foreach (Segment segment in segments)
        {
            connection.UpdateConnectionInfo(Connection.State.Established, lastSegmentSent: segment);
        }

        return connection;
    }

    private static Segment CreateSegment(ushort sequenceNumber)
    {
        return new Segment(sequenceNumber, new SegmentFlags(FlagOptions.None), [0xAA]);
    }

    private sealed class FakeUdpTransport : IUdpTransport
    {
        public event Action<byte[], IPEndPoint>? OnSegmentReceived;

        public List<(byte[] Data, IPEndPoint Endpoint)> SentPackets { get; } = [];
        public Func<byte[], IPEndPoint, Task>? OnSendAsync { get; set; }

        public void StartServer()
        {
        }

        public Task StopServer()
        {
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
}
