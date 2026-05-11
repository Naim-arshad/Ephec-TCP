using Shared.Models;
using System.Collections.Concurrent;

namespace Shared.Protocol.TCP
{
    internal interface ITcpRetransmissionHandler
    {
        Task LoopRetransmissions(
            ConcurrentDictionary<string, Connection> mapConnection,
            CancellationToken cancellationToken);
    }
}
