using System.Net;

namespace Shared.Protocol.UDP
{
    internal interface IUdpTransport
    {
        event Action<byte[], IPEndPoint>? OnSegmentReceived;

        void StartServer();
        Task StopServer();
        Task SendSegmentAsync(byte[] data, IPEndPoint remoteIPEndPoint);
    }
}
