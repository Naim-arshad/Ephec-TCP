using Shared.Logging;
using Shared.Models;
using Shared.Models.Segments;
using Shared.Protocol.Serialization;
using Shared.Protocol.UDP;
using System.Collections.Concurrent;
using System.Net;

namespace Shared.Protocol.TCP
{

    public class TcpConnection
    {
        private readonly static Random rand = new();

        private readonly ConcurrentDictionary<string, Connection> _mapConnection;
        private readonly UdpTransport _udpTransport;
        private readonly TcpRetransmissionHandler _retransmissionHandler;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TimeSpan _timeout;

        public TcpConnection(int port, int timeout)
        {
            _mapConnection = new ConcurrentDictionary<string, Connection>();
            _udpTransport = new UdpTransport(port);
            
            _udpTransport.OnSegmentReceived += OnSegmentReceived;
            _timeout = TimeSpan.FromSeconds(timeout);
            _udpTransport.StartServer();

            _retransmissionHandler = new TcpRetransmissionHandler(_udpTransport);
            _ = _retransmissionHandler.LoopRetransmissions(_mapConnection, _cancellationTokenSource.Token);
        }

        public void OnSegmentReceived(byte[] PacketBinary, IPEndPoint ip)
        {
            Segment segment;
            byte[] packet;
            Logger.Info($"Incoming Packet from IP:{ip}");

            try
            {
                segment = SegmentParser.Deserialize(PacketBinary);

                _mapConnection.TryGetValue(ip.ToString(), out Connection? connectionInfo);
                if (connectionInfo == null)
                {
                    connectionInfo = new(ip, _timeout);
                    _mapConnection[ip.ToString()] = connectionInfo;
                }

                packet = TCPSegmentProcessor.Process(segment, connectionInfo, _mapConnection);

            } catch (Exception ex)
            {
                Logger.Error("Error processing segment: " + ex.Message);
                return;
            }
            if(packet != null && packet.Length >= 5)
                _ = _udpTransport.SendSegmentAsync(packet, ip);
        }

        public async Task InitiatConnection(string ip)
        {
            Logger.Info("Initiating connection to IP:" + ip);

            IPEndPoint endPoint = IPEndPoint.Parse(ip);
            Connection connectionInfo = new(endPoint, _timeout);
            Segment segment = TCPSegmentBuilder.BuildSyn(connectionInfo);

            _mapConnection.TryAdd(endPoint.ToString(), connectionInfo);
            connectionInfo.UpdateConnectionInfo(Connection.State.SynSent, null, segment);

            _ = _udpTransport.SendSegmentAsync(
                SegmentSerializer.Serialize(segment), 
                endPoint);
        }

        public static ushort RandomSequenceNumber(ushort excludeNumber)
        {
            ushort number = (ushort)rand.Next(0, ushort.MaxValue);
            if (number != excludeNumber && (number + 2) <= ushort.MaxValue)
                number += 2;
            else
                number = 0;
            return number;
        }
    }
}
