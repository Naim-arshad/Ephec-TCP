using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Shared.Protocol.UDP;

namespace Shared.Tests.Protocol.UDP;

public class UdpTransportTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_WithInvalidPort_ThrowsArgumentOutOfRangeException(int port)
    {
        var act = () => new UdpTransport(port);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("port");
    }

    [Fact]
    public async Task StopServer_WhenServerWasNotStarted_CompletesWithoutThrowing()
    {
        await using var transport = new UdpTransport(0);

        var act = () => transport.StopServer();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendSegmentAsync_SendsDatagramToRemoteEndpoint()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var transport = new UdpTransport(0);
        var receiverEndpoint = (IPEndPoint)receiver.Client.LocalEndPoint!;
        byte[] payload = [0xCA, 0xFE];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        Task<UdpReceiveResult> receiveTask = receiver.ReceiveAsync(timeout.Token).AsTask();
        await transport.SendSegmentAsync(payload, receiverEndpoint);

        UdpReceiveResult result = await receiveTask;
        result.Buffer.Should().Equal(payload);
    }

    [Fact]
    public async Task StartServer_WhenDatagramIsReceived_RaisesOnSegmentReceived()
    {
        int serverPort = GetAvailableUdpPort();
        await using var transport = new UdpTransport(serverPort);
        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var received = new TaskCompletionSource<(byte[] Buffer, IPEndPoint EndPoint)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] payload = [0x01, 0x02, 0x03];

        transport.OnSegmentReceived += (buffer, endPoint) => received.TrySetResult((buffer, endPoint));
        transport.StartServer();

        await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, serverPort));
        Task completedTask = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        completedTask.Should().BeSameAs(received.Task);
        var result = await received.Task;
        result.Buffer.Should().Equal(payload);
        result.EndPoint.Address.Should().Be(IPAddress.Loopback);
    }

    [Fact]
    public async Task StartServer_WhenCalledTwice_RemainsUsable()
    {
        int serverPort = GetAvailableUdpPort();
        await using var transport = new UdpTransport(serverPort);
        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] payload = [0xAA];

        transport.OnSegmentReceived += (buffer, _) => received.TrySetResult(buffer);

        transport.StartServer();
        transport.StartServer();

        await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, serverPort));
        Task completedTask = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        completedTask.Should().BeSameAs(received.Task);
        (await received.Task).Should().Equal(payload);
    }

    private static int GetAvailableUdpPort()
    {
        using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }
}
