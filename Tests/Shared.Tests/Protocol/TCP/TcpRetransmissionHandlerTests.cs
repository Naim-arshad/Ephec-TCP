using System.Collections.Concurrent;
using FluentAssertions;
using Shared.Models;
using Shared.Protocol.TCP;
using Shared.Protocol.UDP;

namespace Shared.Tests.Protocol.TCP;

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
}
